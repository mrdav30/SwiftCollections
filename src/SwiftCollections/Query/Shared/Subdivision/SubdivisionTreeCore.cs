//=======================================================================
// SubdivisionTreeCore.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Chronicler.Diagnostics;
using SwiftCollections.Diagnostics;
using SwiftCollections.Utility;

namespace SwiftCollections.Query;

/// <summary>
/// Owns keyed entries, subdivision, relocation, merging and traversal shared by quadtrees and octrees.
/// </summary>
internal struct SubdivisionTreeCore<TKey, TVolume, TPartitioner>
    where TKey : notnull
    where TVolume : struct, IBoundVolume<TVolume>
    where TPartitioner : struct, ISubdivisionPartitioner<TVolume>
{
    private readonly TPartitioner _boundsPartitioner;
    private readonly QueryKeyIndexMap<TKey> _keyToEntryIndex;
    private readonly SwiftIntStack _freeEntries;

    private TreeEntry[] _entries;
    private TreeNode _root;
    private int _peakCount;
    private int _count;
    private readonly int _maxDepth;
    private readonly int _nodeCapacity;
    private readonly bool _enableMergeOnRemove;

    internal SubdivisionTreeCore(TVolume worldBounds, int maxDepth, int nodeCapacity, bool enableMergeOnRemove,
        TPartitioner boundsPartitioner, Func<int, TKey, bool> matchesEntryKey,
        Func<int, bool> isAllocatedEntry, Func<int, TKey> getEntryKey)
    {
        WorldBounds = worldBounds;
        _maxDepth = maxDepth;
        _nodeCapacity = nodeCapacity;
        _enableMergeOnRemove = enableMergeOnRemove;
        _boundsPartitioner = boundsPartitioner;
        _peakCount = 0;
        _count = 0;
        int capacity = SwiftHashTools.NextPowerOfTwo(Math.Max(4, nodeCapacity));
        _keyToEntryIndex = new QueryKeyIndexMap<TKey>(capacity, matchesEntryKey, isAllocatedEntry, getEntryKey);
        _freeEntries = new SwiftIntStack();
        _entries = new TreeEntry[capacity];
        _root = new TreeNode(worldBounds, 0, null);
    }

    internal int Count => _count;

    internal TVolume WorldBounds { get; }

    internal int MaxDepth => _maxDepth;

    internal int NodeCapacity => _nodeCapacity;

    internal bool EnableMergeOnRemove => _enableMergeOnRemove;

    internal int DebugNodeCount => CountNodes(_root);

    internal int DebugMaxDepth => GetMaxDepth(_root);

    internal bool DebugRootHasChildren => _root.HasChildren;

    internal bool Insert(TKey key, TVolume bounds)
    {
        ChronicleGuard.ThrowIfNullGeneric(key, nameof(key));
        EnsureWithinWorldBounds(bounds, nameof(bounds));

        int existingIndex = FindEntryIndex(key);
        if (existingIndex >= 0)
        {
            RelocateEntry(existingIndex, bounds);
            return false;
        }

        EnsureEntryCapacity(_count + 1);

        int entryIndex = AllocateEntry(key, bounds);
        _keyToEntryIndex.Insert(key, entryIndex);
        InsertIntoNode(_root, entryIndex);
        _count++;
        return true;
    }

    internal bool Remove(TKey key)
    {
        ChronicleGuard.ThrowIfNullGeneric(key, nameof(key));

        int entryIndex = FindEntryIndex(key);
        if (entryIndex < 0)
            return false;

        TreeNode node = _entries[entryIndex].Node!;
        RemoveEntryFromNode(node, entryIndex);

        _keyToEntryIndex.Remove(key);
        _entries[entryIndex].Reset();
        _freeEntries.Push(entryIndex);
        _count--;

        if (_enableMergeOnRemove)
            TryMergeUp(node);

        return true;
    }

    internal bool TryGetBounds(TKey key, out TVolume bounds)
    {
        ChronicleGuard.ThrowIfNullGeneric(key, nameof(key));

        int entryIndex = FindEntryIndex(key);
        if (entryIndex < 0)
        {
            bounds = default;
            return false;
        }

        bounds = _entries[entryIndex].Bounds;
        return true;
    }

    internal bool UpdateEntryBounds(TKey key, TVolume newBounds)
    {
        ChronicleGuard.ThrowIfNullGeneric(key, nameof(key));

        int entryIndex = FindEntryIndex(key);
        if (entryIndex < 0)
            return false;

        return RelocateEntry(entryIndex, newBounds);
    }

    internal bool Contains(TKey key)
    {
        ChronicleGuard.ThrowIfNullGeneric(key, nameof(key));
        return FindEntryIndex(key) >= 0;
    }

    internal void Query(TVolume queryBounds, ICollection<TKey> results)
    {
        ChronicleGuard.ThrowIfNull(results, nameof(results));

        if (_count == 0 || !_root.Bounds.Intersects(queryBounds))
            return;

        QueryNode(_root, queryBounds, results);
    }

    internal void Clear()
    {
        if (_count == 0)
            return;

        for (int i = 0; i < _peakCount; i++)
            _entries[i].Reset();

        _keyToEntryIndex.Clear();
        _freeEntries.Reset();
        _peakCount = 0;
        _count = 0;
        _root = new TreeNode(WorldBounds, 0, null);
    }

    private void QueryNode(TreeNode node, TVolume queryBounds, ICollection<TKey> results)
    {
        AddIntersectingEntries(node, queryBounds, results);

        if (node.HasChildren)
            QueryIntersectingChildren(node, queryBounds, results);
    }

    private void AddIntersectingEntries(TreeNode node, TVolume queryBounds, ICollection<TKey> results)
    {
        // Query callbacks cannot mutate the tree, so this private list stays stable.
        ReadOnlySpan<int> indices = node.EntryIndices.AsReadOnlySpan();
        for (int i = 0; i < indices.Length; i++)
        {
            int entryIndex = indices[i];
            ref TreeEntry entry = ref _entries[entryIndex];
            if (entry.Bounds.Intersects(queryBounds))
                results.Add(entry.Key);
        }
    }

    private void QueryIntersectingChildren(TreeNode node, TVolume queryBounds, ICollection<TKey> results)
    {
        TreeNode[] children = node.Children!;
        for (int i = 0; i < children.Length; i++)
        {
            TreeNode child = children[i];
            if (child.Bounds.Intersects(queryBounds))
                QueryNode(child, queryBounds, results);
        }
    }

    private bool RelocateEntry(int entryIndex, TVolume newBounds)
    {
        EnsureWithinWorldBounds(newBounds, nameof(newBounds));

        TVolume currentBounds = _entries[entryIndex].Bounds;
        if (currentBounds.BoundsEquals(newBounds))
            return true;

        TreeNode oldNode = _entries[entryIndex].Node!;
        RemoveEntryFromNode(oldNode, entryIndex);

        _entries[entryIndex].Bounds = newBounds;
        InsertIntoNode(_root, entryIndex);

        if (_enableMergeOnRemove)
            TryMergeUp(oldNode);

        return true;
    }

    private void InsertIntoNode(TreeNode node, int entryIndex)
    {
        if (node.HasChildren && _boundsPartitioner.TryGetContainingChildIndex(node.Bounds, _entries[entryIndex].Bounds, out int childIndex))
        {
            InsertIntoNode(node.Children![childIndex], entryIndex);
            return;
        }

        node.EntryIndices.Add(entryIndex);
        _entries[entryIndex].Node = node;

        if (ShouldSubdivide(node))
            Subdivide(node);
    }

    private bool ShouldSubdivide(TreeNode node)
    {
        return !node.HasChildren &&
               node.Depth < _maxDepth &&
               node.EntryIndices.Count > _nodeCapacity &&
               _boundsPartitioner.CanSubdivide(node.Bounds);
    }

    private void Subdivide(TreeNode node)
    {
        CreateChildNodes(node);
        MoveContainedEntriesToChildren(node);
        SubdivideOverflowingChildren(node);
    }

    private void CreateChildNodes(TreeNode node)
    {
        node.Children = new TreeNode[_boundsPartitioner.ChildCount];
        for (int i = 0; i < node.Children.Length; i++)
            node.Children[i] = CreateChildNode(node, i);
    }

    private void MoveContainedEntriesToChildren(TreeNode node)
    {
        TreeNode[] children = node.Children!;
        int entryIndex = 0;
        while (entryIndex < node.EntryIndices.Count)
        {
            int currentEntryIndex = node.EntryIndices[entryIndex];
            if (!_boundsPartitioner.TryGetContainingChildIndex(node.Bounds, _entries[currentEntryIndex].Bounds, out int childIndex))
            {
                entryIndex++;
                continue;
            }

            TreeNode child = children[childIndex];
            child.EntryIndices.Add(currentEntryIndex);
            _entries[currentEntryIndex].Node = child;
            node.EntryIndices.RemoveAt(entryIndex);
        }
    }

    private void SubdivideOverflowingChildren(TreeNode node)
    {
        TreeNode[] children = node.Children!;
        for (int i = 0; i < children.Length; i++)
        {
            TreeNode child = children[i];
            if (ChildShouldSubdivide(child))
                Subdivide(child);
        }
    }

    private bool ChildShouldSubdivide(TreeNode child)
    {
        return child.EntryIndices.Count > _nodeCapacity &&
               child.Depth < _maxDepth &&
               _boundsPartitioner.CanSubdivide(child.Bounds);
    }

    private TreeNode CreateChildNode(TreeNode parent, int childIndex)
    {
        TVolume bounds = _boundsPartitioner.CreateChildBounds(parent.Bounds, childIndex);
        return new TreeNode(bounds, parent.Depth + 1, parent);
    }

    private void TryMergeUp(TreeNode node)
    {
        TreeNode? current = node;
        while (current != null)
        {
            if (current.HasChildren && CanMerge(current))
                CollapseChildrenInto(current);

            current = current.Parent ?? null;
        }
    }

    private bool CanMerge(TreeNode node)
    {
        int totalEntries = node.EntryIndices.Count;
        TreeNode[] children = node.Children!;
        for (int i = 0; i < children.Length; i++)
        {
            TreeNode child = children[i];
            if (child.HasChildren)
                return false;

            totalEntries += child.EntryIndices.Count;
            if (totalEntries > _nodeCapacity)
                return false;
        }

        return true;
    }

    private void CollapseChildrenInto(TreeNode node)
    {
        TreeNode[] children = node.Children!;
        for (int i = 0; i < children.Length; i++)
        {
            TreeNode child = children[i];
            for (int j = 0; j < child.EntryIndices.Count; j++)
            {
                int entryIndex = child.EntryIndices[j];
                node.EntryIndices.Add(entryIndex);
                _entries[entryIndex].Node = node;
            }
        }

        node.Children = null;
    }

    private void RemoveEntryFromNode(TreeNode node, int entryIndex)
    {
        int index = node.EntryIndices.IndexOf(entryIndex);
        node.EntryIndices.RemoveAt(index);
        _entries[entryIndex].Node = null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AllocateEntry(TKey key, TVolume bounds)
    {
        int entryIndex;
        if (_freeEntries.Count > 0)
            entryIndex = _freeEntries.Pop();
        else
            entryIndex = _peakCount++;

        _entries[entryIndex].Key = key;
        _entries[entryIndex].Bounds = bounds;
        _entries[entryIndex].Node = null;
        _entries[entryIndex].IsAllocated = true;
        return entryIndex;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureEntryCapacity(int capacity)
    {
        if (capacity <= _entries.Length)
            return;

        int newCapacity = SwiftHashTools.NextPowerOfTwo(capacity);
        Array.Resize(ref _entries, newCapacity);
        _keyToEntryIndex.ResizeAndRehash(newCapacity, _peakCount);
        SwiftCollectionDiagnostics.Shared.Info($"Resized {(_boundsPartitioner.ChildCount == 4 ? "quadtree" : "octree")} entry storage to {newCapacity} entries.", _boundsPartitioner.DiagnosticSource);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int FindEntryIndex(TKey key)
    {
        return _keyToEntryIndex.Find(key);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool MatchesEntryKey(int index, TKey key)
    {
        return EqualityComparer<TKey>.Default.Equals(_entries[index].Key, key);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool IsAllocatedEntry(int index) => _entries[index].IsAllocated;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal TKey GetEntryKey(int index) => _entries[index].Key;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureWithinWorldBounds(TVolume bounds, string paramName)
    {
        if (!_boundsPartitioner.ContainsBounds(WorldBounds, bounds))
            throw new ArgumentOutOfRangeException(paramName, _boundsPartitioner.WorldBoundsError);
    }

    private static int CountNodes(TreeNode node)
    {
        int count = 1;
        if (!node.HasChildren)
            return count;

        TreeNode[] children = node.Children!;
        for (int i = 0; i < children.Length; i++)
            count += CountNodes(children[i]);

        return count;
    }

    private static int GetMaxDepth(TreeNode node)
    {
        int maxDepth = node.Depth;
        if (!node.HasChildren)
            return maxDepth;

        TreeNode[] children = node.Children!;
        for (int i = 0; i < children.Length; i++)
            maxDepth = Math.Max(maxDepth, GetMaxDepth(children[i]));

        return maxDepth;
    }

    private sealed class TreeNode
    {
        public TreeNode(TVolume bounds, int depth, TreeNode? parent)
        {
            Bounds = bounds;
            Depth = depth;
            Parent = parent;
            EntryIndices = new SwiftList<int>();
        }

        public TVolume Bounds { get; }

        public int Depth { get; }

        public TreeNode? Parent { get; }

        public SwiftList<int> EntryIndices { get; }

        public TreeNode[]? Children { get; set; }

        public bool HasChildren => Children != null;
    }

    private struct TreeEntry
    {
        public TKey Key;
        public TVolume Bounds;
        public TreeNode? Node;
        public bool IsAllocated;

        public void Reset()
        {
            Key = default!;
            Bounds = default;
            Node = null;
            IsAllocated = false;
        }
    }
}
