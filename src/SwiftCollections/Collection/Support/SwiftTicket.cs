//=======================================================================
// SwiftTicket.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;

namespace SwiftCollections;

/// <summary>
/// Identifies an allocation in its originating ticket map by index and generation.
/// </summary>
/// <remarks>
/// A <see cref="SwiftTicket"/> consists of an integer index and an unsigned generation value. 
/// This combination helps detect stale references when slots are reused. Tickets carry no collection identity;
/// use them only with their originating map. Generation counters are finite.
///</remarks>
public readonly struct SwiftTicket : IEquatable<SwiftTicket>
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
    /// Initializes a new instance of the SwiftTicket class with the specified index and generation values.
    /// </summary>
    /// <param name="index">
    /// The zero-based index that identifies the ticket. 
    /// Must be greater than or equal to 0.
    /// </param>
    /// <param name="generation">
    /// The generation value associated with the ticket. 
    /// Used to distinguish between different versions of the same index.
    /// </param>
    public SwiftTicket(int index, uint generation)
    {
        Index = index;
        Generation = generation;
    }

    /// <inheritdoc/>
    public bool Equals(SwiftTicket other)
        => Index == other.Index && Generation == other.Generation;

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is SwiftTicket h && Equals(h);

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(Index, Generation);

    /// <summary>
    /// Returns a string that represents the current ticket, including its index and generation values.
    /// </summary>
    /// <returns>A string in the format "Ticket({Index}:{Generation})" that identifies the ticket by its index and generation.</returns>
    public override string ToString()
        => $"Ticket({Index}:{Generation})";

    /// <summary>
    /// Determines whether two SwiftTicket instances are equal.
    /// </summary>
    public static bool operator ==(SwiftTicket left, SwiftTicket right) => left.Equals(right);

    /// <summary>
    /// Determines whether two SwiftTicket instances are not equal.
    /// </summary>
    public static bool operator !=(SwiftTicket left, SwiftTicket right) => !(left == right);
}
