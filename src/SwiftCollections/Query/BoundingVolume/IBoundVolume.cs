namespace SwiftCollections.Query
{
    /// <summary>
    /// Represents a generic interface for a bounding volume, supporting spatial queries and operations.
    /// </summary>
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
}
