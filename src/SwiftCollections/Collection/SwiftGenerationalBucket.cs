using MemoryPack;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SwiftCollections;

/// <summary>
/// Represents a high-performance generational bucket that assigns stable handles to stored items.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SwiftGenerationalBucket{T}"/> is similar to <see cref="SwiftBucket{T}"/> but adds
/// <b>generation tracking</b> to prevent stale references from accessing reused slots.
/// </para>
///
/// <para>
/// When an item is added, a <see cref="SwiftHandle"/> containing both an index and generation
/// is returned. If the item is removed and the slot reused later, the generation value
/// changes, causing older handles to automatically become invalid.
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
/// <item><description>Stable handles for the lifetime of stored items.</description></item>
/// <item><description>Automatic invalidation of stale handles via generation counters.</description></item>
/// <item><description>Cache-friendly contiguous storage.</description></item>
/// </list>
/// </para>
///
/// <para>
/// Use <see cref="SwiftBucket{T}"/> when raw indices are acceptable.
/// Use <see cref="SwiftGenerationalBucket{T}"/> when handle safety is required.
/// </para>
/// </remarks>
/// <typeparam name="T">Specifies the type of elements stored in the bucket.</typeparam>
[Serializable]
[JsonConverter(typeof(SwiftStateJsonConverterFactory))]
[MemoryPackable]
public sealed partial class SwiftGenerationalBucket<T> : ISwiftCloneable<T>, IEnumerable<T>
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
    /// Initializes a new instance of the SwiftGenerationalBucket class with the default capacity.
    /// </summary>
    public SwiftGenerationalBucket() : this(DefaultCapacity) { }

    /// <summary>
    /// Initializes a new instance of the SwiftGenerationalBucket class with the specified initial capacity.
    /// </summary>
    /// <remarks>
    /// The actual capacity will be set to the next power of two greater than or equal to the specified capacity, 
    /// or to the default capacity if the specified value is too small. 
    /// This ensures efficient internal storage and lookup performance.
    /// </remarks>
    /// <param name="capacity">
    /// The initial number of elements that the bucket can contain. 
    /// If less than or equal to the default capacity, the default capacity is used. 
    /// Must be a non-negative integer.
    /// </param>
    public SwiftGenerationalBucket(int capacity)
    {
        capacity = capacity <= DefaultCapacity
            ? DefaultCapacity
            : SwiftHashTools.NextPowerOfTwo(capacity);

        _entries = new Entry[capacity];
        _freeIndices = new SwiftIntStack(capacity);
    }

    ///  <summary>
    ///  Initializes a new instance of the <see cref="SwiftGenerationalBucket{T}"/> class with the specified <see cref="SwiftBucketState{T}"/>.
    ///  </summary>
    ///  <param name="state">The state containing the internal array, count, offset, and version for initialization.</param>
    [MemoryPackConstructor]
    public SwiftGenerationalBucket(SwiftGenerationalBucketState<T> state)
    {
        State = state;
        // Ensure that the internal structures are initialized even if the state is null or incomplete.
        _entries ??= new Entry[DefaultCapacity];
        _freeIndices ??= new SwiftIntStack(DefaultCapacity);
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
    /// Gets or sets the current state of the generational bucket.
    /// </summary>
    /// <remarks>
    /// This property provides a snapshot of the bucket's internal state, which can be used for serialization, diagnostics, 
    /// or restoring the bucket to a previous state. 
    /// Setting this property replaces the entire state of the bucket, including its contents and allocation metadata.
    /// </remarks>
    [JsonInclude]
    [MemoryPackInclude]
    public SwiftGenerationalBucketState<T> State
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

            return new SwiftGenerationalBucketState<T>(
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

            int sourceLength = Math.Max(items.Length, Math.Max(allocated.Length, generations.Length));
            int capacity = sourceLength < DefaultCapacity
                ? DefaultCapacity
                : SwiftHashTools.NextPowerOfTwo(sourceLength);

            _entries = new Entry[capacity];
            _freeIndices = new SwiftIntStack(Math.Max(SwiftIntStack.DefaultCapacity, freeIndices.Length));

            _count = 0;
            int maxReferencedIndex = -1;

            for (int i = 0; i < sourceLength; i++)
            {
                ref Entry entry = ref _entries[i];

                entry.Generation = generations.Length > i ? generations[i] : 0;
                if (entry.Generation != 0)
                    maxReferencedIndex = i;

                if (allocated.Length > i && allocated[i])
                {
                    if (items.Length > i)
                        entry.Value = items[i];

                    entry.IsUsed = true;
                    _count++;
                    maxReferencedIndex = i;
                }
            }

            foreach (var index in freeIndices)
            {
                if ((uint)index >= (uint)capacity)
                    throw new ArgumentException("Free index is out of range.");

                _freeIndices.Push(index);
                if (index > maxReferencedIndex)
                    maxReferencedIndex = index;
            }

            int peak = value.Peak;
            if (peak < 0)
                peak = 0;

            _peak = Math.Max(peak, maxReferencedIndex + 1);
            if (_peak > capacity)
                _peak = capacity;

            _version = 0;
        }
    }

    #endregion

    #region Core Operations

    /// <summary>
    /// Adds the specified value to the collection and returns a handle that can be used to reference it.
    /// </summary>
    /// <remarks>
    /// The returned handle can be used to access or remove the value later. 
    /// Handles are only valid as long as the value remains in the collection.
    /// </remarks>
    /// <param name="value">The value to add to the collection.</param>
    /// <returns>A <see cref="SwiftHandle"/> that uniquely identifies the added value within the collection.</returns>
    public SwiftHandle Add(T value)
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

        return new SwiftHandle(index, entry.Generation);
    }

    /// <summary>
    /// Attempts to retrieve the value associated with the specified handle.
    /// </summary>
    /// <remarks>
    /// Use this method to safely attempt retrieval without throwing an exception if the handle is invalid or the entry is not in use.
    /// </remarks>
    /// <param name="handle">The handle used to identify the entry to retrieve.</param>
    /// <param name="value">
    /// When this method returns, contains the value associated with the specified handle if the handle is valid 
    /// and the entry is in use; otherwise, the default value for the type of the value parameter. 
    /// This parameter is passed uninitialized.
    /// </param>
    /// <returns>true if the value was found and retrieved successfully; otherwise, false.</returns>
    public bool TryGet(SwiftHandle handle, out T value)
    {
        if ((uint)handle.Index >= (uint)_entries.Length)
        {
            value = default!;
            return false;
        }

        ref Entry entry = ref _entries[handle.Index];

        if (!entry.IsUsed || entry.Generation != handle.Generation)
        {
            value = default!;
            return false;
        }

        value = entry.Value;
        return true;
    }

    /// <summary>
    /// Returns a reference to the value associated with the specified handle.
    /// </summary>
    /// <param name="handle">
    /// A handle that identifies the entry whose value is to be accessed. 
    /// The handle must be valid and refer to an existing entry.
    /// </param>
    /// <returns>A reference to the value of type T associated with the specified handle.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the handle does not refer to a valid or currently used entry.</exception>
    public ref T GetRef(SwiftHandle handle)
    {
        ref Entry entry = ref _entries[handle.Index];

        if (!entry.IsUsed || entry.Generation != handle.Generation)
            throw new InvalidOperationException("Invalid handle");

        return ref entry.Value;
    }

    /// <summary>
    /// Removes the entry associated with the specified handle from the collection.
    /// </summary>
    /// <remarks>
    /// If the handle does not refer to a valid or currently used entry, the method returns false and no action is taken. 
    /// Removing an entry invalidates the handle for future operations.
    /// </remarks>
    /// <param name="handle">The handle identifying the entry to remove. The handle must refer to a valid, currently used entry.</param>
    /// <returns>true if the entry was successfully removed; otherwise, false.</returns>
    public bool Remove(SwiftHandle handle)
    {
        if ((uint)handle.Index >= (uint)_entries.Length)
            return false;

        ref Entry entry = ref _entries[handle.Index];

        if (!entry.IsUsed || entry.Generation != handle.Generation)
            return false;

        entry.Value = default!;
        entry.IsUsed = false;

        entry.Generation++;

        _freeIndices.Push(handle.Index);

        _count--;
        _version++;

        return true;
    }

    /// <summary>
    /// Determines whether the specified handle refers to a valid and currently used entry.
    /// </summary>
    /// <remarks>
    /// A handle may become invalid if the referenced entry has been removed or replaced. 
    /// Use this method to check handle validity before accessing the associated entry.
    /// </remarks>
    /// <param name="handle">The handle to validate. The handle must have been obtained from this collection; otherwise, the result is undefined.</param>
    /// <returns>true if the handle is valid and refers to an active entry; otherwise, false.</returns>
    public bool IsValid(SwiftHandle handle)
    {
        if ((uint)handle.Index >= (uint)_entries.Length)
            return false;

        ref Entry entry = ref _entries[handle.Index];

        return entry.IsUsed && entry.Generation == handle.Generation;
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
        int newCapacity = newSize <= DefaultCapacity ? DefaultCapacity : newSize;

        Entry[] newArray = new Entry[newCapacity];
        Array.Copy(_entries, newArray, _entries.Length);
        _entries = newArray;
    }

    #endregion

    #region Utility

    /// <inheritdoc/>
    public void CloneTo(ICollection<T> output)
    {
        SwiftThrowHelper.ThrowIfNull(output, nameof(output));

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
    /// Determines whether the <see cref="SwiftGenerationalBucket{T}"/> contains an element that matches the conditions defined by the specified predicate.
    /// </summary>
    /// <param name="match">The predicate that defines the conditions of the element to search for.</param>
    /// <returns><c>true</c> if the <see cref="SwiftGenerationalBucket{T}"/> contains one or more elements that match the specified predicate; otherwise, <c>false</c>.</returns>
    public bool Exists(Predicate<T> match)
    {
        SwiftThrowHelper.ThrowIfNull(match, nameof(match));

        uint count = 0;
        uint peak = (uint)_peak;

        for (uint i = 0; i < peak && count < (uint)_count; i++)
        {
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
    /// Searches for an element that matches the conditions defined by the specified predicate, and returns the first matching element in bucket iteration order.
    /// </summary>
    /// <param name="match">The predicate that defines the conditions of the element to search for.</param>
    /// <returns>The first element that matches the conditions defined by the specified predicate, if found; otherwise, the default value for type <typeparamref name="T"/>.</returns>
    public T Find(Predicate<T> match)
    {
        SwiftThrowHelper.ThrowIfNull(match, nameof(match));

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
    public SwiftGenerationalBucketEnumerator GetEnumerator() => new(this);
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Enumerates the elements of a <see cref="SwiftGenerationalBucket{T}"/> collection in a forward-only, read-only manner.
    /// </summary>
    /// <remarks>
    /// The enumerator is invalidated if the underlying collection is modified during enumeration. 
    /// In such cases, subsequent calls to MoveNext will throw an InvalidOperationException. 
    /// This enumerator is typically obtained by calling GetEnumerator on a <see cref="SwiftGenerationalBucket{T}"/> instance.
    /// </remarks>
    public struct SwiftGenerationalBucketEnumerator : IEnumerator<T>
    {
        private readonly SwiftGenerationalBucket<T> _bucket;
        private readonly uint _version;
        private int _index;
        private T _current;

        internal SwiftGenerationalBucketEnumerator(SwiftGenerationalBucket<T> bucket)
        {
            _bucket = bucket;
            _version = bucket._version;
            _index = -1;
            _current = default!;
        }

        /// <inheritdoc/>
        public readonly T Current => _current;

        readonly object IEnumerator.Current => _current ?? throw new InvalidOperationException();

        /// <inheritdoc/>
        public bool MoveNext()
        {
            if (_version != _bucket._version)
                throw new InvalidOperationException("Collection modified");

            uint peak = (uint)_bucket._peak;
            while (++_index < peak)
            {
                if (_bucket._entries[_index].IsUsed)
                {
                    _current = _bucket._entries[_index].Value;
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc/>
        public void Reset()
        {
            _index = -1;
            _current = default!;
        }

        /// <inheritdoc/>
        public void Dispose() => _index = -1;
    }

    #endregion
}
