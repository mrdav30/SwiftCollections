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
public sealed partial class SwiftPackedSet<T> :
    ISwiftCloneable<T>,
    ISet<T>,
    IEnumerable<T>,
    IEnumerable
{
    #region Constants

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
    private object _syncRoot;

    #endregion

    #region Constructors

    public SwiftPackedSet() : this(DefaultCapacity) { }

    public SwiftPackedSet(int capacity)
    {
        capacity = capacity <= DefaultCapacity
            ? DefaultCapacity
            : SwiftHashTools.NextPowerOfTwo(capacity);

        _dense = new T[capacity];
        _lookup = new SwiftDictionary<T, int>(capacity);
    }

    [MemoryPackConstructor]
    public SwiftPackedSet(SwiftArrayState<T> state)
    {
        State = state;
    }

    #endregion

    #region Properties

    [JsonIgnore]
    [MemoryPackIgnore]
    public int Count => _count;

    [JsonIgnore]
    [MemoryPackIgnore]
    public int Capacity => _dense.Length;

    [JsonIgnore]
    [MemoryPackIgnore]
    public bool IsSynchronized => false;

    [JsonIgnore]
    [MemoryPackIgnore]
    public object SyncRoot => _syncRoot ??= new object();

    [JsonIgnore]
    [MemoryPackIgnore]
    public T[] Dense => _dense;

    [JsonIgnore]
    [MemoryPackIgnore]
    public bool IsReadOnly => false;

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
            var values = value.Items ?? Array.Empty<T>();

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

        return default;
    }

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
            _dense[last] = default;
        _lookup.Remove(value);

        _version++;
        return true;
    }

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

    public Enumerator GetEnumerator() => new Enumerator(this);

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public struct Enumerator : IEnumerator<T>
    {
        private readonly SwiftPackedSet<T> _set;
        private readonly uint _version;
        private int _index;

        internal Enumerator(SwiftPackedSet<T> set)
        {
            _set = set;
            _version = set._version;
            _index = -1;
            Current = default;
        }

        public T Current { get; private set; }

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            if (_version != _set._version)
                throw new InvalidOperationException("Collection modified during enumeration");

            int next = _index + 1;
            if (next >= _set._count)
            {
                Current = default;
                return false;
            }

            _index = next;
            Current = _set._dense[next];
            return true;
        }

        public void Reset()
        {
            if (_version != _set._version)
                throw new InvalidOperationException("Collection modified during enumeration");

            _index = -1;
            Current = default;
        }

        public void Dispose() { }
    }

    #endregion

    #region Clone

    public void CloneTo(ICollection<T> output)
    {
        SwiftThrowHelper.ThrowIfNull(output, nameof(output));

        output.Clear();

        for (int i = 0; i < _count; i++)
            output.Add(_dense[i]);
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        SwiftThrowHelper.ThrowIfNull(array, nameof(array));
        if ((uint)arrayIndex > array.Length) throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        if (array.Length - arrayIndex < _count) throw new ArgumentException("Destination array is not long enough.", nameof(array));

        Array.Copy(_dense, 0, array, arrayIndex, _count);
    }

    #endregion

    #region ISet<T> Implementations

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

    public bool IsSupersetOf(IEnumerable<T> other)
    {
        SwiftThrowHelper.ThrowIfNull(other, nameof(other));

        foreach (var item in other)
            if (!Contains(item))
                return false;

        return true;
    }

    public bool Overlaps(IEnumerable<T> other)
    {
        SwiftThrowHelper.ThrowIfNull(other, nameof(other));

        foreach (var item in other)
            if (Contains(item))
                return true;

        return false;
    }

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

    public void UnionWith(IEnumerable<T> other)
    {
        SwiftThrowHelper.ThrowIfNull(other, nameof(other));

        foreach (var item in other)
            Add(item);
    }

    #endregion
}
