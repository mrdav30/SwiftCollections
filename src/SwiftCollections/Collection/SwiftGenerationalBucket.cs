using MemoryPack;
using System;
using System.Collections;
using System.Collections.Generic;

#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
using static System.Runtime.InteropServices.JavaScript.JSType;
#endif
#if !NET8_0_OR_GREATER
using System.Text.Json.Serialization.Shim;
#endif

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
/// When an item is added, a <see cref="Handle"/> containing both an index and generation
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

    public readonly struct Handle : IEquatable<Handle>
    {
        public readonly int Index;
        public readonly uint Generation;

        public Handle(int index, uint generation)
        {
            Index = index;
            Generation = generation;
        }

        public bool Equals(Handle other)
            => Index == other.Index && Generation == other.Generation;

        public override bool Equals(object obj)
            => obj is Handle h && Equals(h);

        public override int GetHashCode()
            => HashCode.Combine(Index, Generation);

        public override string ToString()
            => $"Handle({Index}:{Generation})";

        public static bool operator ==(Handle left, Handle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Handle left, Handle right)
        {
            return !(left == right);
        }
    }

    private struct Entry
    {
        public uint Generation;
        public bool IsUsed;
        public T Value;
    }

    #endregion

    #region Constants

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

    public SwiftGenerationalBucket() : this(DefaultCapacity) { }

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
    }

    #endregion

    #region Properties

    [JsonIgnore]
    [MemoryPackIgnore]
    public int Count => _count;

    [JsonIgnore]
    [MemoryPackIgnore]
    public int Capacity => _entries.Length;

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

            int capacity = items.Length < DefaultCapacity
                ? DefaultCapacity
                : SwiftHashTools.NextPowerOfTwo(items.Length);

            _entries = new Entry[capacity];
            _freeIndices = new SwiftIntStack(value.FreeIndices.Length);

            _peak = value.Peak;
            _count = 0;

            for (int i = 0; i < items.Length; i++)
            {
                ref Entry entry = ref _entries[i];

                entry.Generation = generations.Length > i ? generations[i] : 0;

                if (allocated.Length > i && allocated[i])
                {
                    entry.Value = items[i];
                    entry.IsUsed = true;
                    _count++;
                }
            }

            foreach (var index in value.FreeIndices)
                _freeIndices.Push(index);

            _version = 0;
        }
    }

    #endregion

    #region Core Operations

    public Handle Add(T value)
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

        return new Handle(index, entry.Generation);
    }

    public bool TryGet(Handle handle, out T value)
    {
        if ((uint)handle.Index >= (uint)_entries.Length)
        {
            value = default;
            return false;
        }

        ref Entry entry = ref _entries[handle.Index];

        if (!entry.IsUsed || entry.Generation != handle.Generation)
        {
            value = default;
            return false;
        }

        value = entry.Value;
        return true;
    }

    public ref T GetRef(Handle handle)
    {
        ref Entry entry = ref _entries[handle.Index];

        if (!entry.IsUsed || entry.Generation != handle.Generation)
            ThrowHelper.ThrowInvalidOperationException("Invalid handle");

        return ref entry.Value;
    }

    public bool Remove(Handle handle)
    {
        if ((uint)handle.Index >= (uint)_entries.Length)
            return false;

        ref Entry entry = ref _entries[handle.Index];

        if (!entry.IsUsed || entry.Generation != handle.Generation)
            return false;

        entry.Value = default;
        entry.IsUsed = false;

        entry.Generation++;

        _freeIndices.Push(handle.Index);

        _count--;
        _version++;

        return true;
    }

    public bool IsValid(Handle handle)
    {
        if ((uint)handle.Index >= (uint)_entries.Length)
            return false;

        ref Entry entry = ref _entries[handle.Index];

        return entry.IsUsed && entry.Generation == handle.Generation;
    }

    #endregion

    #region Capacity

    private void Resize(int newSize)
    {
        Entry[] newArray = new Entry[newSize];
        Array.Copy(_entries, newArray, _entries.Length);
        _entries = newArray;
    }

    #endregion

    #region Utility

    public void CloneTo(ICollection<T> output)
    {
        if (output == null)
            ThrowHelper.ThrowArgumentNullException(nameof(output));

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

    #endregion

    #region Enumeration

    public Enumerator GetEnumerator() => new(this);

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public struct Enumerator : IEnumerator<T>
    {
        private readonly SwiftGenerationalBucket<T> _bucket;
        private readonly uint _version;
        private int _index;
        private T _current;

        internal Enumerator(SwiftGenerationalBucket<T> bucket)
        {
            _bucket = bucket;
            _version = bucket._version;
            _index = -1;
            _current = default;
        }

        public readonly T Current => _current;

        readonly object IEnumerator.Current => _current;

        public bool MoveNext()
        {
            if (_version != _bucket._version)
                ThrowHelper.ThrowInvalidOperationException("Collection modified");

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

        public void Reset()
        {
            _index = -1;
            _current = default;
        }

        public readonly void Dispose() { }
    }

    #endregion
}