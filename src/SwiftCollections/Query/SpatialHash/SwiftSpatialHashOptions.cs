using System;

namespace SwiftCollections.Query;

/// <summary>
/// Controls neighborhood query behavior for <see cref="SwiftSpatialHash{TKey, TVolume}"/>.
/// </summary>
public readonly struct SwiftSpatialHashOptions : IEquatable<SwiftSpatialHashOptions>
{
    /// <summary>
    /// The default options value.
    /// </summary>
    public static SwiftSpatialHashOptions Default { get; } = new SwiftSpatialHashOptions(1);

    /// <summary>
    /// Initializes a new instance of the <see cref="SwiftSpatialHashOptions"/> struct.
    /// </summary>
    /// <param name="neighborhoodPadding">The number of additional cells to include around neighborhood queries.</param>
    public SwiftSpatialHashOptions(int neighborhoodPadding)
    {
        if (neighborhoodPadding < 0)
            throw new ArgumentOutOfRangeException(nameof(neighborhoodPadding), neighborhoodPadding, "Neighborhood padding must be zero or greater.");

        NeighborhoodPadding = neighborhoodPadding;
    }

    /// <summary>
    /// Gets the number of extra cells queried beyond the mapped volume range when using neighborhood queries.
    /// </summary>
    public int NeighborhoodPadding { get; }

    /// <inheritdoc />
    public bool Equals(SwiftSpatialHashOptions other) => NeighborhoodPadding == other.NeighborhoodPadding;

    /// <inheritdoc />
    public override bool Equals(object obj) => obj is SwiftSpatialHashOptions other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => NeighborhoodPadding;
}
