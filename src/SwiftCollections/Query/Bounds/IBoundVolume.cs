//=======================================================================
// IBoundVolume.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

namespace SwiftCollections.Query;

/// <summary>
/// Represents a strongly-typed bounding volume contract for spatial operations.
/// </summary>
/// <typeparam name="TVolume">The value-type volume backend providing consistent intersection, union, equality and
/// cost operations.</typeparam>
/// <remarks>
/// All inputs use the same caller-defined coordinate space. Indexes assume union encloses both inputs and
/// intersection/cost/equality remain consistent without side effects. Concrete implementations define numeric
/// limits; long cost alone is not an overflow guarantee.
/// </remarks>
public interface IBoundVolume<TVolume>
    where TVolume : struct, IBoundVolume<TVolume>
{
    /// <summary>
    /// Creates a volume enclosing both input volumes.
    /// </summary>
    /// <param name="other">The value in the same coordinate space to combine or compare; neither input is
    /// mutated.</param>
    /// <returns>A new value enclosing both input regions.</returns>
    TVolume Union(TVolume other);

    /// <summary>
    /// Determines whether the current bounding volume intersects with another.
    /// </summary>
    /// <param name="other">The value to compare or combine, without mutating either input.</param>
    /// <returns>True if the volumes intersect; otherwise, false.</returns>
    bool Intersects(TVolume other);

    /// <summary>
    /// Calculates the backend-defined increase metric used to choose BVH insertion branches.
    /// </summary>
    /// <param name="other">The value to compare or combine, without mutating either input.</param>
    /// <returns>An Int64 heuristic; smaller values are preferred. The backend defines rounding, clamping, and
    /// numeric failure limits.</returns>
    /// <remarks>
    /// This is not required to materialize a representable Size or Volume. The interface alone does not guarantee
    /// full-domain overflow freedom.
    /// </remarks>
    long GetCost(TVolume other);

    /// <summary>
    /// Compares the spatial bounds represented by two values.
    /// </summary>
    /// <param name="other">The value in the same coordinate space to combine or compare; neither input is
    /// mutated.</param>
    /// <returns>true if the represented bounds are equal; otherwise false. Built-in boxes compare both coordinate
    /// endpoints.</returns>
    bool BoundsEquals(TVolume other);
}
