using MemoryPack;
using System;
using System.Collections;
using System.Collections.Generic;

#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#endif
#if !NET8_0_OR_GREATER
using System.Text.Json.Serialization.Shim;
#endif

namespace SwiftCollections;

[Serializable]
[JsonConverter(typeof(SwiftStateJsonConverterFactory))]
[MemoryPackable]
public sealed partial class SwiftPackedSet<T> :
    ISwiftCloneable<T>,
    IEnumerable<T>,
    IEnumerable
{
    #region Constants

    public const int DefaultCapacity = 8;

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

    #endregion

    #region State

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

            _dense = new T[Math.Max(DefaultCapacity, n)];
            _lookup = new SwiftDictionary<T, int>(n);

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

        _dense[last] = default;
        _lookup.Remove(value);

        _version++;
        return true;
    }

    public void Clear()
    {
        if (_count == 0) return;

        Array.Clear(_dense, 0, _count);
        _lookup.Clear();

        _count = 0;
        _version++;
    }

    #endregion

    #region Capacity

    public void EnsureCapacity(int capacity)
    {
        if (capacity <= _dense.Length)
            return;

        int newSize = _dense.Length * 2;
        if (newSize < capacity)
            newSize = capacity;

        var newArray = new T[newSize];
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
                ThrowHelper.ThrowInvalidOperationException("Collection modified during enumeration");

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
                ThrowHelper.ThrowInvalidOperationException("Collection modified during enumeration");

            _index = -1;
            Current = default;
        }

        public void Dispose() { }
    }

    #endregion

    #region Clone

    public void CloneTo(ICollection<T> output)
    {
        if (output == null)
            ThrowHelper.ThrowArgumentNullException(nameof(output));

        output.Clear();

        for (int i = 0; i < _count; i++)
            output.Add(_dense[i]);
    }

    #endregion
}
