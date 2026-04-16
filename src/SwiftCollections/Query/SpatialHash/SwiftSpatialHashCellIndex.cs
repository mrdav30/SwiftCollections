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
    public override bool Equals(object obj)
    {
        return obj is SwiftSpatialHashCellIndex other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + X;
            hash = (hash * 31) + Y;
            hash = (hash * 31) + Z;
            return hash;
        }
    }

    /// <inheritdoc />
    public override string ToString() => $"({X}, {Y}, {Z})";
}
