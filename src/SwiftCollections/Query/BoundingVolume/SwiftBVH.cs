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
        private int _rootIndex;
        private int _nextFreeIndex;

        private int _leafCount;

        private int[] _buckets; // Maps hash indices to node indices
        private int _bucketMask; // Always _nodePool.Length - 1

        private readonly IntStack _freeList = new IntStack();

        #endregion

        #region Nested Types

        private struct IntStack
        {
            public const int DefaultCapacity = 4;
            private int[] _array;
            internal int _count;

            public int Count => _count;

            public IntStack(int capacity)
            {
                _array = capacity == 0 ? new int[DefaultCapacity] : new int[capacity];
                _count = 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Push(int item)
            {
                EnsureCapacity(_count + 1);
                _array[_count++] = item;
            }

            public void EnsureCapacity(int min)
            {
                if (min >= _array.Length)
                {
                    int[] newArray = new int[_array.Length * 2];
                    Array.Copy(_array, 0, newArray, 0, _count);
                    _array = newArray;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Pop() => _array[--_count];

            public void Reset()
            {
                _array = new int[DefaultCapacity];
                _count = 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Clear()
            {
                Array.Clear(_array, 0, _count);
                _count = 0;
            }
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="SwiftBVH{T}"/> class with the specified capacity.
        /// </summary>
        public SwiftBVH(int capacity)
        {
            capacity = HashHelper.NextPowerOfTwo(capacity);
            _nodePool = new SwiftBVHNode<T>[capacity].Populate(() =>
                new SwiftBVHNode<T>() { ParentIndex = -1, LeftChildIndex = -1, RightChildIndex = -1 });
            _buckets = new int[capacity];
            for (int i = 0; i < _buckets.Length; i++)
                _buckets[i] = -1;

            _bucketMask = capacity - 1;
            _rootIndex = -1;

            _freeList = new IntStack(IntStack.DefaultCapacity);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the underlying pool of nodes used in the BVH.
        /// </summary>
        public SwiftBVHNode<T>[] NodePool => _nodePool;

        /// <summary>
        /// Gets the index of the root node in the BVH.
        /// </summary>
        public int RootIndex => _rootIndex;

        /// <summary>
        /// Gets the root node of the BVH.
        /// </summary>
        public SwiftBVHNode<T> RootNode => _nodePool[_rootIndex];

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
        private int AllocateNode(T value, IBoundingVolume bounds, bool isLeaf)
        {
            int index;

            // Check if there are any reusable indices in the freelist
            if (_freeList.Count > 0)
                index = _freeList.Pop(); // Reuse an available index
            else
            {
                if (_nextFreeIndex + 1 >= _nodePool.Length)
                    Resize(_nodePool.Length * 2);

                // Allocate a new index if freelist is empty
                index = _nextFreeIndex++;
            }

            _nodePool[index].Reset(); // Explicit reset
            _nodePool[index].Value = value;
            _nodePool[index].Bounds = bounds;

            if (isLeaf)
            {
                _nodePool[index].IsLeaf = isLeaf;
                _leafCount++;
            }

            return index;
        }

        /// <summary>
        /// Inserts a bounding volume with an associated value into the BVH.
        /// Ensures tree balance and updates hash buckets.
        /// </summary>
        public bool Insert(T value, IBoundingVolume bounds)
        {
            if (bounds == null)
                ThrowHelper.ThrowNotSupportedException($"{nameof(bounds)} cannot be null!");

            lock (_nodePool)
            {
                int newNodeIndex = AllocateNode(value, bounds, true); // Allocate new node as a leaf

                _rootIndex = InsertIntoTree(_rootIndex, newNodeIndex);

                InsertIntoBuckets(value, newNodeIndex);

                return true;
            }
        }

        /// <summary>
        /// Inserts a node into the tree while maintaining tree balance.
        /// Adjusts parent-child relationships as necessary.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private int InsertIntoTree(int nodeIndex, int newNodeIndex)
        {
            if (nodeIndex == -1)
                return newNodeIndex;

            if (_nodePool[nodeIndex].IsLeaf)
            {
                ref SwiftBVHNode<T> currentNode = ref _nodePool[nodeIndex];
                ref SwiftBVHNode<T> newNode = ref _nodePool[newNodeIndex];

                // Create a new parent node
                int parentIndex = AllocateNode(default, currentNode.Bounds.Union(newNode.Bounds), false);
                ref SwiftBVHNode<T> parent = ref _nodePool[parentIndex];
                parent.ParentIndex = currentNode.ParentIndex;

                parent.LeftChildIndex = nodeIndex;
                parent.RightChildIndex = newNodeIndex;

                currentNode.ParentIndex = parentIndex;
                newNode.ParentIndex = parentIndex;

                parent.SubtreeSize = 1 + currentNode.SubtreeSize + newNode.SubtreeSize;
                return parentIndex;
            }

            // Determine optimal child for insertion
            InsertIntoOptimalChild(nodeIndex, newNodeIndex);

            UpdateNodeSubtree(nodeIndex);

            return nodeIndex;
        }

        /// <summary>
        /// Determines the optimal child for insertion based on balance and cost metrics.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InsertIntoOptimalChild(int nodeIndex, int newNodeIndex)
        {
            ref SwiftBVHNode<T> currentNode = ref _nodePool[nodeIndex];

            int leftChildIndex = currentNode.LeftChildIndex;
            int rightChildIndex = currentNode.RightChildIndex;

            // Metrics for balancing
            int leftSize = currentNode.HasLeftChild ? _nodePool[leftChildIndex].SubtreeSize : 0;
            int rightSize = currentNode.HasRightChild ? _nodePool[rightChildIndex].SubtreeSize : 0;

            if (Math.Abs(leftSize - rightSize) > _childBalanceThreshold)
            {
                // Insert into the smaller subtree to maintain balance
                if (leftSize < rightSize)
                    currentNode.LeftChildIndex = InsertIntoTree(leftChildIndex, newNodeIndex);
                else
                    currentNode.RightChildIndex = InsertIntoTree(rightChildIndex, newNodeIndex);
            }
            else
            {
                // Compute cost metrics
                int leftCost = 0;
                if (currentNode.HasLeftChild)
                {
                    IBoundingVolume leftBounds = _nodePool[leftChildIndex].Bounds;
                    IBoundingVolume encapsulatedBounds = leftBounds.Union(_nodePool[newNodeIndex].Bounds);
                    leftCost = (int)Math.Floor(encapsulatedBounds.Volume - leftBounds.Volume);
                }

                int rightCost = 0;
                if (currentNode.HasLeftChild)
                {
                    IBoundingVolume rightBounds = _nodePool[rightChildIndex].Bounds;
                    IBoundingVolume encapsulatedBounds = rightBounds.Union(_nodePool[newNodeIndex].Bounds);
                    rightCost = (int)Math.Floor(encapsulatedBounds.Volume - rightBounds.Volume);
                }

                // Insert into the child with the least volume increase
                if (leftCost < rightCost)
                    currentNode.LeftChildIndex = InsertIntoTree(leftChildIndex, newNodeIndex);
                else
                    currentNode.RightChildIndex = InsertIntoTree(rightChildIndex, newNodeIndex);
            }
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
        public void UpdateEntryBounds(T value, IBoundingVolume newBounds)
        {
            if (value == null) ThrowHelper.ThrowArgumentNullException(nameof(value));

            lock (_nodePool)
            {
                int index = FindEntry(value);
                if (index == -1) return;

                ref SwiftBVHNode<T> node = ref _nodePool[index];
                node.Bounds = newBounds;
                if (node.Bounds.Equals(newBounds)) return; // Skip unnecessary updates

                // Propagate changes up the tree
                int parentIndex = node.ParentIndex;
                while (parentIndex != -1)
                {
                    ref SwiftBVHNode<T> parent = ref _nodePool[parentIndex];
                    IBoundingVolume newParentBounds = GetCombinedBounds(parent.LeftChildIndex, parent.RightChildIndex);
                    if (parent.Bounds.Equals(newParentBounds))
                        break; // No further updates needed

                    parent.Bounds = newParentBounds;
                    parentIndex = parent.ParentIndex;
                }
            }
        }

        /// <summary>
        /// Removes a value and its associated bounding volume from the BVH.
        /// Updates tree structure and clears hash bucket entries.
        /// </summary>
        public bool Remove(T value)
        {
            if (value == null) ThrowHelper.ThrowArgumentNullException(nameof(value));

            lock (_nodePool)
            {
                int nodeIndex = FindEntry(value);
                if (nodeIndex == -1)
                    return false; // Value not found

                // If the node is the root and the only node, reset the BVH
                if (nodeIndex == _rootIndex && _leafCount == 1)
                {
                    Clear();
                    return true;
                }

                RemoveFromBuckets(value); // Ensure the bucket is cleared before further operations

                // Remove node and update tree structure
                RemoveFromTree(nodeIndex);

                return true;
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

            ThrowHelper.ThrowInvalidOperationException($"Failed to locate entry in hash buckets during removal. Value: {value}");
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
                    else if (nodeIndex == _rootIndex)
                    {
                        Clear();
                        return;
                    }

                    if (currentNode.IsLeaf)
                        _leafCount--;

                    currentNode.Reset();
                    _freeList.Push(nodeIndex);
                }
                else
                    UpdateNodeSubtree(nodeIndex);

                nodeIndex = parentIndex;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateNodeSubtree(int nodeIndex)
        {
            ref SwiftBVHNode<T> node = ref _nodePool[nodeIndex];
            node.Bounds = GetCombinedBounds(node.LeftChildIndex, node.RightChildIndex);
            node.SubtreeSize = 1 + GetSubtreeSize(node.LeftChildIndex) + GetSubtreeSize(node.RightChildIndex);
        }

        #endregion

        #region Capacity Management

        /// <summary>
        /// Ensures the BVH has sufficient capacity, resizing the node pool and buckets if needed.
        /// </summary>
        public void EnsureCapacity(int capacity)
        {
            capacity = HashHelper.NextPowerOfTwo(capacity);
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
            Array.Copy(_nodePool, 0, newArray, 0, _nextFreeIndex);

            for (int i = _nextFreeIndex; i < newSize; i++)
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
            _buckets = new int[newSize];
            for (int x = 0; x < _buckets.Length; x++)
                _buckets[x] = -1;
            _bucketMask = newSize - 1;

            // Rehash existing leaf nodes
            for (int i = 0; i < _nextFreeIndex; i++)
            {
                if (_nodePool[i].IsLeaf)
                    InsertIntoBuckets(_nodePool[i].Value, i);
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Updates the bounds and subtree size of a node.
        /// Propagates changes to the parent nodes if necessary.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetSubtreeSize(int index)
        {
            return index != -1 ? _nodePool[index].SubtreeSize : 0;
        }

        /// <summary>
        /// Gets the combined bounding volume of two child nodes.
        /// Handles cases where one or both children are missing.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IBoundingVolume GetCombinedBounds(int leftChildIndex, int rightChildIndex)
        {
            bool hasLeft = leftChildIndex != -1;
            bool hasRight = rightChildIndex != -1;

            if (hasLeft && hasRight)
                return _nodePool[leftChildIndex].Bounds.Union(_nodePool[rightChildIndex].Bounds);
            if (hasLeft)
                return _nodePool[leftChildIndex].Bounds;
            return _nodePool[rightChildIndex].Bounds;
        }

        /// <summary>
        /// Queries the BVH for values whose bounding volumes intersect with the specified volume.
        /// Uses a stack-based approach for efficient traversal.
        /// </summary>
        public void Query(IBoundingVolume queryBounds, ICollection<T> results)
        {
            if (queryBounds == null)
                ThrowHelper.ThrowNotSupportedException($"{nameof(queryBounds)} cannot be null!");

            if (_rootIndex == -1) return;

            IntStack nodeStack = _threadLocalNodeStack.Value;
            nodeStack.EnsureCapacity(_nextFreeIndex + 1);
            nodeStack.Clear();

            nodeStack.Push(_rootIndex);

            while (nodeStack.Count > 0)
            {
                int index = nodeStack.Pop();
                ref SwiftBVHNode<T> node = ref _nodePool[index];

                if (node.Bounds == null)
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

        /// <summary>
        /// Finds the index of a node by its value in the BVH using hash buckets.
        /// Returns -1 if the value is not found.
        /// </summary>
        public int FindEntry(T value)
        {
            if (value == null) ThrowHelper.ThrowArgumentNullException(nameof(value));

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

        /// <summary>
        /// Clears the BVH, resetting all nodes, buckets, and metadata.
        /// </summary>
        public void Clear()
        {
            if (_rootIndex == -1) return;

            for (int i = 0; i < _nextFreeIndex; i++)
                _nodePool[i].Reset();

            for (int i = 0; i < _buckets.Length; i++)
                _buckets[i] = -1;

            _freeList.Reset();

            _leafCount = 0;
            _nextFreeIndex = 0;
            _rootIndex = -1;
        }

        #endregion
    }
}
