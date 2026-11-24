//=======================================================================
// SwiftTicketMap.cs
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

/// <summary>
/// Stores values behind reusable index/generation tickets.
/// </summary>
/// <typeparam name="T">Specifies the type of elements stored in the map.</typeparam>
/// <remarks>
/// Copies are shallow and instances require external synchronization. Enumeration follows increasing
/// allocated-slot order. Slots remain stable until removal. Removed or cleared entries increment finite UInt32
/// generations; old tokens can match again after generation wrap. Default tokens are not guaranteed invalid.
/// </remarks>
[Serializable]
[JsonConverter(typeof(StateJsonConverterFactory))]
[MemoryPackable]
public sealed partial class SwiftTicketMap<T> : IStateBacked<SwiftTicketMapState<T>>, ISwiftCloneable<T>, IEnumerable<T>
{
    #region Nested Types

    private struct Entry
    {
        public uint Generation;
        public bool IsUsed;
        public T Value;
    }

    #endregion

    #region Constants

    /// <summary>
    /// Represents the default initial capacity for the collection.
    /// </summary>
    /// <remarks>
    /// Use this constant when initializing the collection to its default size. The value is typically used to
    /// optimize memory allocation for small collections.
    /// </remarks>
    public const int DefaultCapacity = 8;

    #endregion

    #region Fields

    private Entry[] _entries;
    private SwiftIntStack _freeIndices;

    private int _count;
    private int _peak;

    private uint _version;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the SwiftTicketMap class with the default capacity.
    /// </summary>
    public SwiftTicketMap() : this(DefaultCapacity) { }

    /// <summary>
    /// Initializes a new instance of the SwiftTicketMap class with the specified initial capacity.
    /// </summary>
    /// <param name="capacity">The requested element capacity; values at or below DefaultCapacity use the minimum
    /// allocation. Larger values round up.</param>
    /// <remarks>
    /// The actual capacity will be set to the next power of two greater than or equal to the specified capacity,
    /// or to the default capacity if the specified value is too small. This ensures efficient internal storage
    /// and lookup performance.
    /// </remarks>
    public SwiftTicketMap(int capacity)
    {
        capacity = capacity <= DefaultCapacity
            ? DefaultCapacity
            : SwiftHashTools.NextPowerOfTwo(capacity);

        _entries = new Entry[capacity];
        _freeIndices = new SwiftIntStack(capacity);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.SwiftTicketMap`1" /> class with the
    /// specified <see cref="T:SwiftCollections.SwiftTicketMapState`1" />.
    /// </summary>
    /// <param name="state">The state to restore by shallow-copying arrays. Null arrays are treated as empty; free
    /// indices must be in range, unique and disjoint from allocated slots. Uniqueness/disjointness are not
    /// validated.</param>
    /// <exception cref="T:System.ArgumentException">A state free index is outside reconstructed
    /// capacity.</exception>
    /// <remarks>
    /// Input arrays are copied, not retained. Default state is accepted as empty. Missing item values default;
    /// missing allocation flags are false; missing generations are zero. Peak is normalized into capacity.
    /// Free-index duplicates or overlap with live entries are not rejected and can corrupt later allocation.
    /// </remarks>
    [MemoryPackConstructor]
    public SwiftTicketMap(SwiftTicketMapState<T> state)
    {
        _entries = Array.Empty<Entry>();
        _freeIndices = new SwiftIntStack(0);

        State = state;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the number of elements contained in the collection.
    /// </summary>
    /// <value>The number of live entries, excluding gaps and spare storage.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Count => _count;

    /// <summary>
    /// Gets the total number of elements that the internal data structure can hold without resizing.
    /// </summary>
    /// <value>The allocated slot-array length.</value>
    /// <remarks>
    /// This value represents the allocated size of the underlying storage, which may be greater than the actual
    /// number of elements contained. Capacity is always greater than or equal to the current count of elements.
    /// </remarks>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Capacity => _entries.Length;

    /// <summary>
    /// Gets a shallow snapshot of storage and allocation metadata.
    /// </summary>
    /// <value>A state with copied arrays; referenced elements remain shared.</value>
    /// <remarks>
    /// Stores slot allocation, free-index stack and high-water metadata plus generation history. Restore
    /// normalizes the high-water boundary into capacity and tolerates missing arrays/default values. It validates
    /// free-index range but trusts uniqueness and disjointness; use only consistent states.
    /// </remarks>
    [JsonInclude]
    [MemoryPackInclude]
    public SwiftTicketMapState<T> State
    {
        get
        {
            int length = _entries.Length;

            var items = new T[length];
            var allocated = new bool[length];
            var generations = new uint[length];

            for (int i = 0; i < length; i++)
            {
                generations[i] = _entries[i].Generation;

                if (_entries[i].IsUsed)
                {
                    items[i] = _entries[i].Value;
                    allocated[i] = true;
                }
            }

            int[] free = new int[_freeIndices.Count];
            Array.Copy(_freeIndices.Array, free, _freeIndices.Count);

            return new SwiftTicketMapState<T>(
                items,
                allocated,
                generations,
                free,
                _peak
            );
        }
        internal set
        {
            var items = value.Items ?? Array.Empty<T>();
            var allocated = value.Allocated ?? Array.Empty<bool>();
            var generations = value.Generations ?? Array.Empty<uint>();
            var freeIndices = value.FreeIndices ?? Array.Empty<int>();

            int sourceLength = GetStateSourceLength(items, allocated, generations);
            int capacity = GetStateCapacity(sourceLength);
            InitializeStateStorage(capacity, freeIndices.Length);

            int maxReferencedIndex = RestoreEntries(items, allocated, generations, sourceLength);
            maxReferencedIndex = RestoreFreeIndices(freeIndices, capacity, maxReferencedIndex);

            _peak = NormalizePeak(value.Peak, maxReferencedIndex, capacity);
            _version = 0;
        }
    }

    private static int GetStateSourceLength(T[] items, bool[] allocated, uint[] generations)
    {
        return Math.Max(items.Length, Math.Max(allocated.Length, generations.Length));
    }

    private static int GetStateCapacity(int sourceLength)
    {
        if (sourceLength < DefaultCapacity)
            return DefaultCapacity;

        return SwiftHashTools.NextPowerOfTwo(sourceLength);
    }

    private void InitializeStateStorage(int capacity, int freeIndexCount)
    {
        _entries = new Entry[capacity];
        _freeIndices = new SwiftIntStack(Math.Max(SwiftIntStack.DefaultCapacity, freeIndexCount));
        _count = 0;
    }

    private int RestoreEntries(T[] items, bool[] allocated, uint[] generations, int sourceLength)
    {
        int maxReferencedIndex = -1;
        for (int i = 0; i < sourceLength; i++)
        {
            ref Entry entry = ref _entries[i];
            bool isAllocated = IsStateIndexAllocated(allocated, i);

            entry.Generation = GetStateGeneration(generations, i);
            // Generation history survives Clear; only allocation reserves the high-water range.
            if (isAllocated)
                maxReferencedIndex = i;

            if (isAllocated)
                RestoreAllocatedEntry(ref entry, items, i);
        }

        return maxReferencedIndex;
    }

    private static uint GetStateGeneration(uint[] generations, int index)
    {
        if (generations.Length <= index)
            return 0;

        return generations[index];
    }

    private static bool IsStateIndexAllocated(bool[] allocated, int index)
    {
        return allocated.Length > index && allocated[index];
    }

    private void RestoreAllocatedEntry(ref Entry entry, T[] items, int index)
    {
        if (items.Length > index)
            entry.Value = items[index];

        entry.IsUsed = true;
        _count++;
    }

    private int RestoreFreeIndices(int[] freeIndices, int capacity, int maxReferencedIndex)
    {
        foreach (var index in freeIndices)
        {
            ChronicleGuard.ThrowIfArgument((uint)index >= (uint)capacity, message: "Free index is out of range.");

            _freeIndices.Push(index);
            if (index > maxReferencedIndex)
                maxReferencedIndex = index;
        }

        return maxReferencedIndex;
    }

    private static int NormalizePeak(int peak, int maxReferencedIndex, int capacity)
    {
        if (peak < 0)
            peak = 0;

        return Math.Min(Math.Max(peak, maxReferencedIndex + 1), capacity);
    }

    #endregion

    #region Core Operations

    /// <summary>
    /// Adds the specified value to the collection and returns a ticket that can be used to reference it.
    /// </summary>
    /// <param name="value">The value to store without cloning.</param>
    /// <returns>A token for the new slot and its current generation; valid only with the originating map until
    /// removal, clear, or generation wrap.</returns>
    /// <remarks>
    /// The returned ticket can be used to access or remove the value later. Tickets are only valid as long as the
    /// value remains in the collection.
    /// </remarks>
    public SwiftTicket Add(T value)
    {
        int index;

        if (_freeIndices.Count == 0)
        {
            index = _peak++;

            if ((uint)index >= (uint)_entries.Length)
                Resize(_entries.Length * 2);
        }
        else
        {
            index = _freeIndices.Pop();
        }

        ref Entry entry = ref _entries[index];

        entry.Value = value;
        entry.IsUsed = true;

        _count++;
        _version++;

        return new SwiftTicket(index, entry.Generation);
    }

    /// <summary>
    /// Attempts to retrieve the value associated with the specified ticket.
    /// </summary>
    /// <param name="ticket">The index/generation token from this map. Tokens have no map identity; a foreign or
    /// default token can accidentally match a live entry.</param>
    /// <param name="value">Receives the stored value on success; default(T) on failure.</param>
    /// <returns>true if the slot/token/ID identifies a live entry; false otherwise, with the out value set to
    /// default(T). No mutation.</returns>
    /// <remarks>
    /// Use this method to safely attempt retrieval without throwing an exception if the ticket is invalid or the
    /// entry is not in use.
    /// </remarks>
    public bool TryGet(SwiftTicket ticket, out T value)
    {
        if ((uint)ticket.Index >= (uint)_entries.Length)
        {
            value = default!;
            return false;
        }

        ref Entry entry = ref _entries[ticket.Index];

        if (!entry.IsUsed || entry.Generation != ticket.Generation)
        {
            value = default!;
            return false;
        }

        value = entry.Value;
        return true;
    }

    /// <summary>
    /// Returns a reference to the value associated with the specified ticket.
    /// </summary>
    /// <param name="ticket">The index/generation token from this map. Tokens have no map identity; a foreign or
    /// default token can accidentally match a live entry.</param>
    /// <returns>A writable reference to the live slot value.</returns>
    /// <exception cref="T:System.IndexOutOfRangeException">ticket.Index is outside backing storage.</exception>
    /// <exception cref="T:System.InvalidOperationException">The in-range slot is unused or its generation
    /// differs.</exception>
    /// <remarks>
    /// Borrowed reference into owned storage. Removal/clear/reuse can repurpose the slot; resizing detaches old
    /// storage. Do not retain across those operations. Writes through the reference bypass enumerator version
    /// checks.
    /// </remarks>
    public ref T GetRef(SwiftTicket ticket)
    {
        ref Entry entry = ref _entries[ticket.Index];

        ChronicleGuard.ThrowIfTrue(!entry.IsUsed || entry.Generation != ticket.Generation, message: "Invalid ticket");

        return ref entry.Value;
    }

    /// <summary>
    /// Removes the entry associated with the specified ticket from the collection.
    /// </summary>
    /// <param name="ticket">The index/generation token from this map. Tokens have no map identity; a foreign or
    /// default token can accidentally match a live entry.</param>
    /// <returns>true if a matching live entry was removed; false if absent or invalid, with contents unchanged.
    /// Freed slots are reused later.</returns>
    /// <remarks>
    /// If the ticket does not refer to a valid or currently used entry, the method returns false and no action is
    /// taken. Removing an entry invalidates the ticket for future operations.
    /// </remarks>
    public bool Remove(SwiftTicket ticket)
    {
        if ((uint)ticket.Index >= (uint)_entries.Length)
            return false;

        ref Entry entry = ref _entries[ticket.Index];

        if (!entry.IsUsed || entry.Generation != ticket.Generation)
            return false;

        entry.Value = default!;
        entry.IsUsed = false;

        entry.Generation++;

        _freeIndices.Push(ticket.Index);

        _count--;
        _version++;

        return true;
    }

    /// <summary>
    /// Determines whether the specified ticket refers to a valid and currently used entry.
    /// </summary>
    /// <param name="ticket">The index/generation token from this map. Tokens have no map identity; a foreign or
    /// default token can accidentally match a live entry.</param>
    /// <returns>true if the requested live slot, token, ID or value exists; false for absent/out-of-range inputs.
    /// No mutation.</returns>
    /// <remarks>
    /// A ticket may become invalid if the referenced entry has been removed or replaced. Use this method to check
    /// ticket validity before accessing the associated entry.
    /// </remarks>
    public bool IsValid(SwiftTicket ticket)
    {
        if ((uint)ticket.Index >= (uint)_entries.Length)
            return false;

        ref Entry entry = ref _entries[ticket.Index];

        return entry.IsUsed && entry.Generation == ticket.Generation;
    }

    /// <summary>
    /// Removes all elements and invalidates their tickets without reducing capacity.
    /// </summary>
    /// <remarks>
    /// Empties the collection while preserving capacity. Live generations increment; generation history survives
    /// so cleared tokens normally fail on reuse. UInt32 wrap can eventually repeat a token.
    /// </remarks>
    public void Clear() => Clear(clearRetainedReferences: false);

    /// <summary>
    /// Removes all elements, optionally releasing references throughout retained backing storage.
    /// </summary>
    /// <param name="clearRetainedReferences">If true, also releases references outside the previously used
    /// range.</param>
    /// <remarks>
    /// Empties the collection while preserving capacity. Live generations increment; generation history survives
    /// so cleared tokens normally fail on reuse. UInt32 wrap can eventually repeat a token.
    /// </remarks>
    public void Clear(bool clearRetainedReferences)
    {
        int end = clearRetainedReferences && RuntimeHelpers.IsReferenceOrContainsReferences<T>() ? _entries.Length : _peak;
        for (int i = 0; i < end; i++)
        {
            ref Entry entry = ref _entries[i];
            if (entry.IsUsed)
            {
                entry.IsUsed = false;
                entry.Generation++;
            }
            entry.Value = default!;
        }
        _freeIndices.Reset();
        _count = 0;
        _peak = 0;
        _version++;
    }
    #endregion

    #region Capacity

    /// <summary>
    /// Ensures that the underlying storage has at least the specified capacity, expanding it if necessary.
    /// </summary>
    /// <param name="capacity">The minimum storage length; requests at or below the current capacity change
    /// nothing. Keep requests below 2^30 to avoid the capacity sentinel.</param>
    /// <remarks>
    /// If the current capacity is less than the specified value, the storage is resized to accommodate at least
    /// that many elements. The actual capacity may be rounded up to the next power of two for performance
    /// reasons.
    /// </remarks>
    public void EnsureCapacity(int capacity)
    {
        capacity = SwiftHashTools.NextPowerOfTwo(capacity);
        if (capacity > _entries.Length)
            Resize(capacity);
    }

    private void Resize(int newSize)
    {
        Entry[] newArray = new Entry[newSize];
        Array.Copy(_entries, newArray, _entries.Length);
        _entries = newArray;
    }

    #endregion

    #region Utility

    /// <inheritdoc />
    public void CloneTo(ICollection<T> output)
    {
        ChronicleGuard.ThrowIfNull(output, nameof(output));

        output.Clear();

        uint count = 0;
        uint peak = (uint)_peak;

        for (uint i = 0; i < peak && count < (uint)_count; i++)
        {
            if (_entries[i].IsUsed)
            {
                output.Add(_entries[i].Value);
                count++;
            }
        }
    }

    /// <summary>
    /// Determines whether the <see cref="T:SwiftCollections.SwiftTicketMap`1" /> contains an element that matches
    /// the conditions defined by the specified predicate.
    /// </summary>
    /// <param name="match">The non-null predicate evaluated over live values in slot order; it must not mutate
    /// this instance.</param>
    /// <returns>true if a live value matches the predicate; false if no match exists.</returns>
    /// <exception cref="T:System.ArgumentNullException">match is null.</exception>
    public bool Exists(Predicate<T> match)
    {
        ChronicleGuard.ThrowIfNull(match, nameof(match));

        uint count = 0;
        uint peak = (uint)_peak;

        for (uint i = 0; i < peak; i++)
        {
            if (count >= (uint)_count)
                break;

            if (_entries[i].IsUsed)
            {
                if (match(_entries[i].Value))
                    return true;

                count++;
            }
        }

        return false;
    }

    /// <summary>
    /// Searches for an element that matches the conditions defined by the specified predicate, and returns the
    /// first matching element in map iteration order.
    /// </summary>
    /// <param name="match">The non-null predicate evaluated over live values in slot order; it must not mutate
    /// this instance.</param>
    /// <returns>The first matching live value in slot order, or default(T) when absent; stored default values are
    /// indistinguishable from absence.</returns>
    /// <exception cref="T:System.ArgumentNullException">match is null.</exception>
    public T Find(Predicate<T> match)
    {
        ChronicleGuard.ThrowIfNull(match, nameof(match));

        uint count = 0;
        uint peak = (uint)_peak;

        for (uint i = 0; i < peak && count < (uint)_count; i++)
        {
            if (_entries[i].IsUsed)
            {
                T item = _entries[i].Value;
                if (match(item))
                    return item;

                count++;
            }
        }

        return default!;
    }

    #endregion

    #region Enumeration

    /// <summary>
    /// Creates an enumerator over the live entries.
    /// </summary>
    /// <returns>A value-type enumerator over live entries in increasing allocated-slot order.</returns>
    /// <remarks>
    /// MoveNext and Reset check the mutation version. Direct storage/ref writes bypass the version check.
    /// </remarks>
    public SwiftTicketMapEnumerator GetEnumerator() => new(this);
    /// <summary>
    /// Creates an enumerator through the implemented interface.
    /// </summary>
    /// <returns>An enumerator with the concrete collection's ordering and mutation rules. Interface dispatch may
    /// box the value-type enumerator.</returns>
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
    /// <summary>
    /// Creates an enumerator through the implemented interface.
    /// </summary>
    /// <returns>An enumerator with the concrete collection's ordering and mutation rules. Interface dispatch may
    /// box the value-type enumerator.</returns>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();



    #endregion
}
