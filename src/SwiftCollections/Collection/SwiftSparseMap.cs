using MemoryPack;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#endif
#if !NET8_0_OR_GREATER
using System.Text.Json.Serialization.Shim;
#endif

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
/// </remarks>
/// <typeparam name="T">Value type stored by key.</typeparam>
[Serializable]
[JsonConverter(typeof(SwiftStateJsonConverterFactory))]
[MemoryPackable]
public sealed partial class SwiftSparseMap<T> : ISwiftCloneable<T>, IEnumerable<KeyValuePair<int, T>>, IEnumerable
{
    #region Constants

    public const int DefaultDenseCapacity = 8;
    public const int DefaultSparseCapacity = 8;

    // sparse[key] stores (denseIndex + 1). 0 means "not present".
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
    private object _syncRoot;

    #endregion

    #region Constructors

    public SwiftSparseMap() : this(DefaultSparseCapacity, DefaultDenseCapacity) { }

    public SwiftSparseMap(int sparseCapacity, int denseCapacity)
    {
        if (sparseCapacity < 0) ThrowArgumentOutOfRange(nameof(sparseCapacity));
        if (denseCapacity < 0) ThrowArgumentOutOfRange(nameof(denseCapacity));

        _sparse = sparseCapacity == 0 ? Array.Empty<int>() : new int[sparseCapacity];
        _denseKeys = denseCapacity == 0 ? Array.Empty<int>() : new int[Math.Max(DefaultDenseCapacity, denseCapacity)];
        _denseValues = _denseKeys.Length == 0 ? Array.Empty<T>() : new T[_denseKeys.Length];

        _count = 0;
    }

    [MemoryPackConstructor]
    public SwiftSparseMap(SwiftSparseSetState<T> state)
    {
        State = state;
    }

    #endregion

    #region Properties

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
    /// </summary>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int SparseCapacity => _sparse.Length;

    [JsonIgnore]
    [MemoryPackIgnore]
    public bool IsSynchronized => false;

    [JsonIgnore]
    [MemoryPackIgnore]
    public object SyncRoot => _syncRoot ??= new object();

    /// <summary>
    /// Returns the dense keys array (valid range: [0..Count)).
    /// </summary>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int[] DenseKeys => _denseKeys;

    [JsonIgnore]
    [MemoryPackIgnore]
    public Span<int> Keys => _denseKeys.AsSpan(0, _count);

    /// <summary>
    /// Returns the dense values array (valid range: [0..Count)).
    /// </summary>
    [JsonIgnore]
    [MemoryPackIgnore]
    public T[] DenseValues => _denseValues;

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
            if ((uint)key > int.MaxValue) ThrowArgumentOutOfRange(nameof(key));
            if (key < 0) ThrowArgumentOutOfRange(nameof(key));

            EnsureSparseCapacity(key + 1);

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
            int n = value.DenseKeys?.Length ?? 0;

            if (n != (value.DenseValues?.Length ?? 0))
                ThrowArgumentException("DenseKeys and DenseValues length mismatch.");

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
                    ThrowArgumentException("Key cannot be negative.");

                if (key > maxKey)
                    maxKey = key;
            }

            // Allocate sparse map
            int sparseSize = Math.Max(DefaultSparseCapacity, maxKey + 1);
            _sparse = new int[sparseSize];

            // Rebuild sparse lookup
            for (int i = 0; i < n; i++)
            {
                int key = _denseKeys[i];

                if (_sparse[key] != NotPresent)
                    ThrowArgumentException("Duplicate key in DenseKeys.");

                _sparse[key] = i + 1;
            }

            _version++;
        }
    }

    #endregion

    #region Core Operations

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
        if (key < 0) ThrowArgumentOutOfRange(nameof(key));

        EnsureSparseCapacity(key + 1);
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

        value = default;
        return false;
    }

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
        _denseValues[last] = default;

        _version++;
        return true;
    }

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

    public void EnsureDenseCapacity(int capacity)
    {
        if (capacity <= _denseKeys.Length) return;

        int newCap = _denseKeys.Length == 0 ? DefaultDenseCapacity : _denseKeys.Length * 2;
        if (newCap < capacity) newCap = capacity;

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

    public void EnsureSparseCapacity(int capacity)
    {
        if (capacity <= _sparse.Length) return;

        int newCap = _sparse.Length == 0 ? DefaultSparseCapacity : _sparse.Length * 2;
        if (newCap < capacity) newCap = capacity;

        var newSparse = new int[newCap];
        if (_sparse.Length > 0)
            Array.Copy(_sparse, newSparse, _sparse.Length);

        _sparse = newSparse;
        _version++;
    }

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

        int newSparse = Math.Max(DefaultSparseCapacity, maxKey + 1);
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

    public Enumerator GetEnumerator() => new Enumerator(this);
    IEnumerator<KeyValuePair<int, T>> IEnumerable<KeyValuePair<int, T>>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public struct Enumerator : IEnumerator<KeyValuePair<int, T>>
    {
        private readonly SwiftSparseMap<T> _set;
        private readonly int[] _keys;
        private readonly T[] _values;
        private readonly int _count;
        private readonly uint _version;
        private int _index;

        internal Enumerator(SwiftSparseMap<T> set)
        {
            _set = set;
            _keys = set._denseKeys;
            _values = set._denseValues;
            _count = set._count;
            _version = set._version;
            _index = -1;
            Current = default;
        }

        public KeyValuePair<int, T> Current { get; private set; }
        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            if (_version != _set._version)
                ThrowInvalidOperation("Collection was modified during enumeration.");

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

        public void Reset()
        {
            if (_version != _set._version)
                ThrowInvalidOperation("Collection was modified during enumeration.");
            _index = -1;
            Current = default;
        }

        public void Dispose() { }
    }

    #endregion

    #region Helpers

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetDense(out int[] keys, out T[] values, out int count)
    {
        keys = _denseKeys;
        values = _denseValues;
        count = _count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetDenseIndexOrThrow(int key)
    {
        if ((uint)key >= (uint)_sparse.Length)
            ThrowKeyNotFound();

        int slot = _sparse[key];
        if (slot == NotPresent)
            ThrowKeyNotFound();

        return slot - 1;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowKeyNotFound() => throw new KeyNotFoundException();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowArgumentOutOfRange(string name) => throw new ArgumentOutOfRangeException(name);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowArgumentException(string message) => throw new ArgumentException(message);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowInvalidOperation(string message) => throw new InvalidOperationException(message);

    public void CloneTo(ICollection<T> output)
    {
        if (output == null)
            ThrowArgumentException(nameof(output));

        output.Clear();

        for (int i = 0; i < _count; i++)
            output.Add(_denseValues[i]);
    }

    #endregion
}