//=======================================================================
// SwiftBVHNode.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

namespace SwiftCollections.Query;

/// <summary>
/// Represents a node in a Bounding Volume Hierarchy (BVH). Stores spatial data and maintains hierarchical
/// relationships.
/// </summary>
/// <typeparam name="TKey">The stable key type; equality and hashing must remain unchanged while
/// indexed.</typeparam>
/// <typeparam name="TVolume">The value-type volume backend providing consistent intersection, union, equality and
/// cost operations.</typeparam>
/// <remarks>
/// A mutable topology record copied by value. Changing a RootNode copy has no effect; changing NodePool elements
/// bypasses all tree invariants. Use Default or Reset for -1 relationship sentinels; default(TNode) contains zero
/// indices and its relationship flags can therefore appear true.
/// </remarks>
public struct SwiftBVHNode<TKey, TVolume>
    where TVolume : struct, IBoundVolume<TVolume>
{
    /// <summary>
    /// Gets or sets the value stored in the node.
    /// </summary>
    /// <value>The leaf key; internal/unallocated nodes may contain default(TKey).</value>
    public TKey Value { get; set; }

    /// <summary>
    /// Gets or sets the bounding volume of the node.
    /// </summary>
    /// <value>The copied volume enclosing this node's subtree.</value>
    public TVolume Bounds { get; set; }

    /// <summary>
    /// Gets or sets the index of the parent node.
    /// </summary>
    /// <value>The parent node-pool index, or -1 for no parent.</value>
    public int ParentIndex { get; set; }

    /// <summary>
    /// Determines if the node has a parent.
    /// </summary>
    /// <value>true exactly when ParentIndex is not -1; does not validate allocation.</value>
    public readonly bool HasParent => ParentIndex != -1;

    /// <summary>
    /// Gets or sets the index value associated with this instance.
    /// </summary>
    /// <value>This node's node-pool index; Reset uses -1.</value>
    public int MyIndex { get; set; }

    /// <summary>
    /// Gets or sets the index of the left child node.
    /// </summary>
    /// <value>The left child index, or -1 for none.</value>
    public int LeftChildIndex { get; set; }

    /// <summary>
    /// Determines if the node has a left child.
    /// </summary>
    /// <value>true exactly when LeftChildIndex is not -1; does not validate allocation.</value>
    public readonly bool HasLeftChild => LeftChildIndex != -1;

    /// <summary>
    /// Gets or sets the index of the right child node.
    /// </summary>
    /// <value>The right child index, or -1 for none.</value>
    public int RightChildIndex { get; set; }

    /// <summary>
    /// Determines if the node has a right child.
    /// </summary>
    /// <value>true exactly when RightChildIndex is not -1; does not validate allocation.</value>
    public readonly bool HasRightChild => RightChildIndex != -1;

    /// <summary>
    /// Determines if the node has any children.
    /// </summary>
    /// <value>true when either child index is not -1; does not validate allocation.</value>
    public readonly bool HasChildren => HasLeftChild || HasRightChild;

    /// <summary>
    /// Gets or sets a value indicating whether this node is a leaf node.
    /// </summary>
    /// <value>true for an entry-bearing leaf, false for an internal or reset node.</value>
    public bool IsLeaf { get; set; }

    /// <summary>
    /// Tracks the number of nodes in the subtree rooted at this node.
    /// </summary>
    /// <value>The node count including this node and descendants; allocated leaves use one.</value>
    public int SubtreeSize { get; set; }

    /// <summary>
    /// Determines whether or not this node has been allocated from the pool or is a pooled instance
    /// </summary>
    /// <value>true when the owning tree has allocated this slot.</value>
    public bool IsAllocated { get; set; }

    /// <summary>
    /// Provides a custom default instance of <see cref="T:SwiftCollections.Query.SwiftBVHNode`2" />.
    /// </summary>
    /// <value>A fresh unallocated record with -1 parent/child indices, default values, size zero, and MyIndex
    /// zero. It differs from Reset's MyIndex -1.</value>
    public static SwiftBVHNode<TKey, TVolume> Default => new()
    {
        Value = default!,
        Bounds = default,
        ParentIndex = -1,
        LeftChildIndex = -1,
        RightChildIndex = -1,
        SubtreeSize = 0,
        IsLeaf = false,
        IsAllocated = false
    };

    /// <summary>
    /// Resets the node to its default state. Clears all references and metadata.
    /// </summary>
    /// <remarks>
    /// Clears value/bounds/allocation/leaf/size and sets own, parent and child indices to -1. Does not update an
    /// owning tree's other nodes, counts or lookup.
    /// </remarks>
    public void Reset()
    {
        Value = default!;
        Bounds = default;
        IsLeaf = false;
        ParentIndex = -1;
        MyIndex = -1;
        LeftChildIndex = -1;
        RightChildIndex = -1;
        SubtreeSize = 0;
        IsAllocated = false;
    }
}
