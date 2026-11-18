//=======================================================================
// SwiftStack.cs
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
/// Stores elements in a resizable last-in, first-out stack.
/// </summary>
/// <typeparam name="T">Specifies the type of elements in the stack.</typeparam>
/// <remarks>
/// Copies are shallow. Instances require external synchronization. Structural operations advance an unchecked
/// mutation version; enumerators check that version in MoveNext and Reset. Direct backing-array writes and
/// indexer assignment bypass version checks. Enumeration is top-to-bottom; CopyTo, State, spans, and CloneTo use
/// bottom-to-top storage order.
/// </remarks>
[Serializable]
[JsonConverter(typeof(StateJsonConverterFactory))]
[MemoryPackable]
public sealed partial class SwiftStack<T> : IStateBacked<SwiftArrayState<T>>, ISwiftCloneable<T>, IEnumerable<T>, IEnumerable, ICollection<T>, ICollection
{
    #region Constants

    /// <summary>
    /// The default initial capacity of the SwiftStack if none is specified. Used to allocate a reasonable
    /// starting size to minimize resizing operations.
    /// </summary>
    public const int DefaultCapacity = 8;

    private static readonly T[] _emptyArray = Array.Empty<T>();
    private static readonly bool _clearReleasedSlots = RuntimeHelpers.IsReferenceOrContainsReferences<T>();

    #endregion

    #region Fields

    /// <summary>
    /// The internal array that stores elements of the SwiftStack. Resized as needed to
    /// accommodate additional elements. Not directly exposed outside the stack.
    /// </summary>
    private T[] _innerArray;

    /// <summary>
    /// The current number of elements in the SwiftStack. Represents the total count of
    /// valid elements stored in the stack, also indicating the arrayIndex of the next insertion point.
    /// </summary>
    private int _count;

    [NonSerialized]
    private uint _version;

    [NonSerialized]
    private object? _syncRoot;

    #endregion

    #region Constructors

    /// <summary>
    /// Creates an empty collection with no allocated element storage.
    /// </summary>
    public SwiftStack() : this(0) { }

    /// <summary>
    /// Initializes a new, empty instance of SwiftStack with the specified initial capacity.
    /// </summary>
    /// <param name="capacity">The requested element capacity; zero starts empty, and a negative constructor
    /// capacity uses the minimum allocation. Positive growth is rounded up; requests at or above 2^30 reach the
    /// Int32 capacity sentinel.</param>
    public SwiftStack(int capacity)
    {
        if (capacity == 0)
            _innerArray = _emptyArray;
        else
        {
            capacity = capacity <= DefaultCapacity ? DefaultCapacity : SwiftHashTools.NextPowerOfTwo(capacity);
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
    public SwiftStack(IEnumerable<T> items)
    {
        ChronicleGuard.ThrowIfNull(items, nameof(items));

        if (items is ICollection<T> collection)
        {
            int count = collection.Count;
            if (count == 0)
                _innerArray = _emptyArray;
            else
            {
                int capacity = SwiftHashTools.NextPowerOfTwo(count <= DefaultCapacity ? DefaultCapacity : count);
                _innerArray = new T[capacity];
                collection.CopyTo(_innerArray, 0);
                _count = count;
            }
        }
        else
        {
            _innerArray = new T[DefaultCapacity];
            foreach (T item in items)
                Push(item);
        }
    }

    /// <summary>
    /// Restores a collection by shallow-copying its item state.
    /// </summary>
    /// <param name="state">The item state to shallow-copy into new storage; Items must be non-null.</param>
    /// <exception cref="T:System.ArgumentNullException">state.Items is null, including a default
    /// state.</exception>
    /// <remarks>
    /// Capacity and mutation version are reconstructed, not preserved. Items use bottom-to-top storage order.
    /// </remarks>
    [MemoryPackConstructor]
    public SwiftStack(SwiftArrayState<T> state)
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
    /// Only indices zero through Count minus one are live. Structural mutations can move elements or replace this
    /// array. Old arrays remain detached after replacement. Writes bypass versioning.
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
    /// Gets the interface IsReadOnly.
    /// </summary>
    /// <value>false.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    bool ICollection<T>.IsReadOnly => false;

    /// <summary>
    /// Gets whether collection access is automatically synchronized.
    /// </summary>
    /// <value>false.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public bool IsSynchronized => false;

    /// <summary>
    /// Gets the interface SyncRoot.
    /// </summary>
    /// <value>The owner's lazily allocated synchronization object.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    object ICollection.SyncRoot => _syncRoot ??= new object();

    /// <summary>
    /// Gets or sets the element at a logical index.
    /// </summary>
    /// <param name="index">The zero-based logical index, from zero through Count minus one. Zero addresses the
    /// bottom of the stack.</param>
    /// <value>The element at index in bottom-to-top storage order.</value>
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
    /// <value>A new state containing a newly allocated array in bottom-to-top storage order.</value>
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
                _version = 0;
                return;
            }

            int capacity = SwiftHashTools.NextPowerOfTwo(count <= DefaultCapacity ? DefaultCapacity : count);

            _innerArray = new T[capacity];
            Array.Copy(value.Items, 0, _innerArray, 0, count);

            _count = count;
            _version = 0;
        }
    }

    #endregion

    #region Collection Manipulation

    /// <summary>
    /// Adds an element through the implemented collection interface.
    /// </summary>
    /// <param name="item">The element to compare, add or remove; reference values are not cloned.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void ICollection<T>.Add(T item) => Push(item);

    /// <summary>
    /// Inserts an object at the top of the SwiftStack.
    /// </summary>
    /// <param name="item">The element to store or locate; reference-type elements are not cloned.</param>
    /// <remarks>
    /// Stores the supplied element without cloning; grows backing storage when needed.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Push(T item)
    {
        if ((uint)_count == (uint)_innerArray.Length)
            Resize(_innerArray.Length * 2);
        _innerArray[_count++] = item;
        _version++;
    }

    /// <summary>
    /// Pushes the elements of the specified span onto the stack in order.
    /// </summary>
    /// <param name="items">The elements to copy in source order; source storage is not retained. An empty span
    /// changes nothing.</param>
    /// <remarks>
    /// Copies and appends the supplied elements; source storage is not retained. The last supplied item becomes
    /// the stack top. Empty input changes nothing. Enumerable sources must remain stable and report accurate
    /// counts. Exceptions from enumeration, copying, or comparison can leave partial writes; there is no rollback
    /// guarantee. Do not enumerate this collection as its own source while mutating it.
    /// </remarks>
    public void PushRange(ReadOnlySpan<T> items)
    {
        if (items.Length == 0)
            return;

        if (_count + items.Length > _innerArray.Length)
        {
            int newCapacity = SwiftHashTools.NextPowerOfTwo(_count + items.Length);
            Resize(newCapacity);
        }

        items.CopyTo(_innerArray.AsSpan(_count, items.Length));
        _count += items.Length;
        _version++;
    }

    /// <summary>
    /// Removes a matching element through the implemented interface.
    /// </summary>
    /// <param name="item">The element to compare, add or remove; reference values are not cloned.</param>
    /// <returns>This implementation always throws.</returns>
    /// <exception cref="T:System.NotSupportedException">Removal by value is unsupported; use Dequeue or
    /// Pop.</exception>
    bool ICollection<T>.Remove(T item)
    {
        throw new NotSupportedException("Remove is not supported on Stack.");
    }

    /// <summary>
    /// Removes and returns the object at the top of the SwiftStack.
    /// </summary>
    /// <returns>The top element; removed from the collection.</returns>
    /// <exception cref="T:System.InvalidOperationException">The collection is empty; contents are
    /// unchanged.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Pop()
    {
        ChronicleGuard.ThrowIfTrue((uint)_count == 0, message: "Stack is empty.");
        T item = _innerArray[--_count];
        if (_clearReleasedSlots)
            _innerArray[_count] = default!;
        _version++;
        return item;
    }

    /// <summary>
    /// Removes all elements from the SwiftStack, resetting its count to zero.
    /// </summary>
    /// <remarks>
    /// Empties the collection while preserving capacity. Ordinary clearing releases live references; references
    /// previously hidden by FastClear can remain in spare storage.
    /// </remarks>
    public void Clear()
    {
        if (_count == 0) return;
        if (_clearReleasedSlots)
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
    /// Clears the SwiftStack without releasing the reference to the stored elements. Use FastClear() when you
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
    /// If the current capacity is less than the specified value, the internal storage is increased to the next
    /// power of two greater than or equal to <paramref name="capacity" />. No action is taken if the current
    /// capacity is sufficient.
    /// </remarks>
    public void EnsureCapacity(int capacity)
    {
        capacity = SwiftHashTools.NextPowerOfTwo(capacity);
        if (capacity > _innerArray.Length)
            Resize(capacity);
    }

    /// <summary>
    /// Ensures that the capacity of the stack is sufficient to accommodate the specified number of elements.
    /// The stack capacity can increase by double to balance memory allocation efficiency and space.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Resize(int newSize)
    {
        int newCapacity = newSize <= DefaultCapacity ? DefaultCapacity : newSize;
        T[] newArray = new T[newCapacity];
        if ((uint)_count > 0)
            Array.Copy(_innerArray, 0, newArray, 0, _count);
        _innerArray = newArray;
        _version++;
    }


    /// <summary>
    /// Sets the capacity of a <see cref="T:SwiftCollections.SwiftStack`1" /> to the actual number of elements it
    /// contains, rounded up to a nearby next power of 2 value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TrimCapacity()
    {
        int newCapacity = _count <= DefaultCapacity ? DefaultCapacity : SwiftHashTools.NextPowerOfTwo(_count);
        T[] newArray = new T[newCapacity];
        if ((uint)_count > 0)
            Array.Copy(_innerArray, 0, newArray, 0, _count);
        _innerArray = newArray;
        _version++;
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Returns the object at the top of the SwiftStack without removing it.
    /// </summary>
    /// <returns>The top element; contents are unchanged.</returns>
    /// <exception cref="T:System.InvalidOperationException">The collection is empty; contents are
    /// unchanged.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Peek()
    {
        ChronicleGuard.ThrowIfTrue((uint)_count == 0, message: "Stack is empty.");
        return _innerArray[_count - 1];
    }

    /// <summary>
    /// Returns a string that represents the current stack, including its type and the number of elements it
    /// contains.
    /// </summary>
    /// <returns> A string containing the type name and the current element count if the stack is not empty;
    /// otherwise, a string indicating that the stack is empty. </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => (uint)_count == 0 ? $"{typeof(SwiftStack<T>)}: Empty" : $"{typeof(SwiftStack<T>)}: Count = {_count}";

    /// <summary>
    /// Returns a mutable span over the populated portion of the stack.
    /// </summary>
    /// <returns>A writable borrowed view over the live elements in bottom-to-top storage order.</returns>
    /// <remarks>
    /// No allocation or copy. Do not retain across structural mutations, capacity changes, or clearing; elements
    /// can move, and replaced storage becomes detached. Writes bypass enumerator version checks.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> AsSpan() => _innerArray.AsSpan(0, _count);

    /// <summary>
    /// Returns a read-only span over the populated portion of the stack.
    /// </summary>
    /// <returns>A read-only borrowed view over the live elements in bottom-to-top storage order.</returns>
    /// <remarks>
    /// No allocation or copy. Do not retain across structural mutations, capacity changes, or clearing; elements
    /// can move, and replaced storage becomes detached. Read-only restricts this view, not mutations through the
    /// owner.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<T> AsReadOnlySpan() => _innerArray.AsSpan(0, _count);

    /// <summary>
    /// Copies the elements of the collection to the specified array, starting at the specified array index.
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
    /// Shallow-copies in bottom-to-top storage order; source contents are preserved when the destination does not
    /// alias source storage. The caller retains ownership of the destination; unused destination cells are
    /// preserved. Validation failures occur before copying; incompatible array elements can cause a partial copy.
    /// </remarks>
    public void CopyTo(T[] array, int arrayIndex)
    {
        ChronicleGuard.ThrowIfNull(array, nameof(array));
        ChronicleGuard.ThrowIfArrayIndexInvalid(arrayIndex, array.Length);
        ChronicleGuard.ThrowIfArgument((uint)(array.Length - arrayIndex) < (uint)_count, nameof(array), "Destination array is not long enough.");

        Array.Copy(_innerArray, 0, array, arrayIndex, _count);
    }

    /// <summary>
    /// Copies the populated elements of the SwiftStack into the specified destination span.
    /// </summary>
    /// <param name="destination">The caller-owned destination span with space for at least Count elements. Only
    /// the first Count elements are overwritten.</param>
    /// <exception cref="T:System.ArgumentException">destination is shorter than Count.</exception>
    /// <remarks>
    /// Shallow-copies in bottom-to-top storage order; source contents are preserved when the destination does not
    /// alias source storage. The caller retains ownership of the destination; unused destination cells are
    /// preserved. Validation failures occur before copying; incompatible array elements can cause a partial copy.
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
    /// Shallow-copies in bottom-to-top storage order; source contents are preserved when the destination does not
    /// alias source storage. The caller retains ownership of the destination; unused destination cells are
    /// preserved. Validation failures occur before copying; incompatible array elements can cause a partial copy.
    /// </remarks>
    public void CopyTo(Array array, int arrayIndex)
    {
        ChronicleGuard.ThrowIfNull(array, nameof(array));
        ChronicleGuard.ThrowIfArgument(array.Rank != 1, nameof(array), "Array must be single dimensional.");
        ChronicleGuard.ThrowIfArgument(array.GetLowerBound(0) != 0, nameof(array), "Array must have zero-based indexing.");
        ChronicleGuard.ThrowIfArrayIndexInvalid(arrayIndex, array.Length);
        ChronicleGuard.ThrowIfArgument((uint)(array.Length - arrayIndex) < (uint)_count, nameof(array), "Destination array is not long enough.");

        try
        {
            for (int i = 0; (uint)i < (uint)_count; i++)
                array.SetValue(_innerArray[i], arrayIndex++);
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
        for (int i = 0; (uint)i < (uint)_count; i++)
            output.Add(_innerArray[i]);
    }

    /// <summary>
    /// Checks whether a matching live entry exists.
    /// </summary>
    /// <param name="item">The element to store or locate; reference-type elements are not cloned.</param>
    /// <returns>true if an equal live element exists; otherwise false. Does not change contents.</returns>
    public bool Contains(T item)
    {
        EqualityComparer<T> comparer = EqualityComparer<T>.Default;
        for (int i = 0; i < _count; i++)
        {
            if (comparer.Equals(_innerArray[i], item))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Determines whether the <see cref="T:SwiftCollections.SwiftStack`1" /> contains an element that matches the
    /// conditions defined by the specified predicate.
    /// </summary>
    /// <param name="match">The non-null predicate evaluated in top-to-bottom order. It must not mutate this
    /// collection.</param>
    /// <returns>true on the first matching predicate result; false if no live element matches.</returns>
    /// <exception cref="T:System.ArgumentNullException">match is null.</exception>
    public bool Exists(Predicate<T> match)
    {
        ChronicleGuard.ThrowIfNull(match, nameof(match));

        for (int i = _count - 1; i >= 0; i--)
        {
            if (match(_innerArray[i]))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Searches for an element that matches the conditions defined by the specified predicate, and returns the
    /// first matching element in stack enumeration order.
    /// </summary>
    /// <param name="match">The non-null predicate evaluated in top-to-bottom order. It must not mutate this
    /// collection.</param>
    /// <returns>The first matching element in top-to-bottom order, or default(T) when absent. A stored default
    /// value is indistinguishable from failure.</returns>
    /// <exception cref="T:System.ArgumentNullException">match is null.</exception>
    public T Find(Predicate<T> match)
    {
        ChronicleGuard.ThrowIfNull(match, nameof(match));

        for (int i = _count - 1; i >= 0; i--)
        {
            if (match(_innerArray[i]))
                return _innerArray[i];
        }

        return default!;
    }

    #endregion

    #region Enumerators

    /// <summary>
    /// Creates an enumerator over the live entries.
    /// </summary>
    /// <returns>A value-type enumerator in top-to-bottom order.</returns>
    /// <remarks>
    /// Structural mutations invalidate MoveNext and Reset. Direct storage writes and indexer assignments bypass
    /// the version check.
    /// </remarks>
    public SwiftStackEnumerator GetEnumerator() => new(this);
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
