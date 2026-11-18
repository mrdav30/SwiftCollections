//=======================================================================
// SwiftSparseMap.cs
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
/// Stores values for externally owned non-negative integer IDs in parallel dense arrays.
/// </summary>
/// <typeparam name="T">Value type stored by key.</typeparam>
/// <remarks>
/// Copies are shallow and instances require external synchronization. Enumeration follows dense order. Removal
/// swaps the final dense entry into the gap; dense indices are unstable. Sparse storage scales with the highest
/// ID, not Count. Direct key writes corrupt membership and bypass version checks.
/// </remarks>
[Serializable]
[JsonConverter(typeof(StateJsonConverterFactory))]
[MemoryPackable]
public sealed partial class SwiftSparseMap<T> : IStateBacked<SwiftSparseMapState<T>>, ISwiftCloneable<T>, IEnumerable<KeyValuePair<int, T>>, IEnumerable
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
    /// <param name="sparseCapacity">The requested sparse lookup length (largest ID plus one); zero permits
    /// initially empty storage and negative constructor capacities are rejected.</param>
    /// <param name="denseCapacity">The requested element storage capacity; zero permits initially empty storage
    /// and negative constructor capacities are rejected.</param>
    /// <exception cref="T:System.ArgumentOutOfRangeException">A requested capacity is negative.</exception>
    public SwiftSparseMap(int sparseCapacity, int denseCapacity)
    {
        ChronicleGuard.ThrowIfNegative(sparseCapacity, nameof(sparseCapacity));
        ChronicleGuard.ThrowIfNegative(denseCapacity, nameof(denseCapacity));

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
    /// <param name="state">The state to restore by shallow-copying arrays. Null item arrays are rejected; IDs
    /// must be unique, non-negative and below Int32.MaxValue.</param>
    /// <exception cref="T:System.ArgumentNullException">A required state array is null.</exception>
    /// <exception cref="T:System.ArgumentException">Dense key/value lengths differ, a key is negative, or a key
    /// is duplicated.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">A key is Int32.MaxValue and cannot be directly
    /// indexed.</exception>
    /// <remarks>
    /// Input arrays are copied, not retained. Default state has null arrays and is rejected.
    /// </remarks>
    [MemoryPackConstructor]
    public SwiftSparseMap(SwiftSparseMapState<T> state)
    {
        _sparse = Array.Empty<int>();
        _denseKeys = Array.Empty<int>();
        _denseValues = Array.Empty<T>();

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
    /// Capacity of the dense arrays (Keys/Values storage).
    /// </summary>
    /// <value>The allocated dense-array length; only the first Count entries are live.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int DenseCapacity => _denseKeys.Length;

    /// <summary>
    /// Capacity of the sparse array (max key+1 that can be mapped without resizing). Memory usage grows with this
    /// capacity.
    /// </summary>
    /// <value>The lookup-array length; directly indexed IDs must be below this length or require growth.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int SparseCapacity => _sparse.Length;

    /// <summary>
    /// Gets a value indicating whether access to the collection is synchronized (thread safe).
    /// </summary>
    /// <value>false.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public bool IsSynchronized => false;

    /// <summary>
    /// Gets an object that can be used to synchronize access to the collection.
    /// </summary>
    /// <value>A lazily allocated synchronization object; access is not locked automatically.</value>
    /// <remarks>
    /// Use this object to lock the collection during multithreaded operations to ensure thread safety. The
    /// returned object is unique to this collection instance.
    /// </remarks>
    [JsonIgnore]
    [MemoryPackIgnore]
    public object SyncRoot => _syncRoot ??= new object();

    /// <summary>
    /// Returns the dense keys array (valid range: [0..Count)).
    /// </summary>
    /// <value>A borrowed array including spare slots in dense order.</value>
    /// <remarks>
    /// No copy or allocation. Structural operations can reorder or replace dense storage; do not retain across
    /// them. Key writes are not validated and can corrupt the sparse lookup.
    /// </remarks>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int[] DenseKeys => _denseKeys;

    /// <summary>
    /// Gets a span containing the keys currently stored in the collection.
    /// </summary>
    /// <value>A borrowed span of Count live entries in dense order.</value>
    /// <remarks>
    /// No copy or allocation. Structural operations can reorder or replace dense storage; do not retain across
    /// them. Key writes are not validated and can corrupt the sparse lookup.
    /// </remarks>
    [JsonIgnore]
    [MemoryPackIgnore]
    public Span<int> Keys => _denseKeys.AsSpan(0, _count);

    /// <summary>
    /// Returns the dense values array (valid range: [0..Count)).
    /// </summary>
    /// <value>A borrowed array including spare slots in dense order.</value>
    /// <remarks>
    /// No copy or allocation. Structural operations can reorder or replace dense storage; do not retain across
    /// them. Value writes are visible immediately and bypass enumerator version checks.
    /// </remarks>
    [JsonIgnore]
    [MemoryPackIgnore]
    public T[] DenseValues => _denseValues;

    /// <summary>
    /// Gets a span containing the current values in the collection.
    /// </summary>
    /// <value>A borrowed span of Count live entries in dense order.</value>
    /// <remarks>
    /// No copy or allocation. Structural operations can reorder or replace dense storage; do not retain across
    /// them. Value writes are visible immediately and bypass enumerator version checks.
    /// </remarks>
    [JsonIgnore]
    [MemoryPackIgnore]
    public Span<T> Values => _denseValues.AsSpan(0, _count);

    /// <summary>
    /// Gets/sets the value for a key. Setting: - overwrites if present - inserts if not present
    /// </summary>
    /// <param name="key">The externally owned non-negative ID, below Int32.MaxValue; inserting requires lookup
    /// storage proportional to ID plus one.</param>
    /// <value>The stored value. Setting an absent key inserts it; setting an existing key replaces its
    /// value.</value>
    /// <exception cref="T:System.Collections.Generic.KeyNotFoundException">The getter key is absent, negative, or
    /// outside the sparse lookup.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">An insertion key is negative or
    /// Int32.MaxValue.</exception>
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
    /// Gets a shallow snapshot of storage and allocation metadata.
    /// </summary>
    /// <value>A state with copied arrays; referenced elements remain shared.</value>
    /// <remarks>
    /// Stores live dense entries in order and rebuilds sparse membership on restore. Negative/duplicate IDs and
    /// inconsistent map key/value lengths are rejected.
    /// </remarks>
    [JsonInclude]
    [MemoryPackInclude]
    public SwiftSparseMapState<T> State
    {
        get
        {
            // Serialize only the used portions of dense arrays
            var denseKeys = new int[_count];
            Array.Copy(_denseKeys, denseKeys, _count);

            var denseValues = new T[_count];
            Array.Copy(_denseValues, denseValues, _count);

            return new SwiftSparseMapState<T>(denseKeys, denseValues);
        }
        internal set
        {
            ChronicleGuard.ThrowIfNull(value.DenseKeys);
            ChronicleGuard.ThrowIfNull(value.DenseValues);

            int n = value.DenseKeys.Length;

            ChronicleGuard.ThrowIfArgument(n != value.DenseValues.Length, nameof(value), "DenseKeys and DenseValues length mismatch.");

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
                ChronicleGuard.ThrowIfArgument(key < 0, nameof(value), "Key cannot be negative.");

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

                ChronicleGuard.ThrowIfArgument(_sparse[key] != NotPresent, nameof(value), "Duplicate key in DenseKeys.");

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
    /// <param name="key">The externally owned non-negative ID, below Int32.MaxValue; inserting requires lookup
    /// storage proportional to ID plus one.</param>
    /// <returns>true if the requested live slot, token, ID or value exists; false for absent/out-of-range inputs.
    /// No mutation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsKey(int key)
    {
        if ((uint)key >= (uint)_sparse.Length) return false;
        return _sparse[key] != NotPresent;
    }

    /// <summary>
    /// Adds a key/value only if the key is not present. Returns false if already present.
    /// </summary>
    /// <param name="key">The externally owned non-negative ID, below Int32.MaxValue; inserting requires lookup
    /// storage proportional to ID plus one.</param>
    /// <param name="value">The value to store without cloning.</param>
    /// <returns>true if inserted; false if the ID already exists, preserving the existing value. TryAdd still
    /// throws for invalid ID ranges or allocation failure.</returns>
    /// <exception cref="T:System.ArgumentOutOfRangeException">The ID is negative or Int32.MaxValue.</exception>
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
    /// Inserts or overwrites the value for an ID.
    /// </summary>
    /// <param name="key">The externally owned non-negative ID, below Int32.MaxValue; inserting requires lookup
    /// storage proportional to ID plus one.</param>
    /// <param name="value">The value to store without cloning.</param>
    /// <exception cref="T:System.ArgumentOutOfRangeException">The ID is negative or Int32.MaxValue.</exception>
    /// <remarks>
    /// This Add form is an upsert. Use TryAdd to preserve an existing mapping.
    /// </remarks>
    public void Add(int key, T value) => this[key] = value;

    /// <summary>
    /// Attempts to retrieve the value associated with the specified key.
    /// </summary>
    /// <param name="key">The externally owned non-negative ID, below Int32.MaxValue; inserting requires lookup
    /// storage proportional to ID plus one.</param>
    /// <param name="value">Receives the stored value on success; default(T) on failure.</param>
    /// <returns>true if the slot/token/ID identifies a live entry; false otherwise, with the out value set to
    /// default(T). No mutation.</returns>
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
    /// <param name="key">The externally owned non-negative ID, below Int32.MaxValue; inserting requires lookup
    /// storage proportional to ID plus one.</param>
    /// <returns>true if a matching live entry was removed; false if absent or invalid, with contents unchanged.
    /// Swap-back removal can change dense order.</returns>
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
    /// Empties the collection while preserving capacity. The Boolean overload also releases unused retained value
    /// references when applicable.
    /// </remarks>
    public void Clear()
    {
        if (_count == 0) return;

        // reset sparse for keys that were present
        for (int i = 0; i < _count; i++)
        {
            int key = _denseKeys[i];
            _sparse[key] = NotPresent;
        }

        Array.Clear(_denseKeys, 0, _count);
        Array.Clear(_denseValues, 0, _count);

        _count = 0;
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
            Array.Clear(_denseValues, _count, _denseValues.Length - _count);
        Clear();
    }

    #endregion

    #region Capacity Management

    /// <summary>
    /// Ensures that the internal dense storage has at least the specified capacity, expanding it if necessary.
    /// </summary>
    /// <param name="capacity">The minimum storage length; requests at or below the current capacity change
    /// nothing. Keep requests below 2^30 to avoid the capacity sentinel.</param>
    /// <remarks>
    /// If the current capacity is less than the specified value, the internal storage is resized to accommodate
    /// at least that many elements. Existing elements are preserved. The capacity is increased to the next power
    /// of two greater than or equal to the requested capacity for performance reasons.
    /// </remarks>
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
    /// <param name="capacity">The minimum storage length; requests at or below the current capacity change
    /// nothing. Keep requests below 2^30 to avoid the capacity sentinel.</param>
    /// <remarks>
    /// If the current capacity is less than the specified value, the internal storage is resized to accommodate
    /// at least that many elements. Existing elements are preserved. The capacity is increased to the next power
    /// of two greater than or equal to the requested capacity for performance reasons.
    /// </remarks>
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
    /// Reduces the memory usage of the collection by resizing internal storage to fit the current number of
    /// elements as closely as possible.
    /// </summary>
    /// <remarks>
    /// Call this method to minimize the collection's memory footprint after removing a significant number of
    /// elements. This operation may improve memory efficiency but can be an expensive operation if the collection
    /// is large. The method does not affect the logical contents of the collection.
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

    /// <summary>
    /// Creates an enumerator over the live entries.
    /// </summary>
    /// <returns>A value-type enumerator over live entries in dense order.</returns>
    /// <remarks>
    /// MoveNext and Reset check the mutation version. Direct storage/ref writes bypass the version check.
    /// </remarks>
    public SwiftSparseMapEnumerator GetEnumerator() => new(this);
    /// <summary>
    /// Creates an enumerator through the implemented interface.
    /// </summary>
    /// <returns>An enumerator with the concrete collection's ordering and mutation rules. Interface dispatch may
    /// box the value-type enumerator.</returns>
    IEnumerator<KeyValuePair<int, T>> IEnumerable<KeyValuePair<int, T>>.GetEnumerator() => GetEnumerator();
    /// <summary>
    /// Creates an enumerator through the implemented interface.
    /// </summary>
    /// <returns>An enumerator with the concrete collection's ordering and mutation rules. Interface dispatch may
    /// box the value-type enumerator.</returns>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();



    #endregion

    #region Helpers

    /// <summary>
    /// Retrieves the dense representation of the collection as parallel arrays of keys and values, along with the
    /// number of elements contained.
    /// </summary>
    /// <param name="keys">Receives the borrowed dense backing array; only indices below count are live. Do not
    /// modify keys.</param>
    /// <param name="values">Receives the borrowed dense value array; only indices below count are live. Writes
    /// bypass versioning.</param>
    /// <param name="count">Receives the number of live entries in the returned arrays at this instant.</param>
    /// <remarks>
    /// Returns borrowed storage without allocation. Structural mutation can reorder or replace these arrays;
    /// count is a snapshot, not a live binding. Do not write key arrays or retain views across mutation.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetDense(out int[] keys, out T[] values, out int count)
    {
        keys = _denseKeys;
        values = _denseValues;
        count = _count;
    }

    /// <summary>
    /// Replaces the destination list contents with this map's keys in dense iteration order.
    /// </summary>
    /// <param name="destination">The caller-owned list, cleared before receiving copied IDs; source storage is
    /// not retained by the list.</param>
    /// <exception cref="T:System.ArgumentNullException">destination is null.</exception>
    /// <remarks>
    /// Clears destination before copying IDs in current dense order. The source remains unchanged.
    /// </remarks>
    public void CopyKeysTo(SwiftList<int> destination)
    {
        ChronicleGuard.ThrowIfNull(destination, nameof(destination));

        destination.FastClear();
        destination.AddRange(_denseKeys.AsSpan(0, _count));
    }

    /// <summary>
    /// Replaces the destination list contents with this map's keys sorted in ascending order.
    /// </summary>
    /// <param name="destination">The caller-owned list, cleared before receiving copied IDs; source storage is
    /// not retained by the list.</param>
    /// <exception cref="T:System.ArgumentNullException">destination is null.</exception>
    /// <remarks>
    /// Clears destination before copying IDs in ascending numeric order. The source remains unchanged.
    /// </remarks>
    public void CopySortedKeysTo(SwiftList<int> destination)
    {
        CopyKeysTo(destination);
        destination.SortInPlace(default(SwiftIntAscendingComparer));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetRequiredSparseCapacity(int key)
    {
        ChronicleGuard.ThrowIfNegative(key, nameof(key));
        ChronicleGuard.ThrowIfArgumentOutOfRange(key == int.MaxValue, key, nameof(key), "Key is too large for direct sparse indexing.");

        return key + 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetDenseIndexOrThrow(int key)
    {
        ChronicleGuard.ThrowIfKeyNotFound((uint)key >= (uint)_sparse.Length, key);

        int slot = _sparse[key];

        ChronicleGuard.ThrowIfKeyNotFound(slot == NotPresent, key);

        return slot - 1;
    }

    /// <inheritdoc />
    public void CloneTo(ICollection<T> output)
    {
        ChronicleGuard.ThrowIfNull(output, nameof(output));

        output.Clear();

        for (int i = 0; i < _count; i++)
            output.Add(_denseValues[i]);
    }

    #endregion
}
