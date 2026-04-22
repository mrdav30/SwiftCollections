using MemoryPack;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace SwiftCollections;

/// <summary>
/// Represents a high-performance set that stores unique values in a densely packed array
/// while providing O(1) lookups via an internal hash map.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SwiftPackedSet{T}"/> maintains values in a contiguous array for extremely
/// cache-friendly iteration while using a hash-based lookup table to guarantee fast
/// membership tests and removals.
/// </para>
/// <para>
/// Removal uses a swap-back strategy that keeps the dense storage contiguous but does not
/// preserve ordering. As a result, iteration order is not guaranteed to remain stable.
/// </para>
/// <para>
/// This structure is commonly used in high-performance systems such as ECS (Entity Component Systems)
/// where dense iteration speed is critical.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of elements contained in the set.</typeparam>
[Serializable]
[JsonConverter(typeof(SwiftStateJsonConverterFactory))]
[MemoryPackable]
public sealed partial class SwiftPackedSet<T> : ISwiftCloneable<T>, ISet<T>, IEnumerable<T>, IEnumerable
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
    /// <remarks>
    /// The actual capacity is rounded up to the next power of two greater than or equal to the specified value, 
    /// unless the specified value is less than or equal to the default capacity.
    /// </remarks>
    /// <param name="capacity">
    /// The initial number of elements that the set can contain before resizing. 
    /// If less than or equal to the default capacity, the default capacity is used. 
    /// Must be non-negative.
    /// </param>
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
    /// <param name="state">The state object that provides the initial data and configuration for the set. Cannot be null.</param>
    [MemoryPackConstructor]
    public SwiftPackedSet(SwiftArrayState<T> state)
    {
        State = state;

        SwiftThrowHelper.ThrowIfNull(_dense, nameof(_dense));
        SwiftThrowHelper.ThrowIfNull(_lookup, nameof(_lookup));
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
    /// Gets the total number of elements that the collection can hold without resizing.
    /// </summary>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Capacity => _dense.Length;

    /// <summary>
    /// Gets a value indicating whether access to the collection is synchronized (thread safe).
    /// </summary>
    [JsonIgnore]
    [MemoryPackIgnore]
    public bool IsSynchronized => false;

    /// <inheritdoc/>
    [JsonIgnore]
    [MemoryPackIgnore]
    public object SyncRoot => _syncRoot ??= new object();

    /// <summary>
    /// Gets the underlying dense array of elements.
    /// </summary>
    [JsonIgnore]
    [MemoryPackIgnore]
    public T[] Dense => _dense;

    /// <inheritdoc/>
    [JsonIgnore]
    [MemoryPackIgnore]
    public bool IsReadOnly => false;

    /// <summary>
    /// Gets or sets the current state of the array, including its items and order.
    /// </summary>
    /// <remarks>
    /// Use this property to capture or restore the array's contents and structure. 
    /// Setting this property replaces the entire array with the provided state.
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
            SwiftThrowHelper.ThrowIfNull(value.Items, nameof(value.Items));

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

    /// <inheritdoc/>
    public bool Contains(T value)
        => _lookup.ContainsKey(value);

    /// <summary>
    /// Returns a read-only span over the populated dense portion of the set.
    /// </summary>
    public ReadOnlySpan<T> AsReadOnlySpan() => _dense.AsSpan(0, _count);

    /// <summary>
    /// Determines whether the <see cref="SwiftPackedSet{T}"/> contains an element that matches the conditions defined by the specified predicate.
    /// </summary>
    /// <param name="match">The predicate that defines the conditions of the element to search for.</param>
    /// <returns><c>true</c> if the <see cref="SwiftPackedSet{T}"/> contains one or more elements that match the specified predicate; otherwise, <c>false</c>.</returns>
    public bool Exists(Predicate<T> match)
    {
        SwiftThrowHelper.ThrowIfNull(match, nameof(match));

        for (int i = 0; i < _count; i++)
        {
            if (match(_dense[i]))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Searches for an element that matches the conditions defined by the specified predicate, and returns the first matching element in dense iteration order.
    /// </summary>
    /// <param name="match">The predicate that defines the conditions of the element to search for.</param>
    /// <returns>The first element that matches the conditions defined by the specified predicate, if found; otherwise, the default value for type <typeparamref name="T"/>.</returns>
    public T Find(Predicate<T> match)
    {
        SwiftThrowHelper.ThrowIfNull(match, nameof(match));

        for (int i = 0; i < _count; i++)
        {
            if (match(_dense[i]))
                return _dense[i];
        }

        return default!;
    }

    /// <inheritdoc/>
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

    void ICollection<T>.Add(T item) => Add(item);

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public void Clear()
    {
        if (_count == 0) return;

        if (_clearReleasedSlots)
            Array.Clear(_dense, 0, _count);
        _lookup.Clear();

        _count = 0;
        _version++;
    }

    #endregion

    #region Capacity

    /// <summary>
    /// Ensures that the internal storage has at least the specified capacity, expanding it if necessary.
    /// </summary>
    /// <remarks>
    /// If the current capacity is less than the specified value, the internal storage is resized to
    /// the next power of two greater than or equal to the specified capacity. 
    /// Existing elements are preserved.
    /// </remarks>
    /// <param name="capacity">The minimum number of elements that the internal storage should be able to hold. Must be non-negative.</param>
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

    /// <inheritdoc cref="IEnumerable.GetEnumerator()"/>
    public SwiftPackedSetEnumerator GetEnumerator() => new(this);
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Enumerates the elements of a <see cref="SwiftPackedSet{T}"/> collection.
    /// </summary>
    /// <remarks>
    /// The enumerator is invalidated if the collection is modified after the enumerator is created.
    /// In such cases, calling MoveNext or Reset will throw an InvalidOperationException. 
    /// </remarks>
    public struct SwiftPackedSetEnumerator : IEnumerator<T>
    {
        private readonly SwiftPackedSet<T> _set;
        private readonly uint _version;
        private int _index;

        internal SwiftPackedSetEnumerator(SwiftPackedSet<T> set)
        {
            _set = set;
            _version = set._version;
            _index = -1;
            Current = default!;
        }

        /// <inheritdoc/>
        public T Current { get; private set; }

        object IEnumerator.Current => Current;

        /// <inheritdoc/>
        public bool MoveNext()
        {
            if (_version != _set._version)
                throw new InvalidOperationException("Collection modified during enumeration");

            int next = _index + 1;
            if (next >= _set._count)
            {
                Current = default!;
                return false;
            }

            _index = next;
            Current = _set._dense[next];
            return true;
        }

        /// <inheritdoc/>
        public void Reset()
        {
            if (_version != _set._version)
                throw new InvalidOperationException("Collection modified during enumeration");

            _index = -1;
            Current = default!;
        }

        /// <inheritdoc/>
        public void Dispose() => _index = -1;
    }

    #endregion

    #region Clone

    /// <inheritdoc/>
    public void CloneTo(ICollection<T> output)
    {
        SwiftThrowHelper.ThrowIfNull(output, nameof(output));

        output.Clear();

        for (int i = 0; i < _count; i++)
            output.Add(_dense[i]);
    }

    /// <inheritdoc/>
    public void CopyTo(T[] array, int arrayIndex)
    {
        SwiftThrowHelper.ThrowIfNull(array, nameof(array));
        if ((uint)arrayIndex > array.Length) throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        if (array.Length - arrayIndex < _count) throw new ArgumentException("Destination array is not long enough.", nameof(array));

        Array.Copy(_dense, 0, array, arrayIndex, _count);
    }

    #endregion

    #region ISet<T> Implementations

    /// <inheritdoc/>
    public void ExceptWith(IEnumerable<T> other)
    {
        SwiftThrowHelper.ThrowIfNull(other, nameof(other));

        if (ReferenceEquals(this, other))
        {
            Clear();
            return;
        }

        var otherSet = new SwiftHashSet<T>(other);

        foreach (var item in otherSet)
            Remove(item);
    }

    /// <inheritdoc/>
    public void IntersectWith(IEnumerable<T> other)
    {
        SwiftThrowHelper.ThrowIfNull(other, nameof(other));

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

    /// <inheritdoc/>
    public bool IsProperSubsetOf(IEnumerable<T> other)
    {
        SwiftThrowHelper.ThrowIfNull(other, nameof(other));

        var set = new SwiftHashSet<T>(other);

        if (_count >= set.Count)
            return false;

        for (int i = 0; i < _count; i++)
            if (!set.Contains(_dense[i]))
                return false;

        return true;
    }

    /// <inheritdoc/>
    public bool IsProperSupersetOf(IEnumerable<T> other)
    {
        SwiftThrowHelper.ThrowIfNull(other, nameof(other));

        var set = new SwiftHashSet<T>(other);

        if (_count <= set.Count)
            return false;

        foreach (var item in set)
            if (!Contains(item))
                return false;

        return true;
    }

    /// <inheritdoc/>
    public bool IsSubsetOf(IEnumerable<T> other)
    {
        SwiftThrowHelper.ThrowIfNull(other, nameof(other));

        var set = new SwiftHashSet<T>(other);

        if (_count > set.Count)
            return false;

        for (int i = 0; i < _count; i++)
            if (!set.Contains(_dense[i]))
                return false;

        return true;
    }

    /// <inheritdoc/>
    public bool IsSupersetOf(IEnumerable<T> other)
    {
        SwiftThrowHelper.ThrowIfNull(other, nameof(other));

        foreach (var item in other)
            if (!Contains(item))
                return false;

        return true;
    }

    /// <inheritdoc/>
    public bool Overlaps(IEnumerable<T> other)
    {
        SwiftThrowHelper.ThrowIfNull(other, nameof(other));

        foreach (var item in other)
            if (Contains(item))
                return true;

        return false;
    }

    /// <inheritdoc/>
    public bool SetEquals(IEnumerable<T> other)
    {
        SwiftThrowHelper.ThrowIfNull(other, nameof(other));

        var set = new SwiftHashSet<T>(other);

        if (_count != set.Count)
            return false;

        for (int i = 0; i < _count; i++)
            if (!set.Contains(_dense[i]))
                return false;

        return true;
    }

    /// <inheritdoc/>
    public void SymmetricExceptWith(IEnumerable<T> other)
    {
        SwiftThrowHelper.ThrowIfNull(other, nameof(other));

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

    /// <inheritdoc/>
    public void UnionWith(IEnumerable<T> other)
    {
        SwiftThrowHelper.ThrowIfNull(other, nameof(other));

        foreach (var item in other)
            Add(item);
    }

    #endregion
}
