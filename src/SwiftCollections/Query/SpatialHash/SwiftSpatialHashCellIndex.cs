using System;

namespace SwiftCollections.Query;

/// <summary>
/// Represents an integer cell coordinate in a spatial hash grid.
/// </summary>
public readonly struct SwiftSpatialHashCellIndex : IEquatable<SwiftSpatialHashCellIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SwiftSpatialHashCellIndex"/> struct.
    /// </summary>
    public SwiftSpatialHashCellIndex(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>
    /// Gets the X-axis cell coordinate.
    /// </summary>
    public int X { get; }

    /// <summary>
    /// Gets the Y-axis cell coordinate.
    /// </summary>
    public int Y { get; }

    /// <summary>
    /// Gets the Z-axis cell coordinate.
    /// </summary>
    public int Z { get; }

    /// <inheritdoc />
    public bool Equals(SwiftSpatialHashCellIndex other)
    {
        return X == other.X && Y == other.Y && Z == other.Z;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is SwiftSpatialHashCellIndex other && Equals(other);

    /// <summary>
    /// Determines whether two SwiftSpatialHashCellIndex instances are equal.
    /// </summary>
    public static bool operator ==(SwiftSpatialHashCellIndex left, SwiftSpatialHashCellIndex right) => left.Equals(right);

    /// <summary>
    /// Determines whether two SwiftSpatialHashCellIndex instances are not equal.
    /// </summary>
    public static bool operator !=(SwiftSpatialHashCellIndex left, SwiftSpatialHashCellIndex right) => !left.Equals(right);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(X, Y, Z);

    /// <summary>
    /// Returns a string that represents the current object in the format "(X, Y, Z)".
    /// </summary>
    /// <remarks>
    /// This method is useful for debugging or logging purposes to quickly view the values of the
    /// object's coordinates.
    /// </remarks>
    /// <returns>A string representation of the object, showing the values of X, Y, and Z in parentheses and separated by commas.</returns>
    public override string ToString() => $"({X}, {Y}, {Z})";
}
