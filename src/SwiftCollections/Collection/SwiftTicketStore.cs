//=======================================================================
// SwiftTicketStore.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Chronicler;
using Chronicler.Diagnostics;
using Chronicler.Serialization;
using MemoryPack;
using SwiftCollections.Utility;

namespace SwiftCollections;

/// <summary>Stores packed values under generation-checked tickets issued by another owner.</summary>
/// <typeparam name="T">The payload type; null, default and duplicate values are permitted.</typeparam>
/// <remarks>
/// Membership is local, not proof of issuer liveness. Associate one store with one issuer/world, validate at
/// attachment, and remove components before issuer reuse within a caller-controlled structural boundary.
/// Tickets carry no issuer or timeline identity; matching foreign or abandoned-future tickets can alias.
/// Every UInt32 generation, including zero, is key data. An occupied index rejects any further attachment.
/// Removal uses swap-back, preserving surviving tickets but changing dense positions. The same initial state
/// and ordered operations produce the same dense order, not a canonical or insertion-stable order.
/// Structural changes invalidate positions, enumeration, refs and spans; value writes bypass version checks.
/// Removing or reattaching a component does not advance the issuer generation; delayed component-instance work
/// needs a separate application revision. Multiple stores are independent, not an aggregate transaction.
/// Values and snapshots are shallow. This mutable store requires external synchronization.
/// </remarks>
[Serializable]
[JsonConverter(typeof(StateJsonConverterFactory))]
[MemoryPackable]
public sealed partial class SwiftTicketStore<T> : IStateBacked<SwiftTicketStoreState<T>>, IEnumerable<T>
{
    private int[] _sparse;
    private SwiftTicket[] _tickets;
    private T[] _values;
    private int _count;
    private uint _version;
    private static readonly bool s_clearReleasedValues = RuntimeHelpers.IsReferenceOrContainsReferences<T>();

    /// <summary>Creates an empty store reserving eight sparse and eight dense positions.</summary>
    public SwiftTicketStore() : this(SwiftDenseCapacity.Minimum, SwiftDenseCapacity.Minimum) { }

    /// <summary>Creates an empty store with independent lookup and live-value reservations.</summary>
    /// <param name="sparseCapacity">Lookup length, not population; zero uses empty storage.</param>
    /// <param name="denseCapacity">Live-value capacity; zero uses empty storage.</param>
    /// <exception cref="ArgumentOutOfRangeException">A capacity is negative or greater than 2^30.</exception>
    /// <remarks>Positive capacities round to powers of two with minimum eight. Sparse is validated before dense.
    /// Sparse storage follows the highest index plus one, so huge sparse IDs can exhaust memory at small Count.</remarks>
    public SwiftTicketStore(int sparseCapacity, int denseCapacity)
    {
        ValidateCapacities(sparseCapacity, denseCapacity);
        int sparse = SwiftDenseCapacity.Grow(0, sparseCapacity);
        int dense = SwiftDenseCapacity.Grow(0, denseCapacity);
        _sparse = sparse == 0 ? Array.Empty<int>() : new int[sparse];
        _tickets = dense == 0 ? Array.Empty<SwiftTicket>() : new SwiftTicket[dense];
        _values = dense == 0 ? Array.Empty<T>() : new T[dense];
    }

    /// <summary>Validates and shallow-copies logical entries, rebuilding private lookup storage.</summary>
    /// <param name="state">Parallel live values, indices and generations in exact dense order.
    /// Null arrays normalize to empty; input arrays must not be mutated concurrently.</param>
    /// <exception cref="ArgumentException">Lengths differ, Count exceeds 2^30, an index is unsupported,
    /// or an index occurs more than once, regardless of generation.</exception>
    /// <remarks>Default state is valid empty. Every generation is accepted without consulting an issuer.
    /// Referenced payload objects remain shared. Capacity is reconstructed using the reservation rounding
    /// policy; empty state has zero capacities. Allocation can fail below the supported size limit.</remarks>
    [MemoryPackConstructor]
    public SwiftTicketStore(SwiftTicketStoreState<T> state)
    {
        T[] values = state.DenseValues ?? Array.Empty<T>();
        int[] indices = state.DenseSlots ?? Array.Empty<int>();
        uint[] generations = state.DenseGenerations ?? Array.Empty<uint>();
        ValidateStateLengths(values.Length, indices.Length, generations.Length);
        int requiredSparse = 0;
        for (int i = 0; i < indices.Length; i++)
        {
            ChronicleGuard.ThrowIfArgument((uint)indices[i] >= SwiftDenseCapacity.Maximum, nameof(state), "Unsupported state index.");
            requiredSparse = Math.Max(requiredSparse, indices[i] + 1);
        }
        int sparseCapacity = SwiftDenseCapacity.Grow(0, requiredSparse);
        int denseCapacity = SwiftDenseCapacity.Grow(0, values.Length);
        var sparse = sparseCapacity == 0 ? Array.Empty<int>() : new int[sparseCapacity];
        var tickets = denseCapacity == 0 ? Array.Empty<SwiftTicket>() : new SwiftTicket[denseCapacity];
        var ownedValues = denseCapacity == 0 ? Array.Empty<T>() : new T[denseCapacity];
        for (int i = 0; i < indices.Length; i++)
        {
            int index = indices[i];
            ChronicleGuard.ThrowIfArgument(sparse[index] != 0, nameof(state), "Duplicate state index.");
            sparse[index] = i + 1;
            tickets[i] = new SwiftTicket(index, generations[i]);
        }
        Array.Copy(values, ownedValues, values.Length);
        _sparse = sparse;
        _tickets = tickets;
        _values = ownedValues;
        _count = values.Length;
    }

    // Validate logical shape before deriving or allocating physical capacity.
    private static void ValidateStateLengths(int values, int indices, int generations) =>
        ChronicleGuard.ThrowIfArgument(values != indices || values != generations || values > SwiftDenseCapacity.Maximum,
            "state", "State lengths differ or exceed the supported capacity.");

    private static void ValidateCapacities(int sparseCapacity, int denseCapacity)
    {
        ChronicleGuard.ThrowIfArgumentOutOfRange((uint)sparseCapacity > SwiftDenseCapacity.Maximum, sparseCapacity, nameof(sparseCapacity));
        ChronicleGuard.ThrowIfArgumentOutOfRange((uint)denseCapacity > SwiftDenseCapacity.Maximum, denseCapacity, nameof(denseCapacity));
    }

    /// <summary>Gets the number of locally stored entries.</summary>
    /// <value>The live dense population, not issuer population or proof of liveness.</value>
    [JsonIgnore, MemoryPackIgnore]
    public int Count => _count;

    /// <summary>Gets the allocated sparse lookup length.</summary>
    /// <value>The highest index plus one that can be mapped without sparse growth; removal does not shrink it.</value>
    [JsonIgnore, MemoryPackIgnore]
    public int SparseCapacity => _sparse.Length;

    /// <summary>Gets the allocated live-value capacity.</summary>
    /// <value>The value-array length; only Count entries are live and removal does not shrink storage.</value>
    [JsonIgnore, MemoryPackIgnore]
    public int DenseCapacity => _values.Length;

    /// <summary>Gets a writable contiguous view of the live values in dense order.</summary>
    /// <value>A borrowed span of Count entries; writes do not change ticket membership or structural version.</value>
    /// <remarks>Reacquire after structural mutation. Dense positions can refer to different tickets after removal.</remarks>
    [JsonIgnore, MemoryPackIgnore]
    public Span<T> Values => _values.AsSpan(0, _count);

    /// <summary>Creates a logical snapshot of the live entries in exact dense order.</summary>
    /// <value>Three independently owned shallow arrays; no sparse table, capacity or issuer history is saved.</value>
    /// <remarks>Array writes do not mutate this store; referenced payload objects remain shared. Capture under
    /// external synchronization. Creating a snapshot does not invalidate enumeration or borrowed storage.</remarks>
    [JsonInclude, MemoryPackInclude]
    public SwiftTicketStoreState<T> State
    {
        get
        {
            var values = new T[_count];
            var indices = new int[_count];
            var generations = new uint[_count];
            Array.Copy(_values, values, _count);
            for (int i = 0; i < _count; i++)
            {
                indices[i] = _tickets[i].Index;
                generations[i] = _tickets[i].Generation;
            }
            return new SwiftTicketStoreState<T>(values, indices, generations);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Find(SwiftTicket ticket)
    {
        if ((uint)ticket.Index >= (uint)_sparse.Length) return -1;
        int at = _sparse[ticket.Index] - 1;
        // Only private validated mutations write lookup metadata, establishing the matching index.
        return at >= 0 && _tickets[at].Generation == ticket.Generation ? at : -1;
    }

    /// <summary>Checks whether the exact index/generation pair is locally stored.</summary>
    /// <param name="ticket">The external key; all UInt32 generations, including zero, are permitted.</param>
    /// <returns>True on an exact local match; false for absent, unsupported or mismatched keys, without mutation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsKey(SwiftTicket ticket) => Find(ticket) >= 0;

    /// <summary>Attempts to attach a value at an unoccupied index.</summary>
    /// <param name="ticket">The issuer-validated external key, with index in [0, 2^30).</param>
    /// <param name="value">The value copied or reference retained without cloning or equality checks.</param>
    /// <returns>True on insertion. False for either an exact duplicate or another generation at the occupied
    /// index, preserving values, storage, borrowed views and enumeration.</returns>
    /// <exception cref="ArgumentOutOfRangeException">ticket.Index is negative or at least 2^30.</exception>
    /// <remarks>Allocates all required storage before publication; allocation failure preserves this store.
    /// Success invalidates positions, enumeration and borrows. No issuer state is changed.</remarks>
    public bool TryAdd(SwiftTicket ticket, T value)
    {
        ChronicleGuard.ThrowIfArgumentOutOfRange((uint)ticket.Index >= SwiftDenseCapacity.Maximum, ticket.Index, nameof(ticket));
        if ((uint)ticket.Index < (uint)_sparse.Length && _sparse[ticket.Index] != 0) return false;
        if (ticket.Index >= _sparse.Length || _count == _values.Length)
            EnsureCapacity(ticket.Index + 1, _count + 1);
        int at = _count;
        _tickets[at] = ticket;
        _values[at] = value;
        _sparse[ticket.Index] = at + 1;
        _count = at + 1;
        unchecked { _version++; }
        return true;
    }

    /// <summary>Attaches a value at an unoccupied index, rejecting duplicates and generation conflicts.</summary>
    /// <param name="ticket">The issuer-validated external key, with index in [0, 2^30).</param>
    /// <param name="value">The shallow value or reference to store.</param>
    /// <exception cref="ArgumentOutOfRangeException">ticket.Index is negative or at least 2^30.</exception>
    /// <exception cref="ArgumentException">The index is already occupied, regardless of generation.</exception>
    /// <remarks>Uses TryAdd's allocation/publication rules; failure preserves this store and never replaces an entry.</remarks>
    public void Add(SwiftTicket ticket, T value) =>
        ChronicleGuard.ThrowIfArgument(!TryAdd(ticket, value), nameof(ticket), "The index is already occupied.");

    /// <summary>Attempts to read the value stored under an exact ticket.</summary>
    /// <param name="ticket">The external index/generation pair to match locally.</param>
    /// <param name="value">Receives the shallow stored value on success; default(T) on failure.</param>
    /// <returns>True for an exact local match; otherwise false without mutation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGet(SwiftTicket ticket, out T value)
    {
        int at = Find(ticket);
        if (at < 0) { value = default!; return false; }
        value = _values[at];
        return true;
    }

    /// <summary>Gets a writable reference to the value stored under an exact ticket.</summary>
    /// <param name="ticket">The external index/generation pair to match locally.</param>
    /// <returns>A borrowed reference into live value storage; writes bypass structural version checks.</returns>
    /// <exception cref="KeyNotFoundException">The key is absent, unsupported or has a different generation.</exception>
    /// <remarks>Reacquire after structural mutation. This validates local membership, not issuer liveness.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetRef(SwiftTicket ticket)
    {
        int at = Find(ticket);
        if (at < 0) throw MissingTicket();
        return ref _values[at];
    }

    /// <summary>Gets the copied ticket associated with a live dense position.</summary>
    /// <param name="denseIndex">The zero-based position, less than Count.</param>
    /// <returns>The exact ticket at the position without allocating or exposing writable metadata.</returns>
    /// <exception cref="ArgumentOutOfRangeException">denseIndex is outside the live prefix.</exception>
    /// <remarks>Structural mutation invalidates the position association, not the copied ticket value.
    /// Issuer policy and local membership still govern subsequent use of that ticket.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SwiftTicket GetTicketAt(int denseIndex)
    {
        if ((uint)denseIndex >= (uint)_count)
            throw InvalidDenseIndex(denseIndex);
        return _tickets[denseIndex];
    }

    // A terminal throw in the caller keeps diagnostic construction and register preservation off hot loops.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static KeyNotFoundException MissingTicket() => new("The exact ticket is not stored.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ArgumentOutOfRangeException InvalidDenseIndex(int denseIndex) =>
        new(nameof(denseIndex), denseIndex, "Dense index is outside the live range.");

    /// <summary>Removes an exact local entry, moving the last value into any gap.</summary>
    /// <param name="ticket">The external index/generation pair to remove.</param>
    /// <returns>True on removal; false without mutation for absent, unsupported or mismatched keys.</returns>
    /// <remarks>Does not allocate or mutate issuer generations. Surviving tickets remain stored; positions,
    /// enumeration and borrowed storage invalidate. Discarded payload references are released.</remarks>
    public bool Remove(SwiftTicket ticket)
    {
        int at = Find(ticket);
        if (at < 0) return false;
        int last = --_count;
        _sparse[ticket.Index] = 0;
        if (at != last)
        {
            SwiftTicket moved = _tickets[last];
            _tickets[at] = moved;
            _values[at] = _values[last];
            _sparse[moved.Index] = at + 1;
        }
        _tickets[last] = default;
        if (s_clearReleasedValues) _values[last] = default!;
        unchecked { _version++; }
        return true;
    }

    /// <summary>Removes local entries and releases payload references while retaining capacity.</summary>
    /// <remarks>Runs in O(Count), not sparse capacity. Empty Clear is a no-op. A nonempty Clear invalidates
    /// positions, enumeration and borrows without changing issuer lifetime, generations or liveness.</remarks>
    public void Clear()
    {
        if (_count == 0) return;
        for (int i = 0; i < _count; i++) _sparse[_tickets[i].Index] = 0;
        Array.Clear(_tickets, 0, _count);
        if (s_clearReleasedValues) Array.Clear(_values, 0, _count);
        _count = 0;
        unchecked { _version++; }
    }

    /// <summary>Reserves sparse lookup and dense value capacity without shrinking either.</summary>
    /// <param name="sparseCapacity">Minimum lookup length, meaning highest desired index plus one.</param>
    /// <param name="denseCapacity">Minimum live-value capacity, independent of sparse reservation.</param>
    /// <exception cref="ArgumentOutOfRangeException">A capacity is negative or greater than 2^30.</exception>
    /// <remarks>Validates both arguments before allocation. All replacement arrays publish together, so failure
    /// preserves contents, capacities, borrows and version. Actual growth invalidates positions, enumeration
    /// and borrows even when only sparse storage grows; a no-op preserves them.</remarks>
    public void EnsureCapacity(int sparseCapacity, int denseCapacity)
    {
        ValidateCapacities(sparseCapacity, denseCapacity);
        int s = SwiftDenseCapacity.Grow(_sparse.Length, sparseCapacity);
        int d = SwiftDenseCapacity.Grow(_values.Length, denseCapacity);
        if (s == _sparse.Length && d == _values.Length) return;
        int[] sparse = _sparse;
        SwiftTicket[] tickets = _tickets;
        T[] values = _values;
        if (s != _sparse.Length)
        {
            sparse = new int[s];
            Array.Copy(_sparse, sparse, _sparse.Length);
        }
        if (d != _values.Length)
        {
            tickets = new SwiftTicket[d];
            values = new T[d];
            Array.Copy(_tickets, tickets, _count);
            Array.Copy(_values, values, _count);
        }
        _sparse = sparse;
        _tickets = tickets;
        _values = values;
        unchecked { _version++; }
    }

    /// <summary>Creates a concrete value-type enumerator in dense order.</summary>
    /// <returns>An independent enumerator without allocation or boxing.</returns>
    public SwiftTicketStoreEnumerator GetEnumerator() => new(this);

    /// <summary>Creates a boxed generic enumerator in dense order.</summary>
    /// <returns>An enumerator invalidated by structural changes.</returns>
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    /// <summary>Creates a boxed non-generic enumerator in dense order.</summary>
    /// <returns>An enumerator invalidated by structural changes.</returns>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
