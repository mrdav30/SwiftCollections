//=======================================================================
// SwiftBVH2D.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

namespace SwiftCollections.Query;

/// <summary>Indexes two-dimensional Numerics bounds using the shared bounding volume hierarchy.</summary>
/// <typeparam name="TKey">The non-null key type; equality and hashing must remain stable while indexed.</typeparam>
/// <remarks>
/// Bounds are copied and canonical keys are retained. Inserting an equal key updates its bounds and returns
/// false. Queries append into caller-owned storage; ordering follows topology and mutation history, not key
/// order. Synchronize all access externally and do not reenter the index from callbacks. Borrowed NodePool
/// storage can be replaced by growth, and node indices can be reused after mutation. Numerics arithmetic does
/// not promise cross-platform lockstep determinism. This wrapper uses the existing generic BVH engine unchanged.
/// </remarks>
public class SwiftBVH2D<TKey> : SwiftBVH<TKey, BoundVolume2D>
    where TKey : notnull
{
    /// <summary>Creates an empty two-dimensional BVH.</summary>
    /// <param name="capacity">Initial node storage, including internal nodes. Nonpositive values normalize to
    /// one and growth rounds up. Keep capacities below 2^30 and within available array memory.</param>
    public SwiftBVH2D(int capacity)
        : base(capacity) { }
}
