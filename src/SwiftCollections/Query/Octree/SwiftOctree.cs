//=======================================================================
// SwiftOctree.cs
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
/// Represents a mutable octree that stores keyed bounding volumes within immutable world bounds.
/// </summary>
/// <typeparam name="TKey">The stable key type; equality and hashing must remain unchanged while
/// indexed.</typeparam>
/// <typeparam name="TVolume">The value-type volume backend providing consistent intersection, union, equality and
/// cost operations.</typeparam>
/// <remarks>
/// Mutable runtime index: synchronize all access externally, including queries that use shared scratch/stamps.
/// Key and volume callbacks must not reenter or mutate the index. Bounds are copied and keys are retained;
/// changing a key's equality/hash breaks lookup. Query ordering depends on current topology and mutation history;
/// it is not sorted or a serialized order guarantee. Determinism requires deterministic keys, input order and
/// volume/mapping arithmetic. A key is indexed once; duplicate Insert updates its bounds and returns false. A
/// query adds each matching stored key once, independently of keys already in the destination.
/// </remarks>
public class SwiftOctree<TKey, TVolume>
    where TKey : notnull
    where TVolume : struct, IBoundVolume<TVolume>
{
    private const string _diagnosticSource = nameof(SwiftOctree<TKey, TVolume>);

    private readonly IOctreeBoundsPartitioner<TVolume> _boundsPartitioner;
    private readonly QueryKeyIndexMap<TKey> _keyToEntryIndex;
    private readonly SwiftIntStack _freeEntries;

    private OctreeEntry[] _entries;
    private OctreeNode _root;
    private int _peakCount;
    private int _count;

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.Query.SwiftOctree`2" /> class.
    /// </summary>
    /// <param name="worldBounds">The fixed root region in caller coordinates, copied by value. All inserted entry
    /// bounds must be fully contained, including their boundaries.</param>
    /// <param name="options">The options copied by value. default(SwiftOctreeOptions) yields depth/capacity zero
    /// and merging false; the tree does not validate it.</param>
    /// <param name="boundsPartitioner">The non-null partitioner retained by reference. It must return mutually
    /// consistent containment, child-index and subdivision results without mutating this index.</param>
    /// <exception cref="T:System.ArgumentNullException">The supplied backend is null.</exception>
    public SwiftOctree(TVolume worldBounds, SwiftOctreeOptions options, IOctreeBoundsPartitioner<TVolume> boundsPartitioner)
    {
        ChronicleGuard.ThrowIfNull(boundsPartitioner, nameof(boundsPartitioner));

        WorldBounds = worldBounds;
        Options = options;
        _boundsPartitioner = boundsPartitioner;

        int capacity = SwiftHashTools.NextPowerOfTwo(Math.Max(4, options.NodeCapacity));
        _keyToEntryIndex = new QueryKeyIndexMap<TKey>(capacity, MatchesEntryKey, IsAllocatedEntry, GetEntryKey);
        _freeEntries = new SwiftIntStack();
        _entries = new OctreeEntry[capacity];
        _root = new OctreeNode(worldBounds, 0, null);
    }

    /// <summary>
    /// Gets the number of active entries stored in the octree.
    /// </summary>
    /// <value>The number of indexed distinct keys.</value>
    public int Count => _count;

    /// <summary>
    /// Gets the immutable world bounds covered by this octree.
    /// </summary>
    /// <value>The constructor-supplied root volume, copied by value; the tree does not automatically expand
    /// it.</value>
    public TVolume WorldBounds { get; }

    /// <summary>
    /// Gets the subdivision options used by this octree.
    /// </summary>
    /// <value>The constructor-supplied options value; later changes to the caller's copy have no effect.</value>
    public SwiftOctreeOptions Options { get; }

    internal int DebugNodeCount => CountNodes(_root);

    internal int DebugMaxDepth => GetMaxDepth(_root);

    internal bool DebugRootHasChildren => _root.HasChildren;

    /// <summary>
    /// Inserts a key and copied bounds, or updates the bounds of an existing key.
    /// </summary>
    /// <param name="key">The non-null stable key to insert or locate; reference keys are retained, not
    /// cloned.</param>
    /// <param name="bounds">The volume in the same caller-defined coordinate space and units as other indexed
    /// bounds; copied by value. No transform is applied.</param>
    /// <returns>true for a new key; false for an existing key whose bounds have nevertheless been
    /// updated.</returns>
    /// <exception cref="T:System.ArgumentNullException">key is null.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">bounds is not fully contained within WorldBounds;
    /// this validation precedes insertion/update.</exception>
    /// <remarks>
    /// This operation is not transactional if key hashing, volume operations, mapping, or allocation throws.
    /// Existing bounds may be relocated even though false is returned.
    /// </remarks>
    public bool Insert(TKey key, TVolume bounds)
    {
        ChronicleGuard.ThrowIfNull(key, nameof(key));
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

    /// <summary>
    /// Removes an entry from the octree.
    /// </summary>
    /// <param name="key">The non-null stable key to insert or locate; reference keys are retained, not
    /// cloned.</param>
    /// <returns>true if one matching indexed entry was removed; false if absent, with entries
    /// unchanged.</returns>
    /// <exception cref="T:System.ArgumentNullException">The key is null.</exception>
    public bool Remove(TKey key)
    {
        ChronicleGuard.ThrowIfNullGeneric(key, nameof(key));

        int entryIndex = FindEntryIndex(key);
        if (entryIndex < 0)
            return false;

        OctreeNode node = _entries[entryIndex].Node!;
        RemoveEntryFromNode(node, entryIndex);

        _keyToEntryIndex.Remove(key);
        _entries[entryIndex].Reset();
        _freeEntries.Push(entryIndex);
        _count--;

        if (Options.EnableMergeOnRemove)
            TryMergeUp(node);

        return true;
    }

    /// <summary>
    /// Attempts to retrieve the bounds registered for the supplied key.
    /// </summary>
    /// <param name="key">The non-null stable key to insert or locate; reference keys are retained, not
    /// cloned.</param>
    /// <param name="bounds">Receives a copy of the stored bounds on success; default(TVolume) when the key is
    /// absent.</param>
    /// <returns>true if the key is indexed and bounds is valid; false if absent, with bounds set to default. No
    /// entries change.</returns>
    /// <exception cref="T:System.ArgumentNullException">key is null.</exception>
    public bool TryGetBounds(TKey key, out TVolume bounds)
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

    /// <summary>
    /// Updates the bounds for an existing entry.
    /// </summary>
    /// <param name="key">The non-null stable key to insert or locate; reference keys are retained, not
    /// cloned.</param>
    /// <param name="newBounds">Replacement bounds in the same coordinate space; copied by value. No transform is
    /// applied.</param>
    /// <returns>true if the key exists, including unchanged bounds; false if absent, without changing
    /// entries.</returns>
    /// <exception cref="T:System.ArgumentNullException">The key is null.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">The key exists and newBounds is outside
    /// WorldBounds.</exception>
    /// <remarks>
    /// Updates one matching key's bounds; absent keys change nothing. Bounds outside WorldBounds are rejected for
    /// an existing key; absent keys return false before that bounds validation. Callback/allocation failures can
    /// leave partial mutation.
    /// </remarks>
    public bool UpdateEntryBounds(TKey key, TVolume newBounds)
    {
        ChronicleGuard.ThrowIfNullGeneric(key, nameof(key));

        int entryIndex = FindEntryIndex(key);
        if (entryIndex < 0)
            return false;

        return RelocateEntry(entryIndex, newBounds);
    }

    /// <summary>
    /// Determines whether the octree contains the specified key.
    /// </summary>
    /// <param name="key">The non-null stable key to insert or locate; reference keys are retained, not
    /// cloned.</param>
    /// <returns>true if the key is indexed; otherwise false. No entries change.</returns>
    /// <exception cref="T:System.ArgumentNullException">key is null.</exception>
    public bool Contains(TKey key)
    {
        ChronicleGuard.ThrowIfNullGeneric(key, nameof(key));
        return FindEntryIndex(key) >= 0;
    }

    /// <summary>
    /// Queries the octree and returns only entries whose bounds intersect the supplied query volume.
    /// </summary>
    /// <param name="queryBounds">The query region in the index coordinate space, copied by value; it is not
    /// retained or transformed.</param>
    /// <param name="results">The caller-owned mutable collection to append matching keys to. Existing entries are
    /// preserved; no deduplication against pre-existing entries is performed.</param>
    /// <exception cref="T:System.ArgumentNullException">results is null.</exception>
    /// <remarks>
    /// Appends without clearing results. Empty/no-hit queries preserve existing entries. Destination Add or
    /// volume/backend exceptions can leave partially appended output; no rollback is provided. Do not mutate the
    /// index from result callbacks. Visits current node entries before children in index order; each stored key
    /// is appended once. Boundary contact counts according to the volume backend.
    /// </remarks>
    public void Query(TVolume queryBounds, ICollection<TKey> results)
    {
        ChronicleGuard.ThrowIfNull(results, nameof(results));

        if (_count == 0 || !_root.Bounds.Intersects(queryBounds))
            return;

        QueryNode(_root, queryBounds, results);
    }

    /// <summary>
    /// Removes all entries from the octree while preserving the configured world bounds.
    /// </summary>
    /// <remarks>
    /// Removes all entries and retained keys while preserving reusable entry capacity. Replaces the root and
    /// discards its subdivided node topology.
    /// </remarks>
    public void Clear()
    {
        if (_count == 0)
            return;

        for (int i = 0; i < _peakCount; i++)
            _entries[i].Reset();

        _keyToEntryIndex.Clear();
        _freeEntries.Reset();
        _peakCount = 0;
        _count = 0;
        _root = new OctreeNode(WorldBounds, 0, null);
    }

    private void QueryNode(OctreeNode node, TVolume queryBounds, ICollection<TKey> results)
    {
        AddIntersectingEntries(node, queryBounds, results);

        if (node.HasChildren)
            QueryIntersectingChildren(node, queryBounds, results);
    }

    private void AddIntersectingEntries(OctreeNode node, TVolume queryBounds, ICollection<TKey> results)
    {
        for (int i = 0; i < node.EntryIndices.Count; i++)
        {
            int entryIndex = node.EntryIndices[i];
            ref OctreeEntry entry = ref _entries[entryIndex];
            if (entry.Bounds.Intersects(queryBounds))
                results.Add(entry.Key);
        }
    }

    private void QueryIntersectingChildren(OctreeNode node, TVolume queryBounds, ICollection<TKey> results)
    {
        OctreeNode[] children = node.Children!;
        for (int i = 0; i < children.Length; i++)
        {
            OctreeNode child = children[i];
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

        OctreeNode oldNode = _entries[entryIndex].Node!;
        RemoveEntryFromNode(oldNode, entryIndex);

        _entries[entryIndex].Bounds = newBounds;
        InsertIntoNode(_root, entryIndex);

        if (Options.EnableMergeOnRemove)
            TryMergeUp(oldNode);

        return true;
    }

    private void InsertIntoNode(OctreeNode node, int entryIndex)
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

    private bool ShouldSubdivide(OctreeNode node)
    {
        return !node.HasChildren &&
               node.Depth < Options.MaxDepth &&
               node.EntryIndices.Count > Options.NodeCapacity &&
               _boundsPartitioner.CanSubdivide(node.Bounds);
    }

    private void Subdivide(OctreeNode node)
    {
        CreateChildNodes(node);
        MoveContainedEntriesToChildren(node);
        SubdivideOverflowingChildren(node);
    }

    private void CreateChildNodes(OctreeNode node)
    {
        node.Children = new OctreeNode[8];
        for (int i = 0; i < node.Children.Length; i++)
            node.Children[i] = CreateChildNode(node, i);
    }

    private void MoveContainedEntriesToChildren(OctreeNode node)
    {
        OctreeNode[] children = node.Children!;
        int entryIndex = 0;
        while (entryIndex < node.EntryIndices.Count)
        {
            int currentEntryIndex = node.EntryIndices[entryIndex];
            if (!_boundsPartitioner.TryGetContainingChildIndex(node.Bounds, _entries[currentEntryIndex].Bounds, out int childIndex))
            {
                entryIndex++;
                continue;
            }

            OctreeNode child = children[childIndex];
            child.EntryIndices.Add(currentEntryIndex);
            _entries[currentEntryIndex].Node = child;
            node.EntryIndices.RemoveAt(entryIndex);
        }
    }

    private void SubdivideOverflowingChildren(OctreeNode node)
    {
        OctreeNode[] children = node.Children!;
        for (int i = 0; i < children.Length; i++)
        {
            OctreeNode child = children[i];
            if (ChildShouldSubdivide(child))
                Subdivide(child);
        }
    }

    private bool ChildShouldSubdivide(OctreeNode child)
    {
        return child.EntryIndices.Count > Options.NodeCapacity &&
               child.Depth < Options.MaxDepth &&
               _boundsPartitioner.CanSubdivide(child.Bounds);
    }

    private OctreeNode CreateChildNode(OctreeNode parent, int childIndex)
    {
        TVolume bounds = _boundsPartitioner.CreateChildBounds(parent.Bounds, childIndex);
        return new OctreeNode(bounds, parent.Depth + 1, parent);
    }

    private void TryMergeUp(OctreeNode node)
    {
        OctreeNode? current = node;
        while (current != null)
        {
            if (current.HasChildren && CanMerge(current))
                CollapseChildrenInto(current);

            current = current.Parent ?? null;
        }
    }

    private bool CanMerge(OctreeNode node)
    {
        int totalEntries = node.EntryIndices.Count;
        OctreeNode[] children = node.Children!;
        for (int i = 0; i < children.Length; i++)
        {
            OctreeNode child = children[i];
            if (child.HasChildren)
                return false;

            totalEntries += child.EntryIndices.Count;
            if (totalEntries > Options.NodeCapacity)
                return false;
        }

        return true;
    }

    private void CollapseChildrenInto(OctreeNode node)
    {
        OctreeNode[] children = node.Children!;
        for (int i = 0; i < children.Length; i++)
        {
            OctreeNode child = children[i];
            for (int j = 0; j < child.EntryIndices.Count; j++)
            {
                int entryIndex = child.EntryIndices[j];
                node.EntryIndices.Add(entryIndex);
                _entries[entryIndex].Node = node;
            }
        }

        node.Children = null;
    }

    private void RemoveEntryFromNode(OctreeNode node, int entryIndex)
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
        SwiftCollectionDiagnostics.Shared.Info($"Resized octree entry storage to {newCapacity} entries.", _diagnosticSource);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int FindEntryIndex(TKey key)
    {
        return _keyToEntryIndex.Find(key);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool MatchesEntryKey(int index, TKey key)
    {
        return EqualityComparer<TKey>.Default.Equals(_entries[index].Key, key);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsAllocatedEntry(int index) => _entries[index].IsAllocated;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TKey GetEntryKey(int index) => _entries[index].Key;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureWithinWorldBounds(TVolume bounds, string paramName)
    {
        if (!_boundsPartitioner.ContainsBounds(WorldBounds, bounds))
            throw new ArgumentOutOfRangeException(paramName, "Bounds must be fully contained within the octree world bounds.");
    }

    private static int CountNodes(OctreeNode node)
    {
        int count = 1;
        if (!node.HasChildren)
            return count;

        OctreeNode[] children = node.Children!;
        for (int i = 0; i < children.Length; i++)
            count += CountNodes(children[i]);

        return count;
    }

    private static int GetMaxDepth(OctreeNode node)
    {
        int maxDepth = node.Depth;
        if (!node.HasChildren)
            return maxDepth;

        OctreeNode[] children = node.Children!;
        for (int i = 0; i < children.Length; i++)
            maxDepth = Math.Max(maxDepth, GetMaxDepth(children[i]));

        return maxDepth;
    }

    private sealed class OctreeNode
    {
        public OctreeNode(TVolume bounds, int depth, OctreeNode? parent)
        {
            Bounds = bounds;
            Depth = depth;
            Parent = parent;
            EntryIndices = new SwiftList<int>();
        }

        public TVolume Bounds { get; }

        public int Depth { get; }

        public OctreeNode? Parent { get; }

        public SwiftList<int> EntryIndices { get; }

        public OctreeNode[]? Children { get; set; }

        public bool HasChildren => Children != null;
    }

    private struct OctreeEntry
    {
        public TKey Key;
        public TVolume Bounds;
        public OctreeNode? Node;
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
