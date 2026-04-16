namespace SwiftCollections.Query;

/// <summary>
/// Maps octree subdivision and containment behavior for a specific volume backend.
/// </summary>
/// <typeparam name="TVolume">The volume type used by the octree.</typeparam>
public interface IOctreeBoundsPartitioner<TVolume>
    where TVolume : struct, IBoundVolume<TVolume>
{
    /// <summary>
    /// Determines whether <paramref name="inner"/> is fully contained within <paramref name="outer"/>.
    /// </summary>
    bool ContainsBounds(TVolume outer, TVolume inner);

    /// <summary>
    /// Determines whether the supplied node bounds can be subdivided further.
    /// </summary>
    bool CanSubdivide(TVolume bounds);

    /// <summary>
    /// Attempts to resolve the child octant that fully contains the supplied entry bounds.
    /// </summary>
    bool TryGetContainingChildIndex(TVolume nodeBounds, TVolume entryBounds, out int childIndex);

    /// <summary>
    /// Creates the child-octant bounds for the supplied parent bounds and child index.
    /// </summary>
    TVolume CreateChildBounds(TVolume parentBounds, int childIndex);
}
