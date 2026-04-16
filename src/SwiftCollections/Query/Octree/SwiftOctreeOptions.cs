using System;

namespace SwiftCollections.Query;

/// <summary>
/// Controls node subdivision behavior for <see cref="SwiftOctree{TKey, TVolume}"/>.
/// </summary>
public readonly struct SwiftOctreeOptions : IEquatable<SwiftOctreeOptions>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SwiftOctreeOptions"/> struct.
    /// </summary>
    /// <param name="maxDepth">The maximum child depth allowed below the root node.</param>
    /// <param name="nodeCapacity">The maximum number of entries a node should hold before attempting to split.</param>
    public SwiftOctreeOptions(int maxDepth, int nodeCapacity)
        : this(maxDepth, nodeCapacity, true)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SwiftOctreeOptions"/> struct.
    /// </summary>
    /// <param name="maxDepth">The maximum child depth allowed below the root node.</param>
    /// <param name="nodeCapacity">The maximum number of entries a node should hold before attempting to split.</param>
    /// <param name="enableMergeOnRemove">Whether empty child regions should collapse back into their parent after removals.</param>
    public SwiftOctreeOptions(int maxDepth, int nodeCapacity, bool enableMergeOnRemove)
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
    public int MaxDepth { get; }

    /// <summary>
    /// Gets the preferred number of entries per node before subdivision is attempted.
    /// </summary>
    public int NodeCapacity { get; }

    /// <summary>
    /// Gets a value indicating whether nodes should merge after removals leave sparse children.
    /// </summary>
    public bool EnableMergeOnRemove { get; }

    /// <inheritdoc />
    public bool Equals(SwiftOctreeOptions other)
    {
        return MaxDepth == other.MaxDepth &&
               NodeCapacity == other.NodeCapacity &&
               EnableMergeOnRemove == other.EnableMergeOnRemove;
    }

    /// <inheritdoc />
    public override bool Equals(object obj) => obj is SwiftOctreeOptions other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(MaxDepth, NodeCapacity, EnableMergeOnRemove);
}
