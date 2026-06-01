//=======================================================================
// SwiftBVH.BoundVolume.cs
//=======================================================================
// MIT License, Copyright (c) 2024–present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

namespace SwiftCollections.Query;

/// <summary>
/// Represents a numerics-backed Bounding Volume Hierarchy (BVH) optimized for spatial queries.
/// </summary>
public class SwiftBVH<T> : SwiftBVH<T, BoundVolume>
    where T : notnull
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SwiftBVH{T}"/> class with the specified capacity.
    /// </summary>
    /// <param name="capacity">Initial tree capacity.</param>
    public SwiftBVH(int capacity)
        : base(capacity) { }
}
