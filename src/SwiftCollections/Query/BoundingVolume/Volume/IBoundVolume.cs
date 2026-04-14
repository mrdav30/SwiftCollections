namespace SwiftCollections.Query;

/// <summary>
/// Represents a strongly-typed bounding volume contract for spatial operations.
/// </summary>
/// <typeparam name="TVolume">The concrete bounding volume type.</typeparam>
public interface IBoundVolume<TVolume>
    where TVolume : struct, IBoundVolume<TVolume>
{
    /// <summary>
    /// Combines the current bounding volume with another, creating a new volume that encloses both.
    /// </summary>
    /// <param name="other">The other bounding volume to combine with.</param>
    /// <returns>A new bounding volume that contains both the current and other volumes.</returns>
    TVolume Union(TVolume other);

    /// <summary>
    /// Determines whether the current bounding volume intersects with another.
    /// </summary>
    /// <param name="other">The other bounding volume to test for intersection.</param>
    /// <returns>True if the volumes intersect; otherwise, false.</returns>
    bool Intersects(TVolume other);

    /// <summary>
    /// Calculates the cost of combining the current bounding volume with another.
    /// </summary>
    /// <param name="other">The other bounding volume to consider.</param>
    /// <returns>The cost metric of the union operation.</returns>
    int GetCost(TVolume other);

    /// <summary>
    /// Determines whether the bounds represent the same spatial region.
    /// </summary>
    /// <param name="other">The other bounding volume to compare against.</param>
    /// <returns>True when both bounds share the same minimum and maximum extents; otherwise, false.</returns>
    bool BoundsEquals(TVolume other);
}

/// <summary>
/// Represents a non-generic bounding volume contract.
/// </summary>
/// <remarks>
/// This interface is temporarily retained to support the current BVH engine and will be removed
/// once the tree is fully migrated to typed volume storage.
/// </remarks>
public interface IBoundVolume
{
    /// <summary>
    /// Combines the current bounding volume with another, creating a new volume that encloses both.
    /// </summary>
    /// <param name="other">The other bounding volume to combine with.</param>
    /// <returns>A new bounding volume that contains both the current and other volumes.</returns>
    IBoundVolume Union(IBoundVolume other);

    /// <summary>
    /// Determines whether the current bounding volume intersects with another.
    /// </summary>
    /// <param name="other">The other bounding volume to test for intersection.</param>
    /// <returns>True if the volumes intersect; otherwise, false.</returns>
    bool Intersects(IBoundVolume other);

    /// <summary>
    /// Calculates the cost of combining the current bounding volume with another.
    /// </summary>
    /// <param name="other">The other bounding volume to consider.</param>
    /// <returns>The cost metric of the union operation.</returns>
    int GetCost(IBoundVolume other);
}
