using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace SwiftCollections.Query
{
    /// <summary>
    /// Represents a Bounding Volume Hierarchy (BVH) optimized for spatial queries.
    /// </summary>
    public class SwiftBVH<T>
    {
        #region Static & Constants

        // Thread-local stack for queries
        private static readonly ThreadLocal<IntStack> _threadLocalNodeStack =
            new ThreadLocal<IntStack>(() => new IntStack(0));

        private const int _childBalanceThreshold = 2;

        #endregion

        #region Fields

        private SwiftBVHNode<T>[] _nodePool;
        private int _peakIndex;
        private int _leafCount;

        private int[] _buckets; // Maps hash indices to node indices
        private int _bucketMask; // Always _nodePool.Length - 1

        private readonly IntStack _freeIndices = new IntStack();

        private int _rootNodeIndex;

        private ReaderWriterLockSlim _bvhLock = new ReaderWriterLockSlim();

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="SwiftBVH{T}"/> class with the specified capacity.
        /// </summary>
        public SwiftBVH(int capacity)
        {
            capacity = HashTools.NextPowerOfTwo(capacity);
            _nodePool = new SwiftBVHNode<T>[capacity].Populate(() =>
                new SwiftBVHNode<T>() { ParentIndex = -1, LeftChildIndex = -1, RightChildIndex = -1 });
            _buckets = new int[capacity].Populate(() => -1);

            _bucketMask = capacity - 1;
            _rootNodeIndex = -1;

            _freeIndices = new IntStack(IntStack.DefaultCapacity);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the underlying pool of nodes used in the BVH.
        /// </summary>
        public SwiftBVHNode<T>[] NodePool => _nodePool;

        /// <summary>
        /// Gets the root node of the BVH.
        /// </summary>
        public SwiftBVHNode<T> RootNode => _rootNodeIndex >= 0 && _nodePool[_rootNodeIndex].IsAllocated
            ? _nodePool[_rootNodeIndex]
            : SwiftBVHNode<T>.Default;

        /// <summary>
        /// Gets the index of the root node in the BVH.
        /// </summary>
        public int RootNodeIndex => _rootNodeIndex;

        /// <summary>
        /// Gets the total number of leaf nodes in the BVH.
        /// </summary>
        public int Count => _leafCount;

        #endregion

        #region Collection Manipulation

        /// <summary>
        /// Allocates a new node with the specified value, bounds, and leaf status.
        /// Reuses indices from the freelist when available.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int AllocateNode(T value, IBoundVolume bounds, bool isLeaf)
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

            ref SwiftBVHNode<T> node = ref _nodePool[index];
            node.Reset(); // Explicit reset
            node.MyIndex = index;
            node.Value = value;
            node.Bounds = bounds;

            if (isLeaf)
            {
                node.IsLeaf = isLeaf;
                _leafCount++;
            }

            node.IsAllocated = true;

            return index;
        }

        /// <summary>
        /// Inserts a bounding volume with an associated value into the BVH.
        /// Ensures tree balance and updates hash buckets.
        /// </summary>
        public bool Insert(T value, IBoundVolume bounds)
        {
            if (bounds == null)
                ThrowHelper.ThrowNotSupportedException($"{nameof(bounds)} cannot be null!");

            _bvhLock.EnterWriteLock();
            try
            {
                int newNodeIndex = AllocateNode(value, bounds, true); // Allocate new node as a leaf
                _rootNodeIndex = InsertIntoTree(_rootNodeIndex, newNodeIndex);
                InsertIntoBuckets(value, newNodeIndex);
                return true;
            }
            finally
            {
                _bvhLock.ExitWriteLock();
            }
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

            ref SwiftBVHNode<T> parentNode = ref _nodePool[parentNodeIndex];
            ref SwiftBVHNode<T> newNode = ref _nodePool[newNodeIndex];
            if (parentNode.IsLeaf)
            {
                // Create a new parent node
                int newParentIndex = AllocateNode(default, parentNode.Bounds.Union(newNode.Bounds), false);
                ref SwiftBVHNode<T> newParentNode = ref _nodePool[newParentIndex];
                newParentNode.ParentIndex = parentNode.ParentIndex;

                newParentNode.LeftChildIndex = parentNodeIndex;
                newParentNode.RightChildIndex = newNodeIndex;

                parentNode.ParentIndex = newParentIndex;
                newNode.ParentIndex = newParentIndex;

                newParentNode.SubtreeSize = 1 + parentNode.SubtreeSize + newNode.SubtreeSize;
                return newParentIndex;
            }

            // Determines the optimal child for insertion based on balance and cost metrics.
            SwiftBVHNode<T> leftChild = parentNode.HasLeftChild
               ? _nodePool[parentNode.LeftChildIndex]
               : SwiftBVHNode<T>.Default;
            SwiftBVHNode<T> rightChild = parentNode.HasRightChild
                ? _nodePool[parentNode.RightChildIndex]
                : SwiftBVHNode<T>.Default;

            // Compute balance metrics
            int leftSize = leftChild.IsAllocated ? leftChild.SubtreeSize : 0;
            int rightSize = rightChild.IsAllocated ? rightChild.SubtreeSize : 0;

            bool isInsertingLeft;
            if (Math.Abs(leftSize - rightSize) > _childBalanceThreshold)
            {
                // Insert into the smaller subtree to maintain balance
                if (leftSize < rightSize)
                    isInsertingLeft = true;
                else
                    isInsertingLeft = false;
            }
            else
            {
                // Compute cost metrics
                int leftCost = parentNode.HasLeftChild
                    ? leftChild.Bounds.GetCost(newNode.Bounds)
                    : 0;

                int rightCost = parentNode.HasRightChild
                    ? rightChild.Bounds.GetCost(newNode.Bounds)
                    : 0;

                // Insert into the child with the least volume increase
                if (leftCost < rightCost)
                    isInsertingLeft = true;
                else
                    isInsertingLeft = false;
            }

            if (isInsertingLeft)
            {
                parentNode.LeftChildIndex = InsertIntoTree(parentNode.LeftChildIndex, newNodeIndex);
                leftChild = parentNode.HasLeftChild
                   ? _nodePool[parentNode.LeftChildIndex]
                   : SwiftBVHNode<T>.Default;
            }
            else
            {
                parentNode.RightChildIndex = InsertIntoTree(parentNode.RightChildIndex, newNodeIndex);
                rightChild = parentNode.HasRightChild
                    ? _nodePool[parentNode.RightChildIndex]
                    : SwiftBVHNode<T>.Default;
            }

            parentNode.Bounds = GetCombinedBounds(leftChild, rightChild);
            parentNode.SubtreeSize = 1
                + (leftChild.IsAllocated ? leftChild.SubtreeSize : 0)
                + (rightChild.IsAllocated ? rightChild.SubtreeSize : 0);

            return parentNodeIndex;
        }

        /// <summary>
        /// Inserts a value into the hash bucket for fast lookup.
        /// Handles collisions with linear probing.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InsertIntoBuckets(T value, int nodeIndex)
        {
            int hash = value.GetHashCode() & 0x7FFFFFFF;
            int bucketIndex = hash & _bucketMask;

            // Linear probe to find an empty slot
            while (_buckets[bucketIndex] != -1)
                bucketIndex = (bucketIndex + 1) & _bucketMask;

            _buckets[bucketIndex] = nodeIndex;
        }

        /// <summary>
        /// Updates the bounding volume of a node and propagates changes up the tree.
        /// Ensures consistency in parent bounds and subtree sizes.
        /// </summary>
        public void UpdateEntryBounds(T value, IBoundVolume newBounds)
        {
            if (value == null) ThrowHelper.ThrowArgumentNullException(nameof(value));

            int index = FindEntry(value);
            if (index == -1) return;

            _bvhLock.EnterWriteLock();
            try
            {
                ref SwiftBVHNode<T> node = ref _nodePool[index];
                if (!node.IsAllocated) return; // Skip update if node has been removed

                node.Bounds = newBounds;
                if (node.Bounds.Equals(newBounds)) return; // Skip unnecessary updates

                // Propagate changes up the tree
                int parentIndex = node.ParentIndex;
                while (parentIndex != -1)
                {
                    ref SwiftBVHNode<T> parent = ref _nodePool[parentIndex];
                    SwiftBVHNode<T> leftChild = parent.HasLeftChild
                        ? _nodePool[parent.LeftChildIndex]
                        : SwiftBVHNode<T>.Default;
                    SwiftBVHNode<T> rightChild = parent.HasRightChild
                        ? _nodePool[parent.RightChildIndex]
                        : SwiftBVHNode<T>.Default;

                    IBoundVolume newParentBounds = GetCombinedBounds(leftChild, rightChild);
                    if (parent.Bounds.Equals(newParentBounds))
                        break; // No further updates needed

                    parent.Bounds = newParentBounds;
                    parentIndex = parent.ParentIndex;
                }
            }
            finally
            {
                _bvhLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Removes a value and its associated bounding volume from the BVH.
        /// Updates tree structure and clears hash bucket entries.
        /// </summary>
        public bool Remove(T value)
        {
            if (value == null) ThrowHelper.ThrowArgumentNullException(nameof(value));

            int nodeIndex = FindEntry(value);
            if (nodeIndex == -1) return false;

            _bvhLock.EnterWriteLock();
            try
            {
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
            finally
            {
                _bvhLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Removes an entry from the hash buckets, resolving collisions as necessary.
        /// Throws an exception if the value is not found.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RemoveFromBuckets(T value)
        {
            int hash = value.GetHashCode() & 0x7FFFFFFF;
            int bucketIndex = hash & _bucketMask;

            // Linear probing to resolve collisions
            while (_buckets[bucketIndex] != -1)
            {
                int nodeIndex = _buckets[bucketIndex];
                if (_nodePool[nodeIndex].IsLeaf && EqualityComparer<T>.Default.Equals(_nodePool[nodeIndex].Value, value))
                {
                    _buckets[bucketIndex] = -1;
                    return;
                }

                bucketIndex = (bucketIndex + 1) & _bucketMask;
            }
        }

        /// <summary>
        /// Recursively removes a node and updates the bounds and subtree sizes of parents.
        /// Ensures integrity of the BVH structure.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RemoveFromTree(int nodeIndex)
        {
            while (nodeIndex != -1)
            {
                ref SwiftBVHNode<T> currentNode = ref _nodePool[nodeIndex];

                int parentIndex = currentNode.ParentIndex;
                if (!currentNode.HasChildren)
                {
                    // Clean up leaf or empty parent
                    if (currentNode.HasParent)
                    {
                        // Remove the child reference from the parent root
                        ref SwiftBVHNode<T> parentNode = ref _nodePool[parentIndex];
                        if (parentNode.LeftChildIndex == nodeIndex)
                            parentNode.LeftChildIndex = -1;
                        else if (parentNode.RightChildIndex == nodeIndex)
                            parentNode.RightChildIndex = -1;
                    }
                    else if (nodeIndex == RootNodeIndex)
                    {
                        Clear();
                        return;
                    }

                    if (currentNode.IsLeaf)
                        _leafCount--;

                    currentNode.Reset();
                    _freeIndices.Push(nodeIndex);
                    nodeIndex = parentIndex;
                    continue;
                }

                // Resize parent that still has children...
                SwiftBVHNode<T> leftChild = currentNode.HasLeftChild
                    ? _nodePool[currentNode.LeftChildIndex]
                    : SwiftBVHNode<T>.Default;
                SwiftBVHNode<T> rightChild = currentNode.HasRightChild
                    ? _nodePool[currentNode.RightChildIndex]
                    : SwiftBVHNode<T>.Default;

                currentNode.Bounds = GetCombinedBounds(leftChild, rightChild);
                currentNode.SubtreeSize = 1
                    + (leftChild.IsAllocated ? leftChild.SubtreeSize : 0)
                    + (rightChild.IsAllocated ? rightChild.SubtreeSize : 0);

                nodeIndex = parentIndex;
            }
        }

        #endregion

        #region Capacity Management

        /// <summary>
        /// Ensures the BVH has sufficient capacity, resizing the node pool and buckets if needed.
        /// </summary>
        public void EnsureCapacity(int capacity)
        {
            capacity = HashTools.NextPowerOfTwo(capacity);
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
            SwiftBVHNode<T>[] newArray = new SwiftBVHNode<T>[newSize];
            Array.Copy(_nodePool, 0, newArray, 0, _peakIndex);

            for (int i = _peakIndex; i < newSize; i++)
                newArray[i].Reset(); // set default index lookup values

            _nodePool = newArray;

            ResizeBuckets(newSize);
        }

        /// <summary>
        /// Resizes and rehashes the hash buckets to maintain lookup efficiency.
        /// Rehashes existing nodes after resizing.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ResizeBuckets(int newSize)
        {
            _buckets = new int[newSize].Populate(() => -1);
            _bucketMask = newSize - 1;

            // Rehash existing leaf nodes
            for (int i = 0; i < _peakIndex; i++)
            {
                if (!_nodePool[i].IsLeaf)
                    continue;

                int hash = _nodePool[i].Value.GetHashCode() & 0x7FFFFFFF;
                int bucketIndex = hash & _bucketMask;

                // Linear probe to find an empty slot
                while (_buckets[bucketIndex] != -1)
                    bucketIndex = (bucketIndex + 1) & _bucketMask;

                _buckets[bucketIndex] = i;
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Gets the combined bounding volume of two child nodes.
        /// Handles cases where one or both children are missing.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IBoundVolume GetCombinedBounds(SwiftBVHNode<T> leftChild, SwiftBVHNode<T> rightChild)
        {
            if (leftChild.IsAllocated && rightChild.IsAllocated)
                return leftChild.Bounds.Union(rightChild.Bounds);
            if (leftChild.IsAllocated)
                return leftChild.Bounds;
            return rightChild.Bounds;
        }

        /// <summary>
        /// Queries the BVH for values whose bounding volumes intersect with the specified volume.
        /// Uses a stack-based approach for efficient traversal.
        /// </summary>
        public void Query(IBoundVolume queryBounds, ICollection<T> results)
        {
            if (queryBounds == null)
                ThrowHelper.ThrowNotSupportedException($"{nameof(queryBounds)} cannot be null!");

            if (RootNodeIndex == -1) return;

            _bvhLock.EnterReadLock();
            try
            {
                IntStack nodeStack = _threadLocalNodeStack.Value;
                nodeStack.EnsureCapacity(_peakIndex + 1);
                nodeStack.Clear();

                nodeStack.Push(RootNodeIndex);

                while (nodeStack.Count > 0)
                {
                    int index = nodeStack.Pop();
                    SwiftBVHNode<T> node = _nodePool[index];

                    if (!node.IsAllocated)
                        ThrowHelper.ThrowNotSupportedException($"{nameof(node)} cannot be null!");

                    if (!queryBounds.Intersects(node.Bounds))
                        continue;

                    if (node.IsLeaf)
                    {
                        results.Add(node.Value);
                        continue;
                    }

                    if (node.HasLeftChild)
                        nodeStack.Push(node.LeftChildIndex);
                    if (node.HasRightChild)
                        nodeStack.Push(node.RightChildIndex);
                }
            }
            finally
            {
                _bvhLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Finds the index of a node by its value in the BVH using hash buckets.
        /// Returns -1 if the value is not found.
        /// </summary>
        public int FindEntry(T value)
        {
            if (value == null) ThrowHelper.ThrowArgumentNullException(nameof(value));

            _bvhLock.EnterReadLock();
            try
            {
                int hash = value.GetHashCode() & 0x7FFFFFFF;
                int bucketIndex = hash & _bucketMask;

                // Linear probing to resolve collisions
                while (_buckets[bucketIndex] != -1)
                {
                    int nodeIndex = _buckets[bucketIndex];
                    if (_nodePool[nodeIndex].IsLeaf && EqualityComparer<T>.Default.Equals(_nodePool[nodeIndex].Value, value))
                        return nodeIndex;

                    bucketIndex = (bucketIndex + 1) & _bucketMask;
                }

                return -1; // Not found
            }
            finally
            {
                _bvhLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Clears the BVH, resetting all nodes, buckets, and metadata.
        /// </summary>
        public void Clear()
        {
            if (RootNodeIndex == -1) return;

            for (int i = 0; i < _peakIndex; i++)
                _nodePool[i].Reset();

            for (int i = 0; i < _buckets.Length; i++)
                _buckets[i] = -1;

            _freeIndices.Reset();

            _leafCount = 0;
            _peakIndex = 0;
            _rootNodeIndex = -1;
        }

        #endregion
    }
}
