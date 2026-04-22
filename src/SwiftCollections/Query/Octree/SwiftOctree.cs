using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SwiftCollections.Query;

/// <summary>
/// Represents a mutable octree that stores keyed bounding volumes within immutable world bounds.
/// </summary>
/// <typeparam name="TKey">The key used to identify each stored entry.</typeparam>
/// <typeparam name="TVolume">The volume type used for octree registration and queries.</typeparam>
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
    /// Initializes a new instance of the <see cref="SwiftOctree{TKey, TVolume}"/> class.
    /// </summary>
    /// <param name="worldBounds">The immutable world bounds covered by the octree.</param>
    /// <param name="options">Subdivision options for the octree.</param>
    /// <param name="boundsPartitioner">The backend-owned partitioner that maps bounds into octants.</param>
    public SwiftOctree(TVolume worldBounds, SwiftOctreeOptions options, IOctreeBoundsPartitioner<TVolume> boundsPartitioner)
    {
        SwiftThrowHelper.ThrowIfNull(boundsPartitioner, nameof(boundsPartitioner));

        WorldBounds = worldBounds;
        Options = options;
        _boundsPartitioner = boundsPartitioner;

        int capacity = QueryCollectionGuards.NormalizeCapacity(Math.Max(4, options.NodeCapacity));
        _keyToEntryIndex = new QueryKeyIndexMap<TKey>(capacity);
        _freeEntries = new SwiftIntStack();
        _entries = new OctreeEntry[capacity];
        _root = new OctreeNode(worldBounds, 0, null);
    }

    /// <summary>
    /// Gets the number of active entries stored in the octree.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Gets the immutable world bounds covered by this octree.
    /// </summary>
    public TVolume WorldBounds { get; }

    /// <summary>
    /// Gets the subdivision options used by this octree.
    /// </summary>
    public SwiftOctreeOptions Options { get; }

    internal int DebugNodeCount => CountNodes(_root);

    internal int DebugMaxDepth => GetMaxDepth(_root);

    internal bool DebugRootHasChildren => _root.HasChildren;

    /// <summary>
    /// Inserts a new entry or replaces the bounds of an existing key.
    /// </summary>
    /// <param name="key">The entry key.</param>
    /// <param name="bounds">The entry bounds.</param>
    /// <returns><c>true</c> when a new key was added; <c>false</c> when an existing key was replaced.</returns>
    public bool Insert(TKey key, TVolume bounds)
    {
        QueryCollectionGuards.ThrowIfKeyNull(key, nameof(key));
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
    /// <param name="key">The entry key.</param>
    /// <returns><c>true</c> when the key existed and was removed; otherwise, <c>false</c>.</returns>
    public bool Remove(TKey key)
    {
        QueryCollectionGuards.ThrowIfKeyNull(key, nameof(key));

        int entryIndex = FindEntryIndex(key);
        if (entryIndex < 0)
            return false;

        OctreeNode? node = _entries[entryIndex].Node;
        if (node != null)
            RemoveEntryFromNode(node, entryIndex);

        _keyToEntryIndex.Remove(key, MatchesEntryKey, IsAllocatedEntry, GetEntryKey);
        _entries[entryIndex].Reset();
        _freeEntries.Push(entryIndex);
        _count--;

        if (Options.EnableMergeOnRemove && node != null)
            TryMergeUp(node);

        return true;
    }

    /// <summary>
    /// Attempts to retrieve the bounds registered for the supplied key.
    /// </summary>
    public bool TryGetBounds(TKey key, out TVolume bounds)
    {
        QueryCollectionGuards.ThrowIfKeyNull(key, nameof(key));

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
    /// <param name="key">The entry key.</param>
    /// <param name="newBounds">The replacement bounds.</param>
    /// <returns><c>true</c> when the key existed; otherwise, <c>false</c>.</returns>
    public bool UpdateEntryBounds(TKey key, TVolume newBounds)
    {
        QueryCollectionGuards.ThrowIfKeyNull(key, nameof(key));

        int entryIndex = FindEntryIndex(key);
        if (entryIndex < 0)
            return false;

        return RelocateEntry(entryIndex, newBounds);
    }

    /// <summary>
    /// Determines whether the octree contains the specified key.
    /// </summary>
    public bool Contains(TKey key)
    {
        QueryCollectionGuards.ThrowIfKeyNull(key, nameof(key));
        return FindEntryIndex(key) >= 0;
    }

    /// <summary>
    /// Queries the octree and returns only entries whose bounds intersect the supplied query volume.
    /// </summary>
    /// <param name="queryBounds">The bounds used to test for intersection.</param>
    /// <param name="results">The collection that receives intersecting keys.</param>
    public void Query(TVolume queryBounds, ICollection<TKey> results)
    {
        QueryCollectionGuards.ThrowIfResultsCollectionNull(results, nameof(results));

        if (_count == 0 || !_root.Bounds.Intersects(queryBounds))
            return;

        QueryNode(_root, queryBounds, results);
    }

    /// <summary>
    /// Removes all entries from the octree while preserving the configured world bounds.
    /// </summary>
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
        for (int i = 0; i < node.EntryIndices.Count; i++)
        {
            int entryIndex = node.EntryIndices[i];
            ref OctreeEntry entry = ref _entries[entryIndex];
            if (entry.IsAllocated && entry.Bounds.Intersects(queryBounds))
                results.Add(entry.Key);
        }

        if (!node.HasChildren)
            return;

        for (int i = 0; i < node.Children?.Length; i++)
        {
            OctreeNode? child = node.Children[i];
            if (child != null && child.Bounds.Intersects(queryBounds))
                QueryNode(child, queryBounds, results);
        }
    }

    private bool RelocateEntry(int entryIndex, TVolume newBounds)
    {
        EnsureWithinWorldBounds(newBounds, nameof(newBounds));

        if (!_entries[entryIndex].IsAllocated)
            return false;

        TVolume currentBounds = _entries[entryIndex].Bounds;
        if (currentBounds.BoundsEquals(newBounds))
            return true;

        OctreeNode? oldNode = _entries[entryIndex].Node;
        if (oldNode != null)
            RemoveEntryFromNode(oldNode, entryIndex);

        _entries[entryIndex].Bounds = newBounds;
        InsertIntoNode(_root, entryIndex);

        if (Options.EnableMergeOnRemove && oldNode != null)
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

        if (!node.HasChildren &&
            node.Depth < Options.MaxDepth &&
            node.EntryIndices.Count > Options.NodeCapacity &&
            _boundsPartitioner.CanSubdivide(node.Bounds))
        {
            Subdivide(node);
        }
    }

    private void Subdivide(OctreeNode node)
    {
        node.Children = new OctreeNode[8];
        for (int i = 0; i < node.Children.Length; i++)
            node.Children[i] = CreateChildNode(node, i);

        int entryIndex = 0;
        while (entryIndex < node.EntryIndices.Count)
        {
            int currentEntryIndex = node.EntryIndices[entryIndex];
            if (!_boundsPartitioner.TryGetContainingChildIndex(node.Bounds, _entries[currentEntryIndex].Bounds, out int childIndex))
            {
                entryIndex++;
                continue;
            }

            OctreeNode child = node.Children[childIndex];
            child.EntryIndices.Add(currentEntryIndex);
            _entries[currentEntryIndex].Node = child;
            node.EntryIndices.RemoveAt(entryIndex);
        }

        for (int i = 0; i < node.Children.Length; i++)
        {
            OctreeNode child = node.Children[i];
            if (child.EntryIndices.Count > Options.NodeCapacity &&
                child.Depth < Options.MaxDepth &&
                _boundsPartitioner.CanSubdivide(child.Bounds))
            {
                Subdivide(child);
            }
        }
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
        for (int i = 0; i < node.Children?.Length; i++)
        {
            OctreeNode child = node.Children[i];
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
        for (int i = 0; i < node.Children?.Length; i++)
        {
            OctreeNode child = node.Children[i];
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
        for (int i = 0; i < node.EntryIndices.Count; i++)
        {
            if (node.EntryIndices[i] != entryIndex)
                continue;

            node.EntryIndices.RemoveAt(i);
            _entries[entryIndex].Node = null;
            return;
        }

        throw new InvalidOperationException("Octree entry was not found in its owning node.");
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

        int newCapacity = QueryCollectionGuards.NormalizeCapacity(capacity);
        Array.Resize(ref _entries, newCapacity);
        _keyToEntryIndex.ResizeAndRehash(newCapacity, _peakCount, IsAllocatedEntry, GetEntryKey);
        QueryCollectionDiagnostics.WriteInfo(_diagnosticSource, $"Resized octree entry storage to {newCapacity} entries.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int FindEntryIndex(TKey key)
    {
        return _keyToEntryIndex.Find(key, MatchesEntryKey);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool MatchesEntryKey(int index, TKey key)
    {
        return _entries[index].IsAllocated && EqualityComparer<TKey>.Default.Equals(_entries[index].Key, key);
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

        for (int i = 0; i < node.Children?.Length; i++)
            count += CountNodes(node.Children[i]);

        return count;
    }

    private static int GetMaxDepth(OctreeNode node)
    {
        int maxDepth = node.Depth;
        if (!node.HasChildren)
            return maxDepth;

        for (int i = 0; i < node.Children?.Length; i++)
            maxDepth = Math.Max(maxDepth, GetMaxDepth(node.Children[i]));

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
