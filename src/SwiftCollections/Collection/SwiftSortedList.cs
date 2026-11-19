//=======================================================================
// SwiftSortedList.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Chronicler;
using Chronicler.Diagnostics;
using Chronicler.Serialization;
using MemoryPack;
using SwiftCollections.Utility;

namespace SwiftCollections;

/// <summary>
/// Stores elements in ascending comparer order, allowing duplicates.
/// </summary>
/// <typeparam name="T">The type of elements in the collection.</typeparam>
/// <remarks>
/// Copies are shallow. Instances require external synchronization. Structural operations advance an unchecked
/// mutation version; enumerators check that version in MoveNext and Reset. Direct backing-array writes bypass
/// version checks. Ordering and lookup require a consistent, deterministic comparer. Equal elements have no
/// stable tie order. Enumeration follows comparer order.
/// </remarks>
[Serializable]
[JsonConverter(typeof(StateJsonConverterFactory))]
[MemoryPackable]
public partial class SwiftSortedList<T> : IStateBacked<SwiftArrayState<T>>, ISwiftCloneable<T>, IEnumerable<T>, IEnumerable, ICollection<T>, ICollection, IReadOnlyList<T>
{
    #region Constants

    /// <summary>
    /// The default initial capacity of the <see cref="T:SwiftCollections.SwiftSortedList`1" /> if none is
    /// specified. Used to allocate a reasonable starting size to minimize resizing operations.
    /// </summary>
    public const int DefaultCapacity = 8;

    private static readonly T[] _emptyArray = Array.Empty<T>();

    #endregion

    #region Fields

    /// <summary>
    /// Represents the internal array that stores the sorted elements.
    /// </summary>
    private T[] _innerArray;

    /// <summary>
    /// The number of elements contained in the <see cref="SwiftSortedList{T}"/>.
    /// </summary>
    private int _count;

    /// <summary>
    /// The offset within the internal array where the logical start of the list begins.
    /// Used to efficiently manage insertions and deletions at both ends without excessive shifting.
    /// </summary>
    private int _offset;

    /// <summary>
    /// A version counter used to track modifications to the sorted list.
    /// Incremented on mutations to detect changes during enumeration and ensure enumerator validity.
    /// </summary>
    [NonSerialized]
    private uint _version;

    /// <summary>
    /// The comparer used to sort and compare elements in the collection.
    /// </summary>
    [NonSerialized]
    private IComparer<T> _comparer;

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
    /// <remarks>
    /// This constructor creates an empty sorted list. Items added to the list will be automatically ordered
    /// according to the default comparer for the item type.
    /// </remarks>
    public SwiftSortedList() : this(0) { }

    /// <summary>
    /// Initializes a new, empty instance of <see cref="T:SwiftCollections.SwiftSortedList`1" /> uisng the
    /// specified <see cref="T:System.Collections.Generic.IComparer`1" />.
    /// </summary>
    /// <param name="comparer">The ordering comparer, retained by reference; null uses
    /// Comparer&lt;T&gt;.Default.</param>
    public SwiftSortedList(IComparer<T> comparer) : this(0, comparer) { }

    /// <summary>
    /// Initializes a new, empty instance of <see cref="T:SwiftCollections.SwiftSortedList`1" /> with the
    /// specified initial capacity and <see cref="T:System.Collections.Generic.IComparer`1" />.
    /// </summary>
    /// <param name="capacity">The requested element capacity; zero starts empty, and a negative constructor
    /// capacity uses the minimum allocation. Positive growth is rounded up; requests at or above 2^30 reach the
    /// Int32 capacity sentinel.</param>
    /// <param name="comparer">The ordering comparer, retained by reference; null uses
    /// Comparer&lt;T&gt;.Default.</param>
    public SwiftSortedList(int capacity, IComparer<T>? comparer = null)
    {
        _comparer = comparer ?? Comparer<T>.Default;

        if (capacity == 0)
        {
            _innerArray = _emptyArray;
            _offset = 0;
        }
        else
        {
            capacity = capacity <= DefaultCapacity ? DefaultCapacity : SwiftHashTools.NextPowerOfTwo(capacity);
            _innerArray = new T[capacity];
            _offset = capacity >> 1; // initial offset half of capacity
        }
    }

    /// <summary>
    /// Creates a collection by shallow-copying an enumerable.
    /// </summary>
    /// <param name="items">The elements to copy in source order; source storage is not retained. Null is
    /// rejected.</param>
    /// <param name="comparer">The ordering comparer, retained by reference; null uses
    /// Comparer&lt;T&gt;.Default.</param>
    /// <exception cref="T:System.ArgumentNullException">items is null.</exception>
    /// <remarks>
    /// Known-count sources must report a stable count and remain unchanged while copied. Arbitrary enumerables
    /// are supported. Input is sorted using the selected comparer.
    /// </remarks>
    public SwiftSortedList(IEnumerable<T> items, IComparer<T>? comparer = null)
    {
        ChronicleGuard.ThrowIfNull(items, nameof(items));

        _comparer = comparer ?? Comparer<T>.Default;

        if (items is ICollection<T> collection)
        {
            int count = collection.Count;
            if (count == 0)
            {
                _innerArray = _emptyArray;
                _offset = 0;
            }
            else
            {
                int capacity = SwiftHashTools.NextPowerOfTwo(count <= DefaultCapacity ? DefaultCapacity : count);
                _innerArray = new T[capacity];
                _offset = (capacity - count) >> 1;
                collection.CopyTo(_innerArray, _offset);
                SwiftArraySortHelper.Sort(_innerArray, _offset, count, _comparer);
                _count = count;
            }
        }
        else
        {
            _innerArray = new T[DefaultCapacity];
            _offset = DefaultCapacity >> 1;
            AddRange(items); // Will handle capacity increases as needed
        }
    }

    /// <summary>
    /// Restores a collection by shallow-copying trusted item state without comparing or sorting it.
    /// </summary>
    /// <param name="state">The item state to shallow-copy into new storage; Items must be non-null. Ordering-dependent
    /// operations require items ordered under the active comparer, which restore resets to Comparer&lt;T&gt;.Default.</param>
    /// <exception cref="T:System.ArgumentNullException">state.Items is null, including a default
    /// state.</exception>
    /// <remarks>
    /// Capacity and mutation version are reconstructed, not preserved. The comparer resets to
    /// Comparer&lt;T&gt;.Default; payload order and duplicates are preserved without validation. For custom-comparer
    /// snapshots or unordered items, use new SwiftSortedList&lt;T&gt;(state.Items, desiredComparer) to copy and sort
    /// once with the requested comparer. Alternatively, call SetComparer with a different comparer immediately
    /// after restore, before lookup or mutation; setting the existing comparer is a no-op. Setting a comparer
    /// before assigning State does not preserve it. Comparer semantics must remain consistent during use.
    /// </remarks>
    [MemoryPackConstructor]
    public SwiftSortedList(SwiftArrayState<T> state)
    {
        State = state;

        // Validate that the internal array and comparer are not null after deserialization
        ChronicleGuard.ThrowIfNull(_innerArray, nameof(_innerArray));
        ChronicleGuard.ThrowIfNull(_comparer, nameof(_comparer));
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the underlying array that stores the elements of the collection.
    /// </summary>
    /// <value>The live mutable backing storage, including unused slots.</value>
    /// <remarks>
    /// Only the Count cells starting at Offset are live. Structural mutations can move elements or replace this
    /// array. Old arrays remain detached after replacement. Writes bypass versioning and may invalidate sorted
    /// order.
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
    /// Gets the physical offset of the first live element.
    /// </summary>
    /// <value>The physical backing-array index of logical element zero.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Offset => _offset;

    /// <summary>
    /// Gets the structural mutation version.
    /// </summary>
    /// <value>The unchecked structural-mutation counter used by enumerators.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public uint Version => _version;

    /// <summary>
    /// Gets the current capacity of the internal array.
    /// </summary>
    /// <value>The allocated backing-array length; it may exceed Count.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Capacity => _innerArray.Length;

    /// <summary>
    /// Gets the current ordering or equality comparer.
    /// </summary>
    /// <value>The comparer that defines ordering and equality for searches; retained by reference.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public IComparer<T> Comparer => _comparer;

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
    /// Gets the element at a logical index.
    /// </summary>
    /// <param name="index">The zero-based logical index, from zero through Count minus one. </param>
    /// <value>The element at index in comparer order.</value>
    /// <exception cref="T:System.ArgumentOutOfRangeException">index is outside the live range.</exception>
    [JsonIgnore]
    [MemoryPackIgnore]
    public T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ChronicleGuard.ThrowIfListIndexInvalid(index, _count);
            return _innerArray[GetPhysicalIndex(index)];
        }
    }

    /// <summary>
    /// Gets a shallow snapshot of the live elements.
    /// </summary>
    /// <value>A new state containing a newly allocated array in comparer order.</value>
    /// <remarks>
    /// Element references remain shared. Capacity and version are not serialized. The comparer is not serialized;
    /// restoring copies the exact payload order and selects Comparer&lt;T&gt;.Default without comparing or sorting.
    /// Items must be ordered under the active comparer before ordering-dependent operations. To establish custom
    /// ordering explicitly, construct from State.Items and the desired comparer; this copies and sorts once.
    /// </remarks>
    [JsonInclude]
    [MemoryPackInclude]
    public SwiftArrayState<T> State
    {
        get
        {
            var items = new T[_count];
            Array.Copy(_innerArray, _offset, items, 0, _count);
            return new SwiftArrayState<T>(items);
        }
        internal set
        {
            ChronicleGuard.ThrowIfNull(value.Items, nameof(value.Items));

            int count = value.Items.Length;

            if (count == 0)
            {
                _innerArray = _emptyArray;
                _offset = 0;
                _count = 0;
            }
            else
            {
                int capacity = SwiftHashTools.NextPowerOfTwo(count <= DefaultCapacity ? DefaultCapacity : count);
                _innerArray = new T[capacity];

                int newOffset = (capacity - count) >> 1;

                Array.Copy(value.Items, 0, _innerArray, newOffset, count);

                _offset = newOffset;
                _count = count;
            }

            _version = 0;
            _comparer = Comparer<T>.Default;
        }
    }

    #endregion

    #region Collection Manipulation

    /// <summary>
    /// Inserts an element in comparer order, including comparer-equal duplicates.
    /// </summary>
    /// <param name="item">The element to store or locate; reference-type elements are not cloned.</param>
    /// <remarks>
    /// Stores the supplied element without cloning; grows backing storage when needed.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T item)
    {
        int index = Search(item);
        if (index < 0) index = ~index;
        Insert(item, index);
    }

    /// <summary>
    /// Adds an item if no existing item compares equal to it.
    /// </summary>
    /// <param name="item">The element to store or locate; reference-type elements are not cloned.</param>
    /// <returns>true if inserted; false if the comparer considers an existing item equal, leaving contents
    /// unchanged.</returns>
    public bool TryAddUnique(T item)
    {
        int index = _count == 0 || _comparer.Compare(_innerArray[_offset + _count - 1], item) < 0
            ? ~_count
            : Search(item);
        if (index >= 0)
            return false;
        Insert(item, ~index);
        return true;
    }

    private void Insert(T item, int index)
    {
        ChronicleGuard.ThrowIfArrayIndexInvalid(index, _count, nameof(index));
        if (_count == _innerArray.Length)
            Resize(_innerArray.Length * 2);

        int suffixCount = _count - index;
        if (index >= suffixCount && _offset + _count == _innerArray.Length)
            RecenterArray();

        int physicalIndex = GetPhysicalIndex(index);
        if (_offset > 0 && index < suffixCount)
        {
            if (index != 0)
                Array.Copy(_innerArray, _offset, _innerArray, _offset - 1, index);
            _offset--;
            physicalIndex--;
        }
        else if (suffixCount != 0)
            Array.Copy(_innerArray, physicalIndex, _innerArray, physicalIndex + 1, suffixCount);

        _innerArray[physicalIndex] = item;
        _count++;
        _version++;
    }

    /// <summary>
    /// Adds a range of elements to the collection, ensuring they are sorted and merged efficiently.
    /// </summary>
    /// <param name="items">The elements to copy in source order; source storage is not retained. Null is
    /// rejected.</param>
    /// <exception cref="T:System.ArgumentNullException">items is null.</exception>
    /// <remarks>
    /// Copies and sorts/merges the supplied elements; source storage is not retained. Empty input changes
    /// nothing. Enumerable sources must remain stable and report accurate counts. Exceptions from enumeration,
    /// copying, or comparison can leave partial writes; there is no rollback guarantee. Self-add is copied
    /// through temporary storage.
    /// </remarks>
    public void AddRange(IEnumerable<T> items)
    {
        ChronicleGuard.ThrowIfNull(items, nameof(items));

        if (items is ICollection<T> collection)
        {
            AddRange(collection);
            return;
        }

        if (items is IReadOnlyCollection<T> readOnlyCollection)
        {
            AddRange(readOnlyCollection);
            return;
        }

        T[] sortedItems = CopyToSortedArray(items);
        if (sortedItems.Length == 0)
            return;

        if ((uint)_count == 0)
        {
            InitializeFromSortedItems(sortedItems);
            return;
        }

        MergeSortedItems(sortedItems);
    }

    private void AddRange(ICollection<T> collection)
    {
        int count = collection.Count;
        if (count == 0)
            return;

        if (ShouldUseTemporaryCopy(collection))
        {
            T[] sortedItems = collection.ToArray();
            SwiftArraySortHelper.Sort(sortedItems, 0, sortedItems.Length, _comparer);
            MergeSortedItems(sortedItems);
            return;
        }

        if ((uint)_count == 0)
        {
            InitializeFromCollection(collection);
            return;
        }

        MergeCollectionItems(collection, count);
    }

    private void AddRange(IReadOnlyCollection<T> collection)
    {
        int count = collection.Count;
        if (count == 0)
            return;

        if ((uint)_count == 0)
        {
            InitializeFromEnumerable(collection, count);
            return;
        }

        MergeEnumerableItems(collection, count);
    }

    private bool ShouldUseTemporaryCopy(ICollection<T> collection) =>
        ReferenceEquals(collection, this) ||
        collection is T[] array && ReferenceEquals(array, _innerArray);

    private void InitializeFromCollection(ICollection<T> collection)
    {
        int count = collection.Count;
        EnsureCapacity(count);

        _offset = (_innerArray.Length - count) >> 1;
        collection.CopyTo(_innerArray, _offset);
        SwiftArraySortHelper.Sort(_innerArray, _offset, count, _comparer);

        _count = count;
        _version++;
    }

    private void InitializeFromEnumerable(IEnumerable<T> items, int count)
    {
        EnsureCapacity(count);

        _offset = (_innerArray.Length - count) >> 1;
        int index = _offset;
        foreach (T item in items)
            _innerArray[index++] = item;

        SwiftArraySortHelper.Sort(_innerArray, _offset, count, _comparer);

        _count = count;
        _version++;
    }

    private T[] CopyToSortedArray(IEnumerable<T> items)
    {
        T[] sortedItems = items.ToArray();

        if (sortedItems.Length > 0)
            SwiftArraySortHelper.Sort(sortedItems, 0, sortedItems.Length, _comparer);

        return sortedItems;
    }

    private void InitializeFromSortedItems(T[] sortedItems)
    {
        int initialCapacity = SwiftHashTools.NextPowerOfTwo(sortedItems.Length < DefaultCapacity ? DefaultCapacity : sortedItems.Length);
        int newOffset = (initialCapacity - sortedItems.Length) >> 1;
        if ((uint)_innerArray.Length < (uint)initialCapacity)
            _innerArray = new T[initialCapacity];

        Array.Copy(sortedItems, 0, _innerArray, newOffset, sortedItems.Length);
        _offset = newOffset;
        _count = sortedItems.Length;
        _version++;
    }

    private void MergeSortedItems(T[] sortedItems)
    {
        int newCount = _count + sortedItems.Length;
        int totalRequiredCapacity = newCount + _offset;
        int newCapacity = SwiftHashTools.NextPowerOfTwo(totalRequiredCapacity);
        int mergedOffset = (newCapacity - newCount) >> 1;
        T[] newArray = new T[newCapacity];

        int existingIndex = 0;
        int newItemsIndex = 0;
        int mergedIndex = mergedOffset;

        while (existingIndex < _count && newItemsIndex < sortedItems.Length)
        {
            T existingItem = _innerArray[_offset + existingIndex];
            T newItem = sortedItems[newItemsIndex];

            if (_comparer.Compare(existingItem, newItem) <= 0)
            {
                newArray[mergedIndex++] = existingItem;
                existingIndex++;
            }
            else
            {
                newArray[mergedIndex++] = newItem;
                newItemsIndex++;
            }
        }

        // Copy any remaining existing items
        while (existingIndex < _count)
            newArray[mergedIndex++] = _innerArray[_offset + existingIndex++];

        // Copy any remaining new items
        while (newItemsIndex < sortedItems.Length)
            newArray[mergedIndex++] = sortedItems[newItemsIndex++];

        _innerArray = newArray;
        _offset = mergedOffset;
        _count = newCount;

        _version++;
    }

    private void MergeCollectionItems(ICollection<T> collection, int collectionCount)
    {
        int existingCount = _count;
        int newCount = existingCount + collectionCount;
        T[] target;
        int mergedOffset;

        if (_innerArray.Length >= newCount)
        {
            target = _innerArray;
            mergedOffset = (_innerArray.Length - newCount) >> 1;
            if (mergedOffset != _offset)
                Array.Copy(_innerArray, _offset, target, mergedOffset, existingCount);
        }
        else
        {
            int newCapacity = SwiftHashTools.NextPowerOfTwo(newCount);
            target = new T[newCapacity];
            mergedOffset = (newCapacity - newCount) >> 1;
            Array.Copy(_innerArray, _offset, target, mergedOffset, existingCount);
        }

        int collectionOffset = mergedOffset + existingCount;
        collection.CopyTo(target, collectionOffset);
        SwiftArraySortHelper.Sort(target, mergedOffset, newCount, _comparer);

        _innerArray = target;
        _offset = mergedOffset;
        _count = newCount;

        _version++;
    }

    private void MergeEnumerableItems(IEnumerable<T> items, int itemCount)
    {
        int existingCount = _count;
        int newCount = existingCount + itemCount;
        T[] target;
        int mergedOffset;

        if (_innerArray.Length >= newCount)
        {
            target = _innerArray;
            mergedOffset = (_innerArray.Length - newCount) >> 1;
            if (mergedOffset != _offset)
                Array.Copy(_innerArray, _offset, target, mergedOffset, existingCount);
        }
        else
        {
            int newCapacity = SwiftHashTools.NextPowerOfTwo(newCount);
            target = new T[newCapacity];
            mergedOffset = (newCapacity - newCount) >> 1;
            Array.Copy(_innerArray, _offset, target, mergedOffset, existingCount);
        }

        int index = mergedOffset + existingCount;
        foreach (T item in items)
            target[index++] = item;

        SwiftArraySortHelper.Sort(target, mergedOffset, newCount, _comparer);

        _innerArray = target;
        _offset = mergedOffset;
        _count = newCount;

        _version++;
    }

    /// <summary>
    /// Removes and returns the minimum element in the sorter.
    /// </summary>
    /// <returns>The minimum element; removed from the collection.</returns>
    /// <exception cref="T:System.InvalidOperationException">The collection is empty; contents are
    /// unchanged.</exception>
    public T PopMin()
    {
        ChronicleGuard.ThrowIfTrue((uint)_count == 0, message: "Cannot pop from an empty list.");

        int index = GetPhysicalIndex(0);
        T ret = _innerArray[index];
        _innerArray[index] = default!;
        _count--;
        // Increment _offset as we remove from the front
        _offset = _count == 0 ? _innerArray.Length >> 1 : _offset + 1;

        _version++;

        return ret;
    }

    /// <summary>
    /// Removes and returns the maximum element in the sorter.
    /// </summary>
    /// <returns>The maximum element; removed from the collection.</returns>
    /// <exception cref="T:System.InvalidOperationException">The collection is empty; contents are
    /// unchanged.</exception>
    public T PopMax()
    {
        ChronicleGuard.ThrowIfTrue((uint)_count == 0, message: "Cannot pop from an empty list.");

        int index = GetPhysicalIndex(--_count);
        T ret = _innerArray[index];
        _innerArray[index] = default!;
        if ((uint)_count == 0) _offset = _innerArray.Length >> 1;

        _version++;

        return ret;
    }

    /// <summary>
    /// Removes one matching live entry.
    /// </summary>
    /// <param name="item">The element to store or locate; reference-type elements are not cloned.</param>
    /// <returns>true if one matching element was removed; false if absent, with contents unchanged. Comparer
    /// equality determines the match; any equal duplicate may be removed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove(T item)
    {
        if ((uint)_count == 0) return false;

        int index = Search(item);
        if (index < 0) return false;

        RemoveAt(index);
        return true;
    }

    /// <summary>
    /// Removes the element at the specified arrayIndex from the sorted list. Shifts elements as needed to
    /// maintain the sorted order and efficient space utilization.
    /// </summary>
    /// <param name="index">The zero-based logical index, from zero through Count minus one. </param>
    /// <exception cref="T:System.ArgumentOutOfRangeException">index is outside the permitted logical
    /// range.</exception>
    public void RemoveAt(int index)
    {
        ChronicleGuard.ThrowIfListIndexInvalid(index, _count);

        int physicalIndex = GetPhysicalIndex(index);

        _count--;
        if ((uint)index < (uint)_count)
        {
            int distanceToHead = physicalIndex - _offset;
            int distanceToTail = (_offset + _count) - physicalIndex;

            if (distanceToHead < distanceToTail)
            {
                // Shift elements towards the tail (right) to fill the gap
                if (distanceToHead != 0)
                    Array.Copy(_innerArray, _offset, _innerArray, _offset + 1, distanceToHead);
                _innerArray[_offset++] = default!;
            }
            else
            {
                // Shift elements towards the head (left) to fill the gap
                Array.Copy(_innerArray, physicalIndex + 1, _innerArray, physicalIndex, distanceToTail);
                _innerArray[GetPhysicalIndex(_count)] = default!; // Clear the last element
            }
        }
        else  // Removing the last element; simply clear it
            _innerArray[GetPhysicalIndex(_count)] = default!;

        if (_offset != 0)
        {
            if (_count == 0)
                _offset = _innerArray.Length >> 1;
            // Recenter at the quarter-full boundary to avoid repeated copies during a drain.
            else if (_count == (_innerArray.Length >> 2) - 1)
                RecenterArray();
        }

        _version++;
    }

    /// <summary>
    /// Removes all live entries while preserving reusable capacity.
    /// </summary>
    /// <remarks>
    /// Empties the collection while preserving capacity. Ordinary clearing releases live references; references
    /// previously hidden by FastClear can remain in spare storage.
    /// </remarks>
    public void Clear()
    {
        if ((uint)_count == 0) return;
        Array.Clear(_innerArray, _offset, _count);
        _count = 0;
        _offset = _innerArray.Length >> 1; // Reset _offset to the middle of the array

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
        if (clearRetainedReferences && RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            Array.Clear(_innerArray, 0, _offset);
            Array.Clear(_innerArray, _offset + _count, _innerArray.Length - _offset - _count);
        }
        Clear();
    }

    /// <summary>
    /// Quickly clears the list by resetting the count and offset without modifying the internal array. Note: This
    /// leaves references in the internal array, which may prevent garbage collection of reference types. Use when
    /// performance is critical and you are certain that residual references are acceptable.
    /// </summary>
    /// <remarks>
    /// Resets the logical count without clearing stored values. References can remain retained until overwritten,
    /// reallocated, or Clear(true). Capacity is preserved.
    /// </remarks>
    public void FastClear()
    {
        if ((uint)_count == 0) return;
        _count = 0;
        _offset = _innerArray.Length >> 1; // Reset offset to middle

        _version++;
    }

    #endregion

    #region Capacity Management

    /// <summary>
    /// Ensures that the capacity of <see cref="T:SwiftCollections.SwiftSortedList`1" /> is sufficient to
    /// accommodate the specified number of elements. The capacity can increase by double to balance memory
    /// allocation efficiency and space.
    /// </summary>
    /// <param name="capacity">The minimum element capacity. Values at or below zero normalize to one; requests at
    /// or above 2^30 reach the Int32 capacity sentinel. Existing capacity is never reduced.</param>
    public void EnsureCapacity(int capacity)
    {
        capacity = SwiftHashTools.NextPowerOfTwo(capacity);
        if (capacity > _innerArray.Length)
            Resize(capacity);
    }

    /// <summary>
    /// Ensures that the capacity of <see cref="SwiftSortedList{T}"/> is sufficient to accommodate the specified number of elements.
    /// The capacity can increase by double to balance memory allocation efficiency and space.
    /// </summary>
    private void Resize(int newSize)
    {
        int newCapacity = newSize <= DefaultCapacity ? DefaultCapacity : newSize;

        T[] newArray = new T[newCapacity];
        int newOffset = (newArray.Length - _count) >> 1; // Center the elements in the new array
        if ((uint)_count > 0)
            Array.Copy(_innerArray, _offset, newArray, newOffset, _count);

        _innerArray = newArray;
        _offset = newOffset;

        _version++;
    }

    /// <summary>
    /// Recenters the elements within the internal array to balance available space on both ends.
    /// This minimizes the need for shifting elements during future insertions and deletions.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecenterArray()
    {
        int newOffset = (_innerArray.Length - _count) >> 1;
        if (newOffset == _offset)
            return;

        Array.Copy(_innerArray, _offset, _innerArray, newOffset, _count);
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            int start = newOffset < _offset ? Math.Max(_offset, newOffset + _count) : _offset;
            Array.Clear(_innerArray, start, Math.Min(Math.Abs(newOffset - _offset), _count));
        }
        _offset = newOffset;

        _version++;
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Sets a new comparer for the sorted list and re-sorts the elements.
    /// </summary>
    /// <param name="comparer">The ordering comparer, retained by reference; must be non-null.</param>
    /// <exception cref="T:System.ArgumentNullException">comparer is null.</exception>
    /// <remarks>
    /// Reorders live storage in place; equal items have no stable tie order. A throwing comparer can leave
    /// partially reordered contents and the new comparer installed. Direct storage views observe the changes.
    /// Passing the current comparer instance does nothing; this is not a force-sort operation.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetComparer(IComparer<T> comparer)
    {
        ChronicleGuard.ThrowIfNull(comparer, nameof(comparer));
        if (ReferenceEquals(comparer, _comparer))
            return;

        _comparer = comparer;
        SwiftArraySortHelper.Sort(_innerArray, _offset, _count, _comparer);
        _version++;
    }

    /// <summary>
    /// Converts a logical arrayIndex within the list to the corresponding physical arrayIndex in the internal array.
    /// </summary>
    /// <param name="logicalIndex">The logical arrayIndex within the list.</param>
    /// <returns>The physical arrayIndex within the internal array.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetPhysicalIndex(int logicalIndex) => _offset + logicalIndex;

    /// <summary>
    /// Returns a read-only span over the populated sorted portion of the list.
    /// </summary>
    /// <returns>A read-only borrowed view over the live elements in comparer order.</returns>
    /// <remarks>
    /// No allocation or copy. Do not retain across structural mutations, capacity changes, or clearing; elements
    /// can move, and replaced storage becomes detached. Read-only restricts this view, not mutations through the
    /// owner.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<T> AsReadOnlySpan() => _innerArray.AsSpan(_offset, _count);

    /// <summary>
    /// Returns the minimum element in the sorter without removing it.
    /// </summary>
    /// <returns>The minimum element; contents are unchanged.</returns>
    /// <exception cref="T:System.InvalidOperationException">The collection is empty; contents are
    /// unchanged.</exception>
    public T PeekMin()
    {
        ChronicleGuard.ThrowIfTrue((uint)_count == 0, message: "Cannot peek from an empty list.");
        return _innerArray[GetPhysicalIndex(0)];
    }

    /// <summary>
    /// Returns the maximum element in the sorter without removing it.
    /// </summary>
    /// <returns>The maximum element; contents are unchanged.</returns>
    /// <exception cref="T:System.InvalidOperationException">The collection is empty; contents are
    /// unchanged.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T PeekMax()
    {
        ChronicleGuard.ThrowIfTrue((uint)_count == 0, message: "Cannot peek from an empty list.");
        return _innerArray[GetPhysicalIndex(_count - 1)];
    }

    /// <summary>
    /// Checks whether a matching live entry exists.
    /// </summary>
    /// <param name="item">The element to store or locate; reference-type elements are not cloned.</param>
    /// <returns>true if an equal live element exists; otherwise false. Uses the ordering comparer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(T item) => Search(item) >= 0;

    /// <summary>
    /// Determines whether the <see cref="T:SwiftCollections.SwiftSortedList`1" /> contains an element that
    /// matches the conditions defined by the specified predicate.
    /// </summary>
    /// <param name="match">The non-null predicate evaluated in comparer order. It must not mutate this
    /// collection.</param>
    /// <returns>true on the first matching predicate result; false if no live element matches.</returns>
    /// <exception cref="T:System.ArgumentNullException">match is null.</exception>
    public bool Exists(Predicate<T> match)
    {
        ChronicleGuard.ThrowIfNull(match, nameof(match));

        for (int i = 0; i < _count; i++)
        {
            if (match(_innerArray[GetPhysicalIndex(i)]))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Searches for an element that matches the conditions defined by the specified predicate, and returns the
    /// first matching element in sorted order.
    /// </summary>
    /// <param name="match">The non-null predicate evaluated in comparer order. It must not mutate this
    /// collection.</param>
    /// <returns>The first matching element in comparer order, or default(T) when absent. A stored default value
    /// is indistinguishable from failure.</returns>
    /// <exception cref="T:System.ArgumentNullException">match is null.</exception>
    public T Find(Predicate<T> match)
    {
        ChronicleGuard.ThrowIfNull(match, nameof(match));

        for (int i = 0; i < _count; i++)
        {
            T item = _innerArray[GetPhysicalIndex(i)];
            if (match(item))
                return item;
        }

        return default!;
    }

    /// <summary>
    /// Finds a logical index of any comparer-equal element.
    /// </summary>
    /// <param name="item">The element to store or locate; reference-type elements are not cloned.</param>
    /// <returns>The index of any comparer-equal element, or -1 if absent.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndexOf(T item)
    {
        int index = Search(item);
        return index >= 0 ? index : -1;
    }

    /// <summary>
    /// Determines the insertion point for a specified item in the collection. The insertion point is the
    /// arrayIndex where the item would be inserted if it were not already present.
    /// </summary>
    /// <param name="item">The element to store or locate; reference-type elements are not cloned.</param>
    /// <returns>An index from zero through Count; an existing equal index or the insertion position that
    /// preserves comparer order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int InsertionPoint(T item)
    {
        int index = Search(item);
        return index >= 0 ? index : ~index;
    }

    /// <summary>
    /// Searches for the specified item in the sorted collection.
    /// </summary>
    /// <param name="item">The element to store or locate; reference-type elements are not cloned.</param>
    /// <returns>A non-negative logical index for a comparer-equal element; otherwise the bitwise complement of
    /// its insertion position.</returns>
    public int Search(T item)
    {
        int low = _offset;
        int high = low + _count - 1;
        while (low <= high)
        {
            int mid = low + ((high - low) >> 1);
            int cmp = _comparer.Compare(_innerArray[mid], item);
            if (cmp == 0)
                return mid - _offset;
            if (cmp < 0)
                low = mid + 1;
            else
                high = mid - 1;
        }
        return ~(low - _offset);
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
    /// Shallow-copies in comparer order; source contents are preserved when the destination does not alias source
    /// storage. The caller retains ownership of the destination; unused destination cells are preserved.
    /// Validation failures occur before copying; incompatible array elements can cause a partial copy.
    /// </remarks>
    public void CopyTo(T[] array, int arrayIndex)
    {
        ChronicleGuard.ThrowIfNull(array, nameof(array));
        ChronicleGuard.ThrowIfArrayIndexInvalid(arrayIndex, array.Length, nameof(arrayIndex));
        ChronicleGuard.ThrowIfArgument(array.Length - arrayIndex < _count, nameof(array), "The target array is too small.");

        Array.Copy(_innerArray, _offset, array, arrayIndex, _count);
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
    /// Shallow-copies in comparer order; source contents are preserved when the destination does not alias source
    /// storage. The caller retains ownership of the destination; unused destination cells are preserved.
    /// Validation failures occur before copying; incompatible array elements can cause a partial copy.
    /// </remarks>
    public void CopyTo(Array array, int arrayIndex)
    {
        ChronicleGuard.ThrowIfNull(array, nameof(array));
        ChronicleGuard.ThrowIfArrayIndexInvalid(arrayIndex, array.Length, nameof(arrayIndex));
        ChronicleGuard.ThrowIfArgument(array.Length - arrayIndex < _count, nameof(array), "The target array is too small.");

        Array.Copy(_innerArray, _offset, array, arrayIndex, _count);
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
    /// Returns an enumerator that iterates through <see cref="T:SwiftCollections.SwiftSortedList`1" />.
    /// </summary>
    /// <returns>A value-type enumerator in comparer order.</returns>
    /// <remarks>
    /// Structural mutations invalidate MoveNext and Reset. Direct storage writes and indexer assignments bypass
    /// the version check.
    /// </remarks>
    public SwiftSorterEnumerator GetEnumerator() => new(this);
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
