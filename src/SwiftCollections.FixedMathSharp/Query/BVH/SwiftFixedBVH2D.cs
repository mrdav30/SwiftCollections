//=======================================================================
// SwiftFixedBVH2D.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

namespace SwiftCollections.Query;

/// <summary>Indexes fixed-point rectangles using the shared bounding volume hierarchy.</summary>
/// <typeparam name="TKey">The non-null key type; equality and hashing must stay stable while indexed.</typeparam>
/// <remarks>
/// Bounds are copied and canonical keys retained. Inserting an equal key updates its bounds and returns false.
/// Queries append to caller-owned results in topology-dependent order. Deterministic order requires identical
/// inputs, mutation history and deterministic key behavior; no cross-backend or persisted order is promised.
/// Synchronize access externally and do not reenter the index from key or result callbacks. Borrowed NodePool
/// arrays can be replaced by growth, node indices can be reused, and callers must not modify indexed nodes.
/// </remarks>
public class SwiftFixedBVH2D<TKey> : SwiftBVH<TKey, FixedBoundVolume2D>
    where TKey : notnull
{
    /// <summary>Creates an empty hierarchy with the requested initial node capacity.</summary>
    /// <param name="capacity">Initial node slots, including internal nodes. Nonpositive requests normalize to
    /// one; growth rounds up. Keep capacities below 2^30 and within available array memory.</param>
    public SwiftFixedBVH2D(int capacity) : base(capacity)
    {
    }
}
