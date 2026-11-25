//=======================================================================
// SwiftBVH.cs
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
/// Represents a Bounding Volume Hierarchy (BVH) optimized for spatial queries.
/// </summary>
/// <typeparam name="TKey">The stable key type; equality and hashing must remain unchanged while
/// indexed.</typeparam>
/// <typeparam name="TVolume">The value-type volume backend providing consistent intersection, union, equality and
/// cost operations.</typeparam>
/// <remarks>
/// Mutable runtime index: synchronize all access externally, including queries that use shared scratch/stamps.
/// Key, volume and result callbacks must not reenter or mutate the index. Bounds are copied and keys are retained;
/// changing a key's equality/hash breaks lookup. Query ordering depends on current topology and mutation history;
/// it is not sorted or a serialized order guarantee. Determinism requires deterministic keys, input order and
/// volume/mapping arithmetic. Each non-null key owns one leaf; inserting an equal key updates its bounds.
/// </remarks>
public class SwiftBVH<TKey, TVolume>
    where TKey : notnull
    where TVolume : struct, IBoundVolume<TVolume>
{
    #region Static & Constants

    private const string _diagnosticSource = nameof(SwiftBVH<TKey, TVolume>);

    #endregion

    #region Fields

    private SwiftBVHNode<TKey, TVolume>[] _nodePool;
    private int _peakIndex;
    private int _leafCount;

    private readonly QueryKeyIndexMap<TKey> _keyToNodeIndex;
    // A full binary tree has 2N-1 live nodes; traversal needs at most N pending subtrees.
    private int[] _queryTraversal;

    private readonly SwiftIntStack _freeIndices = new();

    private int _rootNodeIndex;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.Query.SwiftBVH`2" /> class with the
    /// specified capacity.
    /// </summary>
    /// <param name="capacity">The requested initial storage length in nodes, including internal nodes.
    /// Non-positive values normalize to one; growth rounds up. Keep capacities below 2^30 and within available
    /// array memory.</param>
    public SwiftBVH(int capacity)
    {
        capacity = SwiftHashTools.NextPowerOfTwo(capacity);
        _nodePool = new SwiftBVHNode<TKey, TVolume>[capacity].Populate(() =>
            new SwiftBVHNode<TKey, TVolume>() { ParentIndex = -1, LeftChildIndex = -1, RightChildIndex = -1 });
        _queryTraversal = new int[(capacity + 1) / 2];
        _keyToNodeIndex = new QueryKeyIndexMap<TKey>(capacity, MatchesEntryKey, IsLeafNode, GetNodeValue);

        _rootNodeIndex = -1;

        _freeIndices = new SwiftIntStack(SwiftIntStack.DefaultCapacity);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the underlying pool of nodes used in the BVH.
    /// </summary>
    /// <value>The live owned node array, including inactive storage.</value>
    /// <remarks>
    /// Borrow for inspection only. Direct writes can corrupt topology, key lookup and counts. Growth replaces the
    /// array; previously held arrays become detached. Node indices are reused after removal and clear.
    /// </remarks>
    public SwiftBVHNode<TKey, TVolume>[] NodePool => _nodePool;

    /// <summary>
    /// Gets the root node of the BVH.
    /// </summary>
    /// <value>A value copy of the allocated root node, or SwiftBVHNode.Default for an empty/unallocated
    /// root.</value>
    /// <remarks>
    /// Changing this returned struct does not change the tree. Child indices address NodePool and can be
    /// invalidated or reused by mutation.
    /// </remarks>
    public SwiftBVHNode<TKey, TVolume> RootNode => _rootNodeIndex >= 0 && _nodePool[_rootNodeIndex].IsAllocated
        ? _nodePool[_rootNodeIndex]
        : SwiftBVHNode<TKey, TVolume>.Default;

    /// <summary>
    /// Gets the index of the root node in the BVH.
    /// </summary>
    /// <value>The current root's node-pool index; -1 when the tree is empty.</value>
    public int RootNodeIndex => _rootNodeIndex;

    /// <summary>
    /// Gets the total number of leaf nodes in the BVH.
    /// </summary>
    /// <value>The number of distinct indexed keys, each owning one leaf.</value>
    public int Count => _leafCount;

    #endregion

    #region Collection Manipulation

    /// <summary>
    /// Allocates a new node with the specified value, bounds, and leaf status.
    /// Reuses indices from the freelist when available.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AllocateNode(TKey value, TVolume bounds, bool isLeaf)
    {
        int index;

        // Check if there are any reusable indices in the freelist
        if (_freeIndices.Count > 0)
            index = _freeIndices.Pop(); // Reuse an available index
        else
        {
            if (_peakIndex + 1 >= _nodePool.Length)
                Resize(_nodePool.Length * 2);

            // Allocate a new index if freelist is empty
            index = _peakIndex++;
        }

        ref SwiftBVHNode<TKey, TVolume> node = ref _nodePool[index];
        node.Reset(); // Explicit reset
        node.MyIndex = index;
        node.Value = value;
        node.Bounds = bounds;

        if (isLeaf)
        {
            node.IsLeaf = isLeaf;
            node.SubtreeSize = 1;
            _leafCount++;
        }

        node.IsAllocated = true;

        return index;
    }

    /// <summary>
    /// Inserts a key and copied bounds, or updates the bounds of an existing key.
    /// </summary>
    /// <param name="value">The non-null stable key to insert or locate; reference keys are retained, not
    /// cloned.</param>
    /// <param name="bounds">The volume in the same caller-defined coordinate space and units as other indexed
    /// bounds; copied by value. No transform is applied.</param>
    /// <returns>true for a new key; false for an existing key, whose bounds are updated.</returns>
    /// <exception cref="T:System.ArgumentNullException">value is null; the index is unchanged.</exception>
    /// <remarks>
    /// An existing key retains its original stored key and leaf index, without allocating another leaf or
    /// changing Count. Equal bounds leave the tree unchanged. Key/volume/allocation failures can leave partial
    /// mutation; the operation is not transactional.
    /// </remarks>
    public bool Insert(TKey value, TVolume bounds)
    {
        ChronicleGuard.ThrowIfNullGeneric(value, nameof(value));

        int existingIndex = _keyToNodeIndex.Find(value);
        if (existingIndex >= 0)
        {
            UpdateEntryBounds(existingIndex, bounds);
            return false;
        }

        int newNodeIndex = AllocateNode(value, bounds, true); // Allocate new node as a leaf
        _rootNodeIndex = InsertIntoTree(_rootNodeIndex, newNodeIndex);
        InsertIntoBuckets(value, newNodeIndex);
        return true;
    }

    /// <summary>
    /// Inserts a node into the tree while maintaining tree balance.
    /// Adjusts parent-child relationships as necessary.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private int InsertIntoTree(int parentNodeIndex, int newNodeIndex)
    {
        if (parentNodeIndex < 0 || !_nodePool[parentNodeIndex].IsAllocated)
            return newNodeIndex;

        if (_nodePool[parentNodeIndex].IsLeaf)
            return CreateParentForLeaves(parentNodeIndex, newNodeIndex);

        InsertIntoBestChild(parentNodeIndex, newNodeIndex);
        RefreshParentNode(parentNodeIndex);
        return parentNodeIndex;
    }

    private int CreateParentForLeaves(int existingLeafIndex, int newLeafIndex)
    {
        TVolume combinedBounds = _nodePool[existingLeafIndex].Bounds.Union(_nodePool[newLeafIndex].Bounds);
        int oldParentIndex = _nodePool[existingLeafIndex].ParentIndex;
        int newParentIndex = AllocateNode(default!, combinedBounds, false);

        ref SwiftBVHNode<TKey, TVolume> newParentNode = ref _nodePool[newParentIndex];
        newParentNode.ParentIndex = oldParentIndex;
        newParentNode.LeftChildIndex = existingLeafIndex;
        newParentNode.RightChildIndex = newLeafIndex;
        newParentNode.SubtreeSize = 1 + _nodePool[existingLeafIndex].SubtreeSize + _nodePool[newLeafIndex].SubtreeSize;

        _nodePool[existingLeafIndex].ParentIndex = newParentIndex;
        _nodePool[newLeafIndex].ParentIndex = newParentIndex;

        return newParentIndex;
    }

    private void InsertIntoBestChild(int parentNodeIndex, int newNodeIndex)
    {
        ref SwiftBVHNode<TKey, TVolume> parentNode = ref _nodePool[parentNodeIndex];
        if (ShouldInsertIntoLeftChild(parentNodeIndex, newNodeIndex))
            parentNode.LeftChildIndex = InsertIntoTree(parentNode.LeftChildIndex, newNodeIndex);
        else
            parentNode.RightChildIndex = InsertIntoTree(parentNode.RightChildIndex, newNodeIndex);
    }

    private bool ShouldInsertIntoLeftChild(int parentNodeIndex, int newNodeIndex)
    {
        SwiftBVHNode<TKey, TVolume> parentNode = _nodePool[parentNodeIndex];
        SwiftBVHNode<TKey, TVolume> leftChild = _nodePool[parentNode.LeftChildIndex];
        SwiftBVHNode<TKey, TVolume> rightChild = _nodePool[parentNode.RightChildIndex];
        int leftSize = GetSubtreeSize(leftChild);
        int rightSize = GetSubtreeSize(rightChild);

        if (IsSeverelyUnbalanced(leftSize, rightSize))
            return leftSize <= rightSize;

        return ShouldInsertIntoLowerCostChild(
            leftChild,
            rightChild,
            leftSize,
            rightSize,
            _nodePool[newNodeIndex].Bounds);
    }

    private static bool IsSeverelyUnbalanced(int leftSize, int rightSize)
    {
        int maxSize = Math.Max(leftSize, rightSize);
        int minSize = Math.Min(leftSize, rightSize);
        return maxSize > minSize * 2;
    }

    private static bool ShouldInsertIntoLowerCostChild(
        SwiftBVHNode<TKey, TVolume> leftChild,
        SwiftBVHNode<TKey, TVolume> rightChild,
        int leftSize,
        int rightSize,
        TVolume newBounds)
    {
        long leftCost = leftChild.Bounds.GetCost(newBounds);
        long rightCost = rightChild.Bounds.GetCost(newBounds);

        if (leftCost == rightCost)
            return leftSize <= rightSize;

        return leftCost < rightCost;
    }

    private void RefreshParentNode(int parentNodeIndex)
    {
        ref SwiftBVHNode<TKey, TVolume> parentNode = ref _nodePool[parentNodeIndex];
        SwiftBVHNode<TKey, TVolume> leftChild = _nodePool[parentNode.LeftChildIndex];
        SwiftBVHNode<TKey, TVolume> rightChild = _nodePool[parentNode.RightChildIndex];

        parentNode.Bounds = GetCombinedBounds(leftChild, rightChild);
        parentNode.SubtreeSize = 1 + GetSubtreeSize(leftChild) + GetSubtreeSize(rightChild);
    }

    private static int GetSubtreeSize(SwiftBVHNode<TKey, TVolume> node)
    {
        return node.SubtreeSize;
    }

    /// <summary>
    /// Inserts a value into the hash bucket for fast lookup.
    /// Handles collisions with linear probing.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InsertIntoBuckets(TKey value, int nodeIndex)
    {
        _keyToNodeIndex.Insert(value, nodeIndex);
    }

    /// <summary>
    /// Updates the bounding volume of a node and propagates changes up the tree. Ensures consistency in parent
    /// bounds and subtree sizes.
    /// </summary>
    /// <param name="value">The non-null stable key to insert or locate; reference keys are retained, not
    /// cloned.</param>
    /// <param name="newBounds">Replacement bounds in the same coordinate space; copied by value. No transform is
    /// applied.</param>
    /// <exception cref="T:System.ArgumentNullException">The key is null.</exception>
    /// <remarks>
    /// Updates the existing key's bounds; absent keys change nothing. Bounds are copied by value; the caller's
    /// original is not retained. Callback/allocation failures can leave partial mutation.
    /// </remarks>
    public void UpdateEntryBounds(TKey value, TVolume newBounds)
    {
        ChronicleGuard.ThrowIfNullGeneric(value, nameof(value));

        int index = _keyToNodeIndex.Find(value);
        if (index == -1) return;

        UpdateEntryBounds(index, newBounds);
    }

    private void UpdateEntryBounds(int index, TVolume newBounds)
    {
        ref SwiftBVHNode<TKey, TVolume> node = ref _nodePool[index];
        if (!node.IsAllocated) return; // Skip update if node has been removed

        TVolume oldBounds = node.Bounds;
        if (oldBounds.BoundsEquals(newBounds))
            return; // Skip unnecessary updates

        node.Bounds = newBounds;

        // Propagate changes up the tree
        int parentIndex = node.ParentIndex;
        while (parentIndex != -1)
        {
            ref SwiftBVHNode<TKey, TVolume> parent = ref _nodePool[parentIndex];
            SwiftBVHNode<TKey, TVolume> leftChild = _nodePool[parent.LeftChildIndex];
            SwiftBVHNode<TKey, TVolume> rightChild = _nodePool[parent.RightChildIndex];

            TVolume newParentBounds = GetCombinedBounds(leftChild, rightChild);
            if (parent.Bounds.BoundsEquals(newParentBounds))
                break; // No further updates needed

            parent.Bounds = newParentBounds;
            parentIndex = parent.ParentIndex;
        }
    }

    /// <summary>
    /// Removes a value and its associated bounding volume from the BVH. Updates tree structure and clears hash
    /// bucket entries.
    /// </summary>
    /// <param name="value">The non-null stable key to insert or locate; reference keys are retained, not
    /// cloned.</param>
    /// <returns>true if the indexed key and its leaf were removed; false if absent, with entries
    /// unchanged.</returns>
    /// <exception cref="T:System.ArgumentNullException">The key is null.</exception>
    public bool Remove(TKey value)
    {
        ChronicleGuard.ThrowIfNullGeneric(value, nameof(value));

        int nodeIndex = _keyToNodeIndex.Find(value);
        if (nodeIndex == -1) return false;

        // If the node is the root and the only node, reset the BVH
        if (nodeIndex == RootNodeIndex && _leafCount == 1)
        {
            Clear();
            return true;
        }

        RemoveFromBuckets(value); // Ensure the bucket is cleared before further operations

        // Remove node and update tree structure
        RemoveFromTree(nodeIndex);

        return true;
    }

    /// <summary>
    /// Removes an entry from the hash buckets, resolving collisions as necessary.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RemoveFromBuckets(TKey value)
    {
        _keyToNodeIndex.Remove(value);
    }

    /// <summary>
    /// Removes a leaf node from the tree, collapses its parent, and propagates
    /// bound and subtree-size updates upward.  Every internal node is guaranteed
    /// to have exactly two children after this operation completes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RemoveFromTree(int nodeIndex)
    {
        int parentIndex = _nodePool[nodeIndex].ParentIndex;

        int siblingIndex = ReleaseLeafAndParent(nodeIndex, parentIndex, out int grandParentIndex);
        PromoteSiblingToGrandParent(siblingIndex, parentIndex, grandParentIndex);
        if (grandParentIndex != -1)
            RefreshAncestors(grandParentIndex);
    }

    private int ReleaseLeafAndParent(int nodeIndex, int parentIndex, out int grandParentIndex)
    {
        ref SwiftBVHNode<TKey, TVolume> parent = ref _nodePool[parentIndex];
        int siblingIndex = parent.LeftChildIndex == nodeIndex
            ? parent.RightChildIndex
            : parent.LeftChildIndex;
        grandParentIndex = parent.ParentIndex;

        // Push parent before the leaf so that the leaf index sits on top of the
        // freelist stack and is reused first by the next allocation.
        parent.Reset();
        _freeIndices.Push(parentIndex);

        _leafCount--;
        _nodePool[nodeIndex].Reset();
        _freeIndices.Push(nodeIndex);

        return siblingIndex;
    }

    private void PromoteSiblingToGrandParent(int siblingIndex, int parentIndex, int grandParentIndex)
    {
        _nodePool[siblingIndex].ParentIndex = grandParentIndex;

        if (grandParentIndex == -1)
        {
            _rootNodeIndex = siblingIndex;
            return;
        }

        ref SwiftBVHNode<TKey, TVolume> grandParent = ref _nodePool[grandParentIndex];
        if (grandParent.LeftChildIndex == parentIndex)
            grandParent.LeftChildIndex = siblingIndex;
        else
            grandParent.RightChildIndex = siblingIndex;
    }

    private void RefreshAncestors(int current)
    {
        while (current != -1)
        {
            RefreshParentNode(current);
            current = _nodePool[current].ParentIndex;
        }
    }

    #endregion

    #region Capacity Management

    /// <summary>
    /// Ensures the BVH has sufficient capacity, resizing the node pool and buckets if needed.
    /// </summary>
    /// <param name="capacity">The requested initial storage length in nodes, including internal nodes.
    /// Non-positive values normalize to one; growth rounds up. Keep capacities below 2^30 and within available
    /// array memory.</param>
    /// <remarks>
    /// Grows only when the normalized requested length exceeds existing storage. No entries are removed; borrowed
    /// arrays can be detached. Also reserves owner-held query traversal storage for the supported leaf count.
    /// Capacity is node storage, not a no-allocation guarantee for later mutations.
    /// </remarks>
    public void EnsureCapacity(int capacity)
    {
        capacity = SwiftHashTools.NextPowerOfTwo(capacity);
        if (capacity > _nodePool.Length)
            Resize(capacity);
    }

    /// <summary>
    /// Resizes the internal node pool to accommodate additional nodes.
    /// Preserves existing nodes and reinitializes the expanded capacity.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Resize(int newSize)
    {
        SwiftBVHNode<TKey, TVolume>[] newArray = new SwiftBVHNode<TKey, TVolume>[newSize];
        Array.Copy(_nodePool, 0, newArray, 0, _peakIndex);

        for (int i = _peakIndex; i < newSize; i++)
            newArray[i].Reset(); // set default index lookup values

        _nodePool = newArray;
        _queryTraversal = new int[(newSize + 1) / 2];

        ResizeBuckets(newSize);
        SwiftCollectionDiagnostics.Shared.Info($"Resized BVH storage to {newSize} nodes.", _diagnosticSource);
    }

    /// <summary>
    /// Resizes and rehashes the hash buckets to maintain lookup efficiency.
    /// Rehashes existing nodes after resizing.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ResizeBuckets(int newSize)
    {
        _keyToNodeIndex.ResizeAndRehash(newSize, _peakIndex);
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Gets the combined bounding volume of two child nodes.
    /// Handles cases where one or both children are missing.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TVolume GetCombinedBounds(SwiftBVHNode<TKey, TVolume> leftChild, SwiftBVHNode<TKey, TVolume> rightChild)
    {
        return leftChild.Bounds.Union(rightChild.Bounds);
    }

    /// <summary>
    /// Queries the BVH for values whose bounding volumes intersect with the specified volume. Uses a stack-based
    /// approach for efficient traversal.
    /// </summary>
    /// <param name="queryBounds">The query region in the index coordinate space, copied by value; it is not
    /// retained or transformed.</param>
    /// <param name="results">The caller-owned mutable collection to append matching keys to. Existing entries are
    /// preserved; no deduplication against pre-existing entries is performed.</param>
    /// <exception cref="T:System.ArgumentNullException">results is null.</exception>
    /// <exception cref="T:System.InvalidOperationException">Traversal encounters an unallocated node, indicating
    /// invalid tree state.</exception>
    /// <remarks>
    /// Appends without clearing results. Empty/no-hit queries preserve existing entries. Destination Add or
    /// volume/backend exceptions can leave partially appended output; no rollback is provided. Do not reenter or
    /// mutate the index from result callbacks. Visits right subtrees before left because traversal uses a stack.
    /// Appends each matching key once. Traversal storage is reserved with node capacity, so traversal does not
    /// allocate; growing caller-owned results can allocate. Queries share owner-held scratch and require
    /// external synchronization.
    /// </remarks>
    public void Query(TVolume queryBounds, ICollection<TKey> results)
    {
        ChronicleGuard.ThrowIfNull(results, nameof(results));

        if (RootNodeIndex == -1) return;

        Query(queryBounds, results, _queryTraversal);
    }

    /// <summary>
    /// Appends matching values using caller-owned storage without growing either buffer.
    /// </summary>
    /// <param name="queryBounds">The query region in the index coordinate space, copied by value; it is not
    /// retained or transformed.</param>
    /// <param name="results">The caller-owned mutable collection to append matching keys to. Existing entries are
    /// preserved; no deduplication against pre-existing entries is performed.</param>
    /// <param name="traversal">Caller-owned scratch span with at least Count integers, disjoint from results
    /// backing storage. Its contents may be overwritten and are not retained.</param>
    /// <returns>true when traversal completed, including zero hits; false if traversal.Length or spare result
    /// capacity is less than Count, leaving results and scratch unchanged.</returns>
    /// <exception cref="T:System.ArgumentNullException">results is null.</exception>
    /// <exception cref="T:System.ArgumentException">results is a SwiftList subclass or traversal overlaps its
    /// integer backing array.</exception>
    /// <exception cref="T:System.InvalidOperationException">Traversal encounters an unallocated tree
    /// node.</exception>
    /// <remarks>
    /// Requires a plain SwiftList, not a subclass. Checks worst-case space for all leaves, even if few would
    /// match. Appends in the same order as Query without growing buffers. Insufficient space is the only false
    /// result; invalid storage throws. Runtime volume/tree errors can leave partial output.
    /// </remarks>
    public bool TryQuery(TVolume queryBounds, SwiftList<TKey> results, Span<int> traversal)
    {
        ChronicleGuard.ThrowIfNull(results, nameof(results));
        ChronicleGuard.ThrowIfArgument(results.GetType() != typeof(SwiftList<TKey>),
            nameof(results), "Caller-owned query results must be a plain SwiftList.");
        ChronicleGuard.ThrowIfArgument((object)results.InnerArray is int[] backing && traversal.Overlaps(backing),
            nameof(traversal), "Traversal storage overlaps the results.");
        if (traversal.Length < Count || results.Capacity - results.Count < Count)
            return false;

        if (RootNodeIndex != -1)
            Query(queryBounds, results, traversal);
        return true;
    }

    private void Query(TVolume queryBounds, ICollection<TKey> results, Span<int> traversal)
    {
        // Only [0, count) is live, and every slot is written before this query reads it.
        // Old integer indices outside that prefix retain no objects and need no clearing, even after a throw.
        int count = 0;
        traversal[count++] = RootNodeIndex;
        while (count != 0)
        {
            int index = traversal[--count];
            ref SwiftBVHNode<TKey, TVolume> node = ref _nodePool[index];
            ThrowIfQueryNodeIsUnallocated(index, node);
            if (!queryBounds.Intersects(node.Bounds))
                continue;

            if (node.IsLeaf)
                results.Add(node.Value);
            else
            {
                // The pending nodes cover disjoint subtrees, so their count is at most the live leaf count.
                traversal[count++] = node.LeftChildIndex;
                traversal[count++] = node.RightChildIndex;
            }
        }
    }

    private static void ThrowIfQueryNodeIsUnallocated(int index, SwiftBVHNode<TKey, TVolume> node)
    {
        if (node.IsAllocated)
            return;

        SwiftCollectionDiagnostics.Shared.Error($"Encountered an unallocated node at index {index} during query traversal.", _diagnosticSource);
        throw new InvalidOperationException($"Encountered an unallocated node at index {index} during query traversal.");
    }

    /// <summary>
    /// Finds the index of a node by its value in the BVH using hash buckets. Returns -1 if the value is not
    /// found.
    /// </summary>
    /// <param name="value">The non-null stable key to insert or locate; reference keys are retained, not
    /// cloned.</param>
    /// <returns>The physical node index of the matching leaf, or -1 if absent. Indices can be reused after
    /// mutation.</returns>
    /// <exception cref="T:System.ArgumentNullException">value is null.</exception>
    public int FindEntry(TKey value)
    {
        ChronicleGuard.ThrowIfNullGeneric(value, nameof(value));
        return _keyToNodeIndex.Find(value);
    }

    /// <summary>
    /// Clears the BVH, resetting all nodes, buckets, and metadata.
    /// </summary>
    /// <remarks>
    /// Removes all entries and retained keys while preserving reusable entry capacity. Resets key lookup and
    /// free-index metadata. Previously obtained node indices/views are no longer valid.
    /// </remarks>
    public void Clear()
    {
        if (RootNodeIndex == -1) return;

        for (int i = 0; i < _peakIndex; i++)
            _nodePool[i].Reset();

        _keyToNodeIndex.Clear();

        _freeIndices.Reset();

        _leafCount = 0;
        _peakIndex = 0;
        _rootNodeIndex = -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TKey GetNodeValue(int index) => _nodePool[index].Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsLeafNode(int index) => _nodePool[index].IsLeaf;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool MatchesEntryKey(int index, TKey value)
    {
        return _nodePool[index].IsLeaf && EqualityComparer<TKey>.Default.Equals(_nodePool[index].Value, value);
    }

    #endregion
}
