//=======================================================================
// SwiftSpatialHashCellIndex.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using SwiftCollections.Utility;

namespace SwiftCollections.Query;

/// <summary>
/// Represents an integer cell coordinate in a spatial hash grid.
/// </summary>
public readonly struct SwiftSpatialHashCellIndex : IEquatable<SwiftSpatialHashCellIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.Query.SwiftSpatialHashCellIndex" />
    /// struct.
    /// </summary>
    /// <param name="x">The signed X cell coordinate; zero/default is valid.</param>
    /// <param name="y">The signed Y cell coordinate; zero/default is valid.</param>
    /// <param name="z">The signed Z cell coordinate; zero/default is valid.</param>
    public SwiftSpatialHashCellIndex(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>
    /// Gets the X-axis cell coordinate.
    /// </summary>
    /// <value>The signed X coordinate in whole cells, not world units.</value>
    public int X { get; }

    /// <summary>
    /// Gets the Y-axis cell coordinate.
    /// </summary>
    /// <value>The signed Y coordinate in whole cells, not world units.</value>
    public int Y { get; }

    /// <summary>
    /// Gets the Z-axis cell coordinate.
    /// </summary>
    /// <value>The signed Z coordinate in whole cells, not world units.</value>
    public int Z { get; }

    /// <summary>
    /// Compares the stored value components for equality.
    /// </summary>
    /// <param name="other">The value to compare or combine, without mutating either input.</param>
    /// <returns>true if the compared stored components are equal; false for null, other runtime types, or
    /// different components.</returns>
    public bool Equals(SwiftSpatialHashCellIndex other)
    {
        return X == other.X && Y == other.Y && Z == other.Z;
    }

    /// <summary>
    /// Compares the stored value components for equality.
    /// </summary>
    /// <param name="obj">The object to compare; null and other runtime types compare unequal.</param>
    /// <returns>true if the compared stored components are equal; false for null, other runtime types, or
    /// different components.</returns>
    public override bool Equals(object? obj) => obj is SwiftSpatialHashCellIndex other && Equals(other);

    /// <summary>
    /// Determines whether two SwiftSpatialHashCellIndex instances are equal.
    /// </summary>
    /// <param name="left">The left value to compare.</param>
    /// <param name="right">The right value to compare.</param>
    /// <returns>true if the compared values are equal by their stored coordinate/option/endpoint components;
    /// otherwise false.</returns>
    public static bool operator ==(SwiftSpatialHashCellIndex left, SwiftSpatialHashCellIndex right) => left.Equals(right);

    /// <summary>
    /// Determines whether two SwiftSpatialHashCellIndex instances are not equal.
    /// </summary>
    /// <param name="left">The left value to compare.</param>
    /// <param name="right">The right value to compare.</param>
    /// <returns>true if the compared values are unequal by their stored coordinate/option/endpoint components;
    /// otherwise false.</returns>
    public static bool operator !=(SwiftSpatialHashCellIndex left, SwiftSpatialHashCellIndex right) => !left.Equals(right);

    /// <summary>
    /// Returns a hash code for the stored value components.
    /// </summary>
    /// <returns>An Int32 hash suitable for lookup; equal values produce equal hashes within the running
    /// process.</returns>
    public override int GetHashCode() => SwiftHashTools.CombineHashCodes(X, Y, Z);

    /// <summary>
    /// Returns a string that represents the current object in the format "(X, Y, Z)".
    /// </summary>
    /// <returns>A string representation of the object, showing the values of X, Y, and Z in parentheses and
    /// separated by commas.</returns>
    /// <remarks>
    /// This method is useful for debugging or logging purposes to quickly view the values of the object's
    /// coordinates.
    /// </remarks>
    public override string ToString() => $"({X}, {Y}, {Z})";
}
