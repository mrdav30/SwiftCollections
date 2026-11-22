//=======================================================================
// SwiftSpatialHashOptions.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;

namespace SwiftCollections.Query;

/// <summary>
/// Controls neighborhood query behavior for <see cref="T:SwiftCollections.Query.SwiftSpatialHash`2" />.
/// </summary>
/// <remarks>
/// An immutable options value. default construction bypasses validating constructors. Default property uses
/// padding one, whereas default(TOptions) uses zero.
/// </remarks>
public readonly struct SwiftSpatialHashOptions : IEquatable<SwiftSpatialHashOptions>
{
    /// <summary>
    /// The default options value.
    /// </summary>
    /// <value>The predefined value with NeighborhoodPadding equal to one.</value>
    public static SwiftSpatialHashOptions Default { get; } = new SwiftSpatialHashOptions(1);

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.Query.SwiftSpatialHashOptions" /> struct.
    /// </summary>
    /// <param name="neighborhoodPadding">The non-negative number of extra cells visited on every side of a
    /// neighborhood query; ordinary intersection Query ignores this padding.</param>
    /// <exception cref="T:System.ArgumentOutOfRangeException">neighborhoodPadding is negative.</exception>
    public SwiftSpatialHashOptions(int neighborhoodPadding)
    {
        if (neighborhoodPadding < 0)
            throw new ArgumentOutOfRangeException(nameof(neighborhoodPadding), neighborhoodPadding, "Neighborhood padding must be zero or greater.");

        NeighborhoodPadding = neighborhoodPadding;
    }

    /// <summary>
    /// Gets the number of extra cells queried beyond the mapped volume range when using neighborhood queries.
    /// </summary>
    /// <value>The non-negative padding in whole cells, applied only by QueryNeighborhood.</value>
    public int NeighborhoodPadding { get; }

    /// <summary>
    /// Compares the stored value components for equality.
    /// </summary>
    /// <param name="other">The value to compare or combine, without mutating either input.</param>
    /// <returns>true if the compared stored components are equal; false for null, other runtime types, or
    /// different components.</returns>
    public bool Equals(SwiftSpatialHashOptions other) => NeighborhoodPadding == other.NeighborhoodPadding;

    /// <summary>
    /// Compares the stored value components for equality.
    /// </summary>
    /// <param name="obj">The object to compare; null and other runtime types compare unequal.</param>
    /// <returns>true if the compared stored components are equal; false for null, other runtime types, or
    /// different components.</returns>
    public override bool Equals(object? obj) => obj is SwiftSpatialHashOptions other && Equals(other);

    /// <summary>
    /// Determines whether two SwiftSpatialHashOptions instances are equal.
    /// </summary>
    /// <param name="left">The left value to compare.</param>
    /// <param name="right">The right value to compare.</param>
    /// <returns>true if the compared values are equal by their stored coordinate/option/endpoint components;
    /// otherwise false.</returns>
    public static bool operator ==(SwiftSpatialHashOptions left, SwiftSpatialHashOptions right) => left.Equals(right);

    /// <summary>
    /// Determines whether two SwiftSpatialHashOptions instances are not equal.
    /// </summary>
    /// <param name="left">The left value to compare.</param>
    /// <param name="right">The right value to compare.</param>
    /// <returns>true if the compared values are unequal by their stored coordinate/option/endpoint components;
    /// otherwise false.</returns>
    public static bool operator !=(SwiftSpatialHashOptions left, SwiftSpatialHashOptions right) => !left.Equals(right);

    /// <summary>
    /// Returns a hash code for the stored value components.
    /// </summary>
    /// <returns>An Int32 hash suitable for lookup; equal values produce equal hashes within the running
    /// process.</returns>
    public override int GetHashCode() => NeighborhoodPadding;
}
