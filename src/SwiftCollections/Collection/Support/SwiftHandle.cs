using System;

namespace SwiftCollections;

/// <summary>
/// Represents a value type handle that uniquely identifies an object or resource by index and generation.
/// </summary>
/// <remarks>
/// A <see cref="SwiftHandle"/> consists of an integer index and an unsigned generation value. 
/// This combination helps detect stale or invalid references when resources are recycled.
///</remarks>
public readonly struct SwiftHandle : IEquatable<SwiftHandle>
{
    /// <summary>
    /// Gets the zero-based index associated with this instance.
    /// </summary>
    public readonly int Index;

    /// <summary>
    /// Gets the generation number associated with this instance.
    /// </summary>
    public readonly uint Generation;

    /// <summary>
    /// Initializes a new instance of the SwiftHandle class with the specified index and generation values.
    /// </summary>
    /// <param name="index">
    /// The zero-based index that identifies the handle. 
    /// Must be greater than or equal to 0.
    /// </param>
    /// <param name="generation">
    /// The generation value associated with the handle. 
    /// Used to distinguish between different versions of the same index.
    /// </param>
    public SwiftHandle(int index, uint generation)
    {
        Index = index;
        Generation = generation;
    }

    /// <inheritdoc/>
    public bool Equals(SwiftHandle other)
        => Index == other.Index && Generation == other.Generation;

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is SwiftHandle h && Equals(h);

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(Index, Generation);

    /// <summary>
    /// Returns a string that represents the current handle, including its index and generation values.
    /// </summary>
    /// <returns>A string in the format "Handle({Index}:{Generation})" that identifies the handle by its index and generation.</returns>
    public override string ToString()
        => $"Handle({Index}:{Generation})";

    /// <summary>
    /// Determines whether two SwiftHandle instances are equal.
    /// </summary>
    public static bool operator ==(SwiftHandle left, SwiftHandle right) => left.Equals(right);

    /// <summary>
    /// Determines whether two SwiftHandle instances are not equal.
    /// </summary>
    public static bool operator !=(SwiftHandle left, SwiftHandle right) => !(left == right);
}
