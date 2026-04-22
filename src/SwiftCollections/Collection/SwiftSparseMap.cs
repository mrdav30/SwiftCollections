using MemoryPack;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace SwiftCollections;

/// <summary>
/// Represents a high-performance sparse map that stores values indexed by externally supplied integer keys.
/// Provides O(1) Add, Remove, Contains, and lookup operations while maintaining densely packed storage
/// for cache-friendly iteration.
/// </summary>
/// <remarks>
/// Unlike <see cref="SwiftBucket{T}"/>, which internally assigns and manages item indices,
/// <see cref="SwiftSparseMap{T}"/> is externally keyed. The caller supplies the integer key
/// (for example, an entity ID or handle) used to index the value.
///
/// Internally, the container maintains:
/// <list type="bullet">
///     <item>
///         <description>A sparse lookup table mapping keys to dense indices.</description>
///     </item>
///     <item>
///         <description>A dense array of keys.</description>
///     </item>
///     <item>
///         <description>A dense array of values.</description>
///     </item>
/// </list>
///
/// Removal uses a swap-back strategy to keep dense storage contiguous. As a result,
/// iteration order is not guaranteed to remain stable.
///
/// Keys are used as direct indices into the sparse lookup table, so memory usage scales
/// with the highest stored key rather than the number of stored values. This container is
/// intended for compact, non-negative IDs such as entity handles or slot indices. It is
/// not a good fit for arbitrary hashes or widely spaced keys; for those workloads prefer
/// <c>SwiftDictionary&lt;TKey, TValue&gt;</c>.
/// </remarks>
/// <typeparam name="T">Value type stored by key.</typeparam>
[Serializable]
[JsonConverter(typeof(SwiftStateJsonConverterFactory))]
[MemoryPackable]
public sealed partial class SwiftSparseMap<T> : ISwiftCloneable<T>, IEnumerable<KeyValuePair<int, T>>, IEnumerable
{
    #region Constants

    /// <summary>
    /// Represents the default initial capacity for dense collections.
    /// </summary>
    public const int DefaultDenseCapacity = 8;

    /// <summary>
    /// Represents the default initial capacity for sparse collections.
    /// </summary>
    public const int DefaultSparseCapacity = 8;

    /// <summary>
    /// Represents the value used to indicate that a key is not present in the sparse array.
    /// </summary>
    /// <remarks>
    /// A value of 0 signifies that the key is absent. 
    /// When a key is present, the stored value is the dense index plus one.
    /// </remarks>
    private const int NotPresent = 0;

    #endregion

    #region Fields

    private int[] _sparse;       // key -> denseIndex+1
    private int[] _denseKeys;    // denseIndex -> key
    private T[] _denseValues;    // denseIndex -> value
    private int _count;

    [NonSerialized]
    private uint _version;

    [NonSerialized]
    private object? _syncRoot;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the SwiftSparseMap class with default sparse and dense capacities.
    /// </summary>
    public SwiftSparseMap() : this(DefaultSparseCapacity, DefaultDenseCapacity) { }

    /// <summary>
    /// Initializes a new sparse map with the specified sparse and dense capacities.
    /// </summary>
    /// <param name="sparseCapacity">
    /// Initial sparse lookup capacity. This should track the highest expected key plus one,
    /// not the number of stored values.
    /// </param>
    /// <param name="denseCapacity">Initial dense storage capacity for values.</param>
    public SwiftSparseMap(int sparseCapacity, int denseCapacity)
    {
        SwiftThrowHelper.ThrowIfNegative(sparseCapacity, nameof(sparseCapacity));
        SwiftThrowHelper.ThrowIfNegative(denseCapacity, nameof(denseCapacity));

        int sparseSize = sparseCapacity == 0 ? 0 : SwiftHashTools.NextPowerOfTwo(sparseCapacity);
        _sparse = sparseCapacity == 0
            ? Array.Empty<int>()
            : new int[sparseSize];
        int denseSize = denseCapacity < DefaultDenseCapacity
            ? DefaultDenseCapacity
            : SwiftHashTools.NextPowerOfTwo(denseCapacity);
        _denseKeys = denseCapacity == 0
            ? Array.Empty<int>()
            : new int[denseSize];
        _denseValues = _denseKeys.Length == 0 ? Array.Empty<T>() : new T[_denseKeys.Length];

        _count = 0;
    }

    /// <summary>
    /// Initializes a new instance of the SwiftSparseMap class using the specified state.
    /// </summary>
    /// <param name="state">The state object that provides the initial configuration and data for the map. Cannot be null.</param>
    [MemoryPackConstructor]
    public SwiftSparseMap(SwiftSparseSetState<T> state)
    {
        State = state;

        _sparse ??= new int[DefaultSparseCapacity];
        _denseKeys ??= new int[DefaultDenseCapacity];
        _denseValues ??= new T[DefaultDenseCapacity];
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
    /// Capacity of the dense arrays (Keys/Values storage).
    /// </summary>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int DenseCapacity => _denseKeys.Length;

    /// <summary>
    /// Capacity of the sparse array (max key+1 that can be mapped without resizing).
    /// Memory usage grows with this capacity.
    /// </summary>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int SparseCapacity => _sparse.Length;

    /// <summary>
    /// Gets a value indicating whether access to the collection is synchronized (thread safe).
    /// </summary>
    [JsonIgnore]
    [MemoryPackIgnore]
    public bool IsSynchronized => false;

    /// <summary>
    /// Gets an object that can be used to synchronize access to the collection.
    /// </summary>
    /// <remarks>
    /// Use this object to lock the collection during multithreaded operations to ensure thread safety. 
    /// The returned object is unique to this collection instance.
    /// </remarks>
    [JsonIgnore]
    [MemoryPackIgnore]
    public object SyncRoot => _syncRoot ??= new object();

    /// <summary>
    /// Returns the dense keys array (valid range: [0..Count)).
    /// </summary>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int[] DenseKeys => _denseKeys;

    /// <summary>
    /// Gets a span containing the keys currently stored in the collection.
    /// </summary>
    /// <remarks>
    /// The returned span provides a view of the underlying key data and reflects the current state of the collection. 
    /// Modifying the span will affect the collection's contents. 
    /// The span is only valid as long as the underlying collection is not modified.
    /// </remarks>
    [JsonIgnore]
    [MemoryPackIgnore]
    public Span<int> Keys => _denseKeys.AsSpan(0, _count);

    /// <summary>
    /// Returns the dense values array (valid range: [0..Count)).
    /// </summary>
    [JsonIgnore]
    [MemoryPackIgnore]
    public T[] DenseValues => _denseValues;

    /// <summary>
    /// Gets a span containing the current values in the collection.
    /// </summary>
    /// <remarks>
    /// The returned span reflects the live contents of the collection up to the current count.
    /// Modifying the span will update the underlying collection data. 
    /// The span length is equal to the number of elements currently stored.
    /// </remarks>
    [JsonIgnore]
    [MemoryPackIgnore]
    public Span<T> Values => _denseValues.AsSpan(0, _count);

    /// <summary>
    /// Gets/sets the value for a key. Setting:
    /// - overwrites if present
    /// - inserts if not present
    /// </summary>
    [JsonIgnore]
    [MemoryPackIgnore]
    public T this[int key]
    {
        get
        {
            int denseIndex = GetDenseIndexOrThrow(key);
            return _denseValues[denseIndex];
        }
        set
        {
            EnsureSparseCapacity(GetRequiredSparseCapacity(key));

            int slot = _sparse[key];
            if (slot != NotPresent)
            {
                // present -> overwrite
                int denseIndex = slot - 1;
                _denseValues[denseIndex] = value;
                _version++;
                return;
            }

            // not present -> insert
            EnsureDenseCapacity(_count + 1);

            int newIndex = _count++;
            _denseKeys[newIndex] = key;
            _denseValues[newIndex] = value;
            _sparse[key] = newIndex + 1;

            _version++;
        }
    }

    /// <summary>
    /// Gets or sets the current state of the sparse set, including the used dense keys and values.
    /// </summary>
    /// <remarks>
    /// The state includes only the active elements in the set. 
    /// Setting this property replaces the current contents with the provided state. 
    /// The setter is intended for internal use, such as serialization or deserialization scenarios.
    /// </remarks>
    [JsonInclude]
    [MemoryPackInclude]
    public SwiftSparseSetState<T> State
    {
        get
        {
            // Serialize only the used portions of dense arrays
            var denseKeys = new int[_count];
            Array.Copy(_denseKeys, denseKeys, _count);

            var denseValues = new T[_count];
            Array.Copy(_denseValues, denseValues, _count);

            return new SwiftSparseSetState<T>(denseKeys, denseValues);
        }
        internal set
        {
            SwiftThrowHelper.ThrowIfNull(value.DenseKeys);
            SwiftThrowHelper.ThrowIfNull(value.DenseValues);

            int n = value.DenseKeys.Length;

            if (n != value.DenseValues.Length)
                throw new ArgumentException("DenseKeys and DenseValues length mismatch.");

            // Allocate dense storage
            _denseKeys = n == 0 ? Array.Empty<int>() : new int[Math.Max(DefaultDenseCapacity, n)];
            _denseValues = n == 0 ? Array.Empty<T>() : new T[_denseKeys.Length];

            if (n > 0)
            {
                Array.Copy(value.DenseKeys, _denseKeys, n);
                Array.Copy(value.DenseValues, _denseValues, n);
            }

            _count = n;

            // Compute maxKey from dense keys
            int maxKey = -1;
            for (int i = 0; i < n; i++)
            {
                int key = _denseKeys[i];
                if (key < 0)
                    throw new ArgumentException("Key cannot be negative.");

                if (key > maxKey)
                    maxKey = key;
            }

            // Allocate sparse map
            int sparseSize = maxKey < 0
                ? DefaultSparseCapacity
                : Math.Max(DefaultSparseCapacity, GetRequiredSparseCapacity(maxKey));
            _sparse = new int[sparseSize];

            // Rebuild sparse lookup
            for (int i = 0; i < n; i++)
            {
                int key = _denseKeys[i];

                if (_sparse[key] != NotPresent)
                    throw new ArgumentException("Duplicate key in DenseKeys.");

                _sparse[key] = i + 1;
            }

            _version++;
        }
    }

    #endregion

    #region Core Operations

    /// <summary>
    /// Determines whether the collection contains the specified key.
    /// </summary>
    /// <param name="key">The key to locate in the collection.</param>
    /// <returns>true if the collection contains an element with the specified key; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsKey(int key)
    {
        if ((uint)key >= (uint)_sparse.Length) return false;
        return _sparse[key] != NotPresent;
    }

    /// <summary>
    /// Adds a key/value only if the key is not present.
    /// Returns false if already present.
    /// </summary>
    public bool TryAdd(int key, T value)
    {
        EnsureSparseCapacity(GetRequiredSparseCapacity(key));
        if (_sparse[key] != NotPresent)
            return false;

        EnsureDenseCapacity(_count + 1);

        int newIndex = _count++;
        _denseKeys[newIndex] = key;
        _denseValues[newIndex] = value;
        _sparse[key] = newIndex + 1;

        _version++;
        return true;
    }

    /// <summary>
    /// Adds or overwrites (same behavior as indexer set).
    /// </summary>
    public void Add(int key, T value) => this[key] = value;

    /// <summary>
    /// Attempts to retrieve the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key whose associated value is to be retrieved.</param>
    /// <param name="value">
    /// When this method returns, contains the value associated with the specified key, if the key is found; 
    /// otherwise, the default value for the type parameter <typeparamref name="T"/>. 
    /// This parameter is passed uninitialized.
    /// </param>
    /// <returns>true if the key was found and its value was retrieved; otherwise, false.</returns>
    public bool TryGetValue(int key, out T value)
    {
        if ((uint)key < (uint)_sparse.Length)
        {
            int slot = _sparse[key];
            if (slot != NotPresent)
            {
                value = _denseValues[slot - 1];
                return true;
            }
        }

        value = default!;
        return false;
    }

    /// <summary>
    /// Removes the element with the specified key from the collection, if it exists.
    /// </summary>
    /// <param name="key">The key of the element to remove. Must be a non-negative integer within the valid range of keys.</param>
    /// <returns>true if the element is successfully found and removed; otherwise, false.</returns>
    public bool Remove(int key)
    {
        if ((uint)key >= (uint)_sparse.Length) return false;

        int slot = _sparse[key];
        if (slot == NotPresent) return false;

        int index = slot - 1;
        int last = --_count;

        _sparse[key] = NotPresent;

        if (index != last)
        {
            int movedKey = _denseKeys[last];

            _denseKeys[index] = movedKey;
            _denseValues[index] = _denseValues[last];

            _sparse[movedKey] = index + 1;
        }

        _denseKeys[last] = default;
        _denseValues[last] = default!;

        _version++;
        return true;
    }

    /// <summary>
    /// Removes all keys and values from the collection.
    /// </summary>
    /// <remarks>
    /// After calling this method, the collection will be empty and its Count property will be zero.
    /// This method does not reduce the capacity of the underlying storage.
    /// </remarks>
    public void Clear()
    {
        if (_count == 0) return;

        // reset sparse for keys that were present
        for (int i = 0; i < _count; i++)
        {
            int key = _denseKeys[i];
            if ((uint)key < (uint)_sparse.Length)
                _sparse[key] = NotPresent;
        }

        Array.Clear(_denseKeys, 0, _count);
        Array.Clear(_denseValues, 0, _count);

        _count = 0;
        _version++;
    }

    #endregion

    #region Capacity Management

    /// <summary>
    /// Ensures that the internal dense storage has at least the specified capacity, expanding it if necessary.
    /// </summary>
    /// <remarks>
    /// If the current capacity is less than the specified value, the internal storage is resized to accommodate at least that many elements. 
    /// Existing elements are preserved. 
    /// The capacity is increased to the next power of two greater than or equal to the requested capacity for performance reasons.
    /// </remarks>
    /// <param name="capacity">The minimum number of elements that the dense storage must be able to hold. Must be non-negative.</param>
    public void EnsureDenseCapacity(int capacity)
    {
        if (capacity <= _denseKeys.Length) return;

        int newCap = _denseKeys.Length == 0 ? DefaultDenseCapacity : _denseKeys.Length * 2;
        if (newCap < capacity) newCap = capacity;

        newCap = SwiftHashTools.NextPowerOfTwo(newCap);

        var newKeys = new int[newCap];
        var newVals = new T[newCap];

        if (_count > 0)
        {
            Array.Copy(_denseKeys, newKeys, _count);
            Array.Copy(_denseValues, newVals, _count);
        }

        _denseKeys = newKeys;
        _denseValues = newVals;

        _version++;
    }

    /// <summary>
    /// Ensures that the internal sparse array has a capacity at least as large as the specified value.
    /// </summary>
    /// <remarks>
    /// If the current capacity is less than the specified value, the internal storage is resized to accommodate at least that many elements. 
    /// Existing elements are preserved. 
    /// The capacity is increased to the next power of two greater than or equal to the requested capacity for performance reasons.
    /// </remarks>
    /// <param name="capacity">The minimum required capacity for the internal sparse array. Must be non-negative.</param>
    public void EnsureSparseCapacity(int capacity)
    {
        if (capacity <= _sparse.Length) return;

        int newCap = _sparse.Length == 0
            ? DefaultSparseCapacity
            : _sparse.Length * 2;
        if (newCap < capacity) newCap = capacity;

        newCap = SwiftHashTools.NextPowerOfTwo(newCap);

        var newSparse = new int[newCap];
        if (_sparse.Length > 0)
            Array.Copy(_sparse, newSparse, _sparse.Length);

        _sparse = newSparse;
        _version++;
    }

    /// <summary>
    /// Reduces the memory usage of the collection by resizing internal storage to fit the current number of elements as closely as possible.
    /// </summary>
    /// <remarks>
    /// Call this method to minimize the collection's memory footprint after removing a significant number of elements. 
    /// This operation may improve memory efficiency but can be an expensive operation if the collection is large. 
    /// The method does not affect the logical contents of the collection.
    /// </remarks>
    public void TrimExcess()
    {
        // Dense: shrink to Count (with a minimum)
        int newDense = Math.Max(DefaultDenseCapacity, _count);
        if (newDense < _denseKeys.Length)
        {
            var newKeys = new int[newDense];
            var newVals = new T[newDense];
            if (_count > 0)
            {
                Array.Copy(_denseKeys, newKeys, _count);
                Array.Copy(_denseValues, newVals, _count);
            }
            _denseKeys = newKeys;
            _denseValues = newVals;
        }

        // Sparse: shrink to (maxKey+1) based on dense keys
        int maxKey = -1;
        for (int i = 0; i < _count; i++)
            if (_denseKeys[i] > maxKey) maxKey = _denseKeys[i];

        int newSparse = maxKey < 0
            ? DefaultSparseCapacity
            : Math.Max(DefaultSparseCapacity, GetRequiredSparseCapacity(maxKey));
        if (newSparse < _sparse.Length)
        {
            var newMap = new int[newSparse];
            // rebuild from dense
            for (int i = 0; i < _count; i++)
                newMap[_denseKeys[i]] = i + 1;
            _sparse = newMap;
        }

        _version++;
    }

    #endregion

    #region Enumeration

    /// <inheritdoc cref="IEnumerable.GetEnumerator()"/>
    public SwiftSparseMapEnumerator GetEnumerator() => new(this);
    IEnumerator<KeyValuePair<int, T>> IEnumerable<KeyValuePair<int, T>>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Supports iteration over the key/value pairs in a <see cref="SwiftSparseMap{T}"/> collection.
    /// </summary>
    /// <remarks>
    /// The enumerator provides a forward-only, read-only traversal of the collection. 
    /// It is invalidated if the underlying collection is modified during enumeration, and any such modification will cause
    /// subsequent operations to throw an InvalidOperationException.
    /// </remarks>
    public struct SwiftSparseMapEnumerator : IEnumerator<KeyValuePair<int, T>>
    {
        private readonly SwiftSparseMap<T> _set;
        private readonly int[] _keys;
        private readonly T[] _values;
        private readonly int _count;
        private readonly uint _version;
        private int _index;

        internal SwiftSparseMapEnumerator(SwiftSparseMap<T> set)
        {
            _set = set;
            _keys = set._denseKeys;
            _values = set._denseValues;
            _count = set._count;
            _version = set._version;
            _index = -1;
            Current = default;
        }

        /// <inheritdoc/>
        public KeyValuePair<int, T> Current { get; private set; }
        object IEnumerator.Current => Current;

        /// <inheritdoc/>
        public bool MoveNext()
        {
            if (_version != _set._version)
                throw new InvalidOperationException("Collection was modified during enumeration.");

            int next = _index + 1;
            if (next >= _count)
            {
                Current = default;
                return false;
            }

            _index = next;
            Current = new KeyValuePair<int, T>(_keys[_index], _values[_index]);
            return true;
        }

        /// <inheritdoc/>
        public void Reset()
        {
            if (_version != _set._version)
                throw new InvalidOperationException("Collection was modified during enumeration.");
            _index = -1;
            Current = default;
        }

        /// <inheritdoc/>
        public void Dispose() => _index = -1;
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Retrieves the dense representation of the collection as parallel arrays of keys and values, along with the number of elements contained.
    /// </summary>
    /// <remarks>
    /// The arrays returned may be larger than the actual number of elements. 
    /// Only the first <paramref name="count"/> entries in each array are valid and should be used.
    /// </remarks>
    /// <param name="keys">
    /// When this method returns, contains an array of keys representing the dense mapping. 
    /// The array length is at least as large as the number of elements returned in <paramref name="count"/>.
    /// </param>
    /// <param name="values">
    /// When this method returns, contains an array of values corresponding to the keys in <paramref name="keys"/>. 
    /// The array length is at least as large as the number of elements returned in <paramref name="count"/>.
    /// </param>
    /// <param name="count">When this method returns, contains the number of valid key-value pairs in the dense arrays.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetDense(out int[] keys, out T[] values, out int count)
    {
        keys = _denseKeys;
        values = _denseValues;
        count = _count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetRequiredSparseCapacity(int key)
    {
        if (key < 0)
            throw new ArgumentOutOfRangeException(nameof(key), "Key must be non-negative.");

        if (key == int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(key), "Key is too large for direct sparse indexing.");

        return key + 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetDenseIndexOrThrow(int key)
    {
        if ((uint)key >= (uint)_sparse.Length)
            throw new KeyNotFoundException($"Key not found: {key}");

        int slot = _sparse[key];
        if (slot == NotPresent)
            throw new KeyNotFoundException($"Key not found: {key}");

        return slot - 1;
    }

    /// <inheritdoc/>
    public void CloneTo(ICollection<T> output)
    {
        SwiftThrowHelper.ThrowIfNull(output, nameof(output));

        output.Clear();

        for (int i = 0; i < _count; i++)
            output.Add(_denseValues[i]);
    }

    #endregion
}
