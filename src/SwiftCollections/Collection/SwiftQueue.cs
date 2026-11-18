//=======================================================================
// SwiftQueue.cs
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
/// Stores elements in a resizable FIFO ring buffer.
/// </summary>
/// <typeparam name="T">Specifies the type of elements in the queue.</typeparam>
/// <remarks>
/// Copies are shallow. Instances require external synchronization. Structural operations advance an unchecked
/// mutation version; enumerators check that version in MoveNext and Reset. Direct backing-array writes and
/// indexer assignment bypass version checks. Enumeration follows FIFO order.
/// </remarks>
[Serializable]
[JsonConverter(typeof(StateJsonConverterFactory))]
[MemoryPackable]
public sealed partial class SwiftQueue<T> : IStateBacked<SwiftArrayState<T>>, ISwiftCloneable<T>, IEnumerable<T>, IEnumerable, ICollection<T>, ICollection
{
    #region Constants

    /// <summary>
    /// The default initial capacity of the SwiftQueue if none is specified. Used to allocate a reasonable
    /// starting size to minimize resizing operations.
    /// </summary>
    public const int DefaultCapacity = 8;

    private static readonly T[] _emptyArray = Array.Empty<T>();
    private static readonly bool _clearReleasedSlots = RuntimeHelpers.IsReferenceOrContainsReferences<T>();

    #endregion

    #region Fields

    /// <summary>
    /// The internal array that stores elements of the SwiftQueue. Resized as needed to
    /// accommodate additional elements. Not directly exposed outside the queue.
    /// </summary>
    private T[] _innerArray;

    /// <summary>
    /// The current number of elements in the SwiftQueue. Represents the total count of
    /// valid elements stored in the queue, also indicating the arrayIndex of the next insertion point.
    /// </summary>
    private int _count;

    /// <summary>
    /// The arrayIndex of the first element in the queue. Adjusts as elements are dequeued.
    /// </summary>
    private int _head;

    /// <summary>
    /// The arrayIndex at which the next element will be enqueued, wrapping around as needed.
    /// </summary>
    private int _tail;

    /// <summary>
    /// A bitmask used for efficient modulo operations, derived from the capacity of the internal array.
    /// </summary>
    private int _mask;

    /// <summary>
    /// A version number used to track modifications to the SwiftQueue to help detect changes during enumeration and ensures thread safety.
    /// </summary>
    [NonSerialized]
    private uint _version;

    /// <summary>
    /// An object used to synchronize access to the SwiftQueue, ensuring thread safety.
    /// </summary>
    [NonSerialized]
    private object? _syncRoot;

    #endregion

    #region Constructors

    /// <summary>
    /// Creates an empty collection with no allocated element storage.
    /// </summary>
    public SwiftQueue() : this(0) { }

    /// <summary>
    /// Initializes a new, empty instance of SwiftQueue with the specified initial capacity.
    /// </summary>
    /// <param name="capacity">The requested element capacity; zero starts empty, and a negative constructor
    /// capacity uses the minimum allocation. Positive growth is rounded up; requests at or above 2^30 reach the
    /// Int32 capacity sentinel.</param>
    public SwiftQueue(int capacity)
    {
        if (capacity == 0)
        {
            _innerArray = _emptyArray;
            _mask = 0;
        }
        else
        {
            capacity = capacity < DefaultCapacity ? DefaultCapacity : SwiftHashTools.NextPowerOfTwo(capacity);
            _innerArray = new T[capacity];
            _mask = _innerArray.Length - 1;
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
    public SwiftQueue(IEnumerable<T> items)
    {
        ChronicleGuard.ThrowIfNull(items, nameof(items));
        _innerArray = _emptyArray;

        if (items is ICollection<T> collection)
        {
            InitializeFromKnownCountRange(collection, collection.Count);
            return;
        }

        if (items is IReadOnlyCollection<T> readOnlyCollection)
        {
            InitializeFromKnownCountRange(readOnlyCollection, readOnlyCollection.Count);
            return;
        }

        _innerArray = new T[DefaultCapacity];
        _mask = _innerArray.Length - 1;

        foreach (T item in items)
            Enqueue(item);
    }

    private void InitializeFromKnownCountRange(IEnumerable<T> items, int count)
    {
        if (count == 0)
        {
            _innerArray = _emptyArray;
            _mask = 0;
        }
        else
        {
            int capacity = count < DefaultCapacity ? DefaultCapacity : SwiftHashTools.NextPowerOfTwo(count);
            _innerArray = new T[capacity];
            _mask = _innerArray.Length - 1;

            foreach (T item in items)
                _innerArray[_count++] = item;

            _tail = _count & _mask;
        }
    }

    /// <summary>
    /// Restores a collection by shallow-copying its item state.
    /// </summary>
    /// <param name="state">The item state to shallow-copy into new storage; Items must be non-null.</param>
    /// <exception cref="T:System.ArgumentNullException">state.Items is null, including a default
    /// state.</exception>
    /// <remarks>
    /// Capacity and mutation version are reconstructed, not preserved. Items use FIFO order.
    /// </remarks>
    [MemoryPackConstructor]
    public SwiftQueue(SwiftArrayState<T> state)
    {
        State = state;

        ChronicleGuard.ThrowIfNull(_innerArray, nameof(_innerArray));
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the live backing array, including unused capacity.
    /// </summary>
    /// <value>The live mutable backing storage, including unused slots.</value>
    /// <remarks>
    /// Only the ring segment(s) described by GetSegments are live. Structural mutations can move elements or
    /// replace this array. Old arrays remain detached after replacement. Writes bypass versioning.
    /// </remarks>
    [JsonIgnore]
    [MemoryPackIgnore]
    public T[] InnerArray => _innerArray;

    /// <summary>
    /// Gets the number of live elements.
    /// </summary>
    /// <value>The live count, excluding spare storage.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Count => _count;

    /// <summary>
    /// Gets the total number of elements the SwiftQueue can hold without resizing. Reflects the current allocated
    /// size of the internal array.
    /// </summary>
    /// <value>The allocated backing-array length; it may exceed Count.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Capacity => _innerArray.Length;

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
    /// Gets whether the collection rejects all mutations.
    /// </summary>
    /// <value>false.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public bool IsReadOnly => false;

    /// <summary>
    /// Gets or sets the element at a logical index.
    /// </summary>
    /// <param name="index">The zero-based logical index, from zero through Count minus one. Zero addresses the
    /// queue head.</param>
    /// <value>The element at index in FIFO order.</value>
    /// <exception cref="T:System.ArgumentOutOfRangeException">index is outside the live range.</exception>
    [JsonIgnore]
    [MemoryPackIgnore]
    public T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ChronicleGuard.ThrowIfListIndexInvalid(index, _count);
            return _innerArray[(_head + index) & _mask];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            ChronicleGuard.ThrowIfListIndexInvalid(index, _count);
            _innerArray[(_head + index) & _mask] = value;
        }
    }

    /// <summary>
    /// Gets a shallow snapshot of the live elements.
    /// </summary>
    /// <value>A new state containing a newly allocated array in FIFO order.</value>
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

            for (int i = 0; i < _count; i++)
                items[i] = _innerArray[(_head + i) & _mask];

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
                _head = 0;
                _tail = 0;
                _mask = 0;
                _version = 0;
                return;
            }

            int capacity = SwiftHashTools.NextPowerOfTwo(count <= DefaultCapacity ? DefaultCapacity : count);

            _innerArray = new T[capacity];
            Array.Copy(value.Items, 0, _innerArray, 0, count);

            _count = count;
            _head = 0;
            _tail = count;
            _mask = capacity - 1;

            _version = 0;
        }
    }

    #endregion

    #region Collection Management

    /// <summary>
    /// Adds an element through the implemented collection interface.
    /// </summary>
    /// <param name="item">The element to compare, add or remove; reference values are not cloned.</param>
    void ICollection<T>.Add(T item) => Enqueue(item);

    /// <summary>
    /// Adds an item to the end of the queue. Automatically resizes the queue if the capacity is exceeded.
    /// </summary>
    /// <param name="item">The element to store or locate; reference-type elements are not cloned.</param>
    /// <remarks>
    /// Stores the supplied element without cloning; grows backing storage when needed.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Enqueue(T item)
    {
        if ((uint)_count >= (uint)_innerArray.Length)
            Resize(_innerArray.Length * 2);
        _innerArray[_tail] = item;
        _tail = (_tail + 1) & _mask;
        _count++;
        _version++;
    }

    /// <summary>
    /// Adds the elements of the specified collection to the end of the queue.
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnqueueRange(IEnumerable<T> items)
    {
        ChronicleGuard.ThrowIfNull(items, nameof(items));

        if (items is ICollection<T> collection)
        {
            EnqueueKnownCountRange(collection, collection.Count);
            return;
        }

        if (items is IReadOnlyCollection<T> readOnlyCollection)
        {
            EnqueueKnownCountRange(readOnlyCollection, readOnlyCollection.Count);
            return;
        }

        foreach (T item in items)
            Enqueue(item);
    }

    private void EnqueueKnownCountRange(IEnumerable<T> items, int count)
    {
        if (count > 0)
        {
            EnsureAdditionalCapacity(count);

            int appendedCount = 0;
            foreach (T item in items)
            {
                _innerArray[_tail] = item;
                _tail = (_tail + 1) & _mask;
                appendedCount++;
            }

            _count += appendedCount;
            _version++;
        }
    }

    /// <summary>
    /// Adds the elements of the specified array to the end of the queue in queue order.
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
    public void EnqueueRange(T[] items)
    {
        ChronicleGuard.ThrowIfNull(items, nameof(items));
        EnqueueRange(items.AsSpan());
    }

    /// <summary>
    /// Adds the elements of the specified span to the end of the queue in queue order.
    /// </summary>
    /// <param name="items">The elements to copy in source order; source storage is not retained. An empty span
    /// changes nothing.</param>
    /// <remarks>
    /// Copies and appends the supplied elements; source storage is not retained. Empty input changes nothing.
    /// Enumerable sources must remain stable and report accurate counts. Exceptions from enumeration, copying, or
    /// comparison can leave partial writes; there is no rollback guarantee. Do not enumerate this collection as
    /// its own source while mutating it.
    /// </remarks>
    public void EnqueueRange(ReadOnlySpan<T> items)
    {
        if (items.Length == 0)
            return;

        EnsureAdditionalCapacity(items.Length);

        for (int i = 0; i < items.Length; i++)
        {
            _innerArray[_tail] = items[i];
            _tail = (_tail + 1) & _mask;
        }

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
    /// Removes and returns the item at the front of the queue. Throws an InvalidOperationException if the queue
    /// is empty.
    /// </summary>
    /// <returns>The oldest queued element; removed from the collection.</returns>
    /// <exception cref="T:System.InvalidOperationException">The collection is empty; contents are
    /// unchanged.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Dequeue()
    {
        ChronicleGuard.ThrowIfTrue((uint)_count == 0, message: "Queue is Empty");
        T item = _innerArray[_head];
        if (_clearReleasedSlots)
            _innerArray[_head] = default!;
        _head = (_head + 1) & _mask;
        _count--;
        _version++;
        return item;
    }

    /// <summary>
    /// Tries to remove and return the item at the front of the queue.
    /// </summary>
    /// <param name="item">Receives the head element on success; default(T) when the queue is empty.</param>
    /// <returns>true if an element was removed; false if empty, with contents unchanged.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryDequeue(out T item)
    {
        if ((uint)_count == 0)
        {
            item = default!;
            return false;
        }

        item = _innerArray[_head];
        if (_clearReleasedSlots)
            _innerArray[_head] = default!;
        _head = (_head + 1) & _mask;
        _count--;
        _version++;
        return true;
    }

    /// <summary>
    /// Removes a matching element through the implemented interface.
    /// </summary>
    /// <param name="item">The element to compare, add or remove; reference values are not cloned.</param>
    /// <returns>This implementation always throws.</returns>
    /// <exception cref="T:System.NotSupportedException">Removal by value is unsupported; use Dequeue or
    /// Pop.</exception>
    bool ICollection<T>.Remove(T item) => throw new NotSupportedException("Remove is not supported for SwiftQueue.");

    /// <summary>
    /// Returns the item at the front of the queue without removing it. Throws an InvalidOperationException if the
    /// queue is empty.
    /// </summary>
    /// <returns>The oldest queued element; contents are unchanged.</returns>
    /// <exception cref="T:System.InvalidOperationException">The collection is empty; contents are
    /// unchanged.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Peek()
    {
        ChronicleGuard.ThrowIfTrue((uint)_count == 0, message: "Queue is Empty");
        return _innerArray[_head];
    }

    /// <summary>
    /// Tries to return the item at the front of the queue without removing it.
    /// </summary>
    /// <param name="item">Receives the head element on success; default(T) when the queue is empty.</param>
    /// <returns>true if an element was read without removal; false if empty, with contents unchanged.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPeek(out T item)
    {
        if ((uint)_count == 0)
        {
            item = default!;
            return false;
        }

        item = _innerArray[_head];
        return true;
    }

    /// <summary>
    /// Returns the item at the end of the queue without removing it. Throws an InvalidOperationException if the
    /// queue is empty.
    /// </summary>
    /// <returns>The most recently enqueued element; contents are unchanged.</returns>
    /// <exception cref="T:System.InvalidOperationException">The collection is empty; contents are
    /// unchanged.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T PeekTail()
    {
        ChronicleGuard.ThrowIfTrue((uint)_count == 0, message: "Queue is Empty");
        int tailIndex = (_tail - 1) & _mask;
        return _innerArray[tailIndex];
    }

    /// <summary>
    /// Checks whether a matching live entry exists.
    /// </summary>
    /// <param name="item">The element to store or locate; reference-type elements are not cloned.</param>
    /// <returns>true if an equal live element exists; otherwise false. Does not change contents.</returns>
    public bool Contains(T item)
    {
        if ((uint)_count == 0) return false;

        int index = _head;
        for (int i = 0; i < _count; i++)
        {
            if (Equals(_innerArray[index], item))
                return true;

            index = (index + 1) & _mask;
        }

        return false;
    }

    /// <summary>
    /// Determines whether the <see cref="T:SwiftCollections.SwiftQueue`1" /> contains an element that matches the
    /// conditions defined by the specified predicate.
    /// </summary>
    /// <param name="match">The non-null predicate evaluated in FIFO order. It must not mutate this
    /// collection.</param>
    /// <returns>true on the first matching predicate result; false if no live element matches.</returns>
    /// <exception cref="T:System.ArgumentNullException">match is null.</exception>
    public bool Exists(Predicate<T> match)
    {
        ChronicleGuard.ThrowIfNull(match, nameof(match));

        int index = _head;
        for (int i = 0; i < _count; i++)
        {
            if (match(_innerArray[index]))
                return true;

            index = (index + 1) & _mask;
        }

        return false;
    }

    /// <summary>
    /// Searches for an element that matches the conditions defined by the specified predicate, and returns the
    /// first matching element in queue order.
    /// </summary>
    /// <param name="match">The non-null predicate evaluated in FIFO order. It must not mutate this
    /// collection.</param>
    /// <returns>The first matching element in FIFO order, or default(T) when absent. A stored default value is
    /// indistinguishable from failure.</returns>
    /// <exception cref="T:System.ArgumentNullException">match is null.</exception>
    public T Find(Predicate<T> match)
    {
        ChronicleGuard.ThrowIfNull(match, nameof(match));

        int index = _head;
        for (int i = 0; i < _count; i++)
        {
            T item = _innerArray[index];
            if (match(item))
                return item;

            index = (index + 1) & _mask;
        }

        return default!;
    }

    /// <summary>
    /// Removes all elements from the SwiftQueue, resetting its count to zero.
    /// </summary>
    /// <remarks>
    /// Empties the collection while preserving capacity. Ordinary clearing releases live references; references
    /// previously hidden by FastClear can remain in spare storage.
    /// </remarks>
    public void Clear()
    {
        if (_count == 0) return;

        if (_clearReleasedSlots)
        {
            if ((uint)_head < (uint)_tail)
                Array.Clear(_innerArray, _head, _count);
            else
            {
                Array.Clear(_innerArray, _head, _innerArray.Length - _head);
                Array.Clear(_innerArray, 0, _tail);
            }
        }

        _count = 0;
        _head = 0;
        _tail = 0;
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
        {
            Array.Clear(_innerArray, 0, _innerArray.Length);
            FastClear();
            return;
        }
        Clear();
    }

    /// <summary>
    /// Clears the SwiftQueue without releasing the reference to the stored elements. Use FastClear() when you
    /// want to quickly reset the list without reallocating memory.
    /// </summary>
    /// <remarks>
    /// Resets the logical count without clearing stored values. References can remain retained until overwritten,
    /// reallocated, or Clear(true). Capacity is preserved.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FastClear()
    {
        _count = 0;
        _tail = 0;
        _head = 0;
        _version++;
    }

    #endregion

    #region Capacity Management

    /// <summary>
    /// Ensures that the internal storage has at least the specified capacity, resizing if necessary.
    /// </summary>
    /// <param name="capacity">The minimum element capacity. Values at or below zero normalize to one; requests at
    /// or above 2^30 reach the Int32 capacity sentinel. Existing capacity is never reduced.</param>
    /// <remarks>
    /// If the specified capacity is not a power of two, it is rounded up to the next power of two to optimize
    /// internal operations.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureCapacity(int capacity)
    {
        capacity = SwiftHashTools.NextPowerOfTwo(capacity);  // Capacity must be a power of 2 for proper masking
        if (capacity > _innerArray.Length)
            Resize(capacity);
    }

    /// <summary>
    /// Ensures that the capacity of the queue is sufficient to accommodate the specified number of elements.
    /// The capacity increases to the next power of two greater than or equal to the required minimum capacity.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Resize(int newSize)
    {
        var newArray = new T[newSize <= DefaultCapacity ? DefaultCapacity : newSize];
        if ((uint)_count > 0)
        {
            // If we are not wrapped around...
            if ((uint)_head < (uint)_tail)
            {
                // ...copy from head to tail into new array starting at arrayIndex 0
                Array.Copy(_innerArray, _head, newArray, 0, _count);
            }
            // Else if we are wrapped around...
            else
            {
                // ...copy from head to end of old array to beginning of new array
                Array.Copy(_innerArray, _head, newArray, 0, _innerArray.Length - _head);
                // ...copy from start of old array to tail into new array
                Array.Copy(_innerArray, 0, newArray, _innerArray.Length - _head, _tail);
            }
        }

        _innerArray = newArray;
        _mask = _innerArray.Length - 1;
        _head = 0;
        _tail = _count & _mask;
        _version++;
    }

    /// <summary>
    /// Reduces the capacity of the SwiftQueue if the element count is significantly less than the current
    /// capacity. This method resizes the internal array to the next power of two greater than or equal to the
    /// current count, optimizing memory usage.
    /// </summary>
    public void TrimExcessCapacity()
    {
        int newSize = _count < DefaultCapacity ? DefaultCapacity : SwiftHashTools.NextPowerOfTwo(_count);
        if (newSize >= _innerArray.Length) return;

        var newArray = new T[newSize];

        if ((uint)_count != 0)
        {
            if ((uint)_head < (uint)_tail)
            {
                // No wrap-around, simple copy
                Array.Copy(_innerArray, _head, newArray, 0, _count);
            }
            else
            {
                // Wrap-around, copy in two parts
                Array.Copy(_innerArray, _head, newArray, 0, _innerArray.Length - _head);
                Array.Copy(_innerArray, 0, newArray, _innerArray.Length - _head, _tail);
            }
        }

        _innerArray = newArray;
        _mask = _innerArray.Length - 1;
        _head = 0;
        _tail = _count & _mask;
        _version++;
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Copies the elements of the SwiftQueue to a new array.
    /// </summary>
    /// <returns>A newly allocated shallow copy of Count elements in FIFO order.</returns>
    public T[] ToArray()
    {
        var result = new T[_count];
        if ((uint)_count == 0) return result;
        if ((uint)_head < (uint)_tail)
            Array.Copy(_innerArray, _head, result, 0, _count);
        else
        {
            int firstPartLength = _innerArray.Length - _head;
            Array.Copy(_innerArray, _head, result, 0, firstPartLength);
            Array.Copy(_innerArray, 0, result, firstPartLength, _tail);
        }
        return result;
    }

    /// <summary>
    /// Returns the current queue contents as up to two read-only spans.
    /// </summary>
    /// <param name="first">Receives the first borrowed FIFO segment; empty when the queue is empty.</param>
    /// <param name="second">Receives the wrapped remainder, or an empty span when storage is contiguous.</param>
    /// <remarks>
    /// Concatenating first and second gives Count items in FIFO order. No allocation or copy. Any mutation can
    /// change the data; resize detaches the old storage. Do not retain the segments across mutations.
    /// </remarks>
    public void GetSegments(out ReadOnlySpan<T> first, out ReadOnlySpan<T> second)
    {
        if ((uint)_count == 0)
        {
            first = ReadOnlySpan<T>.Empty;
            second = ReadOnlySpan<T>.Empty;
            return;
        }

        if ((uint)_head < (uint)_tail)
        {
            first = _innerArray.AsSpan(_head, _count);
            second = ReadOnlySpan<T>.Empty;
            return;
        }

        int firstPartLength = _innerArray.Length - _head;
        first = _innerArray.AsSpan(_head, firstPartLength);
        second = _innerArray.AsSpan(0, _tail);
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
    /// Shallow-copies in FIFO order; source contents are preserved when the destination does not alias source
    /// storage. The caller retains ownership of the destination; unused destination cells are preserved.
    /// Validation failures occur before copying; incompatible array elements can cause a partial copy.
    /// </remarks>
    public void CopyTo(Array array, int arrayIndex)
    {
        ChronicleGuard.ThrowIfNull(array, nameof(array));
        ChronicleGuard.ThrowIfArgument((uint)array.Rank != 1, nameof(array), "Array must be single dimensional.");
        ChronicleGuard.ThrowIfArgument((uint)array.GetLowerBound(0) != 0, nameof(array), "Array must have zero-based indexing.");
        ChronicleGuard.ThrowIfArrayIndexInvalid(arrayIndex, array.Length);
        ChronicleGuard.ThrowIfArgument((uint)(array.Length - arrayIndex) < _count, nameof(array), "Destination array is not long enough.");

        if ((uint)_count == 0)
            return;

        try
        {
            CopyToInternal(array, arrayIndex);
        }
        catch (ArrayTypeMismatchException)
        {
            throw new ArgumentException("Invalid array type.", nameof(array));
        }
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
    /// Shallow-copies in FIFO order; source contents are preserved when the destination does not alias source
    /// storage. The caller retains ownership of the destination; unused destination cells are preserved.
    /// Validation failures occur before copying; incompatible array elements can cause a partial copy.
    /// </remarks>
    public void CopyTo(T[] array, int arrayIndex)
    {
        ChronicleGuard.ThrowIfNull(array, nameof(array));
        ChronicleGuard.ThrowIfArgument((uint)array.Rank != 1, nameof(array), "Array must be single dimensional.");
        ChronicleGuard.ThrowIfArgument((uint)array.GetLowerBound(0) != 0, nameof(array), "Array must have zero-based indexing.");
        ChronicleGuard.ThrowIfArrayIndexInvalid(arrayIndex, array.Length);
        ChronicleGuard.ThrowIfArgument((uint)(array.Length - arrayIndex) < _count, nameof(array), "Destination array is not long enough.");

        if ((uint)_count == 0) return;

        CopyToInternal(array, arrayIndex);
    }

    /// <summary>
    /// Copies the elements of the SwiftQueue into the specified destination span.
    /// </summary>
    /// <param name="destination">The caller-owned destination span with space for at least Count elements. Only
    /// the first Count elements are overwritten.</param>
    /// <exception cref="T:System.ArgumentException">destination is shorter than Count.</exception>
    /// <remarks>
    /// Shallow-copies in FIFO order; source contents are preserved when the destination does not alias source
    /// storage. The caller retains ownership of the destination; unused destination cells are preserved.
    /// Validation failures occur before copying; incompatible array elements can cause a partial copy.
    /// </remarks>
    public void CopyTo(Span<T> destination)
    {
        ChronicleGuard.ThrowIfArgument((uint)destination.Length < _count, nameof(destination), "Destination span is not long enough.");

        GetSegments(out ReadOnlySpan<T> first, out ReadOnlySpan<T> second);
        first.CopyTo(destination);

        if (second.Length > 0)
            second.CopyTo(destination[first.Length..]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CopyToInternal(Array destination, int arrayIndex)
    {
        if ((uint)_head < (uint)_tail)
        {
            Array.Copy(_innerArray, _head, destination, arrayIndex, _count);
        }
        else
        {
            int firstPartLength = _innerArray.Length - _head;
            Array.Copy(_innerArray, _head, destination, arrayIndex, firstPartLength);
            Array.Copy(_innerArray, 0, destination, arrayIndex + firstPartLength, _tail);
        }
    }

    /// <inheritdoc />
    public void CloneTo(ICollection<T> output)
    {
        ChronicleGuard.ThrowIfNull(output, nameof(output));
        output.Clear();
        foreach (var item in this)
            output.Add(item);
    }

    #endregion

    #region Enumerators

    /// <summary>
    /// Returns an enumerator that iterates through the SwiftList.
    /// </summary>
    /// <returns>A value-type enumerator in FIFO order.</returns>
    /// <remarks>
    /// Structural mutations invalidate MoveNext and Reset. Direct storage writes and indexer assignments bypass
    /// the version check.
    /// </remarks>
    public SwiftQueueEnumerator GetEnumerator() => new(this);
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
