//=======================================================================
// SwiftTicket.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;

namespace SwiftCollections;

/// <summary>
/// Identifies an allocation in its issuing owner by index and generation.
/// </summary>
/// <remarks>
/// A <see cref="T:SwiftCollections.SwiftTicket" /> consists of an integer index and an unsigned generation value.
/// This combination helps detect stale references when slots are reused. Tickets carry no collection identity;
/// use them with their issuing owner or stores the caller explicitly associates with that owner. Generation policy belongs to
/// the owner: SwiftTicketMap increments with finite wrap, while SwiftDenseTicketMap retires terminal slots.
/// Reconstructing an older state can revive numeric tickets; tickets do not identify a world or timeline.
/// </remarks>
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
    /// Creates a ticket containing the supplied index and generation.
    /// </summary>
    /// <param name="index">The signed index stored verbatim; construction performs no validation.</param>
    /// <param name="generation">The generation stored verbatim without validation. Issuance, invalidation and
    /// terminal-generation behavior belong to the issuing owner.</param>
    public SwiftTicket(int index, uint generation)
    {
        Index = index;
        Generation = generation;
    }

    /// <summary>
    /// Compares the stored value components for equality.
    /// </summary>
    /// <param name="other">The ticket to compare by Index and Generation; an object of another type or null
    /// compares unequal.</param>
    /// <returns>true if the compared stored components are equal; false for null, other runtime types, or
    /// different components.</returns>
    public bool Equals(SwiftTicket other)
        => Index == other.Index && Generation == other.Generation;

    /// <summary>
    /// Compares the stored value components for equality.
    /// </summary>
    /// <param name="obj">The ticket to compare by Index and Generation; an object of another type or null
    /// compares unequal.</param>
    /// <returns>true if the compared stored components are equal; false for null, other runtime types, or
    /// different components.</returns>
    public override bool Equals(object? obj)
        => obj is SwiftTicket h && Equals(h);

    /// <summary>
    /// Returns a hash code for the stored value components.
    /// </summary>
    /// <returns>An Int32 hash suitable for lookup; equal values produce equal hashes within the running
    /// process.</returns>
    /// <remarks>
    /// The hash is for in-process lookup; it is not a stable serialized or cross-process identifier.
    /// </remarks>
    public override int GetHashCode()
        => HashCode.Combine(Index, Generation);

    /// <summary>
    /// Returns a string that represents the current ticket, including its index and generation values.
    /// </summary>
    /// <returns>A string in the format "Ticket({Index}:{Generation})" that identifies the ticket by its index and
    /// generation.</returns>
    public override string ToString()
        => $"Ticket({Index}:{Generation})";

    /// <summary>
    /// Determines whether two SwiftTicket instances are equal.
    /// </summary>
    /// <param name="left">The ticket value to compare; equality compares both Index and Generation, without map
    /// identity.</param>
    /// <param name="right">The ticket value to compare; equality compares both Index and Generation, without map
    /// identity.</param>
    /// <returns>true if Index and Generation both match; otherwise false. No map identity is compared.</returns>
    public static bool operator ==(SwiftTicket left, SwiftTicket right) => left.Equals(right);

    /// <summary>
    /// Determines whether two SwiftTicket instances are not equal.
    /// </summary>
    /// <param name="left">The ticket value to compare; equality compares both Index and Generation, without map
    /// identity.</param>
    /// <param name="right">The ticket value to compare; equality compares both Index and Generation, without map
    /// identity.</param>
    /// <returns>true if Index and Generation differ in either component; otherwise false. No map identity is
    /// compared.</returns>
    public static bool operator !=(SwiftTicket left, SwiftTicket right) => !(left == right);
}
