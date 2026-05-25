using Chronicler;
using MemoryPack;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace SwiftCollections;

/// <summary>
/// Represents a high-performance sparse set for externally supplied non-negative integer IDs.
/// Provides O(1) Add, Remove, Contains, and densely packed iteration.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SwiftSparseSet"/> is intended for membership workloads where the caller already owns
/// compact integer IDs, such as entity handles, body IDs, or slot indices.
/// </para>
/// <para>
/// Internally, IDs are stored in a dense array for cache-friendly iteration while a sparse lookup
/// table maps each ID directly to its dense position. Removal uses swap-back, so iteration order is
/// not stable.
/// </para>
/// <para>
/// Memory usage scales with the highest stored ID rather than only the number of IDs. For arbitrary,
/// huge, or widely spaced keys, prefer <see cref="SwiftHashSet{T}"/> with <c>int</c> keys.
/// </para>
/// </remarks>
[Serializable]
[JsonConverter(typeof(StateJsonConverterFactory))]
[MemoryPackable]
public sealed partial class SwiftSparseSet : IStateBacked<SwiftArrayState<int>>, ISwiftCloneable<int>, ISet<int>, IReadOnlyCollection<int>, ICollection
{
    #region Constants

    /// <summary>
    /// Represents the default initial capacity for dense ID storage.
    /// </summary>
    public const int DefaultDenseCapacity = 8;

    /// <summary>
    /// Represents the default initial capacity for sparse ID lookup.
    /// </summary>
    public const int DefaultSparseCapacity = 8;

    private const int NotPresent = 0;

    #endregion

    #region Fields

    private int[] _sparse;       // id -> denseIndex+1
    private int[] _denseKeys;    // denseIndex -> id
    private int _count;

    [NonSerialized]
    private uint _version;

    [NonSerialized]
    private object? _syncRoot;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="SwiftSparseSet"/> class with default sparse and dense capacities.
    /// </summary>
    public SwiftSparseSet() : this(DefaultSparseCapacity, DefaultDenseCapacity) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SwiftSparseSet"/> class with matching sparse and dense capacities.
    /// </summary>
    /// <param name="capacity">The initial sparse and dense capacity.</param>
    public SwiftSparseSet(int capacity) : this(capacity, capacity) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SwiftSparseSet"/> class with explicit sparse and dense capacities.
    /// </summary>
    /// <param name="sparseCapacity">
    /// Initial sparse lookup capacity. This should track the highest expected ID plus one,
    /// not just the number of stored IDs.
    /// </param>
    /// <param name="denseCapacity">Initial dense storage capacity for IDs.</param>
    public SwiftSparseSet(int sparseCapacity, int denseCapacity)
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
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SwiftSparseSet"/> class using the specified state.
    /// </summary>
    /// <param name="state">The state object that provides the initial IDs. Cannot be null.</param>
    [MemoryPackConstructor]
    public SwiftSparseSet(SwiftArrayState<int> state)
    {
        _sparse = Array.Empty<int>();
        _denseKeys = Array.Empty<int>();

        State = state;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the number of IDs contained in the set.
    /// </summary>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Count => _count;

    /// <summary>
    /// Capacity of the dense ID storage.
    /// </summary>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int DenseCapacity => _denseKeys.Length;

    /// <summary>
    /// Capacity of the sparse lookup table.
    /// </summary>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int SparseCapacity => _sparse.Length;

    /// <inheritdoc/>
    [JsonIgnore]
    [MemoryPackIgnore]
    public bool IsReadOnly => false;

    /// <summary>
    /// Gets a value indicating whether access to the collection is synchronized.
    /// </summary>
    [JsonIgnore]
    [MemoryPackIgnore]
    public bool IsSynchronized => false;

    /// <summary>
    /// Gets an object that can be used to synchronize access to the collection.
    /// </summary>
    [JsonIgnore]
    [MemoryPackIgnore]
    public object SyncRoot => _syncRoot ??= new object();

    /// <summary>
    /// Returns the dense ID array. Only the range [0..Count) is populated.
    /// </summary>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int[] DenseKeys => _denseKeys;

    /// <summary>
    /// Gets a span containing the current IDs in dense iteration order.
    /// </summary>
    [JsonIgnore]
    [MemoryPackIgnore]
    public Span<int> Keys => _denseKeys.AsSpan(0, _count);

    /// <summary>
    /// Gets or sets the current state of the sparse set.
    /// </summary>
    [JsonInclude]
    [MemoryPackInclude]
    public SwiftArrayState<int> State
    {
        get
        {
            var items = new int[_count];
            Array.Copy(_denseKeys, items, _count);
            return new SwiftArrayState<int>(items);
        }
        internal set
        {
            SwiftThrowHelper.ThrowIfNull(value.Items, nameof(value.Items));

            RestoreDenseKeys(value.Items);
            int maxKey = ValidateDenseKeys(nameof(value.Items));
            RestoreSparseLookup(maxKey, nameof(value));

            _version++;
        }
    }

    private void RestoreDenseKeys(int[] items)
    {
        int count = items.Length;
        _denseKeys = count == 0
            ? Array.Empty<int>()
            : new int[Math.Max(DefaultDenseCapacity, SwiftHashTools.NextPowerOfTwo(count))];

        if (count > 0)
            Array.Copy(items, _denseKeys, count);

        _count = count;
    }

    private int ValidateDenseKeys(string paramName)
    {
        int maxKey = -1;
        for (int i = 0; i < _count; i++)
        {
            int key = _denseKeys[i];
            SwiftThrowHelper.ThrowIfNegative(key, paramName);
            SwiftThrowHelper.ThrowIfArgumentOutOfRange(key == int.MaxValue, key, paramName, "ID is too large for direct sparse indexing.");

            if (key > maxKey)
                maxKey = key;
        }

        return maxKey;
    }

    private void RestoreSparseLookup(int maxKey, string paramName)
    {
        int sparseSize = maxKey < 0
            ? DefaultSparseCapacity
            : Math.Max(DefaultSparseCapacity, GetRequiredSparseCapacity(maxKey));
        _sparse = new int[sparseSize];

        for (int i = 0; i < _count; i++)
        {
            int key = _denseKeys[i];
            SwiftThrowHelper.ThrowIfArgument(_sparse[key] != NotPresent, paramName, "Duplicate ID in sparse set state.");
            _sparse[key] = i + 1;
        }
    }

    #endregion

    #region Core Operations

    /// <summary>
    /// Determines whether the set contains the specified ID.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(int item)
    {
        if ((uint)item >= (uint)_sparse.Length) return false;
        return _sparse[item] != NotPresent;
    }

    /// <summary>
    /// Determines whether the set contains the specified key. Alias for <see cref="Contains(int)"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsKey(int key) => Contains(key);

    /// <summary>
    /// Adds the specified ID if it is not already present.
    /// </summary>
    /// <returns>true if the ID was added; false if it was already present.</returns>
    public bool Add(int item)
    {
        EnsureSparseCapacity(GetRequiredSparseCapacity(item));
        if (_sparse[item] != NotPresent)
            return false;

        EnsureDenseCapacity(_count + 1);

        int newIndex = _count++;
        _denseKeys[newIndex] = item;
        _sparse[item] = newIndex + 1;

        _version++;
        return true;
    }

    /// <summary>
    /// Adds the specified ID if it is not already present.
    /// </summary>
    public bool TryAdd(int item) => Add(item);

    void ICollection<int>.Add(int item) => Add(item);

    /// <summary>
    /// Removes the specified ID from the set.
    /// </summary>
    /// <returns>true if the ID was found and removed; otherwise, false.</returns>
    public bool Remove(int item)
    {
        if ((uint)item >= (uint)_sparse.Length) return false;

        int slot = _sparse[item];
        if (slot == NotPresent) return false;

        int index = slot - 1;
        int last = --_count;

        _sparse[item] = NotPresent;

        if (index != last)
        {
            int movedKey = _denseKeys[last];
            _denseKeys[index] = movedKey;
            _sparse[movedKey] = index + 1;
        }

        _denseKeys[last] = default;
        _version++;
        return true;
    }

    /// <summary>
    /// Removes all IDs from the set without reducing capacity.
    /// </summary>
    public void Clear()
    {
        if (_count == 0) return;

        for (int i = 0; i < _count; i++)
        {
            int key = _denseKeys[i];
            _sparse[key] = NotPresent;
            _denseKeys[i] = default;
        }

        _count = 0;
        _version++;
    }

    #endregion

    #region Set Operations

    /// <inheritdoc/>
    public void ExceptWith(IEnumerable<int> other)
    {
        SwiftThrowHelper.ThrowIfNull(other, nameof(other));

        if (_count == 0) return;
        if (ReferenceEquals(other, this))
        {
            Clear();
            return;
        }

        foreach (int item in other)
            Remove(item);
    }

    /// <inheritdoc/>
    public void IntersectWith(IEnumerable<int> other)
    {
        SwiftThrowHelper.ThrowIfNull(other, nameof(other));

        if (_count == 0 || ReferenceEquals(other, this)) return;

        if (other is SwiftSparseSet sparseSet)
        {
            RemoveWhereMissingFrom(sparseSet);
            return;
        }

        if (other is ISet<int> set)
        {
            RemoveWhereMissingFrom(set);
            return;
        }

        RemoveWhereMissingFrom(new HashSet<int>(other));
    }

    /// <inheritdoc/>
    public bool IsProperSubsetOf(IEnumerable<int> other)
    {
        SwiftThrowHelper.ThrowIfNull(other, nameof(other));

        if (other is SwiftSparseSet sparseSet)
            return _count < sparseSet._count && IsSubsetOf(sparseSet);

        if (other is ISet<int> set)
            return _count < set.Count && IsSubsetOf(set);

        var lookup = new HashSet<int>(other);
        return _count < lookup.Count && IsSubsetOf(lookup);
    }

    /// <inheritdoc/>
    public bool IsProperSupersetOf(IEnumerable<int> other)
    {
        SwiftThrowHelper.ThrowIfNull(other, nameof(other));

        if (other is SwiftSparseSet sparseSet)
            return _count > sparseSet._count && IsSupersetOf(sparseSet);

        if (other is ISet<int> set)
            return _count > set.Count && IsSupersetOf(set);

        var lookup = new HashSet<int>(other);
        return _count > lookup.Count && IsSupersetOf(lookup);
    }

    /// <inheritdoc/>
    public bool IsSubsetOf(IEnumerable<int> other)
    {
        SwiftThrowHelper.ThrowIfNull(other, nameof(other));

        if (_count == 0 || ReferenceEquals(other, this)) return true;

        if (other is SwiftSparseSet sparseSet)
            return IsSubsetOfSparseSet(sparseSet);

        if (other is ISet<int> set)
            return IsSubsetOfSet(set);

        return IsSubsetOfSet(new HashSet<int>(other));
    }

    /// <inheritdoc/>
    public bool IsSupersetOf(IEnumerable<int> other)
    {
        SwiftThrowHelper.ThrowIfNull(other, nameof(other));

        if (ReferenceEquals(other, this)) return true;

        foreach (int item in other)
        {
            if (!Contains(item))
                return false;
        }

        return true;
    }

    /// <inheritdoc/>
    public bool Overlaps(IEnumerable<int> other)
    {
        SwiftThrowHelper.ThrowIfNull(other, nameof(other));

        foreach (int item in other)
        {
            if (Contains(item))
                return true;
        }

        return false;
    }

    /// <inheritdoc/>
    public bool SetEquals(IEnumerable<int> other)
    {
        SwiftThrowHelper.ThrowIfNull(other, nameof(other));

        if (ReferenceEquals(other, this)) return true;

        if (other is SwiftSparseSet sparseSet)
            return SetEqualsSparseSet(sparseSet);

        if (other is ISet<int> set)
            return SetEqualsSet(set);

        return SetEqualsSet(new HashSet<int>(other));
    }

    /// <inheritdoc/>
    public void SymmetricExceptWith(IEnumerable<int> other)
    {
        SwiftThrowHelper.ThrowIfNull(other, nameof(other));

        if (ReferenceEquals(other, this))
        {
            Clear();
            return;
        }

        if (other is SwiftSparseSet sparseSet)
        {
            SymmetricExceptWithSet(sparseSet);
            return;
        }

        if (other is ISet<int> set)
        {
            SymmetricExceptWithSet(set);
            return;
        }

        SymmetricExceptWithSet(new HashSet<int>(other));
    }

    /// <inheritdoc/>
    public void UnionWith(IEnumerable<int> other)
    {
        SwiftThrowHelper.ThrowIfNull(other, nameof(other));

        foreach (int item in other)
            Add(item);
    }

    private void RemoveWhereMissingFrom(SwiftSparseSet other)
    {
        int index = 0;
        while (index < _count)
        {
            int key = _denseKeys[index];
            if (other.Contains(key))
                index++;
            else
                Remove(key);
        }
    }

    private void RemoveWhereMissingFrom(ISet<int> other)
    {
        int index = 0;
        while (index < _count)
        {
            int key = _denseKeys[index];
            if (other.Contains(key))
                index++;
            else
                Remove(key);
        }
    }

    private bool AllKeysIn(SwiftSparseSet other)
    {
        for (int i = 0; i < _count; i++)
        {
            if (!other.Contains(_denseKeys[i]))
                return false;
        }

        return true;
    }

    private bool AllKeysIn(ISet<int> other)
    {
        for (int i = 0; i < _count; i++)
        {
            if (!other.Contains(_denseKeys[i]))
                return false;
        }

        return true;
    }

    private bool IsSubsetOfSparseSet(SwiftSparseSet other) =>
        _count <= other._count && AllKeysIn(other);

    private bool IsSubsetOfSet(ISet<int> other) =>
        _count <= other.Count && AllKeysIn(other);

    private bool SetEqualsSparseSet(SwiftSparseSet other) =>
        _count == other._count && AllKeysIn(other);

    private bool SetEqualsSet(ISet<int> other) =>
        _count == other.Count && AllKeysIn(other);

    private void SymmetricExceptWithSet(IEnumerable<int> other)
    {
        foreach (int item in other)
        {
            if (!Remove(item))
                Add(item);
        }
    }

    #endregion

    #region Capacity Management

    /// <summary>
    /// Ensures that dense storage can hold at least the specified number of IDs.
    /// </summary>
    public void EnsureDenseCapacity(int capacity)
    {
        if (capacity <= _denseKeys.Length) return;

        int newCap = _denseKeys.Length == 0 ? DefaultDenseCapacity : _denseKeys.Length * 2;
        if (newCap < capacity) newCap = capacity;

        newCap = SwiftHashTools.NextPowerOfTwo(newCap);

        var newKeys = new int[newCap];
        if (_count > 0)
            Array.Copy(_denseKeys, newKeys, _count);

        _denseKeys = newKeys;
        _version++;
    }

    /// <summary>
    /// Ensures that the sparse lookup table has at least the specified capacity.
    /// </summary>
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
    /// Reduces unused dense and sparse capacity while preserving all IDs.
    /// </summary>
    public void TrimExcess()
    {
        TrimDenseStorage();
        TrimSparseLookup();
        _version++;
    }

    private void TrimDenseStorage()
    {
        int newDense = Math.Max(DefaultDenseCapacity, _count);
        if (newDense >= _denseKeys.Length) return;

        var newKeys = new int[newDense];
        if (_count > 0)
            Array.Copy(_denseKeys, newKeys, _count);
        _denseKeys = newKeys;
    }

    private void TrimSparseLookup()
    {
        int maxKey = -1;
        for (int i = 0; i < _count; i++)
            if (_denseKeys[i] > maxKey) maxKey = _denseKeys[i];

        int newSparse = maxKey < 0
            ? DefaultSparseCapacity
            : Math.Max(DefaultSparseCapacity, GetRequiredSparseCapacity(maxKey));
        if (newSparse >= _sparse.Length) return;

        var newMap = new int[newSparse];
        for (int i = 0; i < _count; i++)
            newMap[_denseKeys[i]] = i + 1;
        _sparse = newMap;
    }

    #endregion

    #region Copy and Enumeration

    /// <summary>
    /// Returns a read-only span over the populated dense ID range.
    /// </summary>
    public ReadOnlySpan<int> AsReadOnlySpan() => _denseKeys.AsSpan(0, _count);

    /// <summary>
    /// Retrieves the backing dense ID array and current count.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetDense(out int[] keys, out int count)
    {
        keys = _denseKeys;
        count = _count;
    }

    /// <inheritdoc/>
    public void CopyTo(int[] array, int arrayIndex)
    {
        SwiftThrowHelper.ThrowIfNull(array, nameof(array));
        SwiftThrowHelper.ThrowIfArrayIndexInvalid(arrayIndex, array.Length, message: "Array index is out of range.");
        SwiftThrowHelper.ThrowIfArgument(array.Length - arrayIndex < _count, nameof(array), "The array is not large enough to hold the elements.");

        Array.Copy(_denseKeys, 0, array, arrayIndex, _count);
    }

    /// <inheritdoc/>
    void ICollection.CopyTo(Array array, int index)
    {
        SwiftThrowHelper.ThrowIfNull(array, nameof(array));
        SwiftThrowHelper.ThrowIfArgument(array.Rank != 1, nameof(array), "Only single dimensional arrays are supported.");
        SwiftThrowHelper.ThrowIfArgument(array.GetLowerBound(0) != 0, nameof(array), "Non-zero lower bound arrays are not supported.");
        SwiftThrowHelper.ThrowIfArrayIndexInvalid(index, array.Length, nameof(index), "Array index is out of range.");
        SwiftThrowHelper.ThrowIfArgument(array.Length - index < _count, nameof(array), "The array is not large enough to hold the elements.");

        if (array is int[] intArray)
        {
            CopyTo(intArray, index);
            return;
        }

        Type? elementType = array.GetType().GetElementType();
        if (array is object[] objects && elementType != null && elementType.IsAssignableFrom(typeof(int)))
        {
            for (int i = 0; i < _count; i++)
                objects[index + i] = _denseKeys[i];
            return;
        }

        SwiftThrowHelper.ThrowIfArgument(true, nameof(array), "Invalid array type.");
    }

    /// <inheritdoc/>
    public void CloneTo(ICollection<int> output)
    {
        SwiftThrowHelper.ThrowIfNull(output, nameof(output));

        output.Clear();

        for (int i = 0; i < _count; i++)
            output.Add(_denseKeys[i]);
    }

    /// <inheritdoc cref="IEnumerable.GetEnumerator()"/>
    public SwiftSparseSetEnumerator GetEnumerator() => new(this);
    IEnumerator<int> IEnumerable<int>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Supports iteration over IDs in a <see cref="SwiftSparseSet"/>.
    /// </summary>
    public struct SwiftSparseSetEnumerator : IEnumerator<int>
    {
        private readonly SwiftSparseSet _set;
        private readonly int[] _keys;
        private readonly int _count;
        private readonly uint _version;
        private int _index;

        internal SwiftSparseSetEnumerator(SwiftSparseSet set)
        {
            _set = set;
            _keys = set._denseKeys;
            _count = set._count;
            _version = set._version;
            _index = -1;
            Current = default;
        }

        /// <inheritdoc/>
        public int Current { get; private set; }
        object IEnumerator.Current => Current;

        /// <inheritdoc/>
        public bool MoveNext()
        {
            SwiftThrowHelper.ThrowIfTrue(_version != _set._version, message: "Collection was modified during enumeration.");

            int next = _index + 1;
            if (next >= _count)
            {
                Current = default;
                return false;
            }

            _index = next;
            Current = _keys[_index];
            return true;
        }

        /// <inheritdoc/>
        public void Reset()
        {
            SwiftThrowHelper.ThrowIfTrue(_version != _set._version, message: "Collection was modified during enumeration.");

            _index = -1;
            Current = default;
        }

        /// <inheritdoc/>
        public void Dispose() => _index = -1;
    }

    #endregion

    #region Helpers

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetRequiredSparseCapacity(int key)
    {
        SwiftThrowHelper.ThrowIfNegative(key, nameof(key));
        SwiftThrowHelper.ThrowIfArgumentOutOfRange(key == int.MaxValue, key, nameof(key), "ID is too large for direct sparse indexing.");

        return key + 1;
    }

    #endregion
}
