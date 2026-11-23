//=======================================================================
// SwiftDenseTicketMap.cs
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

/// <summary>Owns reusable generation-checked tickets and densely packed mutable values.</summary>
/// <typeparam name="T">The value type; duplicate, null and default values are allowed.</typeparam>
/// <remarks>
/// Removal moves the last live value into the gap, preserving surviving tickets. Dense order is deterministic
/// for the same initial state and ordered mutations, rather than insertion or slot order. Fresh generations
/// start at one; default tickets are invalid. Maximum-generation slots retire instead of wrapping.
/// Tickets contain no owner identity: matching foreign tickets can alias. Reconstructing from an old state
/// can revive numeric tickets; discard handles from an abandoned future. Copies of reference payloads are
/// shallow. Instances require external synchronization. Structural changes invalidate borrowed positions,
/// refs and spans, including growth that detaches backing arrays. Tickets do not promise stable addresses.
/// </remarks>
[Serializable]
[JsonConverter(typeof(StateJsonConverterFactory))]
[MemoryPackable]
public sealed partial class SwiftDenseTicketMap<T> : IStateBacked<SwiftDenseTicketMapState<T>>, ISwiftCloneable<T>, IEnumerable<T>
{
    // Live positions are nonnegative; free positions encode -2-next, with end/head -1.
    private struct Slot
    {
        public uint Generation;
        public int Position;
    }

    /// <summary>The initial live capacity used by the parameterless constructor.</summary>
    public const int DefaultCapacity = 8;
    private const int MaxCapacity = 1 << 30;
    private const int Unassigned = int.MinValue;
    private static readonly bool s_clearReleasedValues = RuntimeHelpers.IsReferenceOrContainsReferences<T>();

    private Slot[] _slots = Array.Empty<Slot>();
    private T[] _values = Array.Empty<T>();
    private int[] _denseSlots = Array.Empty<int>();
    private int _count;
    private int _issued;
    private int _retired;
    private int _freeHead = -1;
    private int _freeCount;
    private uint _version;

    /// <summary>Creates an empty owner with the default reserved capacity.</summary>
    public SwiftDenseTicketMap() : this(DefaultCapacity) { }

    /// <summary>Creates an empty owner with capacity for the requested live population.</summary>
    /// <param name="capacity">Nonnegative capacity up to 2^30; zero uses empty storage. Positive requests round
    /// up to a power of two, with a minimum of eight. Allocation can fail below the supported limit.</param>
    /// <exception cref="ArgumentOutOfRangeException">capacity is negative or greater than 2^30.</exception>
    public SwiftDenseTicketMap(int capacity) => EnsureCapacity(capacity);

    /// <summary>Reconstructs an owner by validating and shallow-copying logical state.</summary>
    /// <param name="state">Dense values/slots, all issued generations and the exact bottom-to-top free order.
    /// Null arrays normalize to empty. Input arrays must not be mutated concurrently.</param>
    /// <exception cref="ArgumentException">The supported-size state has inconsistent lengths, invalid or
    /// duplicate IDs, live/free overlap, or incomplete generation ownership.</exception>
    /// <exception cref="InvalidOperationException">The state exceeds the supported slot/identity budget.</exception>
    /// <remarks>Default state is valid empty. Every zero issued generation is retired. Every nonzero generation
    /// must belong to exactly one live or free slot. Array storage is copied; referenced payload objects remain
    /// shared. Capacity is reconstructed, not persisted. Failure does not replace another owner.</remarks>
    [MemoryPackConstructor]
    public SwiftDenseTicketMap(SwiftDenseTicketMapState<T> state)
    {
        T[] values = state.DenseValues ?? Array.Empty<T>();
        int[] denseSlots = state.DenseSlots ?? Array.Empty<int>();
        uint[] generations = state.SlotGenerations ?? Array.Empty<uint>();
        int[] freeSlots = state.FreeSlots ?? Array.Empty<int>();
        ChronicleGuard.ThrowIfArgument(values.Length != denseSlots.Length, nameof(state), "Dense lengths differ.");
        GetRequiredSlots(generations.Length, 0, values.Length);
        var slots = new Slot[generations.Length];
        for (int i = 0; i < slots.Length; i++)
            slots[i] = new Slot { Generation = generations[i], Position = Unassigned };

        for (int i = 0; i < denseSlots.Length; i++)
        {
            int slot = denseSlots[i];
            ValidateStateSlot(slots, slot);
            slots[slot].Position = i;
        }

        int freeHead = -1;
        for (int i = 0; i < freeSlots.Length; i++)
        {
            int slot = freeSlots[i];
            ValidateStateSlot(slots, slot);
            slots[slot].Position = -2 - freeHead;
            freeHead = slot;
        }

        int retired = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].Generation == 0)
                retired++;
            else
                ChronicleGuard.ThrowIfArgument(slots[i].Position == Unassigned, nameof(state), "An issued slot is unaccounted for.");
        }

        _values = (T[])values.Clone();
        _denseSlots = (int[])denseSlots.Clone();
        _slots = slots;
        _count = values.Length;
        _issued = generations.Length;
        _retired = retired;
        _freeHead = freeHead;
        _freeCount = freeSlots.Length;
    }

    private static void ValidateStateSlot(Slot[] slots, int slot)
    {
        ChronicleGuard.ThrowIfArgument((uint)slot >= (uint)slots.Length || slots[slot].Generation == 0 ||
            slots[slot].Position != Unassigned, "state", "Invalid, retired or duplicate state slot.");
    }

    /// <summary>Gets the number of live values.</summary>
    /// <value>The dense live population, excluding free, retired and unissued slots.</value>
    [JsonIgnore, MemoryPackIgnore]
    public int Count => _count;

    /// <summary>Gets the allocated value capacity.</summary>
    /// <value>The value-array length; removal does not shrink it.</value>
    [JsonIgnore, MemoryPackIgnore]
    public int DenseCapacity => _values.Length;

    /// <summary>Gets the allocated slot-metadata capacity.</summary>
    /// <value>The slot-array length, including free, retired and unissued storage; not a remaining issuance budget.</value>
    [JsonIgnore, MemoryPackIgnore]
    public int SlotCapacity => _slots.Length;

    /// <summary>Gets a writable view of the live values in dense order.</summary>
    /// <value>A borrowed span of length Count; writes do not change ticket ownership or enumeration versions.</value>
    /// <remarks>Reacquire after structural changes. A moved value can occupy a previous value's address.</remarks>
    [JsonIgnore, MemoryPackIgnore]
    public Span<T> Values => _values.AsSpan(0, _count);

    /// <summary>Creates a logical snapshot preserving dense order, identity history and future reuse.</summary>
    /// <value>A new four-array shallow snapshot; mutating its arrays does not mutate this owner.</value>
    /// <remarks>Referenced payload objects remain shared. Free slots are exported bottom to top; capacities
    /// and reverse lookup positions are not serialized. Snapshot creation does not invalidate borrowed storage.</remarks>
    [JsonInclude, MemoryPackInclude]
    public SwiftDenseTicketMapState<T> State
    {
        get
        {
            var values = new T[_count];
            var denseSlots = new int[_count];
            var generations = new uint[_issued];
            var free = new int[_freeCount];
            Array.Copy(_values, values, _count);
            Array.Copy(_denseSlots, denseSlots, _count);
            for (int i = 0; i < _issued; i++) generations[i] = _slots[i].Generation;
            int cursor = _freeHead;
            for (int i = free.Length - 1; i >= 0; i--)
            {
                free[i] = cursor;
                cursor = -2 - _slots[cursor].Position;
            }
            return new SwiftDenseTicketMapState<T>(values, denseSlots, generations, free);
        }
    }

    /// <summary>Appends a value and issues a ticket identifying its allocation.</summary>
    /// <param name="value">The value copied or reference retained without cloning.</param>
    /// <returns>A ticket from the free-slot LIFO, or the next unissued slot with generation one.</returns>
    /// <exception cref="InvalidOperationException">The remaining slot/identity budget cannot support another entry.</exception>
    /// <remarks>Completes capacity allocation before consuming identity or publishing contents. Allocation
    /// failure preserves storage, tickets, values and reuse order. Success invalidates enumeration and borrows.</remarks>
    public SwiftTicket Add(T value)
    {
        Reserve(_count + 1);
        int slot;
        if (_freeHead >= 0)
        {
            slot = _freeHead;
            _freeHead = -2 - _slots[slot].Position;
            _freeCount--;
        }
        else
        {
            slot = _issued++;
            _slots[slot].Generation = 1;
        }
        _slots[slot].Position = _count;
        _values[_count] = value;
        _denseSlots[_count++] = slot;
        unchecked { _version++; }
        return new SwiftTicket(slot, _slots[slot].Generation);
    }

    /// <summary>Checks whether a ticket matches a live allocation in this owner.</summary>
    /// <param name="ticket">The issuer-scoped index/generation pair; foreign numeric aliases cannot be detected.</param>
    /// <returns>True for a matching issued live slot; false otherwise, without mutation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsValid(SwiftTicket ticket) => (uint)ticket.Index < (uint)_issued &&
        _slots[ticket.Index].Position >= 0 && _slots[ticket.Index].Generation == ticket.Generation;

    /// <summary>Attempts to read the value identified by a ticket.</summary>
    /// <param name="ticket">The issuer-scoped ticket to validate.</param>
    /// <param name="value">Receives the shallow stored value on success; default(T) on failure.</param>
    /// <returns>True for a live matching allocation; false for every invalid ticket. No mutation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGet(SwiftTicket ticket, out T value)
    {
        if (IsValid(ticket))
        {
            value = _values[_slots[ticket.Index].Position];
            return true;
        }
        value = default!;
        return false;
    }

    /// <summary>Gets a writable reference to the value identified by a valid ticket.</summary>
    /// <param name="ticket">A live ticket from this owner.</param>
    /// <returns>A borrowed reference into this owner's live value storage.</returns>
    /// <exception cref="ArgumentException">ticket is invalid, unissued, removed, stale or retired.</exception>
    /// <remarks>Reacquire after structural changes. Writes through the reference bypass version checks.</remarks>
    public ref T GetRef(SwiftTicket ticket)
    {
        ChronicleGuard.ThrowIfArgument(!IsValid(ticket), nameof(ticket), "Invalid ticket.");
        return ref _values[_slots[ticket.Index].Position];
    }

    /// <summary>Gets the ticket associated with a live dense position.</summary>
    /// <param name="denseIndex">The zero-based position, less than Count; valid only until structural mutation.</param>
    /// <returns>The current live ticket, without allocating a ticket projection.</returns>
    /// <exception cref="ArgumentOutOfRangeException">denseIndex is outside the live dense prefix.</exception>
    public SwiftTicket GetTicketAt(int denseIndex)
    {
        ChronicleGuard.ThrowIfArgumentOutOfRange((uint)denseIndex >= (uint)_count, denseIndex, nameof(denseIndex));
        int slot = _denseSlots[denseIndex];
        return new SwiftTicket(slot, _slots[slot].Generation);
    }

    /// <summary>Removes a matching allocation and repairs dense storage by moving the last live value.</summary>
    /// <param name="ticket">The issuer-scoped ticket to remove.</param>
    /// <returns>True if removed; false without mutation for an invalid ticket.</returns>
    /// <remarks>Does not allocate. Surviving tickets remain valid; dense indices, enumeration and borrows
    /// invalidate. Releases discarded references. A maximum-generation slot retires; other slots advance and
    /// enter the free LIFO. This does not remove entries from other collections using the ticket.</remarks>
    public bool Remove(SwiftTicket ticket)
    {
        if (!IsValid(ticket)) return false;
        int at = _slots[ticket.Index].Position;
        int last = --_count;
        if (at != last)
        {
            int moved = _denseSlots[last];
            _values[at] = _values[last];
            _denseSlots[at] = moved;
            _slots[moved].Position = at;
        }
        if (s_clearReleasedValues) _values[last] = default!;
        Release(ticket.Index);
        unchecked { _version++; }
        return true;
    }

    private void Release(int slot)
    {
        ref Slot entry = ref _slots[slot];
        if (entry.Generation == uint.MaxValue)
        {
            entry.Generation = 0;
            entry.Position = -1;
            _retired++;
        }
        else
        {
            entry.Generation++;
            entry.Position = -2 - _freeHead;
            _freeHead = slot;
            _freeCount++;
        }
    }

    /// <summary>Removes all live values, preserving identity history and reusable capacity.</summary>
    /// <remarks>Does not allocate. Releases discarded references and invalidates live tickets and borrows.
    /// Increasing old dense order appends newly free slots to the existing free sequence; terminal generations
    /// retire. Existing free order is preserved. Empty Clear is a no-op, including enumeration versions.</remarks>
    public void Clear()
    {
        if (_count == 0) return;
        for (int i = 0; i < _count; i++) Release(_denseSlots[i]);
        if (s_clearReleasedValues) Array.Clear(_values, 0, _count);
        _count = 0;
        unchecked { _version++; }
    }

    /// <summary>Reserves storage for a live population, accounting for currently retired slots.</summary>
    /// <param name="capacity">Nonnegative requested live capacity up to 2^30; smaller requests do not shrink storage.</param>
    /// <exception cref="ArgumentOutOfRangeException">capacity is negative or greater than 2^30.</exception>
    /// <exception cref="InvalidOperationException">The owner's remaining identity budget cannot satisfy the request.</exception>
    /// <remarks>Actual growth invalidates enumeration and borrows; a no-op preserves them. All required arrays
    /// publish together, so allocation failure preserves existing storage and state. Later retirement consumes
    /// usable slots and can require growth even if the live population stays within this reservation.</remarks>
    public void EnsureCapacity(int capacity)
    {
        ChronicleGuard.ThrowIfArgumentOutOfRange((uint)capacity > MaxCapacity, capacity, nameof(capacity));
        Reserve(capacity);
    }

    private void Reserve(int capacity)
    {
        int requiredSlots = GetRequiredSlots(_issued, _retired, capacity);
        int denseCapacity = GetGrowthCapacity(_values.Length, capacity);
        int slotCapacity = GetGrowthCapacity(_slots.Length, requiredSlots);
        if (denseCapacity == _values.Length && slotCapacity == _slots.Length) return;

        T[] values = _values;
        int[] denseSlots = _denseSlots;
        Slot[] slots = _slots;
        if (denseCapacity != _values.Length)
        {
            values = new T[denseCapacity];
            denseSlots = new int[denseCapacity];
            Array.Copy(_values, values, _count);
            Array.Copy(_denseSlots, denseSlots, _count);
        }
        if (slotCapacity != _slots.Length)
        {
            slots = new Slot[slotCapacity];
            Array.Copy(_slots, slots, _issued);
        }
        _values = values;
        _denseSlots = denseSlots;
        _slots = slots;
        unchecked { _version++; }
    }

    private static int GetRequiredSlots(int issued, int retired, long requestedLive)
    {
        long required = Math.Max(issued, retired + requestedLive);
        ChronicleGuard.ThrowIfTrue(required > MaxCapacity, message: "The supported identity budget is exhausted.");
        return (int)required;
    }

    private static int GetGrowthCapacity(int current, int required)
    {
        if (required <= current) return current;
        long target = Math.Min(MaxCapacity, Math.Max(required, Math.Max(DefaultCapacity, (long)current * 2)));
        // NextPowerOfTwo uses Int32.MaxValue as its sentinel at the exact supported limit.
        return Math.Min(MaxCapacity, SwiftHashTools.NextPowerOfTwo((int)target));
    }

    /// <summary>Copies live values into a destination after clearing it.</summary>
    /// <param name="output">The caller-owned collection to clear and populate in dense order.</param>
    /// <exception cref="ArgumentNullException">output is null.</exception>
    /// <remarks>Copies are shallow and contain no tickets or generation history. Destination callbacks must not
    /// mutate this owner; a destination exception can leave partial output. This owner is not otherwise mutated.</remarks>
    public void CloneTo(ICollection<T> output)
    {
        ChronicleGuard.ThrowIfNull(output, nameof(output));
        output.Clear();
        for (int i = 0; i < _count; i++) output.Add(_values[i]);
    }

    /// <summary>Creates a value-type enumerator over live values in dense order.</summary>
    /// <returns>An independent enumerator; concrete enumeration does not allocate or box.</returns>
    public SwiftDenseTicketMapEnumerator GetEnumerator() => new(this);

    /// <summary>Creates a boxed enumerator over live values in dense order.</summary>
    /// <returns>A generic enumerator invalidated by structural changes.</returns>
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    /// <summary>Creates a boxed enumerator over live values in dense order.</summary>
    /// <returns>A non-generic enumerator invalidated by structural changes.</returns>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
