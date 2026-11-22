//=======================================================================
// QuadtreePartitioner.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System.Runtime.CompilerServices;

namespace SwiftCollections.Query;

/// <summary>
/// Adapts a quadtree bounds partitioner to the shared subdivision engine with four children.
/// </summary>
internal readonly struct QuadtreePartitioner<TVolume> : ISubdivisionPartitioner<TVolume>
    where TVolume : struct, IBoundVolume<TVolume>
{
    private readonly IQuadtreeBoundsPartitioner<TVolume> _partitioner;

    internal QuadtreePartitioner(IQuadtreeBoundsPartitioner<TVolume> partitioner) => _partitioner = partitioner;

    public int ChildCount => 4;

    public string DiagnosticSource => "SwiftQuadtree";

    public string WorldBoundsError => "Bounds must be fully contained within the quadtree world bounds.";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsBounds(TVolume outer, TVolume inner) => _partitioner.ContainsBounds(outer, inner);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool CanSubdivide(TVolume bounds) => _partitioner.CanSubdivide(bounds);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetContainingChildIndex(TVolume nodeBounds, TVolume entryBounds, out int childIndex) =>
        _partitioner.TryGetContainingChildIndex(nodeBounds, entryBounds, out childIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TVolume CreateChildBounds(TVolume parentBounds, int childIndex) =>
        _partitioner.CreateChildBounds(parentBounds, childIndex);
}
