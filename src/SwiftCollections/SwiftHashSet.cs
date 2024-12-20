using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace SwiftCollections
{
    /// <summary>
    /// Represents a high-performance set of unique values with efficient operations for addition, removal, and lookup
    /// </summary>
    /// <typeparam name="T">The type of elements in the set.</typeparam>
    [Serializable]
    public sealed class SwiftHashSet<T> : ICollection<T>, IEnumerable<T>, IEnumerable, IReadOnlyCollection<T>, ISerializable, IDeserializationCallback
    {
        #region Constants

        /// <summary>
        /// The default initial capacity of the set.
        /// </summary>
        public const int DefaultCapacity = 8;

        /// <summary>
        /// Determines the maximum allowable load factor before resizing the hash set to maintain performance.
        /// </summary>
        private const float _LoadFactorThreshold = 0.85f;

        #endregion

        #region Fields

        /// <summary>
        /// The array containing the entries of the SwiftHashSet.
        /// </summary>
        /// <remarks>
        /// Capacity will always be a power of two for efficient pooling cache.
        /// </remarks>
        private Entry[] _entries;

        /// <summary>
        /// The total number of entries in the hash set
        /// </summary>
        private int _count;

        private int _lastIndex;

        /// <summary>
        /// A mask used for efficiently computing the entry index from a hash code.
        /// This is typically the size of the entry array minus one, assuming the size is a power of two.
        /// </summary>
        private int _entryMask;

        /// <summary>
        /// The comparer used to determine equality of keys and to generate hash codes.
        /// </summary>
        private IEqualityComparer<T> _comparer;

        /// <summary>
        /// Specifies the dynamic growth factor for resizing, adjusted based on recent usage patterns.
        /// </summary>
        private int _adaptiveResizeFactor;

        /// <summary>
        /// Tracks the count threshold at which the hash set should resize based on the load factor.
        /// </summary>
        private uint _nextResizeCount;

        /// <summary>
        /// Represents the moving average of the fill rate, used to dynamically adjust resizing behavior.
        /// </summary>
        private double _movingFillRate;

        private int _maxStepCount;

        /// <summary>
        /// A version counter used to track modifications to the set.
        /// Incremented on mutations to detect changes during enumeration and ensure enumerator validity.
        /// </summary>
        private uint _version;

        #endregion

        #region Nested Types

        /// <summary>
        /// Represents a single value in the set, including its hash code for quick access.
        /// </summary>
        private struct Entry
        {
            public T Value;
            public int HashCode;    // Lower 31 bits of hash code, -1 if unused
            public bool IsUsed;
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Initialize a new instance of <see cref="SwiftHashSet{T}"/> with customizable capacity and comparer for optimal performance tailored to your needs.
        /// </summary>
        public SwiftHashSet() : this(DefaultCapacity, null) { }

        /// <inheritdoc cref="SwiftHashSet()"/>
        public SwiftHashSet(IEqualityComparer<T> comparer) : this(DefaultCapacity, comparer) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="SwiftHashSet{T}"/> class that is empty and has the default initial capacity.
        /// </summary>
        public SwiftHashSet(int capacity, IEqualityComparer<T> comparer = null)
        {
            _comparer = comparer ?? EqualityComparer<T>.Default;
            Initialize(capacity);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SwiftHashSet{T}"/> class that contains elements copied from the specified collection.
        /// </summary>
        /// <param name="collection">The collection whose elements are copied to the new set.</param>
        /// <param name="comparer">The comparer to use when comparing elements.</param>
        public SwiftHashSet(IEnumerable<T> collection, IEqualityComparer<T> comparer = null)
        {
            if (collection == null) ThrowHelper.ThrowArgumentNullException(nameof(collection));

            _comparer = comparer ?? EqualityComparer<T>.Default;
            int count = (collection as ICollection<T>)?.Count ?? DefaultCapacity;

            // Dynamic padding based on collision estimation
            int size = (int)(count / _LoadFactorThreshold);
            Initialize(size);

            foreach (T item in collection)
                InsertIfNotExists(item);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SwiftHashSet{T}"/> class with serialized data.
        /// </summary>
        /// <param name="info"></param>
        /// <param name="context"></param>
        public SwiftHashSet(SerializationInfo info, StreamingContext context)
        {
            HashHelper.SerializationInfoTable.Add(this, info);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the number of elements contained in the set.
        /// </summary>
        public int Count => _count;

        /// <summary>
        /// Gets the <see cref="IEqualityComparer{T}"/> object that is used to determine equality for the values in the set.
        /// </summary>
        public IEqualityComparer<T> Comparer => _comparer;

        bool ICollection<T>.IsReadOnly => false;

        #endregion

        #region Collection Manipulation

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Add(T item)
        {
            CheckLoadThreshold();
            return InsertIfNotExists(item);
        }
        void ICollection<T>.Add(T item) => Add(item);

        /// <summary>
        /// Adds the specified element to the set if it's not already present.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool InsertIfNotExists(T item)
        {
            if (item == null) ThrowHelper.ThrowArgumentNullException(nameof(item));

            int hashCode = _comparer.GetHashCode(item) & 0x7FFFFFFF;
            int entryIndex = hashCode & _entryMask;

            int step = 1;
            while (_entries[entryIndex].IsUsed)
            {
                if (_entries[entryIndex].HashCode == hashCode && _comparer.Equals(_entries[entryIndex].Value, item))
                    return false; // Item already exists

                entryIndex = (entryIndex + step * step) & _entryMask; // Quadratic probing
                step++;
            }

            if ((uint)entryIndex > (uint)_lastIndex) _lastIndex = entryIndex;

            _entries[entryIndex].HashCode = hashCode;
            _entries[entryIndex].Value = item;
            _entries[entryIndex].IsUsed = true;
            _count++;
            _version++;

            if ((uint)step > (uint)_maxStepCount)
            {
                _maxStepCount = step;
                if (_comparer is not IRandomedEqualityComparer && _maxStepCount > 100)
                    SwitchToRandomizedComparer();  // Attempt to recompute hash code with potential randomization for better distribution
            }

            return true;
        }

        /// <summary>
        /// Removes the specified element from the set.
        /// </summary>
        /// <param name="item">The element to remove from the set.</param>
        /// <returns>
        /// True if the element is successfully found and removed; otherwise, false.
        /// </returns>
        public bool Remove(T item)
        {
            if (item == null) ThrowHelper.ThrowArgumentNullException(nameof(item));

            int hashCode = _comparer.GetHashCode(item) & 0x7FFFFFFF;
            int entryIndex = hashCode & _entryMask;

            int step = 1;
            while ((uint)step <= (uint)_lastIndex)
            {
                ref Entry entry = ref _entries[entryIndex];
                // Stop probing if an unused entry is found (not deleted)
                if (!entry.IsUsed && entry.HashCode != -1)
                    return false;
                if (entry.IsUsed && entry.HashCode == hashCode && _comparer.Equals(entry.Value, item))
                {
                    // Mark entry as deleted
                    entry.IsUsed = false;
                    entry.Value = default;
                    entry.HashCode = -1;
                    _count--;
                    if ((uint)_count == 0) _lastIndex = 0;
                    _version++;
                    return true;
                }

                // Entry not found in expected entry, it either doesn't exist or was moved via quadratic probing
                entryIndex = (entryIndex + step * step) & _entryMask;
                step++;
            }
            return false; // Item not found after full loop
        }

        /// <summary>
        /// Removes all elements from the set.
        /// </summary>
        public void Clear()
        {
            if ((uint)_count == 0) return;

            for (uint i = 0; i <= (uint)_lastIndex; i++)
            {
                _entries[i].HashCode = -1;
                _entries[i].Value = default;
                _entries[i].IsUsed = false;
            }

            _count = 0;
            _lastIndex = 0;
            _maxStepCount = 0;
            _movingFillRate = 0;
            _adaptiveResizeFactor = 4;

            _version++;
        }

        #endregion

        #region Capacity Management

        /// <summary>
        /// Ensures that the hash set is resized when the current load factor exceeds the predefined threshold.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckLoadThreshold()
        {
            if ((uint)_count >= _nextResizeCount)
                Resize(_entries.Length * _adaptiveResizeFactor);
        }

        /// <summary>
        /// Ensures that the set can hold up to the specified number of elements without resizing.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureCapacity(int capacity)
        {
            capacity = HashHelper.NextPowerOfTwo(capacity);  // Capacity must be a power of 2 for proper masking
            if (capacity > _entries.Length)
                Resize(capacity);
        }

        /// <summary>
        /// Resizes the hash set to the specified capacity, redistributing all entries to maintain efficiency.
        /// </summary>
        /// <param name="newSize"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Resize(int newSize)
        {
            Entry[] newEntries = new Entry[newSize];
            int newMask = newSize - 1;

            int lastIndex = 0;
            for (uint i = 0; i <= (uint)_lastIndex; i++)
            {
                if (_entries[i].IsUsed)
                {
                    ref Entry oldEntry = ref _entries[i];
                    int newIndex = oldEntry.HashCode & newMask;
                    // If current entry not available, perform Quadratic probing to find the next available entry
                    int step = 1;
                    while (newEntries[newIndex].IsUsed)
                    {
                        newIndex = (newIndex + step * step) & newMask;
                        step++;
                    }
                    newEntries[newIndex] = oldEntry;
                    if (newIndex > lastIndex) lastIndex = newIndex;
                }
            }

            _lastIndex = lastIndex;

            CalculateAdaptiveResizeFactors(newSize);

            _entries = newEntries;
            _entryMask = newMask;

            _version++;
        }

        /// <summary>
        /// Sets the capacity of a <see cref="SwiftHashSet{T}"/> to the actual 
        /// number of elements it contains, rounded up to a nearby next power of 2 value.
        /// </summary>
        public void TrimExcess()
        {
            int newSize = _count <= DefaultCapacity ? DefaultCapacity : HashHelper.NextPowerOfTwo(_count);
            if (newSize >= _entries.Length) return;

            Entry[] newEntries = new Entry[newSize];
            int newMask = newSize - 1;

            int lastIndex = 0;
            for (int i = 0; i <= (uint)_lastIndex; i++)
            {
                if (_entries[i].IsUsed)
                {
                    ref Entry oldEntry = ref _entries[i];
                    int newIndex = oldEntry.HashCode & newMask;
                    // If current entry not available, perform quadratic probing to find the next available entry
                    int step = 1;
                    while (newEntries[newIndex].IsUsed)
                    {
                        newIndex = (newIndex + step * step) & newMask;
                        step++;
                    }
                    newEntries[newIndex] = oldEntry;
                    if (newIndex > lastIndex) lastIndex = newIndex;
                }
            }

            _lastIndex = lastIndex;

            CalculateAdaptiveResizeFactors(newSize);

            _entryMask = newMask;
            _entries = newEntries;

            _version++;
        }

        /// <summary>
        ///  Updates adaptive resize parameters based on the current fill rate to balance memory usage and performance.
        /// </summary>
        /// <param name="newSize"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CalculateAdaptiveResizeFactors(int newSize)
        {
            // Calculate current fill rate and update moving average
            double currentFillRate = (double)_count / newSize;
            _movingFillRate = _movingFillRate == 0 ? currentFillRate : (_movingFillRate * 0.7 + currentFillRate * 0.3);

            if (_movingFillRate > 0.3f)
                _adaptiveResizeFactor = 2; // Growth stabilizing
            else if (_movingFillRate < 0.28f)
                _adaptiveResizeFactor = 4; // Rapid growth

            // Reset the resize threshold based on the new size
            _nextResizeCount = (uint)(newSize * _LoadFactorThreshold);
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Initializes the hash set with a given capacity, ensuring it starts with an optimal internal structure.
        /// </summary>
        /// <param name="capacity"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Initialize(int capacity)
        {
            int size = capacity < DefaultCapacity ? DefaultCapacity: HashHelper.NextPowerOfTwo(capacity);
            _entries = new Entry[size];
            _entryMask = size - 1;

            _nextResizeCount = (uint)(size * _LoadFactorThreshold);
            _adaptiveResizeFactor = 4; // start agressive
            _movingFillRate = 0.0;
        }

        /// <summary>
        /// Determines whether the set contains the specified element.
        /// </summary>
        /// <param name="item">The element to locate in the set.</param>
        /// <returns>
        /// True if the set contains the specified element; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(T item) => FindEntry(item) >= 0;

        /// <summary>
        /// Searches the set for a given value and returns the equal value it finds, if any.
        /// </summary>
        public bool TryGetValue(T expected, out T actual)
        {
            int index = FindEntry(expected);
            if (index >= 0)
            {
                actual = _entries[index].Value;
                return true;
            }
            actual = default;
            return false;
        }

        /// <summary>
        /// Copies the elements of the set to an array, starting at the specified array index.
        /// </summary>
        public void CopyTo(T[] array, int arrayIndex)
        {
            if (array == null) ThrowHelper.ThrowArgumentNullException(nameof(array));
            if ((uint)arrayIndex > array.Length) ThrowHelper.ThrowArgumentOutOfRangeException();
            if (array.Length - arrayIndex < _count) ThrowHelper.ThrowInvalidOperationException("The array is not large enough to hold the elements.");

            for (uint i = 0; i <= (uint)_lastIndex; i++)
            {
                if (_entries[i].IsUsed)
                    array[arrayIndex++] = _entries[i].Value;
            }
        }

        /// <summary>
        /// Switches the hash set's comparer and rehashes all entries 
        /// using the new comparer to redistribute them across <see cref="_entries"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SwitchComparer(IEqualityComparer<T> newComparer = null)
        {
            if (newComparer == null || newComparer.Equals(this)) return;
            _comparer = newComparer;
            RehashEntries();
            _version++;
        }

        /// <summary>
        /// Replaces the hash set's comparer with a randomized comparer to mitigate high collision rates.
        /// </summary>
        private void SwitchToRandomizedComparer()
        {
            if (_comparer == EqualityComparer<string>.Default || _comparer == EqualityComparer<object>.Default)
                _comparer = (IEqualityComparer<T>)HashHelper.GetSwiftEqualityComparer(_comparer);
            else return; // nothing to do here

            RehashEntries();
            _maxStepCount = 0;

            _version++;
        }

        /// <summary>
        /// Reconstructs the internal entry structure to align with updated hash codes, ensuring efficient access and storage.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RehashEntries()
        {
            Entry[] newEntries = new Entry[_entries.Length];
            int newMask = newEntries.Length - 1;

            int lastIndex = 0;
            for (uint i = 0; i <= (uint)_lastIndex; i++)
            {
                if (_entries[i].IsUsed)
                {
                    ref Entry oldEntry = ref _entries[i];
                    oldEntry.HashCode = _comparer.GetHashCode(oldEntry.Value) & 0x7FFFFFFF;
                    int newIndex = oldEntry.HashCode & newMask;
                    int step = 1;
                    while (newEntries[newIndex].IsUsed)
                    {
                        newIndex = (newIndex + step * step) & newMask; // Quadratic probing
                        step++;
                    }
                    newEntries[newIndex] = _entries[i];
                    if (newIndex > lastIndex) lastIndex = newIndex;
                }
            }

            _lastIndex = lastIndex;

            _entryMask = newMask;
            _entries = newEntries;

            _version++;
        }

        /// <summary>
        /// Searches for an entry in the hash set by following its probing sequence, returning its index if found.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int FindEntry(T item)
        {
            if (item == null) return -1;

            int hashCode = _comparer.GetHashCode(item) & 0x7FFFFFFF;
            int entryIndex = hashCode & _entryMask;

            int step = 1;
            while ((uint)step <= (uint)_lastIndex)
            {
                ref Entry entry = ref _entries[entryIndex];
                // Stop probing if an unused entry is found (not deleted)
                if (!entry.IsUsed && entry.HashCode != -1)
                    return -1;
                if (entry.IsUsed && entry.HashCode == hashCode && _comparer.Equals(entry.Value, item))
                    return entryIndex; // Match found
                // Perform quadratic probing to see if maybe the entry was shifted.
                entryIndex = (entryIndex + step * step) & _entryMask;
                step++;
            }
            return -1; // Item not found, full loop completed
        }

        #endregion

        #region Enumerators

        /// <summary>
        /// Returns an enumerator that iterates through the set.
        /// </summary>
        public SwiftHashSetEnumerator GetEnumerator() => new SwiftHashSetEnumerator(this);
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Provides an enumerator for iterating through the elements of the hash set, ensuring consistency during enumeration.
        /// </summary>
        public struct SwiftHashSetEnumerator : IEnumerator<T>, IEnumerator, IDisposable
        {
            private readonly SwiftHashSet<T> _set;
            private readonly Entry[] _entries;
            private readonly uint _version;
            private int _index;
            private T _current;

            internal SwiftHashSetEnumerator(SwiftHashSet<T> set)
            {
                _set = set;
                _version = set._version;
                _entries = set._entries; // Cache the entry array
                _index = -1;
                _current = default;
            }

            public T Current => _current;

            object IEnumerator.Current
            {
                get
                {
                    if (_index > (uint)_set._lastIndex) ThrowHelper.ThrowInvalidOperationException("Bad enumeration");
                    return _current;
                }
            }

            public bool MoveNext()
            {
                if (_version != _set._version)
                    throw new InvalidOperationException("Collection was modified during enumeration.");

                while (++_index <= (uint)_set._lastIndex)
                {
                    if (_entries[_index].IsUsed)
                    {
                        _current = _entries[_index].Value;
                        return true;
                    }
                }

                _current = default;
                return false;
            }

            public void Reset()
            {
                if (_version != _set._version)
                    ThrowHelper.ThrowInvalidOperationException("Collection was modified during enumeration.");

                _index = -1;
                _current = default;
            }

            public void Dispose() { }
        }

        #endregion

        #region Serialization

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null) ThrowHelper.ThrowArgumentNullException(nameof(info));

            info.AddValue("Version", _version);
            info.AddValue("Comparer", _comparer, typeof(IEqualityComparer<T>));
            info.AddValue("Capacity", _entries?.Length ?? 0);

            if (_entries != null)
            {
                T[] array = new T[_count];
                CopyTo(array, 0);
                info.AddValue("Elements", array, typeof(T[]));
            }
        }

        public void OnDeserialization(object sender)
        {
            if (_entries != null) return; // Already initialized

            if (!HashHelper.SerializationInfoTable.TryGetValue(this, out SerializationInfo info))
                return;

            int realVersion = info.GetInt32("Version");
            int capacity = info.GetInt32("Capacity");
            _comparer = (IEqualityComparer<T>)info.GetValue("Comparer", typeof(IEqualityComparer<T>)) ?? EqualityComparer<T>.Default;

            if (capacity != 0)
            {
                Initialize(capacity);

                T[] elements = (T[])info.GetValue("Elements", typeof(T[]));
                if (elements == null) ThrowHelper.ThrowSerializationException("Missing entries!");

                foreach (T item in elements)
                {
                    if (item != null)
                        InsertIfNotExists(item);
                }
            }
            else
                Initialize(DefaultCapacity);

            _version = (uint)realVersion;
            HashHelper.SerializationInfoTable.Remove(this);
        }

        #endregion
    }
}
