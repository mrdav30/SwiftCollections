//=======================================================================
// SwiftList.cs
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
/// Stores elements in a resizable, indexable contiguous list.
/// </summary>
/// <typeparam name="T">Specifies the type of elements in the list.</typeparam>
/// <remarks>
/// Copies are shallow. Instances require external synchronization. Structural operations advance an unchecked
/// mutation version; enumerators check that version in MoveNext and Reset. Direct backing-array writes and
/// indexer assignment bypass version checks. Enumeration follows list index order.
/// </remarks>
[Serializable]
[JsonConverter(typeof(StateJsonConverterFactory))]
[MemoryPackable]
public partial class SwiftList<T> : IStateBacked<SwiftArrayState<T>>, ISwiftCloneable<T>, IEnumerable<T>, IEnumerable, ICollection<T>, ICollection, IList<T>, IList
{
    #region Constants

    /// <summary>
    /// The default initial capacity of the <see cref="T:SwiftCollections.SwiftList`1" /> if none is specified.
    /// Used to allocate a reasonable starting size to minimize resizing operations.
    /// </summary>
    public const int DefaultCapacity = 8;

    private static readonly T[] _emptyArray = Array.Empty<T>();
    private static readonly bool _clearReleasedSlots = RuntimeHelpers.IsReferenceOrContainsReferences<T>();

    #endregion

    #region Fields

    /// <summary>
    /// Stores live elements followed by spare capacity.
    /// </summary>
    /// <remarks>
    /// Derived implementations must preserve Count, backing bounds, and mutation version invariants when changing
    /// this storage.
    /// </remarks>
    protected T[] _innerArray;

    /// <summary>
    /// Stores the number of live elements in the initial backing-array range.
    /// </summary>
    protected int _count;

    /// <summary>
    /// Stores the mutation version checked by enumerators.
    /// </summary>
    /// <remarks>
    /// Increment for structural mutations; indexer and direct storage writes do not increment it. Arithmetic
    /// wraps after UInt32.MaxValue.
    /// </remarks>
    [NonSerialized]
    protected uint _version;

    /// <summary>
    /// An object that can be used to synchronize access to the SwiftList.
    /// </summary>
    [NonSerialized]
    private object? _syncRoot;

    #endregion

    #region Constructors

    /// <summary>
    /// Creates an empty collection with no allocated element storage.
    /// </summary>
    public SwiftList() : this(0) { }

    /// <summary>
    /// Initializes a new, empty instance of <see cref="T:SwiftCollections.SwiftList`1" /> with the specified
    /// initial capacity.
    /// </summary>
    /// <param name="capacity">The requested element capacity; zero starts empty, and a negative constructor
    /// capacity uses the minimum allocation. Positive growth is rounded up; requests at or above 2^30 reach the
    /// Int32 capacity sentinel.</param>
    public SwiftList(int capacity)
    {
        if (capacity == 0)
            _innerArray = _emptyArray;
        else
        {
            capacity = SwiftHashTools.NextPowerOfTwo(capacity <= DefaultCapacity ? DefaultCapacity : capacity);
            _innerArray = new T[capacity];
        }
    }

    /// <summary>
    /// Creates a collection by shallow-copying an enumerable.
    /// </summary>
    /// <param name="items">The elements to copy in source order; source storage is not retained. Null is
    /// rejected.</param>
    /// <exception cref="T:System.ArgumentNullException">items is null.</exception>
    /// <remarks>
    /// Known-count sources must report a stable count and remain unchanged while copied. Arbitrary enumerables
    /// are supported. The source sequence is stored in enumeration order.
    /// </remarks>
    public SwiftList(IEnumerable<T> items)
    {
        ChronicleGuard.ThrowIfNull(items, nameof(items));

        if (items is ICollection<T> collection)
        {
            int count = collection.Count;
            if (count == 0)
                _innerArray = _emptyArray;
            else
            {
                _innerArray = new T[count];
                collection.CopyTo(_innerArray, 0);
                _count = count;
            }
        }
        else
        {
            _innerArray = new T[DefaultCapacity];
            AddRange(items); // Will handle capacity increases as needed
        }
    }

    /// <summary>
    /// Restores a collection by shallow-copying its item state.
    /// </summary>
    /// <param name="state">The item state to shallow-copy into new storage; Items must be non-null.</param>
    /// <exception cref="T:System.ArgumentNullException">state.Items is null, including a default
    /// state.</exception>
    /// <remarks>
    /// Capacity and mutation version are reconstructed, not preserved. Items use list index order.
    /// </remarks>
    [MemoryPackConstructor]
    public SwiftList(SwiftArrayState<T> state)
    {
        State = state;

        // Validate that the internal array is not null after deserialization
        ChronicleGuard.ThrowIfNull(_innerArray, nameof(_innerArray));
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the underlying array that stores the elements of the collection.
    /// </summary>
    /// <value>The live mutable backing storage, including unused slots.</value>
    /// <remarks>
    /// Only indices zero through Count minus one are live. Structural mutations can move elements or replace this
    /// array. Old arrays remain detached after replacement. Writes bypass versioning.
    /// </remarks>
    [JsonIgnore]
    [MemoryPackIgnore]
    public T[] InnerArray => _innerArray;

    /// <summary>
    /// Gets the total number of elements the SwiftList can hold without resizing. Reflects the current allocated
    /// size of the internal array.
    /// </summary>
    /// <value>The allocated backing-array length; it may exceed Count.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Capacity => _innerArray.Length;

    /// <summary>
    /// Gets the number of live elements.
    /// </summary>
    /// <value>The live count, excluding spare storage.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Count => _count;

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
    /// <value>A lazily created lock object; callers must coordinate all access using the same lock.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public object SyncRoot => _syncRoot ??= new object();

    /// <summary>
    /// Gets whether the collection has fixed size.
    /// </summary>
    /// <value>false.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public bool IsFixedSize => false;

    /// <summary>
    /// Gets or sets the interface indexed value.
    /// </summary>
    /// <param name="index">The zero-based logical or destination index, interpreted by this interface
    /// operation.</param>
    /// <value>The indexed element. Uses the concrete public indexer after converting supplied values to its
    /// generic types.</value>
    /// <exception cref="T:System.ArgumentOutOfRangeException">The getter index is outside the live
    /// range.</exception>
    /// <exception cref="T:System.NotSupportedException">The setter cannot convert the supplied value, or the
    /// underlying setter fails (including an invalid index).</exception>
    [JsonIgnore]
    [MemoryPackIgnore]
    object? IList.this[int index]
    {
        get => this[index] ?? default!;
        set
        {
            try
            {
                this[index] = (T)value!;
            }
            catch
            {
                throw new NotSupportedException($"Unsupported value type for {value}");
            }
        }
    }

    /// <summary>
    /// Gets or sets the element at a logical index.
    /// </summary>
    /// <param name="index">The zero-based logical index, from zero through Count minus one. </param>
    /// <value>The element at index in list index order.</value>
    /// <exception cref="T:System.ArgumentOutOfRangeException">index is outside the live range.</exception>
    [JsonIgnore]
    [MemoryPackIgnore]
    public T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ChronicleGuard.ThrowIfListIndexInvalid(index, _count);
            return _innerArray[index];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            ChronicleGuard.ThrowIfListIndexInvalid(index, _count);
            _innerArray[index] = value;
        }
    }

    /// <summary>
    /// Gets a shallow snapshot of the live elements.
    /// </summary>
    /// <value>A new state containing a newly allocated array in list index order.</value>
    /// <remarks>
    /// Element references remain shared. Capacity and version are not serialized. The setter is internal and
    /// restores from copied Items.
    /// </remarks>
    [JsonInclude]
    [MemoryPackInclude]
    public SwiftArrayState<T> State
    {
        get
        {
            var items = new T[_count];
            Array.Copy(_innerArray, 0, items, 0, _count);
            return new SwiftArrayState<T>(items);
        }
        internal set
        {
            ChronicleGuard.ThrowIfNull(value.Items, nameof(value.Items));

            int count = value.Items.Length;

            if (count == 0)
            {
                _innerArray = _emptyArray;
                _count = 0;
            }
            else
            {
                _innerArray = new T[count <= DefaultCapacity ? DefaultCapacity : count];
                Array.Copy(value.Items, 0, _innerArray, 0, count);
                _count = count;
            }

            _version = 0;
        }
    }

    #endregion

    #region Collection Manipulation

    /// <summary>
    /// Adds an object to the end of the SwiftList.
    /// </summary>
    /// <param name="item">The element to store or locate; reference-type elements are not cloned.</param>
    /// <remarks>
    /// Stores the supplied element without cloning; grows backing storage when needed.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual void Add(T item)
    {
        if ((uint)_count == (uint)_innerArray.Length)
            Resize(_innerArray.Length * 2);
        _innerArray[_count++] = item;
        _version++;
    }

    /// <summary>
    /// Adds an element through the implemented collection interface.
    /// </summary>
    /// <param name="value">The supplied value; must convert to the concrete generic type.</param>
    /// <returns>The index of the added element.</returns>
    int IList.Add(object? value)
    {
        try
        {
            Add((T)value!);
        }
        catch (InvalidCastException)
        {
            throw new NotSupportedException($"Wrong value type for {value}");
        }

        return _count - 1;
    }

    /// <summary>
    /// Adds the elements of the specified collection to the end of the SwiftList.
    /// </summary>
    /// <param name="items">The elements to copy in source order; source storage is not retained. Null is
    /// rejected.</param>
    /// <exception cref="T:System.ArgumentNullException">items is null.</exception>
    /// <remarks>
    /// Copies and appends the supplied elements; source storage is not retained. Empty input changes nothing.
    /// Enumerable sources must remain stable and report accurate counts. Exceptions from enumeration, copying, or
    /// comparison can leave partial writes; there is no rollback guarantee. Do not enumerate this collection as
    /// its own source while mutating it.
    /// </remarks>
    public virtual void AddRange(IEnumerable<T> items)
    {
        ChronicleGuard.ThrowIfNull(items, nameof(items));

        if (items is ICollection<T> collection)
        {
            int count = collection.Count;
            if (count > 0)
            {
                EnsureAdditionalCapacity(count);
                collection.CopyTo(_innerArray, _count);
                _count += count;
                _version++;
            }

            return;
        }

        if (items is IReadOnlyCollection<T> readOnlyCollection)
        {
            AddKnownCountRange(readOnlyCollection, readOnlyCollection.Count);
            return;
        }

        foreach (T item in items)
            Add(item);
    }

    private void AddKnownCountRange(IEnumerable<T> items, int count)
    {
        if (count > 0)
        {
            EnsureAdditionalCapacity(count);

            foreach (T item in items)
                _innerArray[_count++] = item;

            _version++;
        }
    }

    /// <summary>
    /// Adds the elements of the specified array to the end of the SwiftList.
    /// </summary>
    /// <param name="items">The elements to copy in source order; source storage is not retained. Null is
    /// rejected.</param>
    /// <exception cref="T:System.ArgumentNullException">items is null.</exception>
    /// <remarks>
    /// Copies and appends the supplied elements; source storage is not retained. Empty input changes nothing.
    /// Enumerable sources must remain stable and report accurate counts. Exceptions from enumeration, copying, or
    /// comparison can leave partial writes; there is no rollback guarantee. Do not enumerate this collection as
    /// its own source while mutating it.
    /// </remarks>
    public virtual void AddRange(T[] items)
    {
        ChronicleGuard.ThrowIfNull(items, nameof(items));
        AddRange(items.AsSpan());
    }

    /// <summary>
    /// Adds the elements of the specified span to the end of the SwiftList.
    /// </summary>
    /// <param name="items">The elements to copy in source order; source storage is not retained. An empty span
    /// changes nothing.</param>
    /// <remarks>
    /// Copies and appends the supplied elements; source storage is not retained. Empty input changes nothing.
    /// Enumerable sources must remain stable and report accurate counts. Exceptions from enumeration, copying, or
    /// comparison can leave partial writes; there is no rollback guarantee. Do not enumerate this collection as
    /// its own source while mutating it.
    /// </remarks>
    public virtual void AddRange(ReadOnlySpan<T> items)
    {
        if (items.Length == 0)
            return;

        EnsureAdditionalCapacity(items.Length);

        items.CopyTo(_innerArray.AsSpan(_count, items.Length));
        _count += items.Length;
        _version++;
    }

    private void EnsureAdditionalCapacity(int additionalCount)
    {
        long requiredCount = (long)_count + additionalCount;
        ChronicleGuard.ThrowIfTrue(requiredCount > int.MaxValue, message: "The collection is too large.");
        EnsureCapacity((int)requiredCount);
    }

    /// <summary>
    /// Removes the first occurrence of a specific object from the SwiftList.
    /// </summary>
    /// <param name="item">The element to store or locate; reference-type elements are not cloned.</param>
    /// <returns>true if one matching element was removed; false if absent, with contents unchanged. The first
    /// match in index order is removed and later elements shift left.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual bool Remove(T item)
    {
        int index = IndexOf(item);
        if (index < 0) return false; // Item not found return false;
        RemoveAt(index);
        return true;
    }

    /// <summary>
    /// Removes a matching element through the implemented interface.
    /// </summary>
    /// <param name="value">The supplied value; must convert to the concrete generic type.</param>
    void IList.Remove(object? value)
    {
        try
        {
            Remove((T)value!);
        }
        catch (InvalidCastException)
        {
            throw new NotSupportedException($"Wrong value type for {value}");
        }
    }

    /// <summary>
    /// Removes the element at the specified arrayIndex of the SwiftList.
    /// </summary>
    /// <param name="index">The zero-based logical index, from zero through Count minus one. </param>
    /// <exception cref="T:System.ArgumentOutOfRangeException">index is outside the permitted logical
    /// range.</exception>
    public virtual void RemoveAt(int index)
    {
        ChronicleGuard.ThrowIfListIndexInvalid(index, _count);
        Array.Copy(_innerArray, index + 1, _innerArray, index, _count - index - 1);
        _count--;
        if (_clearReleasedSlots)
            _innerArray[_count] = default!;
        _version++;
    }

    /// <summary>
    /// Removes all the elements that match the conditions defined by the specified predicate.
    /// </summary>
    /// <param name="match">The non-null predicate evaluated in list index order. It must not mutate this
    /// collection.</param>
    /// <returns>The number of removed elements; zero leaves contents unchanged.</returns>
    /// <exception cref="T:System.ArgumentNullException">match is null.</exception>
    /// <remarks>
    /// Preserves relative order of retained elements. A throwing predicate can leave partial compaction without
    /// updating Count; this operation is not transactional.
    /// </remarks>
    public virtual int RemoveAll(Predicate<T> match)
    {
        ChronicleGuard.ThrowIfNull(match, nameof(match));

        int firstMatchIndex = IndexOfFirstMatch(match);
        if (firstMatchIndex >= _count)
            return 0;

        int newCount = CompactUnmatchedItems(firstMatchIndex, match);
        int removedCount = _count - newCount;
        ClearReleasedSlots(newCount, removedCount);
        _count = newCount;
        _version++;

        return removedCount;
    }

    private int IndexOfFirstMatch(Predicate<T> match)
    {
        int index = 0;
        while (index < _count && !match(_innerArray[index]))
            index++;

        return index;
    }

    private int CompactUnmatchedItems(int writeIndex, Predicate<T> match)
    {
        for (int readIndex = writeIndex + 1; readIndex < _count; readIndex++)
        {
            if (!match(_innerArray[readIndex]))
                _innerArray[writeIndex++] = _innerArray[readIndex];
        }

        return writeIndex;
    }

    private void ClearReleasedSlots(int startIndex, int count)
    {
        if (_clearReleasedSlots)
            Array.Clear(_innerArray, startIndex, count);
    }

    /// <summary>
    /// Inserts an element into the SwiftList at the specified arrayIndex.
    /// </summary>
    /// <param name="index">The zero-based insertion index, from zero through Count inclusive.</param>
    /// <param name="item">The element to store or locate; reference-type elements are not cloned.</param>
    /// <exception cref="T:System.ArgumentOutOfRangeException">index is outside the permitted logical
    /// range.</exception>
    public virtual void Insert(int index, T item)
    {
        ChronicleGuard.ThrowIfArrayIndexInvalid(index, _count);
        if ((uint)_count == (uint)_innerArray.Length)
            Resize(_innerArray.Length * 2);
        if ((uint)index < (uint)_count)
            Array.Copy(_innerArray, index, _innerArray, index + 1, _count - index);
        _innerArray[index] = item;
        _count++;
        _version++;
    }

    /// <summary>
    /// Inserts an element after converting it to the generic element type.
    /// </summary>
    /// <param name="index">The zero-based logical or destination index, interpreted by this interface
    /// operation.</param>
    /// <param name="value">The supplied value; must convert to the concrete generic type.</param>
    void IList.Insert(int index, object? value)
    {
        try
        {
            Insert(index, (T)value!);
        }
        catch (InvalidCastException)
        {
            throw new NotSupportedException($"Wrong value type for {value}");
        }
    }

    /// <summary>
    /// Reverses the order of the elements in the entire <see cref="T:SwiftCollections.SwiftList`1" />.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reverse()
    {
        Array.Reverse(_innerArray, 0, _count);
        _version++;
    }

    /// <summary>
    /// Sorts the populated elements of the <see cref="T:SwiftCollections.SwiftList`1" /> in place.
    /// </summary>
    /// <param name="comparer">The ordering comparer used only during this call; null uses
    /// Comparer&lt;T&gt;.Default.</param>
    /// <remarks>
    /// Reorders live storage in place; equal items have no stable tie order. A throwing comparer can leave
    /// partially reordered contents. Direct storage views observe the changes.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SortInPlace(IComparer<T>? comparer = null)
    {
        SwiftArraySortHelper.Sort(_innerArray, 0, _count, comparer);
        _version++;
    }

    /// <summary>
    /// Sorts the populated elements of the <see cref="T:SwiftCollections.SwiftList`1" /> in place using a struct
    /// comparer.
    /// </summary>
    /// <typeparam name="TComparer">The value-type ordering comparer.</typeparam>
    /// <param name="comparer">The value-type ordering comparer, passed by value and not retained after
    /// sorting.</param>
    /// <remarks>
    /// Reorders live storage in place; equal items have no stable tie order. A throwing comparer can leave
    /// partially reordered contents. Direct storage views observe the changes.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SortInPlace<TComparer>(TComparer comparer)
        where TComparer : struct, IComparer<T>
    {
        SwiftArraySortHelper.Sort(_innerArray, 0, _count, comparer);
        _version++;
    }

    /// <summary>
    /// Removes all elements from the <see cref="T:SwiftCollections.SwiftList`1" />, resetting its count to zero.
    /// </summary>
    /// <remarks>
    /// Empties the collection while preserving capacity. Ordinary clearing releases live references; references
    /// previously hidden by FastClear can remain in spare storage.
    /// </remarks>
    public virtual void Clear()
    {
        if (_count != 0 && _clearReleasedSlots)
            Array.Clear(_innerArray, 0, _count);
        _count = 0;
        _version++;
    }

    /// <summary>
    /// Removes all elements, optionally releasing references throughout retained backing storage.
    /// </summary>
    /// <param name="clearRetainedReferences">If true, also releases references outside the live range; otherwise
    /// uses ordinary clearing.</param>
    /// <remarks>
    /// Empties the collection while preserving capacity. The Boolean overload also clears references outside the
    /// live range when requested.
    /// </remarks>
    public void Clear(bool clearRetainedReferences)
    {
        if (clearRetainedReferences && _clearReleasedSlots)
            Array.Clear(_innerArray, _count, _innerArray.Length - _count);
        Clear();
    }

    /// <summary>
    /// Clears the <see cref="T:SwiftCollections.SwiftList`1" /> without releasing the reference to the stored
    /// elements. Use FastClear() when you want to quickly reset the list without reallocating memory.
    /// </summary>
    /// <remarks>
    /// Resets the logical count without clearing stored values. References can remain retained until overwritten,
    /// reallocated, or Clear(true). Capacity is preserved.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FastClear()
    {
        _count = 0;
        _version++;
    }

    #endregion

    #region Capacity Management

    /// <summary>
    /// Ensures that the capacity of <see cref="T:SwiftCollections.SwiftList`1" /> is sufficient to accommodate
    /// the specified number of elements. The capacity can increase by double to balance memory allocation
    /// efficiency and space.
    /// </summary>
    /// <param name="capacity">The minimum element capacity. Values at or below zero normalize to one; requests at
    /// or above 2^30 reach the Int32 capacity sentinel. Existing capacity is never reduced.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureCapacity(int capacity)
    {
        capacity = SwiftHashTools.NextPowerOfTwo(capacity);
        if (capacity > _innerArray.Length)
            Resize(capacity);
    }

    /// <summary>
    /// Resizes the internal array to accommodate the specified number of elements.
    /// </summary>
    /// <param name="newSize">The requested backing length, clamped to DefaultCapacity. It must accommodate Count
    /// or copying throws.</param>
    /// <remarks>
    /// If the specified size is less than or equal to the default capacity, the internal array is set to the
    /// default capacity. Existing elements are preserved up to the current count.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void Resize(int newSize)
    {
        int newCapacity = newSize <= DefaultCapacity ? DefaultCapacity : newSize;

        T[] newArray = new T[newCapacity];
        if (_count > 0)
            Array.Copy(_innerArray, 0, newArray, 0, _count);
        _innerArray = newArray;
        _version++;
    }

    /// <summary>
    /// Reallocates backing storage to the count-rounded capacity with a minimum of eight.
    /// </summary>
    /// <remarks>
    /// Unconditionally calls Array.Resize to max(DefaultCapacity, NextPowerOfTwo(Count)); it can grow an
    /// initially empty zero-capacity list. Live elements are preserved. Replacement invalidates borrowed
    /// array/span bindings and advances the version.
    /// </remarks>
    public void TrimExcessCapacity()
    {
        int newCapacity = _count < DefaultCapacity ? DefaultCapacity : SwiftHashTools.NextPowerOfTwo(_count);
        Array.Resize(ref _innerArray, newCapacity);
        _version++;
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Searches for the specified object and returns the zero-based arrayIndex of the first occurrence within the
    /// SwiftList.
    /// </summary>
    /// <param name="item">The element to store or locate; reference-type elements are not cloned.</param>
    /// <returns>The first matching logical index, or -1 if absent.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndexOf(T item) => Array.IndexOf(_innerArray, item, 0, _count);

    /// <summary>
    /// Finds the logical index of an equal element.
    /// </summary>
    /// <param name="value">The supplied value; must convert to the concrete generic type.</param>
    /// <returns>The first matching index, or -1 if absent.</returns>
    int IList.IndexOf(object? value)
    {
        int index;
        try
        {
            index = Array.IndexOf(_innerArray, (T)value!, 0, _count);
        }
        catch
        {
            throw new NotSupportedException($"Unsupported value type for {value}");
        }
        return index;
    }

    /// <summary>
    /// Copies the elements of the SwiftList to a new array.
    /// </summary>
    /// <returns>A newly allocated shallow copy of Count elements in list index order.</returns>
    public T[] ToArray()
    {
        T[] result = new T[_count];
        Array.Copy(_innerArray, result, _count);
        return result;
    }

    /// <summary>
    /// Returns a mutable span over the populated portion of the list.
    /// </summary>
    /// <returns>A writable borrowed view over the live elements in list index order.</returns>
    /// <remarks>
    /// No allocation or copy. Do not retain across structural mutations, capacity changes, or clearing; elements
    /// can move, and replaced storage becomes detached. Writes bypass enumerator version checks.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> AsSpan() => _innerArray.AsSpan(0, _count);

    /// <summary>
    /// Returns a read-only span over the populated portion of the list.
    /// </summary>
    /// <returns>A read-only borrowed view over the live elements in list index order.</returns>
    /// <remarks>
    /// No allocation or copy. Do not retain across structural mutations, capacity changes, or clearing; elements
    /// can move, and replaced storage becomes detached. Read-only restricts this view, not mutations through the
    /// owner.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<T> AsReadOnlySpan() => _innerArray.AsSpan(0, _count);

    /// <summary>
    /// Returns a string that represents the current collection.
    /// </summary>
    /// <returns>A comma-separated list of the collection's elements, or the default string representation if the
    /// collection is empty.</returns>
    /// <remarks>
    /// This method provides a human-readable representation of the collection's contents, which can be useful for
    /// debugging or logging purposes.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => _count == 0
        ? base.ToString()!
        : string.Join(", ", this);

    /// <summary>
    /// Determines whether an element is in the SwiftList.
    /// </summary>
    /// <param name="item">The element to store or locate; reference-type elements are not cloned.</param>
    /// <returns>true if an equal live element exists; otherwise false. Does not change contents.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(T item) => IndexOf(item) != -1;

    /// <summary>
    /// Determines whether the <see cref="T:SwiftCollections.SwiftList`1" /> contains an element that matches the
    /// conditions defined by the specified predicate.
    /// </summary>
    /// <param name="match">The non-null predicate evaluated in list index order. It must not mutate this
    /// collection.</param>
    /// <returns>true on the first matching predicate result; false if no live element matches.</returns>
    /// <exception cref="T:System.ArgumentNullException">match is null.</exception>
    public bool Exists(Predicate<T> match)
    {
        ChronicleGuard.ThrowIfNull(match, nameof(match));

        for (int i = 0; i < _count; i++)
        {
            if (match(_innerArray[i]))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Searches for an element that matches the conditions defined by the specified predicate, and returns the
    /// first occurrence within the entire <see cref="T:SwiftCollections.SwiftList`1" />.
    /// </summary>
    /// <param name="match">The non-null predicate evaluated in list index order. It must not mutate this
    /// collection.</param>
    /// <returns>The first matching element in list index order, or default(T) when absent. A stored default value
    /// is indistinguishable from failure.</returns>
    /// <exception cref="T:System.ArgumentNullException">match is null.</exception>
    public T Find(Predicate<T> match)
    {
        ChronicleGuard.ThrowIfNull(match, nameof(match));

        for (int i = 0; i < _count; i++)
        {
            if (match(_innerArray[i]))
                return _innerArray[i];
        }

        return default!;
    }

    /// <summary>
    /// Checks membership through the implemented interface.
    /// </summary>
    /// <param name="value">The supplied value; must convert to the concrete generic type.</param>
    /// <returns>true if an equal live entry exists; false otherwise. Uses the concrete collection's equality
    /// rules.</returns>
    bool IList.Contains(object? value)
    {
        int index;
        try
        {
            index = IndexOf((T)value!);
        }
        catch
        {
            throw new NotSupportedException($"Unsupported value type for {value}");
        }
        return index != -1;
    }

    /// <summary>
    /// Swaps the values of two elements in the SwiftList. This method exchanges the values referenced by two
    /// variables.
    /// </summary>
    /// <param name="indexA">The zero-based physical backing-array index to swap; this method does not validate
    /// against Count.</param>
    /// <param name="indexB">The zero-based physical backing-array index to swap; this method does not validate
    /// against Count.</param>
    /// <exception cref="T:System.IndexOutOfRangeException">Either physical index is outside the backing
    /// array.</exception>
    /// <remarks>
    /// Swaps physical slots even outside the live range if within Capacity. Does not increment the mutation
    /// version.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Swap(int indexA, int indexB) => (_innerArray[indexB], _innerArray[indexA]) = (_innerArray[indexA], _innerArray[indexB]);

    /// <summary>
    /// Copies the elements of the SwiftList to the specified target SwiftList. The target list will resize if it
    /// lacks sufficient capacity, but retains any existing elements beyond the copied range.
    /// </summary>
    /// <param name="target">The destination list whose contents and count are replaced; null is not
    /// accepted.</param>
    /// <remarks>
    /// Shallow-copies in list index order; source contents are preserved when the destination does not alias
    /// source storage. Target contents and Count are replaced. Growth doubles the target only once; callers must
    /// provision target.Capacity &gt;= source.Count to avoid an undersized-copy failure.
    /// </remarks>
    public void CopyTo(SwiftList<T> target)
    {
        if (_count + 1 > target._innerArray.Length)
            target.Resize(target._innerArray.Length * 2);
        Array.Copy(_innerArray, 0, target._innerArray, 0, _count);
        target._count = _count;
        target._version++;
    }

    /// <summary>
    /// Copies live elements into caller-owned storage.
    /// </summary>
    /// <param name="array">The caller-owned destination array; existing elements outside the copied range are
    /// preserved.</param>
    /// <param name="arrayIndex">The zero-based destination offset, from zero through array.Length; the remaining
    /// space must hold Count elements.</param>
    /// <exception cref="T:System.ArgumentNullException">array is null.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">arrayIndex is outside zero through
    /// array.Length.</exception>
    /// <exception cref="T:System.ArgumentException">The destination is too small or incompatible with the copied
    /// elements.</exception>
    /// <remarks>
    /// Shallow-copies in list index order; source contents are preserved when the destination does not alias
    /// source storage. The caller retains ownership of the destination; unused destination cells are preserved.
    /// Validation failures occur before copying; incompatible array elements can cause a partial copy.
    /// </remarks>
    public void CopyTo(T[] array, int arrayIndex)
    {
        ChronicleGuard.ThrowIfNull(array, nameof(array));
        ChronicleGuard.ThrowIfArrayIndexInvalid(arrayIndex, array.Length);
        ChronicleGuard.ThrowIfArgument(array.Length - arrayIndex < _count, nameof(array), "Destination array is not long enough.");

        Array.Copy(_innerArray, 0, array, arrayIndex, _count);
    }

    /// <summary>
    /// Copies the populated elements of the SwiftList into the specified destination span.
    /// </summary>
    /// <param name="destination">The caller-owned destination span with space for at least Count elements. Only
    /// the first Count elements are overwritten.</param>
    /// <exception cref="T:System.ArgumentException">destination is shorter than Count.</exception>
    /// <remarks>
    /// Shallow-copies in list index order; source contents are preserved when the destination does not alias
    /// source storage. The caller retains ownership of the destination; unused destination cells are preserved.
    /// Validation failures occur before copying; incompatible array elements can cause a partial copy.
    /// </remarks>
    public void CopyTo(Span<T> destination)
    {
        ChronicleGuard.ThrowIfArgument(destination.Length < _count, nameof(destination), "Destination span is not long enough.");

        AsSpan().CopyTo(destination);
    }

    /// <summary>
    /// Copies live elements into caller-owned storage.
    /// </summary>
    /// <param name="array">The caller-owned destination array; existing elements outside the copied range are
    /// preserved.</param>
    /// <param name="arrayIndex">The zero-based destination offset, from zero through array.Length; the remaining
    /// space must hold Count elements.</param>
    /// <exception cref="T:System.ArgumentNullException">array is null.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">arrayIndex is outside zero through
    /// array.Length.</exception>
    /// <exception cref="T:System.ArgumentException">The destination is too small or incompatible with the copied
    /// elements.</exception>
    /// <remarks>
    /// Shallow-copies in list index order; source contents are preserved when the destination does not alias
    /// source storage. The caller retains ownership of the destination; unused destination cells are preserved.
    /// Validation failures occur before copying; incompatible array elements can cause a partial copy.
    /// </remarks>
    public void CopyTo(Array array, int arrayIndex)
    {
        ChronicleGuard.ThrowIfNull(array, nameof(array));
        ChronicleGuard.ThrowIfArgument(array.Rank != 1, nameof(array), "Array must be single dimensional.");
        ChronicleGuard.ThrowIfArgument(array.GetLowerBound(0) != 0, nameof(array), "Array must have zero-based indexing.");
        ChronicleGuard.ThrowIfArrayIndexInvalid(arrayIndex, array.Length);
        ChronicleGuard.ThrowIfArgument(array.Length - arrayIndex < _count, nameof(array), "Destination array is not long enough.");

        Array.Copy(_innerArray, 0, array, arrayIndex, _count);
    }

    /// <inheritdoc />
    public void CloneTo(ICollection<T> output)
    {
        output.Clear();
        foreach (var item in this)
            output.Add(item);
    }

    #endregion

    #region Enumerators

    /// <summary>
    /// Returns an enumerator that iterates through the <see cref="T:SwiftCollections.SwiftList`1" />.
    /// </summary>
    /// <returns>A value-type enumerator in list index order.</returns>
    /// <remarks>
    /// Structural mutations invalidate MoveNext and Reset. Direct storage writes and indexer assignments bypass
    /// the version check.
    /// </remarks>
    public SwiftListEnumerator GetEnumerator() => new(this);
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
