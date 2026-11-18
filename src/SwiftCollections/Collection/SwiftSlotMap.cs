//=======================================================================
// SwiftSlotMap.cs
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
/// Stores values in stable, reusable integer slots owned by the container.
/// </summary>
/// <typeparam name="T">Specifies the type of elements in the map.</typeparam>
/// <remarks>
/// Copies are shallow and instances require external synchronization. Enumeration follows increasing
/// allocated-slot order. Slots remain stable until removal. Removed indices are reused in last-freed-first order;
/// integer slots do not protect against stale references. Use SwiftTicketMap when generation checks are required.
/// </remarks>
[Serializable]
[JsonConverter(typeof(StateJsonConverterFactory))]
[MemoryPackable]
public sealed partial class SwiftSlotMap<T> : IStateBacked<SwiftSlotMapState<T>>, ISwiftCloneable<T>, IEnumerable<T>, ICollection<T>, ICollection
{
    #region Constants

    /// <summary>
    /// Represents the default initial capacity used when no specific capacity is provided.
    /// </summary>
    public const int DefaultCapacity = 8;

    #endregion

    #region Fields

    private Entry[] _innerArray;

    private int _count;

    private int _peakCount;

    private SwiftIntStack _freeIndices;

    [NonSerialized]
    private uint _version;

    [NonSerialized]
    private object? _syncRoot;

    #endregion

    #region Nested Types

    [Serializable]
    private struct Entry
    {
        public T Value;
        public bool IsUsed;
    }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.SwiftSlotMap`1" /> class.
    /// </summary>
    public SwiftSlotMap() : this(DefaultCapacity) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.SwiftSlotMap`1" /> class with the
    /// specified capacity.
    /// </summary>
    /// <param name="capacity">The requested element capacity; values at or below DefaultCapacity use the minimum
    /// allocation. Larger values round up.</param>
    public SwiftSlotMap(int capacity)
    {
        capacity = capacity <= DefaultCapacity ? DefaultCapacity : SwiftHashTools.NextPowerOfTwo(capacity);
        _innerArray = new Entry[capacity];
        _freeIndices = new SwiftIntStack(capacity);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.SwiftSlotMap`1" /> class with the
    /// specified <see cref="T:SwiftCollections.SwiftArrayState`1" />.
    /// </summary>
    /// <param name="state">The state to restore by shallow-copying arrays. Null arrays are treated as empty; free
    /// indices must be in range, unique and disjoint from allocated slots. Uniqueness/disjointness are not
    /// validated.</param>
    /// <exception cref="T:System.InvalidOperationException">A state free index is outside reconstructed
    /// capacity.</exception>
    /// <remarks>
    /// Input arrays are copied, not retained. Default state is accepted as empty. Missing item values default;
    /// missing allocation flags are false. Peak is normalized into capacity. Free-index duplicates or overlap
    /// with live entries are not rejected and can corrupt later allocation.
    /// </remarks>
    [MemoryPackConstructor]
    public SwiftSlotMap(SwiftSlotMapState<T> state)
    {
        State = state;

        ChronicleGuard.ThrowIfNull(_innerArray, nameof(state.Items));
        ChronicleGuard.ThrowIfNull(_freeIndices, nameof(state.FreeIndices));
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the number of elements contained in the <see cref="T:SwiftCollections.SwiftSlotMap`1" />.
    /// </summary>
    /// <value>The number of live entries, excluding gaps and spare storage.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Count => _count;

    /// <summary>
    /// Gets the slot high-water boundary.
    /// </summary>
    /// <value>One more than the highest reserved slot, not the historical maximum live Count. May include freed
    /// gaps; trim can reduce it.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int PeakCount => _peakCount;

    /// <summary>
    /// Gets the total capacity of the <see cref="T:SwiftCollections.SwiftSlotMap`1" />.
    /// </summary>
    /// <value>The allocated slot-array length.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Capacity => _innerArray.Length;

    /// <summary>
    /// Gets or sets the element at the specified arrayIndex. Throws <see
    /// cref="T:System.InvalidOperationException" /> if the arrayIndex is invalid or unallocated.
    /// </summary>
    /// <param name="index">The zero-based slot index; must already be allocated.</param>
    /// <value>The stored value. Setting replaces the allocated slot value and advances the version.</value>
    /// <exception cref="T:System.InvalidOperationException">The slot is out of range or unallocated.</exception>
    [JsonIgnore]
    [MemoryPackIgnore]
    public T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ChronicleGuard.ThrowIfTrue(!IsAllocated(index), nameof(index), message: "Index is out of range or unallocated.");
            return _innerArray[index].Value;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            ChronicleGuard.ThrowIfTrue(!IsAllocated(index), nameof(index), message: "Index is out of range or unallocated.");
            _innerArray[index].Value = value;
            _version++;
        }
    }

    /// <summary>
    /// Gets whether the collection rejects all mutations.
    /// </summary>
    /// <value>false.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public bool IsReadOnly => false;

    /// <summary>
    /// Gets whether collection access is automatically synchronized.
    /// </summary>
    /// <value>false.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public bool IsSynchronized => false;

    /// <summary>
    /// Gets the external synchronization object.
    /// </summary>
    /// <value>A lazily allocated synchronization object; access is not locked automatically.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public object SyncRoot => _syncRoot ??= new object();

    /// <summary>
    /// Gets a shallow snapshot of storage and allocation metadata.
    /// </summary>
    /// <value>A state with copied arrays; referenced elements remain shared.</value>
    /// <remarks>
    /// Stores slot allocation, free-index stack and high-water metadata. Restore normalizes the high-water
    /// boundary into capacity and tolerates missing arrays/default values. It validates free-index range but
    /// trusts uniqueness and disjointness; use only consistent states.
    /// </remarks>
    [JsonInclude]
    [MemoryPackInclude]
    public SwiftSlotMapState<T> State
    {
        get
        {
            int length = _innerArray.Length;

            var items = new T[length];
            var allocated = new bool[length];

            for (int i = 0; i < length; i++)
            {
                if (_innerArray[i].IsUsed)
                {
                    items[i] = _innerArray[i].Value;
                    allocated[i] = true;
                }
            }

            int[] free = new int[_freeIndices.Count];
            Array.Copy(_freeIndices.Array, free, _freeIndices.Count);

            return new SwiftSlotMapState<T>(
                items,
                allocated,
                free,
                _peakCount
            );
        }
        internal set
        {
            var items = value.Items ?? Array.Empty<T>();
            var allocated = value.Allocated ?? Array.Empty<bool>();
            var freeIndices = value.FreeIndices ?? Array.Empty<int>();

            int sourceLength = Math.Max(items.Length, allocated.Length);
            int capacity = sourceLength < DefaultCapacity ? DefaultCapacity : SwiftHashTools.NextPowerOfTwo(sourceLength);

            _innerArray = new Entry[capacity];
            _freeIndices = new SwiftIntStack(capacity);

            _count = 0;
            int maxReferencedIndex = RestoreAllocatedEntries(items, allocated, sourceLength);
            maxReferencedIndex = RestoreFreeIndices(freeIndices, capacity, maxReferencedIndex);
            _peakCount = NormalizePeakCount(value.PeakCount, maxReferencedIndex, capacity);
            _version = 0;
        }
    }

    private int RestoreAllocatedEntries(T[] items, bool[] allocated, int sourceLength)
    {
        int maxReferencedIndex = -1;
        for (int i = 0; i < sourceLength; i++)
        {
            if (allocated.Length <= i || !allocated[i])
                continue;

            if (items.Length > i)
                _innerArray[i].Value = items[i];

            _innerArray[i].IsUsed = true;
            _count++;
            maxReferencedIndex = i;
        }

        return maxReferencedIndex;
    }

    private int RestoreFreeIndices(int[] freeIndices, int capacity, int maxReferencedIndex)
    {
        foreach (var index in freeIndices)
        {
            ChronicleGuard.ThrowIfTrue((uint)index >= (uint)capacity, message: "Free index is out of range.");

            _freeIndices.Push(index);
            if (index > maxReferencedIndex)
                maxReferencedIndex = index;
        }

        return maxReferencedIndex;
    }

    private static int NormalizePeakCount(int peakCount, int maxReferencedIndex, int capacity)
    {
        if (peakCount < 0)
            peakCount = 0;

        int normalizedPeak = Math.Max(peakCount, maxReferencedIndex + 1);
        return normalizedPeak > capacity ? capacity : normalizedPeak;
    }

    #endregion

    #region Collection Management

    /// <summary>
    /// Adds an item to the map and returns its arrayIndex.
    /// </summary>
    /// <param name="item">The value to store or locate; reference values are not cloned.</param>
    /// <returns>The allocated slot index, reusing the most recently freed slot or extending the high-water
    /// boundary.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Add(T item)
    {
        int index;
        if ((uint)_freeIndices.Count == 0)
        {
            index = _peakCount++;
            if ((uint)index >= (uint)_innerArray.Length)
                Resize(_innerArray.Length * 2);
        }
        else index = _freeIndices.Pop();

        _innerArray[index].Value = item;
        _innerArray[index].IsUsed = true;
        _count++;
        _version++;
        return index;
    }

    /// <summary>
    /// Adds an element through the implemented collection interface.
    /// </summary>
    /// <param name="item">The element to compare, add or remove; reference values are not cloned.</param>
    void ICollection<T>.Add(T item) => Add(item);

    /// <summary>
    /// Inserts an item at the specified arrayIndex. If an item already exists at that arrayIndex, it will be
    /// replaced.
    /// </summary>
    /// <param name="item">The value to store or locate; reference values are not cloned.</param>
    /// <param name="index">The non-negative slot to overwrite or allocate; absent slots are allocated and storage
    /// grows when necessary. Keep index + 1 representable and within available array memory.</param>
    /// <exception cref="T:System.ArgumentOutOfRangeException">index is negative.</exception>
    /// <remarks>
    /// Overwrites an allocated slot or allocates an unallocated slot, growing storage if needed. Does not remove
    /// that index from the free stack. Re-inserting a previously freed slot can make a later Add overwrite it;
    /// avoid that sequence until the tracked defect is fixed.
    /// </remarks>
    public void InsertAt(int index, T item)
    {
        ChronicleGuard.ThrowIfNegative(index, nameof(index));
        if ((uint)index >= (uint)_innerArray.Length)
            Resize(SwiftHashTools.NextPowerOfTwo(index + 1));
        if (!_innerArray[index].IsUsed)
        {
            _count++;
            if ((uint)index >= (uint)_peakCount)
                _peakCount = index + 1;
        }
        _innerArray[index].Value = item;
        _innerArray[index].IsUsed = true;
        _version++;
    }

    /// <summary>
    /// Removes the first occurrence of a specific object from the map.
    /// </summary>
    /// <param name="item">The value to store or locate; reference values are not cloned.</param>
    /// <returns>true if a matching live entry was removed; false if absent or invalid, with contents unchanged.
    /// Freed slots are reused later.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRemove(T item)
    {
        int index = IndexOf(item);
        if (index < 0) return false;
        RemoveAt(index);
        return true;
    }

    /// <summary>
    /// Removes a matching element through the implemented interface.
    /// </summary>
    /// <param name="item">The element to compare, add or remove; reference values are not cloned.</param>
    /// <returns>true if an entry was removed; false if absent.</returns>
    bool ICollection<T>.Remove(T item) => TryRemove(item);

    /// <summary>
    /// Removes the item at the specified arrayIndex if it has been allocated.
    /// </summary>
    /// <param name="index">The zero-based slot index; negative or absent slots fail checked lookups without
    /// mutation.</param>
    /// <returns>true if a matching live entry was removed; false if absent or invalid, with contents unchanged.
    /// Freed slots are reused later.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRemoveAt(int index)
    {
        if (IsAllocated(index))
        {
            RemoveAt(index);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Removes the item at the specified arrayIndex.
    /// </summary>
    /// <param name="index">The zero-based slot index; must already be allocated.</param>
    /// <exception cref="T:System.IndexOutOfRangeException">index is outside backing storage.</exception>
    /// <remarks>
    /// Unchecked fast path: directly clears the backing slot, decrements Count and pushes the index to the free
    /// stack. Passing an unallocated slot corrupts Count/free metadata; use TryRemoveAt for unknown allocation
    /// state.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RemoveAt(int index)
    {
        _innerArray[index] = default;
        _count--;
        _freeIndices.Push(index);
        _version++;
    }

    /// <summary>
    /// Removes all items from the map.
    /// </summary>
    /// <remarks>
    /// Empties the collection while preserving capacity. The Boolean overload also releases unused retained value
    /// references when applicable.
    /// </remarks>
    public void Clear()
    {
        if ((uint)_count == 0) return;
        for (int i = 0; i < _peakCount; i++)
            _innerArray[i] = default;
        _freeIndices.Reset();
        _count = 0;
        _peakCount = 0;
        _version++;
    }

    /// <summary>
    /// Removes all elements, optionally releasing references throughout retained backing storage.
    /// </summary>
    /// <param name="clearRetainedReferences">If true, also releases references outside the live range; otherwise
    /// uses ordinary clearing.</param>
    /// <remarks>
    /// Empties the collection while preserving capacity. The Boolean overload also releases unused retained value
    /// references when applicable.
    /// </remarks>
    public void Clear(bool clearRetainedReferences)
    {
        if (clearRetainedReferences && RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            int start = _count == 0 ? 0 : _peakCount;
            Array.Clear(_innerArray, start, _innerArray.Length - start);
        }
        Clear();
    }

    #endregion

    #region Capacity Management

    /// <summary>
    /// Ensures that the internal storage has at least the specified capacity, expanding it if necessary.
    /// </summary>
    /// <param name="capacity">The minimum storage length; requests at or below the current capacity change
    /// nothing. Keep requests below 2^30 to avoid the capacity sentinel.</param>
    /// <remarks>
    /// If the current capacity is less than the specified value, the internal storage is increased to the next
    /// power of two greater than or equal to the requested capacity. No action is taken if the current capacity
    /// is sufficient.
    /// </remarks>
    public void EnsureCapacity(int capacity)
    {
        capacity = SwiftHashTools.NextPowerOfTwo(capacity);
        if (capacity > _innerArray.Length)
            Resize(capacity);
        else
            _freeIndices.EnsureCapacity(capacity);
    }

    private void Resize(int newSize)
    {
        int newCapacity = newSize;
        int copyLength = Math.Min(_peakCount, _innerArray.Length);

        Entry[] newArray = new Entry[newCapacity];
        if (copyLength > 0)
            Array.Copy(_innerArray, 0, newArray, 0, copyLength);
        _innerArray = newArray;
        _freeIndices.EnsureCapacity(newCapacity);

        _version++;
    }

    /// <summary>
    /// Reduces unused tail capacity while preserving stable indices for all currently allocated entries.
    /// </summary>
    public void TrimExcessCapacity()
    {
        int newPeak = GetLivePeakCount();
        int newCapacity = newPeak <= DefaultCapacity ? DefaultCapacity : SwiftHashTools.NextPowerOfTwo(newPeak);

        Entry[] newArray = new Entry[newCapacity];
        if (newPeak > 0)
            Array.Copy(_innerArray, 0, newArray, 0, newPeak);

        _innerArray = newArray;
        _peakCount = newPeak;
        RebuildFreeIndices();

        _version++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetLivePeakCount()
    {
        for (int i = _peakCount - 1; i >= 0; i--)
            if (_innerArray[i].IsUsed)
                return i + 1;

        return 0;
    }

    private void RebuildFreeIndices()
    {
        _freeIndices = new SwiftIntStack(_innerArray.Length);

        for (int i = 0; i < _peakCount; i++)
        {
            if (!_innerArray[i].IsUsed)
                _freeIndices.Push(i);
        }
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Attempts to get the value at the specified arrayIndex.
    /// </summary>
    /// <param name="key">The zero-based slot index; negative or absent slots fail checked lookups without
    /// mutation.</param>
    /// <param name="value">Receives the stored value on success; default(T) on failure.</param>
    /// <returns>true if the slot/token/ID identifies a live entry; false otherwise, with the out value set to
    /// default(T). No mutation.</returns>
    public bool TryGetValue(int key, out T value)
    {
        if (!IsAllocated(key))
        {
            value = default!;
            return false;
        }

        value = _innerArray[key].Value;
        return true;
    }

    /// <summary>
    /// Determines whether the map contains a specific value.
    /// </summary>
    /// <param name="item">The value to store or locate; reference values are not cloned.</param>
    /// <returns>true if the requested live slot, token, ID or value exists; false for absent/out-of-range inputs.
    /// No mutation.</returns>
    /// <remarks>
    /// This method performs a linear search and has a time complexity of O(n). It is recommended to store the
    /// indices returned by the <see cref="M:SwiftCollections.SwiftSlotMap`1.Add(`0)" /> method for faster lookups
    /// using the indexer.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(T item) => IndexOf(item) != -1;

    /// <summary>
    /// Determines whether the <see cref="T:SwiftCollections.SwiftSlotMap`1" /> contains an element that matches
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

        for (int i = 0; i < _peakCount && count < (uint)_count; i++)
        {
            if (_innerArray[i].IsUsed)
            {
                if (match(_innerArray[i].Value))
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

        for (int i = 0; i < _peakCount && count < (uint)_count; i++)
        {
            if (_innerArray[i].IsUsed)
            {
                T item = _innerArray[i].Value;
                if (match(item))
                    return item;

                count++;
            }
        }

        return default!;
    }

    /// <summary>
    /// Determines whether the element at the specified index is currently allocated.
    /// </summary>
    /// <param name="index">The zero-based slot index; negative or absent slots fail checked lookups without
    /// mutation.</param>
    /// <returns>true if the requested live slot, token, ID or value exists; false for absent/out-of-range inputs.
    /// No mutation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsAllocated(int index) => !((uint)index >= (uint)_innerArray.Length) && _innerArray[index].IsUsed;

    /// <summary>
    /// Searches for the specified object and returns the zero-based arrayIndex of the first occurrence within the
    /// map.
    /// </summary>
    /// <param name="item">The value to store or locate; reference values are not cloned.</param>
    /// <returns>The first allocated slot containing an equal value, or -1 if absent. Gaps are skipped.</returns>
    /// <remarks>
    /// This method performs a linear search and has a time complexity of O(n). It is recommended to store the
    /// indices returned by the <see cref="M:SwiftCollections.SwiftSlotMap`1.Add(`0)" /> method for faster lookups
    /// using the indexer.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndexOf(T item)
    {
        uint count = 0;

        if (item == null)
        {
            for (int i = 0; i < (uint)_peakCount && count < (uint)_count; i++)
            {
                if (_innerArray[i].IsUsed)
                {
                    if (_innerArray[i].Value == null)
                        return i;
                    count++;
                }
            }

            return -1;
        }

        for (int j = 0; j < (uint)_peakCount && count < (uint)_count; j++)
        {
            if (_innerArray[j].IsUsed)
            {
                if (EqualityComparer<T>.Default.Equals(_innerArray[j].Value, item))
                    return j;
                count++;
            }
        }
        return -1;
    }

    /// <summary>
    /// Copies the elements of the map to an <see cref="T:System.Array" />, starting at a particular Array
    /// arrayIndex.
    /// </summary>
    /// <param name="array">The caller-owned destination array; only the copied range is overwritten.</param>
    /// <param name="arrayIndex">The zero-based destination offset with room for Count live values.</param>
    /// <exception cref="T:System.ArgumentNullException">array is null.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">arrayIndex is outside zero through
    /// array.Length.</exception>
    /// <exception cref="T:System.ArgumentException">The remaining destination space is shorter than
    /// Count.</exception>
    /// <remarks>
    /// Shallow-copies Count live values in increasing allocated-slot order without clearing the destination.
    /// </remarks>
    public void CopyTo(T[] array, int arrayIndex)
    {
        ChronicleGuard.ThrowIfNull(array, nameof(array));
        ChronicleGuard.ThrowIfArrayIndexInvalid(arrayIndex, array.Length);
        ChronicleGuard.ThrowIfArgument(array.Length - arrayIndex < _count, nameof(array), "The array is not large enough to hold the elements.");

        uint count = 0;
        for (uint i = 0; i < (uint)_peakCount && count < (uint)_count; i++)
        {
            if (_innerArray[i].IsUsed)
            {
                array[arrayIndex++] = _innerArray[i].Value;
                count++;
            }
        }
    }

    /// <summary>
    /// Copies live elements into a caller-owned array.
    /// </summary>
    /// <param name="array">The caller-owned, one-dimensional, zero-based destination array.</param>
    /// <param name="arrayIndex">The zero-based destination offset with space for the live count.</param>
    /// <remarks>
    /// Uses concrete collection copy order and validation. The destination is overwritten only in the copied
    /// range; incompatible elements can cause partial copying.
    /// </remarks>
    void ICollection.CopyTo(Array array, int arrayIndex)
    {
        ChronicleGuard.ThrowIfNull(array, nameof(array));
        ChronicleGuard.ThrowIfArgument((uint)array.Rank != 1, nameof(array), "Array must be single dimensional.");
        ChronicleGuard.ThrowIfArgument((uint)array.GetLowerBound(0) != 0, nameof(array), "Array must have zero-based indexing.");
        ChronicleGuard.ThrowIfArrayIndexInvalid(arrayIndex, array.Length);
        ChronicleGuard.ThrowIfArgument(array.Length - arrayIndex < _count, nameof(array), "The array is not large enough to hold the elements.");

        try
        {
            uint count = 0;
            for (uint i = 0; i < (uint)_peakCount && count < (uint)_count; i++)
            {
                if (_innerArray[i].IsUsed)
                {
                    array.SetValue(_innerArray[i].Value, arrayIndex++);
                    count++;
                }
            }
        }
        catch (InvalidCastException)
        {
            throw new ArgumentException("Invalid array type.");
        }
    }

    /// <inheritdoc />
    public void CloneTo(ICollection<T> output)
    {
        output.Clear();
        uint count = 0;
        for (uint i = 0; i < (uint)_peakCount && count < (uint)_count; i++)
        {
            if (_innerArray[i].IsUsed)
            {
                output.Add(_innerArray[i].Value);
                count++;
            }
        }
    }

    #endregion

    #region Enumerator

    /// <summary>
    /// Returns an enumerator that iterates through the <see cref="T:SwiftCollections.SwiftSlotMap`1" />.
    /// </summary>
    /// <returns>A value-type enumerator over live entries in increasing allocated-slot order.</returns>
    /// <remarks>
    /// MoveNext and Reset check the mutation version. Direct storage/ref writes bypass the version check.
    /// </remarks>
    public SwiftSlotMapEnumerator GetEnumerator() => new(this);
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
