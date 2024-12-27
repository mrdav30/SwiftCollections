namespace SwiftCollections.Query
{
    /// <summary>
    /// Represents a node in a Bounding Volume Hierarchy (BVH).
    /// Stores spatial data and maintains hierarchical relationships.
    /// </summary>
    public struct SwiftBVHNode<T>
    {
        /// <summary>
        /// Gets or sets the value stored in the node.
        /// </summary>
        public T Value { get; set; }

        /// <summary>
        /// Gets or sets the bounding volume of the node.
        /// </summary>
        public IBoundingVolume Bounds { get; set; }

        /// <summary>
        /// Gets or sets the index of the parent node.
        /// </summary>
        public int ParentIndex { get; set; }

        /// <summary>
        /// Determines if the node has a parent.
        /// </summary>
        public bool HasParent => ParentIndex != -1;

        /// <summary>
        /// Gets or sets the index of the left child node.
        /// </summary>
        public int LeftChildIndex { get; set; }

        /// <summary>
        /// Determines if the node has a left child.
        /// </summary>
        public bool HasLeftChild => LeftChildIndex != -1;

        /// <summary>
        /// Gets or sets the index of the right child node.
        /// </summary>
        public int RightChildIndex { get; set; }

        /// <summary>
        /// Determines if the node has a right child.
        /// </summary>
        public bool HasRightChild => RightChildIndex != -1;

        /// <summary>
        /// Determines if the node has any children.
        /// </summary>
        public bool HasChildren => HasLeftChild || HasRightChild;

        /// <summary>
        /// Gets or sets a value indicating whether this node is a leaf node.
        /// </summary>
        public bool IsLeaf { get; set; }

        /// <summary>
        /// Tracks the number of nodes in the subtree rooted at this node.
        /// </summary>
        public int SubtreeSize { get; set; }

        /// <summary>
        /// Resets the node to its default state.
        /// Clears all references and metadata.
        /// </summary>
        public void Reset()
        {
            Value = default;
            Bounds = default;
            IsLeaf = false;
            ParentIndex = -1;
            LeftChildIndex = -1;
            RightChildIndex = -1;
            SubtreeSize = 0;
        }
    }
}
