//=======================================================================
// SwiftQuadtreeOptions.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using SwiftCollections.Utility;

namespace SwiftCollections.Query;

/// <summary>
/// Controls node subdivision behavior for <see cref="T:SwiftCollections.Query.SwiftQuadtree`2" />.
/// </summary>
/// <remarks>
/// An immutable options value. default construction bypasses validating constructors. default(TOptions) has
/// MaxDepth=0, NodeCapacity=0 and EnableMergeOnRemove=false; the tree accepts it and does not subdivide.
/// </remarks>
public readonly struct SwiftQuadtreeOptions : IEquatable<SwiftQuadtreeOptions>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.Query.SwiftQuadtreeOptions" /> struct.
    /// </summary>
    /// <param name="maxDepth">The non-negative maximum subdivision depth; root depth is zero, so zero disables
    /// subdivision.</param>
    /// <param name="nodeCapacity">The positive live-entry threshold above which a node may split, subject to
    /// depth and size limits.</param>
    /// <exception cref="T:System.ArgumentOutOfRangeException">maxDepth is negative or nodeCapacity is not positive.</exception>
    public SwiftQuadtreeOptions(int maxDepth, int nodeCapacity)
        : this(maxDepth, nodeCapacity, true) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.Query.SwiftQuadtreeOptions" /> struct.
    /// </summary>
    /// <param name="maxDepth">The non-negative maximum subdivision depth; root depth is zero, so zero disables
    /// subdivision.</param>
    /// <param name="nodeCapacity">The positive live-entry threshold above which a node may split, subject to
    /// depth and size limits.</param>
    /// <param name="enableMergeOnRemove">true to merge sufficiently sparse leaf children after
    /// removal/relocation; false to retain subdivisions.</param>
    /// <exception cref="T:System.ArgumentOutOfRangeException">maxDepth is negative or nodeCapacity is not positive.</exception>
    public SwiftQuadtreeOptions(int maxDepth, int nodeCapacity, bool enableMergeOnRemove)
    {
        if (maxDepth < 0)
            throw new ArgumentOutOfRangeException(nameof(maxDepth), maxDepth, "Maximum depth must be zero or greater.");

        if (nodeCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(nodeCapacity), nodeCapacity, "Node capacity must be greater than zero.");

        MaxDepth = maxDepth;
        NodeCapacity = nodeCapacity;
        EnableMergeOnRemove = enableMergeOnRemove;
    }

    /// <summary>
    /// Gets the maximum child depth allowed below the root node.
    /// </summary>
    /// <value>The maximum subdivision depth; zero means root only.</value>
    public int MaxDepth { get; }

    /// <summary>
    /// Gets the preferred number of entries per node before subdivision is attempted.
    /// </summary>
    /// <value>The split/merge entry threshold; constructors require positive values, default(TOptions) is
    /// zero.</value>
    public int NodeCapacity { get; }

    /// <summary>
    /// Gets a value indicating whether nodes should merge after removals leave sparse children.
    /// </summary>
    /// <value>true to attempt sparse-child merges after removal/relocation; false to retain topology.</value>
    public bool EnableMergeOnRemove { get; }

    /// <summary>
    /// Compares the stored value components for equality.
    /// </summary>
    /// <param name="other">The options value to compare without mutating either input.</param>
    /// <returns>true when all three option components are equal; otherwise false.</returns>
    public bool Equals(SwiftQuadtreeOptions other)
    {
        return MaxDepth == other.MaxDepth &&
               NodeCapacity == other.NodeCapacity &&
               EnableMergeOnRemove == other.EnableMergeOnRemove;
    }

    /// <summary>
    /// Compares the stored value components for equality.
    /// </summary>
    /// <param name="obj">The object to compare; null and other runtime types compare unequal.</param>
    /// <returns>true if the compared stored components are equal; false for null, other runtime types, or
    /// different components.</returns>
    public override bool Equals(object? obj) => obj is SwiftQuadtreeOptions other && Equals(other);

    /// <summary>
    /// Determines whether two SwiftQuadtreeOptions instances are equal.
    /// </summary>
    /// <param name="left">The left value to compare.</param>
    /// <param name="right">The right value to compare.</param>
    /// <returns>true if the compared values are equal by their stored option components;
    /// otherwise false.</returns>
    public static bool operator ==(SwiftQuadtreeOptions left, SwiftQuadtreeOptions right) => left.Equals(right);

    /// <summary>
    /// Determines whether two SwiftQuadtreeOptions instances are not equal.
    /// </summary>
    /// <param name="left">The left value to compare.</param>
    /// <param name="right">The right value to compare.</param>
    /// <returns>true if the compared values are unequal by their stored option components;
    /// otherwise false.</returns>
    public static bool operator !=(SwiftQuadtreeOptions left, SwiftQuadtreeOptions right) => !(left == right);

    /// <summary>
    /// Returns a hash code for the stored value components.
    /// </summary>
    /// <returns>A deterministic hash of the integer option components; equal values produce equal hashes.</returns>
    public override int GetHashCode() => SwiftHashTools.CombineHashCodes(MaxDepth, NodeCapacity, EnableMergeOnRemove ? 1 : 0);
}
