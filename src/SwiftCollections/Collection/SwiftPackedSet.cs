//=======================================================================
// SwiftPackedSet.cs
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
/// Stores unique values in dense storage with hash-based membership.
/// </summary>
/// <typeparam name="T">The type of elements contained in the set.</typeparam>
/// <remarks>
/// Instances require external synchronization; keys and elements must keep equality/hash identity stable while
/// stored. Copies are shallow. Removal swaps the last dense element into the removed slot, so indices and
/// ordering are unstable. Do not write Dense directly; doing so bypasses the membership lookup.
/// </remarks>
[Serializable]
[JsonConverter(typeof(StateJsonConverterFactory))]
[MemoryPackable]
public sealed partial class SwiftPackedSet<T> : IStateBacked<SwiftArrayState<T>>, ISwiftCloneable<T>, ISet<T>, IEnumerable<T>, IEnumerable
    where T : notnull
{
    #region Constants

    /// <summary>
    /// Represents the default initial capacity value used when no specific capacity is provided.
    /// </summary>
    public const int DefaultCapacity = 8;

    private static readonly bool _clearReleasedSlots = RuntimeHelpers.IsReferenceOrContainsReferences<T>();

    #endregion

    #region Fields

    private T[] _dense;
    private SwiftDictionary<T, int> _lookup;
    private int _count;

    [NonSerialized]
    private uint _version;

    [NonSerialized]
    private object? _syncRoot;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the SwiftPackedSet class with the default capacity.
    /// </summary>
    public SwiftPackedSet() : this(DefaultCapacity) { }

    /// <summary>
    /// Initializes a new instance of the SwiftPackedSet class with the specified initial capacity.
    /// </summary>
    /// <param name="capacity">Requested storage capacity; values below DefaultCapacity use the minimum
    /// allocation. Larger values round up. Keep requests below 2^30, where the capacity helper returns a
    /// sentinel.</param>
    /// <remarks>
    /// The actual capacity is rounded up to the next power of two greater than or equal to the specified value,
    /// unless the specified value is less than or equal to the default capacity.
    /// </remarks>
    public SwiftPackedSet(int capacity)
    {
        capacity = capacity <= DefaultCapacity
            ? DefaultCapacity
            : SwiftHashTools.NextPowerOfTwo(capacity);

        _dense = new T[capacity];
        _lookup = new SwiftDictionary<T, int>(capacity);
    }

    /// <summary>
    /// Initializes a new instance of the SwiftPackedSet class with the specified array state.
    /// </summary>
    /// <param name="state">The state whose non-null Items array is copied and indexed. Items must be non-null and
    /// unique under Swift default equality.</param>
    /// <exception cref="T:System.ArgumentNullException">state.Items is null, including a default
    /// state.</exception>
    /// <remarks>
    /// Shallow-copies trusted items and reconstructs storage using Swift default equality. There is no custom
    /// comparer API. Duplicate items are not validated against the dense count; supply unique non-null items.
    /// </remarks>
    [MemoryPackConstructor]
    public SwiftPackedSet(SwiftArrayState<T> state)
    {
        State = state;

        ChronicleGuard.ThrowIfNull(_dense, nameof(_dense));
        ChronicleGuard.ThrowIfNull(_lookup, nameof(_lookup));
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the number of live entries.
    /// </summary>
    /// <value>The count of stored elements or mappings, excluding spare storage and deleted slots.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Count => _count;

    /// <summary>
    /// Gets the total number of elements that the collection can hold without resizing.
    /// </summary>
    /// <value>The allocated number of storage slots; insertion can resize before this many entries are
    /// live.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Capacity => _dense.Length;

    /// <summary>
    /// Gets a value indicating whether access to the collection is synchronized (thread safe).
    /// </summary>
    /// <value>false.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public bool IsSynchronized => false;

    /// <summary>
    /// Gets the external synchronization object.
    /// </summary>
    /// <value>A lazily allocated lock object. Callers must coordinate all related access themselves.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public object SyncRoot => _syncRoot ??= new object();

    /// <summary>
    /// Gets the underlying dense array of elements.
    /// </summary>
    /// <value>The live dense backing array; only indices below Count are live.</value>
    /// <remarks>
    /// Borrowed mutable storage, including unused slots. Mutations can reorder elements; capacity changes detach
    /// old arrays. Writing values directly can corrupt the lookup invariant and bypass version checks.
    /// </remarks>
    [JsonIgnore]
    [MemoryPackIgnore]
    public T[] Dense => _dense;

    /// <summary>
    /// Gets whether the collection rejects all mutations.
    /// </summary>
    /// <value>false.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public bool IsReadOnly => false;

    /// <summary>
    /// Gets a shallow item snapshot for serialization.
    /// </summary>
    /// <value>A state containing copied live items in dense storage order; reference-type elements remain
    /// shared.</value>
    /// <remarks>
    /// Restore rebuilds storage using Swift default equality; this type has no custom comparer API. Capacity,
    /// hash slots and version are not serialized. Packed state requires distinct non-null values; duplicate state
    /// entries are copied into dense storage without deduplication.
    /// </remarks>
    [JsonInclude]
    [MemoryPackInclude]
    public SwiftArrayState<T> State
    {
        get
        {
            var values = new T[_count];
            Array.Copy(_dense, values, _count);

            return new SwiftArrayState<T>(values);
        }
        internal set
        {
            ChronicleGuard.ThrowIfNull(value.Items, nameof(value.Items));

            var values = value.Items;

            int n = values.Length;
            int newCapacity = n < DefaultCapacity
                ? DefaultCapacity
                : SwiftHashTools.NextPowerOfTwo(n);

            _dense = new T[newCapacity];
            _lookup = new SwiftDictionary<T, int>(newCapacity);

            if (n > 0)
            {
                Array.Copy(values, _dense, n);

                for (int i = 0; i < n; i++)
                    _lookup.Add(values[i], i);
            }

            _count = n;
            _version++;
        }
    }

    #endregion

    #region Core Operations

    /// <summary>
    /// Checks whether a matching live entry exists.
    /// </summary>
    /// <param name="value">The element or value; stored reference values are not cloned.</param>
    /// <returns>true if a matching live entry exists; otherwise false. No mutation.</returns>
    public bool Contains(T value)
        => _lookup.ContainsKey(value);

    /// <summary>
    /// Returns a read-only span over the populated dense portion of the set.
    /// </summary>
    /// <returns>A borrowed read-only span of the first Count dense elements.</returns>
    /// <remarks>
    /// No copy or allocation. Removal reorders dense elements and growth detaches old storage. Do not retain
    /// across mutations; read-only does not freeze owner writes.
    /// </remarks>
    public ReadOnlySpan<T> AsReadOnlySpan() => _dense.AsSpan(0, _count);

    /// <summary>
    /// Determines whether the <see cref="T:SwiftCollections.SwiftPackedSet`1" /> contains an element that matches
    /// the conditions defined by the specified predicate.
    /// </summary>
    /// <param name="match">The non-null predicate evaluated in dense order; must not mutate this set.</param>
    /// <returns>true if a predicate match exists; otherwise false.</returns>
    /// <exception cref="T:System.ArgumentNullException">match is null.</exception>
    public bool Exists(Predicate<T> match)
    {
        ChronicleGuard.ThrowIfNull(match, nameof(match));

        for (int i = 0; i < _count; i++)
        {
            if (match(_dense[i]))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Searches for an element that matches the conditions defined by the specified predicate, and returns the
    /// first matching element in dense iteration order.
    /// </summary>
    /// <param name="match">The non-null predicate evaluated in dense order; must not mutate this set.</param>
    /// <returns>The first dense-order match, or default(T) if none; a stored default is indistinguishable from
    /// absence.</returns>
    /// <exception cref="T:System.ArgumentNullException">match is null.</exception>
    public T Find(Predicate<T> match)
    {
        ChronicleGuard.ThrowIfNull(match, nameof(match));

        for (int i = 0; i < _count; i++)
        {
            if (match(_dense[i]))
                return _dense[i];
        }

        return default!;
    }

    /// <summary>
    /// Adds an element or mapping without replacing an existing equal key.
    /// </summary>
    /// <param name="value">The element or value; stored reference values are not cloned.</param>
    /// <returns>true if inserted; false for an existing equal element, with contents and capacity
    /// unchanged.</returns>
    /// <exception cref="T:System.ArgumentNullException">A required key or element is null.</exception>
    public bool Add(T value)
    {
        if (_lookup.ContainsKey(value))
            return false;

        EnsureCapacity(_count + 1);

        _dense[_count] = value;
        _lookup.Add(value, _count);

        _count++;
        _version++;

        return true;
    }

    /// <summary>
    /// Adds an element through the implemented collection interface.
    /// </summary>
    /// <param name="item">The element to compare, add or remove; reference values are not cloned.</param>
    void ICollection<T>.Add(T item) => Add(item);

    /// <summary>
    /// Removes one matching live entry.
    /// </summary>
    /// <param name="value">The element or value; stored reference values are not cloned.</param>
    /// <returns>true if the matching element was removed using swap-back; false for an absent or null value, with
    /// contents unchanged.</returns>
    public bool Remove(T value)
    {
        if (!_lookup.TryGetValue(value, out int index))
            return false;

        int last = --_count;

        if (index != last)
        {
            T moved = _dense[last];

            _dense[index] = moved;
            _lookup[moved] = index;
        }

        if (_clearReleasedSlots)
            _dense[last] = default!;
        _lookup.Remove(value);

        _version++;
        return true;
    }

    /// <summary>
    /// Removes all live entries while preserving reusable capacity.
    /// </summary>
    /// <remarks>
    /// Removes live entries while preserving capacity. Ordinary clearing resets active table metadata and stored
    /// references.
    /// </remarks>
    public void Clear()
    {
        if (_count == 0) return;

        if (_clearReleasedSlots)
            Array.Clear(_dense, 0, _count);
        _lookup.Clear();

        _count = 0;
        _version++;
    }

    /// <summary>
    /// Removes all elements, optionally releasing references throughout retained backing storage.
    /// </summary>
    /// <param name="clearRetainedReferences">If true, also releases references outside the live range; otherwise
    /// uses ordinary clearing.</param>
    /// <remarks>
    /// Removes live entries while preserving capacity. The Boolean overload can additionally release references
    /// in unused storage.
    /// </remarks>
    public void Clear(bool clearRetainedReferences)
    {
        if (clearRetainedReferences && _clearReleasedSlots)
        {
            Array.Clear(_dense, _count, _dense.Length - _count);
            _lookup.Clear(clearRetainedReferences: true);
        }
        Clear();
    }

    #endregion

    #region Capacity

    /// <summary>
    /// Ensures that the internal storage has at least the specified capacity, expanding it if necessary.
    /// </summary>
    /// <param name="capacity">Requested storage capacity; values below DefaultCapacity use the minimum
    /// allocation. Larger values round up. Keep requests below 2^30, where the capacity helper returns a
    /// sentinel.</param>
    /// <remarks>
    /// Grows storage only when needed; existing values survive. Capacity is storage size, not an insertion count
    /// guarantee. Keep requests below 2^30; large allocations may fail.
    /// </remarks>
    public void EnsureCapacity(int capacity)
    {
        int newCapacity = SwiftHashTools.NextPowerOfTwo(capacity);
        if (newCapacity <= _dense.Length)
            return;

        var newArray = new T[newCapacity];
        Array.Copy(_dense, newArray, _count);

        _dense = newArray;
    }

    #endregion

    #region Enumeration

    /// <summary>
    /// Creates an enumerator over the live entries.
    /// </summary>
    /// <returns>A value-type enumerator in dense storage order.</returns>
    /// <remarks>
    /// Mutation versions are checked by MoveNext and Reset. Ordering is not a persistence contract.
    /// </remarks>
    public SwiftPackedSetEnumerator GetEnumerator() => new(this);
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

    #region Clone

    /// <inheritdoc />
    public void CloneTo(ICollection<T> output)
    {
        ChronicleGuard.ThrowIfNull(output, nameof(output));

        output.Clear();

        for (int i = 0; i < _count; i++)
            output.Add(_dense[i]);
    }

    /// <summary>
    /// Copies live elements into caller-owned storage.
    /// </summary>
    /// <param name="array">The caller-owned destination; only the copied range is overwritten.</param>
    /// <param name="arrayIndex">The zero-based destination offset, from zero through array.Length, with remaining
    /// space for Count elements.</param>
    /// <exception cref="T:System.ArgumentNullException">array is null.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">arrayIndex is outside zero through
    /// array.Length.</exception>
    /// <exception cref="T:System.ArgumentException">There is insufficient remaining space or the array shape/type
    /// is unsupported.</exception>
    /// <remarks>
    /// Copies Count live entries in dense storage order into existing caller storage; does not append or clear
    /// the destination. Incompatible array elements can leave a partial copy.
    /// </remarks>
    public void CopyTo(T[] array, int arrayIndex)
    {
        ChronicleGuard.ThrowIfNull(array, nameof(array));
        ChronicleGuard.ThrowIfArrayIndexInvalid(arrayIndex, array.Length);
        ChronicleGuard.ThrowIfArgument(array.Length - arrayIndex < _count, nameof(array), "Destination array is not long enough.");

        Array.Copy(_dense, 0, array, arrayIndex, _count);
    }

    #endregion

    #region ISet<T> Implementations

    /// <summary>
    /// Removes elements belonging to the other input.
    /// </summary>
    /// <param name="other">The non-null sequence whose distinct elements define the other set; the sequence is
    /// consumed, not retained.</param>
    /// <exception cref="T:System.ArgumentNullException">other is null.</exception>
    /// <remarks>
    /// Removes members of other; self-exclusion clears this set. Input is consumed without retaining its storage.
    /// Enumeration or hashing failures may leave partial mutation; no rollback is guaranteed.
    /// </remarks>
    public void ExceptWith(IEnumerable<T> other)
    {
        ChronicleGuard.ThrowIfNull(other, nameof(other));

        if (ReferenceEquals(this, other))
        {
            Clear();
            return;
        }

        var otherSet = new SwiftHashSet<T>(other);

        foreach (var item in otherSet)
            Remove(item);
    }

    /// <summary>
    /// Retains elements belonging to both inputs.
    /// </summary>
    /// <param name="other">The non-null sequence whose distinct elements define the other set; the sequence is
    /// consumed, not retained.</param>
    /// <exception cref="T:System.ArgumentNullException">other is null.</exception>
    /// <remarks>
    /// Retains only members also in other; self-intersection changes nothing. Input is consumed without retaining
    /// its storage. Enumeration or hashing failures may leave partial mutation; no rollback is guaranteed.
    /// </remarks>
    public void IntersectWith(IEnumerable<T> other)
    {
        ChronicleGuard.ThrowIfNull(other, nameof(other));

        if (ReferenceEquals(this, other))
            return;

        var otherSet = new SwiftHashSet<T>(other);

        for (int i = _count - 1; i >= 0; i--)
        {
            var value = _dense[i];
            if (!otherSet.Contains(value))
                Remove(value);
        }
    }

    /// <summary>
    /// Checks for a subset with fewer distinct elements.
    /// </summary>
    /// <param name="other">The non-null sequence whose distinct elements define the other set; the sequence is
    /// consumed, not retained.</param>
    /// <returns>true if this set is a subset of other with fewer distinct elements; otherwise false. This set is
    /// not mutated.</returns>
    /// <exception cref="T:System.ArgumentNullException">other is null.</exception>
    /// <remarks>
    /// Uses this set's equality semantics. Duplicate input values do not change set membership; temporary set
    /// allocation may occur.
    /// </remarks>
    public bool IsProperSubsetOf(IEnumerable<T> other)
    {
        ChronicleGuard.ThrowIfNull(other, nameof(other));

        var set = new SwiftHashSet<T>(other);

        if (_count >= set.Count)
            return false;

        for (int i = 0; i < _count; i++)
            if (!set.Contains(_dense[i]))
                return false;

        return true;
    }

    /// <summary>
    /// Checks for a superset with more distinct elements.
    /// </summary>
    /// <param name="other">The non-null sequence whose distinct elements define the other set; the sequence is
    /// consumed, not retained.</param>
    /// <returns>true if this set is a superset of other with more distinct elements; otherwise false. This set is
    /// not mutated.</returns>
    /// <exception cref="T:System.ArgumentNullException">other is null.</exception>
    /// <remarks>
    /// Uses this set's equality semantics. Duplicate input values do not change set membership; temporary set
    /// allocation may occur.
    /// </remarks>
    public bool IsProperSupersetOf(IEnumerable<T> other)
    {
        ChronicleGuard.ThrowIfNull(other, nameof(other));

        var set = new SwiftHashSet<T>(other);

        if (_count <= set.Count)
            return false;

        foreach (var item in set)
            if (!Contains(item))
                return false;

        return true;
    }

    /// <summary>
    /// Checks whether all stored elements belong to the other input.
    /// </summary>
    /// <param name="other">The non-null sequence whose distinct elements define the other set; the sequence is
    /// consumed, not retained.</param>
    /// <returns>true if every element of this set belongs to other; otherwise false. This set is not
    /// mutated.</returns>
    /// <exception cref="T:System.ArgumentNullException">other is null.</exception>
    /// <remarks>
    /// Uses this set's equality semantics. Duplicate input values do not change set membership; temporary set
    /// allocation may occur.
    /// </remarks>
    public bool IsSubsetOf(IEnumerable<T> other)
    {
        ChronicleGuard.ThrowIfNull(other, nameof(other));

        var set = new SwiftHashSet<T>(other);

        if (_count > set.Count)
            return false;

        for (int i = 0; i < _count; i++)
            if (!set.Contains(_dense[i]))
                return false;

        return true;
    }

    /// <summary>
    /// Checks whether all other-input elements are stored here.
    /// </summary>
    /// <param name="other">The non-null sequence whose distinct elements define the other set; the sequence is
    /// consumed, not retained.</param>
    /// <returns>true if every distinct element of other belongs to this set; otherwise false. This set is not
    /// mutated.</returns>
    /// <exception cref="T:System.ArgumentNullException">other is null.</exception>
    /// <remarks>
    /// Uses this set's equality semantics. Duplicate input values do not change set membership; temporary set
    /// allocation may occur.
    /// </remarks>
    public bool IsSupersetOf(IEnumerable<T> other)
    {
        ChronicleGuard.ThrowIfNull(other, nameof(other));

        foreach (var item in other)
            if (!Contains(item))
                return false;

        return true;
    }

    /// <summary>
    /// Checks whether at least one element is shared.
    /// </summary>
    /// <param name="other">The non-null sequence whose distinct elements define the other set; the sequence is
    /// consumed, not retained.</param>
    /// <returns>true if at least one element is shared; otherwise false. This set is not mutated.</returns>
    /// <exception cref="T:System.ArgumentNullException">other is null.</exception>
    /// <remarks>
    /// Uses this set's equality semantics. Duplicate input values do not change set membership; temporary set
    /// allocation may occur.
    /// </remarks>
    public bool Overlaps(IEnumerable<T> other)
    {
        ChronicleGuard.ThrowIfNull(other, nameof(other));

        foreach (var item in other)
            if (Contains(item))
                return true;

        return false;
    }

    /// <summary>
    /// Checks whether both inputs contain the same distinct elements.
    /// </summary>
    /// <param name="other">The non-null sequence whose distinct elements define the other set; the sequence is
    /// consumed, not retained.</param>
    /// <returns>true if both sets contain exactly the same distinct elements; otherwise false. This set is not
    /// mutated.</returns>
    /// <exception cref="T:System.ArgumentNullException">other is null.</exception>
    /// <remarks>
    /// Uses this set's equality semantics. Duplicate input values do not change set membership; temporary set
    /// allocation may occur.
    /// </remarks>
    public bool SetEquals(IEnumerable<T> other)
    {
        ChronicleGuard.ThrowIfNull(other, nameof(other));

        var set = new SwiftHashSet<T>(other);

        if (_count != set.Count)
            return false;

        for (int i = 0; i < _count; i++)
            if (!set.Contains(_dense[i]))
                return false;

        return true;
    }

    /// <summary>
    /// Retains elements belonging to exactly one input.
    /// </summary>
    /// <param name="other">The non-null sequence whose distinct elements define the other set; the sequence is
    /// consumed, not retained.</param>
    /// <exception cref="T:System.ArgumentNullException">other is null.</exception>
    /// <remarks>
    /// Keeps elements belonging to exactly one input; duplicate input occurrences are treated once.
    /// Self-comparison clears this set. Input is consumed without retaining its storage. Enumeration or hashing
    /// failures may leave partial mutation; no rollback is guaranteed.
    /// </remarks>
    public void SymmetricExceptWith(IEnumerable<T> other)
    {
        ChronicleGuard.ThrowIfNull(other, nameof(other));

        if (ReferenceEquals(this, other))
        {
            Clear();
            return;
        }

        var set = new SwiftHashSet<T>(other);

        foreach (var item in set)
        {
            if (!Remove(item))
                Add(item);
        }
    }

    /// <summary>
    /// Adds elements belonging to the other input.
    /// </summary>
    /// <param name="other">The non-null sequence whose distinct elements define the other set; the sequence is
    /// consumed, not retained.</param>
    /// <exception cref="T:System.ArgumentNullException">other is null.</exception>
    /// <remarks>
    /// Adds members of other, preserving existing members and ignoring duplicates. Input is consumed without
    /// retaining its storage. Enumeration or hashing failures may leave partial mutation; no rollback is
    /// guaranteed.
    /// </remarks>
    public void UnionWith(IEnumerable<T> other)
    {
        ChronicleGuard.ThrowIfNull(other, nameof(other));

        foreach (var item in other)
            Add(item);
    }

    #endregion
}
