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
/// Represents a high-performance ticket map that assigns stable tickets to stored items.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SwiftTicketMap{T}"/> is similar to <see cref="SwiftSlotMap{T}"/> but adds
/// <b>generation tracking</b> to prevent stale references from accessing reused slots.
/// </para>
///
/// <para>
/// When an item is added, a <see cref="SwiftTicket"/> containing both an index and generation
/// is returned. If the item is removed and the slot reused later, the generation value
/// changes, causing older tickets to automatically become invalid.
/// </para>
///
/// <para>
/// This pattern is widely used to safely reference objects without risking accidental access 
/// to recycled memory slots.
/// </para>
///
/// <para>
/// Key characteristics:
/// <list type="bullet">
/// <item><description>O(1) insertion and removal.</description></item>
/// <item><description>Stable tickets for the lifetime of stored items.</description></item>
/// <item><description>Automatic invalidation of stale tickets via generation counters.</description></item>
/// <item><description>Cache-friendly contiguous storage.</description></item>
/// </list>
/// </para>
///
/// <para>
/// Use <see cref="SwiftSlotMap{T}"/> when raw indices are acceptable.
/// Use <see cref="SwiftTicketMap{T}"/> when ticket safety is required.
/// </para>
/// </remarks>
/// <typeparam name="T">Specifies the type of elements stored in the map.</typeparam>
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
    /// Use this constant when initializing the collection to its default size. 
    /// The value is typically used to optimize memory allocation for small collections.
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
    /// <remarks>
    /// The actual capacity will be set to the next power of two greater than or equal to the specified capacity, 
    /// or to the default capacity if the specified value is too small. 
    /// This ensures efficient internal storage and lookup performance.
    /// </remarks>
    /// <param name="capacity">
    /// The initial number of elements that the map can contain. 
    /// If less than or equal to the default capacity, the default capacity is used. 
    /// Must be a non-negative integer.
    /// </param>
    public SwiftTicketMap(int capacity)
    {
        capacity = capacity <= DefaultCapacity
            ? DefaultCapacity
            : SwiftHashTools.NextPowerOfTwo(capacity);

        _entries = new Entry[capacity];
        _freeIndices = new SwiftIntStack(capacity);
    }

    ///  <summary>
    ///  Initializes a new instance of the <see cref="SwiftTicketMap{T}"/> class with the specified <see cref="SwiftTicketMapState{T}"/>.
    ///  </summary>
    ///  <param name="state">The state containing the internal array, count, offset, and version for initialization.</param>
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
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Count => _count;

    /// <summary>
    /// Gets the total number of elements that the internal data structure can hold without resizing.
    /// </summary>
    /// <remarks>
    /// This value represents the allocated size of the underlying storage, which may be greater than the actual number of elements contained. 
    /// Capacity is always greater than or equal to the current count of elements.
    /// </remarks>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Capacity => _entries.Length;

    /// <summary>
    /// Gets or sets the current state of the ticket map.
    /// </summary>
    /// <remarks>
    /// This property provides a snapshot of the map's internal state, which can be used for serialization, diagnostics, 
    /// or restoring the map to a previous state. 
    /// Setting this property replaces the entire state of the map, including its contents and allocation metadata.
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
    /// <remarks>
    /// The returned ticket can be used to access or remove the value later. 
    /// Tickets are only valid as long as the value remains in the collection.
    /// </remarks>
    /// <param name="value">The value to add to the collection.</param>
    /// <returns>A <see cref="SwiftTicket"/> that uniquely identifies the added value within the collection.</returns>
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
    /// <remarks>
    /// Use this method to safely attempt retrieval without throwing an exception if the ticket is invalid or the entry is not in use.
    /// </remarks>
    /// <param name="ticket">The ticket used to identify the entry to retrieve.</param>
    /// <param name="value">
    /// When this method returns, contains the value associated with the specified ticket if the ticket is valid 
    /// and the entry is in use; otherwise, the default value for the type of the value parameter. 
    /// This parameter is passed uninitialized.
    /// </param>
    /// <returns>true if the value was found and retrieved successfully; otherwise, false.</returns>
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
    /// <param name="ticket">
    /// A ticket that identifies the entry whose value is to be accessed. 
    /// The ticket must be valid and refer to an existing entry.
    /// </param>
    /// <returns>A reference to the value of type T associated with the specified ticket.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the ticket does not refer to a valid or currently used entry.</exception>
    public ref T GetRef(SwiftTicket ticket)
    {
        ref Entry entry = ref _entries[ticket.Index];

        ChronicleGuard.ThrowIfTrue(!entry.IsUsed || entry.Generation != ticket.Generation, message: "Invalid ticket");

        return ref entry.Value;
    }

    /// <summary>
    /// Removes the entry associated with the specified ticket from the collection.
    /// </summary>
    /// <remarks>
    /// If the ticket does not refer to a valid or currently used entry, the method returns false and no action is taken. 
    /// Removing an entry invalidates the ticket for future operations.
    /// </remarks>
    /// <param name="ticket">The ticket identifying the entry to remove. The ticket must refer to a valid, currently used entry.</param>
    /// <returns>true if the entry was successfully removed; otherwise, false.</returns>
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
    /// <remarks>
    /// A ticket may become invalid if the referenced entry has been removed or replaced. 
    /// Use this method to check ticket validity before accessing the associated entry.
    /// </remarks>
    /// <param name="ticket">The ticket to validate. The ticket must have been obtained from this collection; otherwise, the result is undefined.</param>
    /// <returns>true if the ticket is valid and refers to an active entry; otherwise, false.</returns>
    public bool IsValid(SwiftTicket ticket)
    {
        if ((uint)ticket.Index >= (uint)_entries.Length)
            return false;

        ref Entry entry = ref _entries[ticket.Index];

        return entry.IsUsed && entry.Generation == ticket.Generation;
    }

    /// <summary>Removes all elements and invalidates their tickets without reducing capacity.</summary>
    public void Clear() => Clear(clearRetainedReferences: false);

    /// <summary>Removes all elements, optionally releasing references throughout retained backing storage.</summary>
    /// <param name="clearRetainedReferences">If true, also releases references outside the previously used range.</param>
    /// <remarks>Stored generations survive clearing so old tickets remain invalid on reuse. Capacity is preserved.</remarks>
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
    /// <remarks>
    /// If the current capacity is less than the specified value, the storage is resized to accommodate at least that many elements. 
    /// The actual capacity may be rounded up to the next power of two for performance reasons.
    /// </remarks>
    /// <param name="capacity">The minimum number of elements that the storage should be able to hold. Must be a non-negative integer.</param>
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

    /// <inheritdoc/>
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
    /// Determines whether the <see cref="SwiftTicketMap{T}"/> contains an element that matches the conditions defined by the specified predicate.
    /// </summary>
    /// <param name="match">The predicate that defines the conditions of the element to search for.</param>
    /// <returns><c>true</c> if the <see cref="SwiftTicketMap{T}"/> contains one or more elements that match the specified predicate; otherwise, <c>false</c>.</returns>
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
    /// Searches for an element that matches the conditions defined by the specified predicate, and returns the first matching element in map iteration order.
    /// </summary>
    /// <param name="match">The predicate that defines the conditions of the element to search for.</param>
    /// <returns>The first element that matches the conditions defined by the specified predicate, if found; otherwise, the default value for type <typeparamref name="T"/>.</returns>
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

    /// <inheritdoc cref="IEnumerable.GetEnumerator()"/>
    public SwiftTicketMapEnumerator GetEnumerator() => new(this);
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();



    #endregion
}
