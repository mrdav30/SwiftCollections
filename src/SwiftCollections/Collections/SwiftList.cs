using System.Collections;
using System.Collections.Generic;
using System;
using System.Runtime.CompilerServices;

namespace SwiftCollections
{
    /// <summary>
    /// <c>SwiftList&lt;T&gt;</c> is a high-performance, memory-efficient dynamic list designed to outperform
    /// traditional generic lists in speed-critical applications. 
    /// <para>
    /// By utilizing custom growth and
    /// shrink strategies, SwiftList optimizes memory allocation and minimizes resizing overhead,
    /// all while maintaining compact storage. With aggressive inlining and optimized algorithms,
    /// SwiftList delivers faster iteration, insertion, and overall memory management compared to
    /// standard List. It is ideal for scenarios where predictable performance and minimal
    /// memory allocations are essential.
    /// </para>
    /// <para>
    /// This implementation is optimized for performance and does not perform versioning checks.
    /// Modifying the list during enumeration may result in undefined behavior.
    /// </para>
    /// </summary>
    /// <typeparam name="T">Specifies the type of elements in the list.</typeparam>
    [Serializable]
    public class SwiftList<T> : ISwiftCloneable<T>, IEnumerable<T>, IEnumerable, ICollection<T>, ICollection, IList<T>, IList
    {
        #region Constants

        /// <summary>
        /// The default initial capacity of the <see cref="SwiftList{T}"/> if none is specified.
        /// Used to allocate a reasonable starting size to minimize resizing operations.
        /// </summary>
        public const int DefaultCapacity = 4;

        private static readonly T[] _emptyArray = new T[0];

        #endregion

        #region Fields

        /// <summary>
        /// The internal array that stores elements of the SwiftList. Resized as needed to
        /// accommodate additional elements. Not directly exposed outside the list.
        /// </summary>
        private T[] _innerArray;

        /// <summary>
        /// The current number of elements in the SwiftList. Represents the total count of
        /// valid elements stored in the list, also indicating the arrayIndex of the next insertion point.
        /// </summary>
        protected int _count;

        /// <summary>
        /// An object that can be used to synchronize access to the SwiftList.
        /// </summary>
        [NonSerialized]
        private object _syncRoot;

        private uint _version;

        #endregion

        #region Constructors

        public SwiftList() : this(0) { }

        /// <summary>
        /// Initializes a new, empty instance of <see cref="SwiftList{T}"/> with the specified initial capacity.
        /// </summary>
        public SwiftList(int capacity)
        {
            if (capacity == 0)
                _innerArray = _emptyArray;
            else
            {
                capacity = HashTools.NextPowerOfTwo(capacity <= DefaultCapacity ? DefaultCapacity : capacity);
                _innerArray = new T[capacity];
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SwiftList{T}"/> class with elements from the specified collection.
        /// The collection must have a known count for optimized memory allocation.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown if the input collection does not have a known count.</exception>
        public SwiftList(IEnumerable<T> items)
        {
            if (items == null) ThrowHelper.ThrowArgumentNullException(nameof(items));

            if (items is ICollection<T> collection)
            {
                int count = collection.Count;
                if (count == 0)
                    _innerArray = _emptyArray;
                else
                {
                    _innerArray = new T[count];
                    collection.CopyTo(_innerArray, 0);
                    _count = count;
                }
            }
            else
            {
                _innerArray = new T[DefaultCapacity];
                AddRange(items); // Will handle capacity increases as needed
            }
        }

        #endregion

        #region Properties

        public T[] InnerArray => _innerArray;

        /// <summary>
        /// Gets the total number of elements the SwiftList can hold without resizing.
        /// Reflects the current allocated size of the internal array.
        /// </summary>
        public int Capacity => _innerArray.Length;

        /// <inheritdoc cref="_count"/>
        public int Count => _count;

        public bool IsReadOnly => false;
        public bool IsSynchronized => false;
        object ICollection.SyncRoot => _syncRoot ??= new object();

        bool IList.IsFixedSize => false;

        object IList.this[int index]
        {
            get => this[index];
            set
            {
                try
                {
                    this[index] = (T)value;
                }
                catch
                {
                    ThrowHelper.ThrowNotSupportedException($"Unsupported value type for {value}");
                }
            }
        }

        /// <summary>
        /// Gets the element at the specified arrayIndex.
        /// </summary>
        public T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if ((uint)index >= (uint)_count) ThrowHelper.ThrowArgumentOutOfRangeException();
                return _innerArray[index];
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if ((uint)index >= (uint)_count) ThrowHelper.ThrowArgumentOutOfRangeException();
                _innerArray[index] = value;
            }
        }

        #endregion

        #region Collection Manipulation

        /// <summary>
        /// Adds an object to the end of the SwiftList.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(T item)
        {
            if ((uint)_count == (uint)_innerArray.Length)
                Resize(_innerArray.Length * 2);
            _innerArray[_count++] = item;
            _version++;
        }

        int IList.Add(object value)
        {
            try
            {
                Add((T)value);
            }
            catch (InvalidCastException)
            {
                ThrowHelper.ThrowNotSupportedException($"Wrong value type for {value}");
            }

            return _count - 1;
        }

        /// <summary>
        /// Adds the elements of the specified collection to the end of the SwiftList.
        /// </summary>
        public void AddRange(IEnumerable<T> items)
        {
            if (items == null) ThrowHelper.ThrowArgumentNullException(nameof(items));

            if (items is ICollection<T> collection)
            {
                // Ensure capacity to fit all new items
                if (_count + collection.Count > _innerArray.Length)
                {
                    int newCapacity = HashTools.NextPowerOfTwo(_count + collection.Count);
                    Resize(newCapacity);
                }

                // Copy new items directly into the internal array
                collection.CopyTo(_innerArray, _count);
                _count += collection.Count;
                _version++;

                return;
            }

            // Fallback for non-ICollection, adding each item individually
            foreach (T item in items)
                Add(item);
        }

        /// <summary>
        /// Removes the first occurrence of a specific object from the SwiftList.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(T item)
        {
            int index = IndexOf(item);
            if (index < 0) return false; // Item not found return false;
            RemoveAt(index);
            return true;
        }

        void IList.Remove(object value)
        {
            try
            {
                Remove((T)value);
            }
            catch (InvalidCastException)
            {
                ThrowHelper.ThrowNotSupportedException($"Wrong value type for {value}");
            }
        }

        /// <summary>
        /// Removes the element at the specified arrayIndex of the SwiftList.
        /// </summary>
        public void RemoveAt(int index)
        {
            if ((uint)index >= (uint)_count) ThrowHelper.ThrowArgumentOutOfRangeException();
            Array.Copy(_innerArray, index + 1, _innerArray, index, _count - index - 1);
            _count--;
            _innerArray[_count] = default;
            _version++;
        }

        /// <summary>
        /// Removes all the elements that match the conditions defined by the specified predicate.
        /// </summary>
        public int RemoveAll(Predicate<T> match)
        {
            if (match == null) ThrowHelper.ThrowArgumentNullException(nameof(match));

            int i = 0;
            // Move to the first element that should be removed
            while (i < _count && !match(_innerArray[i])) i++;

            if (i >= _count) return 0;  // No items to remove

            int j = i + 1;
            while (j < _count)
            {
                // Find the next element to keep
                while (j < _count && match(_innerArray[j])) j++;

                if (j < _count)
                    _innerArray[i++] = _innerArray[j++];
            }

            // Clear out the trailing elements to ensure no lingering references
            Array.Clear(_innerArray, i, _count - i);

            int removedCount = _count - i;
            _count = i;

            _version++;

            return removedCount;
        }

        /// <summary>
        /// Inserts an element into the SwiftList at the specified arrayIndex.
        /// </summary>
        public void Insert(int index, T item)
        {
            if ((uint)index > (uint)_innerArray.Length) ThrowHelper.ThrowArgumentOutOfRangeException();
            if ((uint)_count == (uint)_innerArray.Length)
                Resize(_innerArray.Length * 2);
            if ((uint)index < (uint)_count)
                Array.Copy(_innerArray, index, _innerArray, index + 1, _count - index);
            _innerArray[index] = item;
            _count++;
            _version++;
        }

        void IList.Insert(int index, object value)
        {
            try
            {
                Insert(index, (T)value);
            }
            catch (InvalidCastException)
            {
                ThrowHelper.ThrowNotSupportedException($"Wrong value type for {value}");
            }
        }

        /// <summary>
        /// Reverses the order of the elements in the entire <see cref="SwiftList{T}"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reverse()
        {
            Array.Reverse(_innerArray, 0, _count);
            _version++;
        }

        /// <summary>
        /// Sorts the elements in the <see cref="SwiftList{T}"/> using the <see cref="IComparable"/> interface implementation of each element of the _innerArray.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Sort()
        {
            Array.Sort(_innerArray, 0, _count, Comparer<T>.Default);
            _version++;
        }

        /// <summary>
        /// Sorts the elements in the entire <see cref="SwiftList{T}"/> using the specified comparer.
        /// </summary>
        /// <param name="comparer">The comparer to use for comparing elements.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Sort(IComparer<T> comparer)
        {
            Array.Sort(_innerArray, 0, _count, comparer);
            _version++;
        }

        /// <summary>
        /// Removes all elements from the <see cref="SwiftList{T}"/>, resetting its count to zero.
        /// </summary>
        public void Clear()
        {
            Array.Clear(_innerArray, 0, _count);
            _count = 0;
            _version++;
        }

        /// <summary>
        /// Clears the <see cref="SwiftList{T}"/> without releasing the reference to the stored elements.
        /// Use FastClear() when you want to quickly reset the list without reallocating memory.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FastClear()
        {
            _count = 0;
            _version++;
        }

        #endregion

        #region Capacity Management

        /// <summary>
        /// Ensures that the capacity of <see cref="SwiftList{T}"/> is sufficient to accommodate the specified number of elements.
        /// The capacity can increase by double to balance memory allocation efficiency and space.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureCapacity(int capacity)
        {
            capacity = HashTools.NextPowerOfTwo(capacity);
            if (capacity > _innerArray.Length)
                Resize(capacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Resize(int newSize)
        {
            int newCapacity = newSize <= DefaultCapacity ? DefaultCapacity : newSize;

            T[] newArray = new T[newCapacity];
            if (_count > 0)
                Array.Copy(_innerArray, 0, newArray, 0, _count);
            _innerArray = newArray;
            _version++;
        }

        /// <summary>
        /// Reduces the capacity of the SwiftList if the element count falls below 50% of the current capacity. 
        /// Ensures efficient memory usage by resizing the internal array to match the current count when necessary.
        /// </summary>
        public void TrimExcessCapacity()
        {
            int newCapacity = _count < DefaultCapacity ? DefaultCapacity : HashTools.NextPowerOfTwo(_count);
            Array.Resize(ref _innerArray, newCapacity);
            _version++;
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Searches for the specified object and returns the zero-based arrayIndex of the first occurrence within the SwiftList.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int IndexOf(T item) => Array.IndexOf(_innerArray, item, 0, _count);

        int IList.IndexOf(object value)
        {
            int index = -1;
            try
            {
                index = Array.IndexOf(_innerArray, (T)value, 0, _count);
            }
            catch
            {
                ThrowHelper.ThrowNotSupportedException($"Unsupported value type for {value}");
            }
            return index;
        }

        /// <summary>
        /// Copies the elements of the SwiftList to a new array.
        /// </summary>
        public T[] ToArray()
        {
            T[] result = new T[_count];
            Array.Copy(_innerArray, result, _count);
            return result;
        }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString() => _count == 0 ? base.ToString() : string.Join(", ", this);

        /// <summary>
        /// Determines whether an element is in the SwiftList.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(T item) => IndexOf(item) != -1;

        bool IList.Contains(object value)
        {
            int index = -1;
            try
            {
                index = IndexOf((T)value);
            }
            catch
            {
                ThrowHelper.ThrowNotSupportedException($"Unsupported value type for {value}");
            }
            return index != -1;
        }

        /// <summary>
        /// Swaps the values of two elements in the SwiftList.
        /// This method exchanges the values referenced by two variables.
        /// </summary>
        /// <param name="indexA">The first element to swap.</param>
        /// <param name="indexB">The second element to swap.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Swap(int indexA, int indexB) => (_innerArray[indexB], _innerArray[indexA]) = (_innerArray[indexA], _innerArray[indexB]);

        /// <summary>
        /// Copies the elements of the SwiftList to the specified target SwiftList.
        /// The target list will resize if it lacks sufficient capacity, 
        /// but retains any existing elements beyond the copied range.
        /// </summary>
        public void CopyTo(SwiftList<T> target)
        {
            if (_count + 1 > target._innerArray.Length)
                target.Resize(target._innerArray.Length * 2);
            Array.Copy(_innerArray, 0, target._innerArray, 0, _count);
            target._count = _count;
            target._version++;
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            if (array == null) ThrowHelper.ThrowArgumentNullException(nameof(array));
            if ((uint)arrayIndex > array.Length) ThrowHelper.ThrowArgumentOutOfRangeException();
            if (array.Length - arrayIndex < _count) ThrowHelper.ThrowArgumentException("Destination array is not long enough.");

            Array.Copy(_innerArray, 0, array, arrayIndex, _count);
        }

        void ICollection.CopyTo(Array array, int arrayIndex)
        {
            if (array == null) ThrowHelper.ThrowArgumentNullException(nameof(array));
            if (array.Rank != 1) ThrowHelper.ThrowArgumentException("Array must be single dimensional.");
            if (array.GetLowerBound(0) != 0) ThrowHelper.ThrowArgumentException("Array must have zero-based indexing.");
            if ((uint)arrayIndex > array.Length) ThrowHelper.ThrowArgumentOutOfRangeException();
            if (array.Length - arrayIndex < _count) ThrowHelper.ThrowArgumentException("Destination array is not long enough.");

            Array.Copy(_innerArray, 0, array, arrayIndex, _count);
        }

        public void CloneTo(ICollection<T> output)
        {
            output.Clear();
            foreach (var item in this)
                output.Add(item);
        }

        #endregion

        #region Enumerators

        /// <summary>
        /// Returns an enumerator that iterates through the <see cref="SwiftList{T}"/>.
        /// </summary>
        public SwiftListEnumerator GetEnumerator() => new SwiftListEnumerator(this);
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public struct SwiftListEnumerator : IEnumerator<T>, IEnumerator, IDisposable
        {
            private readonly SwiftList<T> _list;
            private readonly T[] _array;
            private readonly uint _count;
            private readonly uint _version;
            private uint _index;

            private T _current;

            public SwiftListEnumerator(SwiftList<T> list)
            {
                _list = list;
                _array = list._innerArray;
                _count = (uint)list._count;
                _version = list._version;
                _index = 0;
                _current = default;
            }

            public T Current => _current;

            object IEnumerator.Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    if (_index >= _count) ThrowHelper.ThrowInvalidOperationException("Bad enumeration");
                    return _current;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                if (_version != _list._version)
                    throw new InvalidOperationException("Collection was modified during enumeration.");

                if (_index >= _count) return false;
                _current = _array[_index++];
                return true;
            }

            public void Reset()
            {
                if (_version != _list._version)
                    throw new InvalidOperationException("Collection was modified during enumeration.");

                _index = 0;
                _current = default;
            }

            public void Dispose() { }
        }

        #endregion
    }
}
