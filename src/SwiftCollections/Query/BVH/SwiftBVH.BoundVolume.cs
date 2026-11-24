//=======================================================================
// SwiftBVH.BoundVolume.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

namespace SwiftCollections.Query;

/// <summary>
/// Represents a numerics-backed Bounding Volume Hierarchy (BVH) optimized for spatial queries.
/// </summary>
/// <typeparam name="T">The stable key type; equality and hashing must remain unchanged while indexed.</typeparam>
/// <remarks>
/// Mutable runtime index: synchronize all access externally, including queries that use shared scratch/stamps.
/// Key and volume callbacks must not reenter or mutate the index. Bounds are copied and keys are retained;
/// changing a key's equality/hash breaks lookup. Query ordering depends on current topology and mutation history;
/// it is not sorted or a serialized order guarantee. Determinism requires deterministic keys, input order and
/// volume/mapping arithmetic. Each non-null key owns one leaf; inserting an equal key updates its bounds.
/// </remarks>
public class SwiftBVH<T> : SwiftBVH<T, BoundVolume>
    where T : notnull
{
    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.Query.SwiftBVH`1" /> class with the
    /// specified capacity.
    /// </summary>
    /// <param name="capacity">The requested initial storage length in nodes, including internal nodes.
    /// Non-positive values normalize to one; growth rounds up. Keep capacities below 2^30 and within available
    /// array memory.</param>
    public SwiftBVH(int capacity)
        : base(capacity) { }
}
