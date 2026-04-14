namespace SwiftCollections.Query;

/// <summary>
/// Represents a numerics-backed Bounding Volume Hierarchy (BVH) optimized for spatial queries.
/// </summary>
public class SwiftBVH<T> : SwiftBVH<T, BoundVolume>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SwiftBVH{T}"/> class with the specified capacity.
    /// </summary>
    /// <param name="capacity">Initial tree capacity.</param>
    public SwiftBVH(int capacity)
        : base(capacity)
    {
    }
}
