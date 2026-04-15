namespace SwiftCollections.Query;

/// <summary>
/// Represents a fixed-point Bounding Volume Hierarchy (BVH) optimized for spatial queries.
/// </summary>
public class SwiftFixedBVH<T> : SwiftBVH<T, FixedBoundVolume>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SwiftFixedBVH{T}"/> class with the specified capacity.
    /// </summary>
    /// <param name="capacity">Initial tree capacity.</param>
    public SwiftFixedBVH(int capacity)
        : base(capacity)
    {
    }
}
