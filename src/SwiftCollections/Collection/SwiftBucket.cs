using MemoryPack;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace SwiftCollections;

/// <summary>
/// Represents a high-performance bucket collection that assigns and manages stable integer indices
/// for stored items. Provides O(1) insertion, removal, and lookup by internally generated index.
/// </summary>
/// <remarks>
/// Unlike <see cref="SwiftSparseMap{T}"/>, which requires callers to provide the key used to store values,
/// <see cref="SwiftBucket{T}"/> internally generates and manages indices for each inserted item.
///
/// These indices remain stable for the lifetime of the item unless it is removed.
///
/// The container is optimized for scenarios requiring:
/// <list type="bullet">
///     <item>
///         <description>Stable handles or identifiers.</description>
///     </item>
///     <item>
///         <description>Fast addition and removal.</description>
///     </item>
///     <item>
///         <description>Dense storage and iteration performance.</description>
///     </item>
/// </list>
///
/// **Efficient Lookups Using Indices**:
/// When you add items to the bucket using the <see cref="Add"/> method, it returns an arrayIndex that you can store externally.
/// You can then use this arrayIndex to access the item directly via the indexer, and check if it's still present using the <see cref="IsAllocated"/> method.
/// This approach allows for O(1) time complexity for lookups and existence checks, avoiding the need for O(n) searches using methods like <see cref="IndexOf"/> or <see cref="Contains"/>.
///
/// **Note**: iteration over the collection does not follow any guaranteed order and depends on internal allocation.
/// </remarks>
/// <typeparam name="T">Specifies the type of elements in the bucket.</typeparam>
[Serializable]
[JsonConverter(typeof(SwiftStateJsonConverterFactory))]
[MemoryPackable]
public sealed partial class SwiftBucket<T> : ISwiftCloneable<T>, IEnumerable<T>, ICollection<T>, ICollection
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
    /// Initializes a new instance of the <see cref="SwiftBucket{T}"/> class.
    /// </summary>
    public SwiftBucket() : this(DefaultCapacity) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SwiftBucket{T}"/> class with the specified capacity.
    /// </summary>
    /// <param name="capacity">The initial capacity of the bucket.</param>
    public SwiftBucket(int capacity)
    {
        capacity = capacity <= DefaultCapacity ? DefaultCapacity : SwiftHashTools.NextPowerOfTwo(capacity);
        _innerArray = new Entry[capacity];
        _freeIndices = new SwiftIntStack(SwiftIntStack.DefaultCapacity);
    }

    ///  <summary>
    ///  Initializes a new instance of the <see cref="SwiftBucket{T}"/> class with the specified <see cref="SwiftArrayState{T}"/>.
    ///  </summary>
    ///  <param name="state">The state containing the internal array, count, offset, and version for initialization.</param>
    [MemoryPackConstructor]
    public SwiftBucket(SwiftBucketState<T> state)
    {
        State = state;

        SwiftThrowHelper.ThrowIfNull(_innerArray, nameof(state.Items));
        SwiftThrowHelper.ThrowIfNull(_freeIndices, nameof(state.FreeIndices));
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the number of elements contained in the <see cref="SwiftBucket{T}"/>.
    /// </summary>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Count => _count;

    /// <summary>
    /// Gets the highest value recorded for the count during the lifetime of the object.
    /// </summary>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int PeakCount => _peakCount;

    /// <summary>
    /// Gets the total capacity of the <see cref="SwiftBucket{T}"/>.
    /// </summary>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Capacity => _innerArray.Length;

    /// <summary>
    /// Gets or sets the element at the specified arrayIndex.
    /// Throws <see cref="ArgumentOutOfRangeException"/> if the arrayIndex is invalid or unallocated.
    /// </summary>
    /// <param name="index">The zero-based arrayIndex of the element to get or set.</param>
    [JsonIgnore]
    [MemoryPackIgnore]
    public T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (!IsAllocated(index)) throw new ArgumentOutOfRangeException(nameof(index));
            return _innerArray[index].Value;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            if (!IsAllocated(index)) throw new ArgumentOutOfRangeException(nameof(index));
            _innerArray[index].Value = value;
            _version++;
        }
    }

    ///<inheritdoc/>
    [JsonIgnore]
    [MemoryPackIgnore]
    public bool IsReadOnly => false;

    ///<inheritdoc/>
    [JsonIgnore]
    [MemoryPackIgnore]
    public bool IsSynchronized => false;

    ///<inheritdoc/>
    [JsonIgnore]
    [MemoryPackIgnore]
    public object SyncRoot => _syncRoot ??= new object();

    /// <summary>
    /// Gets or sets the current state of the bucket, including all items, allocation status, and free indices.
    /// </summary>
    /// <remarks>
    /// Use this property to capture or restore the complete state of the bucket, such as for serialization or checkpointing scenarios. 
    /// Setting this property replaces the entire internal state, including items and allocation metadata.
    /// </remarks>
    [JsonInclude]
    [MemoryPackInclude]
    public SwiftBucketState<T> State
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

            return new SwiftBucketState<T>(
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
            _freeIndices = new SwiftIntStack(Math.Max(SwiftIntStack.DefaultCapacity, freeIndices.Length));

            _count = 0;
            int maxReferencedIndex = -1;

            for (int i = 0; i < sourceLength; i++)
            {
                if (allocated.Length > i && allocated[i])
                {
                    if (items.Length > i)
                        _innerArray[i].Value = items[i];

                    _innerArray[i].IsUsed = true;
                    _count++;
                    maxReferencedIndex = i;
                }
            }

            foreach (var index in freeIndices)
            {
                if ((uint)index >= (uint)capacity)
                    throw new InvalidOperationException("Free index is out of range.");

                _freeIndices.Push(index);
                if (index > maxReferencedIndex)
                    maxReferencedIndex = index;
            }

            int peakCount = value.PeakCount;
            if (peakCount < 0)
                peakCount = 0;

            _peakCount = Math.Max(peakCount, maxReferencedIndex + 1);
            if (_peakCount > capacity)
                _peakCount = capacity;

            _version = 0;
        }
    }

    #endregion

    #region Collection Management

    /// <summary>
    /// Adds an item to the bucket and returns its arrayIndex.
    /// </summary>
    /// <param name="item">The item to add.</param>
    /// <returns>The arrayIndex where the item was added.</returns>
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

    void ICollection<T>.Add(T item) => Add(item);

    /// <summary>
    /// Inserts an item at the specified arrayIndex.
    /// If an item already exists at that arrayIndex, it will be replaced.
    /// </summary>
    /// <param name="index">The arrayIndex at which to insert the item.</param>
    /// <param name="item">The item to insert.</param>
    public void InsertAt(int index, T item)
    {
        SwiftThrowHelper.ThrowIfNegative(index, nameof(index));
        if ((uint)index >= (uint)_innerArray.Length)
            Resize(_innerArray.Length * 2);
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
    /// Removes the first occurrence of a specific object from the bucket.
    /// </summary>
    /// <param name="item">The object to remove.</param>
    /// <returns><c>true</c> if item was successfully removed; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRemove(T item)
    {
        int index = IndexOf(item);
        if (index < 0) return false;
        RemoveAt(index);
        return true;
    }

    bool ICollection<T>.Remove(T item) => TryRemove(item);

    /// <summary>
    /// Removes the item at the specified arrayIndex if it has been allocated.
    /// </summary>
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
    /// <param name="index">The arrayIndex of the item to remove.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RemoveAt(int index)
    {
        _innerArray[index] = default;
        _count--;
        _freeIndices.Push(index);
        _version++;
    }

    /// <summary>
    /// Removes all items from the bucket.
    /// </summary>
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

    #endregion

    #region Capacity Management

    /// <summary>
    /// Ensures that the internal storage has at least the specified capacity, expanding it if necessary.
    /// </summary>
    /// <remarks>
    /// If the current capacity is less than the specified value, the internal storage is increased to 
    /// the next power of two greater than or equal to the requested capacity. 
    /// No action is taken if the current capacity is sufficient.
    /// </remarks>
    /// <param name="capacity">The minimum number of elements that the internal storage should be able to hold. Must be a non-negative value.</param>
    public void EnsureCapacity(int capacity)
    {
        capacity = SwiftHashTools.NextPowerOfTwo(capacity);
        if (capacity > _innerArray.Length)
            Resize(capacity);
    }

    private void Resize(int newSize)
    {
        int newCapacity = newSize <= DefaultCapacity ? DefaultCapacity : newSize;
        int copyLength = Math.Min(_peakCount, _innerArray.Length);

        Entry[] newArray = new Entry[newCapacity];
        if (copyLength > 0)
            Array.Copy(_innerArray, 0, newArray, 0, copyLength);
        _innerArray = newArray;

        _version++;
    }

    /// <summary>
    /// Reduces unused tail capacity while preserving stable handles for all currently allocated entries.
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
        _freeIndices = new SwiftIntStack(Math.Max(SwiftIntStack.DefaultCapacity, _peakCount - _count));

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
    /// <param name="key">The arrayIndex of the item to get.</param>
    /// <param name="value">When this method returns, contains the value associated with the specified arrayIndex, if the arrayIndex is found; otherwise, the default value for the type of the value parameter.</param>
    /// <returns><c>true</c> if the bucket contains an element at the specified arrayIndex; otherwise, <c>false</c>.</returns>
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
    /// Determines whether the bucket contains a specific value.
    /// </summary>
    /// <param name="item">The object to locate in the bucket.</param>
    /// <returns><c>true</c> if item is found; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// This method performs a linear search and has a time complexity of O(n).
    /// It is recommended to store the indices returned by the <see cref="Add"/> method for faster lookups using the indexer.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(T item) => IndexOf(item) != -1;

    /// <summary>
    /// Determines whether the <see cref="SwiftBucket{T}"/> contains an element that matches the conditions defined by the specified predicate.
    /// </summary>
    /// <param name="match">The predicate that defines the conditions of the element to search for.</param>
    /// <returns><c>true</c> if the <see cref="SwiftBucket{T}"/> contains one or more elements that match the specified predicate; otherwise, <c>false</c>.</returns>
    public bool Exists(Predicate<T> match)
    {
        SwiftThrowHelper.ThrowIfNull(match, nameof(match));

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
    /// Searches for an element that matches the conditions defined by the specified predicate, and returns the first matching element in bucket iteration order.
    /// </summary>
    /// <param name="match">The predicate that defines the conditions of the element to search for.</param>
    /// <returns>The first element that matches the conditions defined by the specified predicate, if found; otherwise, the default value for type <typeparamref name="T"/>.</returns>
    public T Find(Predicate<T> match)
    {
        SwiftThrowHelper.ThrowIfNull(match, nameof(match));

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
    /// <param name="index">The zero-based index of the element to check. Must be greater than or equal to 0 and less than the length of the underlying array.</param>
    /// <returns>true if the element at the specified index is allocated; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsAllocated(int index) => !((uint)index >= (uint)_innerArray.Length) && _innerArray[index].IsUsed;

    /// <summary>
    /// Searches for the specified object and returns the zero-based arrayIndex of the first occurrence within the bucket.
    /// </summary>
    /// <param name="item">The object to locate in the bucket.</param>
    /// <returns>
    /// The zero-based arrayIndex of the first occurrence of <paramref name="item"/> within the bucket, if found; otherwise, <c>-1</c>.
    /// </returns>
    /// <remarks>
    /// This method performs a linear search and has a time complexity of O(n).
    /// It is recommended to store the indices returned by the <see cref="Add"/> method for faster lookups using the indexer.
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
    /// Copies the elements of the bucket to an <see cref="Array"/>, starting at a particular Array arrayIndex.
    /// </summary>
    /// <param name="array">The one-dimensional Array that is the destination of the elements copied from bucket.</param>
    /// <param name="arrayIndex">The zero-based arrayIndex in array at which copying begins.</param>
    public void CopyTo(T[] array, int arrayIndex)
    {
        SwiftThrowHelper.ThrowIfNull(array, nameof(array));
        if ((uint)arrayIndex > array.Length) throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        if (array.Length - arrayIndex < _count) throw new InvalidOperationException("The array is not large enough to hold the elements.");

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

    void ICollection.CopyTo(Array array, int arrayIndex)
    {
        SwiftThrowHelper.ThrowIfNull(array, nameof(array));
        if ((uint)array.Rank != 1) throw new ArgumentException("Array must be single dimensional.");
        if ((uint)array.GetLowerBound(0) != 0) throw new ArgumentException("Array must have zero-based indexing.");
        if ((uint)arrayIndex > array.Length) throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        if (array.Length - arrayIndex < _count) throw new InvalidOperationException("The array is not large enough to hold the elements.");

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
        catch (ArrayTypeMismatchException)
        {
            throw new ArgumentException("Invalid array type.");
        }
    }

    /// <inheritdoc/>
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
    /// Returns an enumerator that iterates through the <see cref="SwiftBucket{T}"/>.
    /// </summary>
    /// <returns>An enumerator for the bucket.</returns>
    public SwiftBucketEnumerator GetEnumerator() => new(this);
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Enumerates the elements of a <see cref="SwiftBucket{T}"/> collection.
    /// </summary>
    /// <remarks>
    /// The enumerator provides read-only, forward-only iteration over the elements in the <see cref="SwiftBucket{T}"/>. 
    /// The enumerator is invalidated if the collection is modified after the enumerator is created.
    /// </remarks>
    public struct SwiftBucketEnumerator : IEnumerator<T>, IDisposable
    {
        private readonly SwiftBucket<T> _bucket;
        private readonly Entry[] _entries;
        private readonly uint _version;
        private int _index;
        private T _current;

        internal SwiftBucketEnumerator(SwiftBucket<T> bucket)
        {
            _bucket = bucket;
            _entries = bucket._innerArray;
            _version = bucket._version;
            _index = -1;
            _current = default!;
        }

        /// <inheritdoc/>
        public T Current => _current;

        object IEnumerator.Current
        {
            get
            {
                if (_index > (uint)_bucket._count) throw new InvalidOperationException("Bad enumeration");
                return _current!;
            }
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (_version != _bucket._version)
                throw new InvalidOperationException("Enumerator modified outside of enumeration!");

            uint count = (uint)_bucket._peakCount;
            while (++_index < count)
            {
                if (_entries[_index].IsUsed)
                {
                    _current = _entries[_index].Value;
                    return true;
                }
            }
            return false;
        }

        /// <inheritdoc/>
        public void Reset()
        {
            if (_version != _bucket._version)
                throw new InvalidOperationException("Enumerator modified outside of enumeration!");

            _index = -1;
            _current = default!;
        }

        /// <inheritdoc/>
        public void Dispose() => _index = -1;
    }

    #endregion
}
