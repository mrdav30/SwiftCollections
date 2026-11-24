//=======================================================================
// SwiftSparseSet.cs
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
/// Stores membership for externally owned non-negative integer IDs in dense storage.
/// </summary>
/// <remarks>
/// Copies are shallow and instances require external synchronization. Enumeration follows dense order. Removal
/// swaps the final dense entry into the gap; dense indices are unstable. Sparse storage scales with the highest
/// ID, not Count. Direct key writes corrupt membership and bypass version checks.
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
    /// Initializes a new instance of the <see cref="T:SwiftCollections.SwiftSparseSet" /> class with default
    /// sparse and dense capacities.
    /// </summary>
    public SwiftSparseSet() : this(DefaultSparseCapacity, DefaultDenseCapacity) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.SwiftSparseSet" /> class with matching
    /// sparse and dense capacities.
    /// </summary>
    /// <param name="capacity">The requested element storage capacity; zero permits initially empty storage and
    /// negative constructor capacities are rejected.</param>
    /// <exception cref="T:System.ArgumentOutOfRangeException">A requested capacity is negative.</exception>
    public SwiftSparseSet(int capacity) : this(capacity, capacity) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.SwiftSparseSet" /> class with explicit
    /// sparse and dense capacities.
    /// </summary>
    /// <param name="sparseCapacity">The requested sparse lookup length (largest ID plus one); zero permits
    /// initially empty storage and negative constructor capacities are rejected.</param>
    /// <param name="denseCapacity">The requested element storage capacity; zero permits initially empty storage
    /// and negative constructor capacities are rejected.</param>
    /// <exception cref="T:System.ArgumentOutOfRangeException">A requested capacity is negative.</exception>
    public SwiftSparseSet(int sparseCapacity, int denseCapacity)
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
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.SwiftSparseSet" /> class using the
    /// specified state.
    /// </summary>
    /// <param name="state">The state to restore by shallow-copying arrays. Null item arrays are rejected; IDs
    /// must be unique, non-negative and below Int32.MaxValue.</param>
    /// <exception cref="T:System.ArgumentNullException">A required state array is null.</exception>
    /// <exception cref="T:System.ArgumentException">State IDs are duplicated or map array lengths
    /// differ.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">A state ID is outside the supported
    /// range.</exception>
    /// <remarks>
    /// Input arrays are copied, not retained. Default state has null arrays and is rejected.
    /// </remarks>
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
    /// <value>The number of live entries, excluding gaps and spare storage.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Count => _count;

    /// <summary>
    /// Capacity of the dense ID storage.
    /// </summary>
    /// <value>The allocated dense-array length; only the first Count entries are live.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int DenseCapacity => _denseKeys.Length;

    /// <summary>
    /// Capacity of the sparse lookup table.
    /// </summary>
    /// <value>The lookup-array length; directly indexed IDs must be below this length or require growth.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int SparseCapacity => _sparse.Length;

    /// <summary>
    /// Gets whether the collection rejects all mutations.
    /// </summary>
    /// <value>false.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public bool IsReadOnly => false;

    /// <summary>
    /// Gets a value indicating whether access to the collection is synchronized.
    /// </summary>
    /// <value>false.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public bool IsSynchronized => false;

    /// <summary>
    /// Gets an object that can be used to synchronize access to the collection.
    /// </summary>
    /// <value>A lazily allocated synchronization object; access is not locked automatically.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public object SyncRoot => _syncRoot ??= new object();

    /// <summary>
    /// Returns the dense ID array. Only the range [0..Count) is populated.
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
    /// Gets a span containing the current IDs in dense iteration order.
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
    /// Gets a shallow snapshot of storage and allocation metadata.
    /// </summary>
    /// <value>A state with copied arrays; referenced elements remain shared.</value>
    /// <remarks>
    /// Stores live dense entries in order and rebuilds sparse membership on restore. Negative/duplicate IDs and
    /// inconsistent map key/value lengths are rejected.
    /// </remarks>
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
            ChronicleGuard.ThrowIfNull(value.Items, nameof(value.Items));

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
            ChronicleGuard.ThrowIfNegative(key, paramName);
            ChronicleGuard.ThrowIfArgumentOutOfRange(key == int.MaxValue, key, paramName, "ID is too large for direct sparse indexing.");

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
            ChronicleGuard.ThrowIfArgument(_sparse[key] != NotPresent, paramName, "Duplicate ID in sparse set state.");
            _sparse[key] = i + 1;
        }
    }

    #endregion

    #region Core Operations

    /// <summary>
    /// Determines whether the set contains the specified ID.
    /// </summary>
    /// <param name="item">The externally owned non-negative ID, below Int32.MaxValue; inserting requires lookup
    /// storage proportional to ID plus one.</param>
    /// <returns>true if the requested live slot, token, ID or value exists; false for absent/out-of-range inputs.
    /// No mutation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(int item)
    {
        if ((uint)item >= (uint)_sparse.Length) return false;
        return _sparse[item] != NotPresent;
    }

    /// <summary>
    /// Determines whether the set contains the specified key. Alias for <see
    /// cref="M:SwiftCollections.SwiftSparseSet.Contains(System.Int32)" />.
    /// </summary>
    /// <param name="key">The externally owned non-negative ID, below Int32.MaxValue; inserting requires lookup
    /// storage proportional to ID plus one.</param>
    /// <returns>true if the requested live slot, token, ID or value exists; false for absent/out-of-range inputs.
    /// No mutation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsKey(int key) => Contains(key);

    /// <summary>
    /// Adds the specified ID if it is not already present.
    /// </summary>
    /// <param name="item">The externally owned non-negative ID, below Int32.MaxValue; inserting requires lookup
    /// storage proportional to ID plus one.</param>
    /// <returns>true if inserted; false if the ID already exists, preserving the existing value. TryAdd still
    /// throws for invalid ID ranges or allocation failure.</returns>
    /// <exception cref="T:System.ArgumentOutOfRangeException">The ID is negative or Int32.MaxValue.</exception>
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
    /// <param name="item">The externally owned non-negative ID, below Int32.MaxValue; inserting requires lookup
    /// storage proportional to ID plus one.</param>
    /// <returns>true if inserted; false if the ID already exists, preserving the existing value. TryAdd still
    /// throws for invalid ID ranges or allocation failure.</returns>
    /// <exception cref="T:System.ArgumentOutOfRangeException">The ID is negative or Int32.MaxValue.</exception>
    public bool TryAdd(int item) => Add(item);

    /// <summary>
    /// Adds an element through the implemented collection interface.
    /// </summary>
    /// <param name="item">The element to compare, add or remove; reference values are not cloned.</param>
    void ICollection<int>.Add(int item) => Add(item);

    /// <summary>
    /// Removes the specified ID from the set.
    /// </summary>
    /// <param name="item">The externally owned non-negative ID, below Int32.MaxValue; inserting requires lookup
    /// storage proportional to ID plus one.</param>
    /// <returns>true if a matching live entry was removed; false if absent or invalid, with contents unchanged.
    /// Swap-back removal can change dense order.</returns>
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
    /// <remarks>
    /// Empties the collection while preserving capacity. The Boolean argument has no effect because IDs contain
    /// no references.
    /// </remarks>
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

    /// <summary>
    /// Removes all elements, optionally releasing references throughout retained backing storage.
    /// </summary>
    /// <param name="clearRetainedReferences">Ignored compatibility argument; integer IDs do not retain object
    /// references.</param>
    /// <remarks>
    /// Empties the collection while preserving capacity. The Boolean argument has no effect because IDs contain
    /// no references.
    /// </remarks>
    public void Clear(bool clearRetainedReferences)
    {
        Clear();
    }

    #endregion

    #region Set Operations

    /// <summary>
    /// Removes elements belonging to the other input.
    /// </summary>
    /// <param name="other">The non-null ID sequence consumed for the operation. Duplicate IDs count once for set
    /// comparisons.</param>
    /// <exception cref="T:System.ArgumentNullException">other is null.</exception>
    /// <remarks>
    /// Mutates membership according to ExceptWith. Input storage is not retained; failures during input
    /// enumeration can leave partial mutation. Negative and Int32.MaxValue IDs throw when insertion is required;
    /// membership/removal checks treat them as absent.
    /// </remarks>
    public void ExceptWith(IEnumerable<int> other)
    {
        ChronicleGuard.ThrowIfNull(other, nameof(other));

        if (_count == 0) return;
        if (ReferenceEquals(other, this))
        {
            Clear();
            return;
        }

        foreach (int item in other)
            Remove(item);
    }

    /// <summary>
    /// Retains elements belonging to both inputs.
    /// </summary>
    /// <param name="other">The non-null ID sequence consumed for the operation. Duplicate IDs count once for set
    /// comparisons.</param>
    /// <exception cref="T:System.ArgumentNullException">other is null.</exception>
    /// <remarks>
    /// Mutates membership according to IntersectWith. Input storage is not retained; failures during input
    /// enumeration can leave partial mutation. Negative and Int32.MaxValue IDs throw when insertion is required;
    /// membership/removal checks treat them as absent.
    /// </remarks>
    public void IntersectWith(IEnumerable<int> other)
    {
        ChronicleGuard.ThrowIfNull(other, nameof(other));

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

    /// <summary>
    /// Checks for a subset with fewer distinct elements.
    /// </summary>
    /// <param name="other">The non-null ID sequence consumed for the operation. Duplicate IDs count once for set
    /// comparisons.</param>
    /// <returns>true if all stored IDs belong to other and other has more distinct IDs; otherwise false. Contents
    /// are unchanged.</returns>
    /// <exception cref="T:System.ArgumentNullException">other is null.</exception>
    public bool IsProperSubsetOf(IEnumerable<int> other)
    {
        ChronicleGuard.ThrowIfNull(other, nameof(other));

        if (other is SwiftSparseSet sparseSet)
            return _count < sparseSet._count && IsSubsetOf(sparseSet);

        if (other is ISet<int> set)
            return _count < set.Count && IsSubsetOf(set);

        var lookup = new HashSet<int>(other);
        return _count < lookup.Count && IsSubsetOf(lookup);
    }

    /// <summary>
    /// Checks for a superset with more distinct elements.
    /// </summary>
    /// <param name="other">The non-null ID sequence consumed for the operation. Duplicate IDs count once for set
    /// comparisons.</param>
    /// <returns>true if all distinct IDs in other are stored here and this set has more IDs; otherwise false.
    /// Contents are unchanged.</returns>
    /// <exception cref="T:System.ArgumentNullException">other is null.</exception>
    public bool IsProperSupersetOf(IEnumerable<int> other)
    {
        ChronicleGuard.ThrowIfNull(other, nameof(other));

        if (other is SwiftSparseSet sparseSet)
            return _count > sparseSet._count && IsSupersetOf(sparseSet);

        if (other is ISet<int> set)
            return _count > set.Count && IsSupersetOf(set);

        var lookup = new HashSet<int>(other);
        return _count > lookup.Count && IsSupersetOf(lookup);
    }

    /// <summary>
    /// Checks whether all stored elements belong to the other input.
    /// </summary>
    /// <param name="other">The non-null ID sequence consumed for the operation. Duplicate IDs count once for set
    /// comparisons.</param>
    /// <returns>true if all stored IDs belong to other; otherwise false. Contents are unchanged.</returns>
    /// <exception cref="T:System.ArgumentNullException">other is null.</exception>
    public bool IsSubsetOf(IEnumerable<int> other)
    {
        ChronicleGuard.ThrowIfNull(other, nameof(other));

        if (_count == 0 || ReferenceEquals(other, this)) return true;

        if (other is SwiftSparseSet sparseSet)
            return IsSubsetOfSparseSet(sparseSet);

        if (other is ISet<int> set)
            return IsSubsetOfSet(set);

        return IsSubsetOfSet(new HashSet<int>(other));
    }

    /// <summary>
    /// Checks whether all other-input elements are stored here.
    /// </summary>
    /// <param name="other">The non-null ID sequence consumed for the operation. Duplicate IDs count once for set
    /// comparisons.</param>
    /// <returns>true if all distinct IDs in other are stored here; otherwise false. Contents are
    /// unchanged.</returns>
    /// <exception cref="T:System.ArgumentNullException">other is null.</exception>
    public bool IsSupersetOf(IEnumerable<int> other)
    {
        ChronicleGuard.ThrowIfNull(other, nameof(other));

        if (ReferenceEquals(other, this)) return true;

        foreach (int item in other)
        {
            if (!Contains(item))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Checks whether at least one element is shared.
    /// </summary>
    /// <param name="other">The non-null ID sequence consumed for the operation. Duplicate IDs count once for set
    /// comparisons.</param>
    /// <returns>true if at least one ID is shared; otherwise false. Contents are unchanged.</returns>
    /// <exception cref="T:System.ArgumentNullException">other is null.</exception>
    public bool Overlaps(IEnumerable<int> other)
    {
        ChronicleGuard.ThrowIfNull(other, nameof(other));

        foreach (int item in other)
        {
            if (Contains(item))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks whether both inputs contain the same distinct elements.
    /// </summary>
    /// <param name="other">The non-null ID sequence consumed for the operation. Duplicate IDs count once for set
    /// comparisons.</param>
    /// <returns>true if both inputs contain exactly the same distinct IDs; otherwise false. Contents are
    /// unchanged.</returns>
    /// <exception cref="T:System.ArgumentNullException">other is null.</exception>
    public bool SetEquals(IEnumerable<int> other)
    {
        ChronicleGuard.ThrowIfNull(other, nameof(other));

        if (ReferenceEquals(other, this)) return true;

        if (other is SwiftSparseSet sparseSet)
            return SetEqualsSparseSet(sparseSet);

        if (other is ISet<int> set)
            return SetEqualsSet(set);

        return SetEqualsSet(new HashSet<int>(other));
    }

    /// <summary>
    /// Retains elements belonging to exactly one input.
    /// </summary>
    /// <param name="other">The non-null ID sequence consumed for the operation. Duplicate IDs count once for set
    /// comparisons.</param>
    /// <exception cref="T:System.ArgumentNullException">other is null.</exception>
    /// <remarks>
    /// Mutates membership according to SymmetricExceptWith. Input storage is not retained; failures during input
    /// enumeration can leave partial mutation. Negative and Int32.MaxValue IDs throw when insertion is required;
    /// membership/removal checks treat them as absent.
    /// </remarks>
    public void SymmetricExceptWith(IEnumerable<int> other)
    {
        ChronicleGuard.ThrowIfNull(other, nameof(other));

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

    /// <summary>
    /// Adds elements belonging to the other input.
    /// </summary>
    /// <param name="other">The non-null ID sequence consumed for the operation. Duplicate IDs count once for set
    /// comparisons.</param>
    /// <exception cref="T:System.ArgumentNullException">other is null.</exception>
    /// <remarks>
    /// Mutates membership according to UnionWith. Input storage is not retained; failures during input
    /// enumeration can leave partial mutation. Negative and Int32.MaxValue IDs throw when insertion is required;
    /// membership/removal checks treat them as absent.
    /// </remarks>
    public void UnionWith(IEnumerable<int> other)
    {
        ChronicleGuard.ThrowIfNull(other, nameof(other));

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
    /// <param name="capacity">The minimum storage length; requests at or below the current capacity change
    /// nothing. Keep requests below 2^30 to avoid the capacity sentinel.</param>
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
    /// <param name="capacity">The minimum storage length; requests at or below the current capacity change
    /// nothing. Keep requests below 2^30 to avoid the capacity sentinel.</param>
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
    /// <returns>A borrowed read-only span of Count live IDs in dense order.</returns>
    /// <remarks>
    /// No copy or allocation. Removal reorders IDs and growth detaches storage. Do not retain across structural
    /// mutation.
    /// </remarks>
    public ReadOnlySpan<int> AsReadOnlySpan() => _denseKeys.AsSpan(0, _count);

    /// <summary>
    /// Retrieves the backing dense ID array and current count.
    /// </summary>
    /// <param name="keys">Receives the borrowed dense backing array; only indices below count are live. Do not
    /// modify keys.</param>
    /// <param name="count">Receives the number of live entries in the returned arrays at this instant.</param>
    /// <remarks>
    /// Returns borrowed storage without allocation. Structural mutation can reorder or replace these arrays;
    /// count is a snapshot, not a live binding. Do not write key arrays or retain views across mutation.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetDense(out int[] keys, out int count)
    {
        keys = _denseKeys;
        count = _count;
    }

    /// <summary>
    /// Replaces the destination list contents with this set's keys in dense iteration order.
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
    /// Replaces the destination list contents with this set's keys sorted in ascending order.
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

    /// <summary>
    /// Copies live elements into caller-owned storage.
    /// </summary>
    /// <param name="array">The caller-owned destination array; only the copied range is overwritten.</param>
    /// <param name="arrayIndex">The zero-based destination offset with room for Count live values.</param>
    /// <exception cref="T:System.ArgumentNullException">array is null.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">arrayIndex is outside zero through
    /// array.Length.</exception>
    /// <exception cref="T:System.ArgumentException">The remaining destination space is shorter than
    /// Count.</exception>
    /// <remarks>
    /// Shallow-copies Count live values in dense order without clearing the destination.
    /// </remarks>
    public void CopyTo(int[] array, int arrayIndex)
    {
        ChronicleGuard.ThrowIfNull(array, nameof(array));
        ChronicleGuard.ThrowIfArrayIndexInvalid(arrayIndex, array.Length);
        ChronicleGuard.ThrowIfArgument(array.Length - arrayIndex < _count, nameof(array), "The array is not large enough to hold the elements.");

        Array.Copy(_denseKeys, 0, array, arrayIndex, _count);
    }

    /// <summary>
    /// Copies live elements into a caller-owned array.
    /// </summary>
    /// <param name="array">The caller-owned, one-dimensional, zero-based destination array.</param>
    /// <param name="index">The zero-based logical or destination index, interpreted by this interface
    /// operation.</param>
    /// <remarks>
    /// Uses concrete collection copy order and validation. The destination is overwritten only in the copied
    /// range; incompatible elements can cause partial copying.
    /// </remarks>
    void ICollection.CopyTo(Array array, int index)
    {
        ChronicleGuard.ThrowIfNull(array, nameof(array));
        ChronicleGuard.ThrowIfArgument(array.Rank != 1, nameof(array), "Only single dimensional arrays are supported.");
        ChronicleGuard.ThrowIfArgument(array.GetLowerBound(0) != 0, nameof(array), "Non-zero lower bound arrays are not supported.");
        ChronicleGuard.ThrowIfArrayIndexInvalid(index, array.Length, nameof(index));
        ChronicleGuard.ThrowIfArgument(array.Length - index < _count, nameof(array), "The array is not large enough to hold the elements.");

        if (array is int[] intArray)
        {
            CopyTo(intArray, index);
            return;
        }

        Type elementType = array.GetType().GetElementType()!;
        if (array is object[] objects && elementType.IsAssignableFrom(typeof(int)))
        {
            for (int i = 0; i < _count; i++)
                objects[index + i] = _denseKeys[i];
            return;
        }

        throw new ArgumentException("Invalid array type.", nameof(array));
    }

    /// <inheritdoc />
    public void CloneTo(ICollection<int> output)
    {
        ChronicleGuard.ThrowIfNull(output, nameof(output));

        output.Clear();

        for (int i = 0; i < _count; i++)
            output.Add(_denseKeys[i]);
    }

    /// <summary>
    /// Creates an enumerator over the live entries.
    /// </summary>
    /// <returns>A value-type enumerator over live entries in dense order.</returns>
    /// <remarks>
    /// MoveNext and Reset check the mutation version. Direct storage/ref writes bypass the version check.
    /// </remarks>
    public SwiftSparseSetEnumerator GetEnumerator() => new(this);
    /// <summary>
    /// Creates an enumerator through the implemented interface.
    /// </summary>
    /// <returns>An enumerator with the concrete collection's ordering and mutation rules. Interface dispatch may
    /// box the value-type enumerator.</returns>
    IEnumerator<int> IEnumerable<int>.GetEnumerator() => GetEnumerator();
    /// <summary>
    /// Creates an enumerator through the implemented interface.
    /// </summary>
    /// <returns>An enumerator with the concrete collection's ordering and mutation rules. Interface dispatch may
    /// box the value-type enumerator.</returns>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();



    #endregion

    #region Helpers

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetRequiredSparseCapacity(int key)
    {
        ChronicleGuard.ThrowIfNegative(key, nameof(key));
        ChronicleGuard.ThrowIfArgumentOutOfRange(key == int.MaxValue, key, nameof(key), "ID is too large for direct sparse indexing.");

        return key + 1;
    }

    #endregion
}
