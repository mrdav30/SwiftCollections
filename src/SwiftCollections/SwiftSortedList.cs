using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using static UnityEditor.Experimental.GraphView.Port;

#if UNITY_EDITOR
using UnityEngine;
#endif

namespace SwiftCollections
{
    /// <summary>
    /// Represents a dynamically sorted collection of elements.
    /// Provides efficient O(log n) operations for adding, removing, and checking for the presence of elements.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    [Serializable]
    public class SwiftSortedList<T> : ISwiftCloneable<T>, IEnumerable<T>, IEnumerable, ICollection<T>, ICollection, IReadOnlyList<T>
    {
        #region Constants

        /// <summary>
        /// The default initial capacity of the <see cref="SwiftSortedList{T}"/> if none is specified.
        /// Used to allocate a reasonable starting size to minimize resizing operations.
        /// </summary>
        public const int DefaultCapacity = 4;

        private static readonly T[] _emptyArray = new T[0];

        #endregion

        #region Fields

        /// <summary>
        /// Represents the internal array that stores the sorted elements.
        /// </summary>
#if UNITY_EDITOR
        [SerializeField]
#endif
        private T[] _innerArray;

        private int _count;

        /// <summary>
        /// The offset within the internal array where the logical start of the list begins.
        /// Used to efficiently manage insertions and deletions at both ends without excessive shifting.
        /// </summary>
        private int _offset;

        /// <summary>
        /// A version counter used to track modifications to the sorted list.
        /// Incremented on mutations to detect changes during enumeration and ensure enumerator validity.
        /// </summary>
        private uint _version;

        /// <summary>
        /// The comparer used to sort and compare elements in the collection.
        /// </summary>
        private readonly IComparer<T> _comparer;

        /// <summary>
        /// An object that can be used to synchronize access to the SwiftList.
        /// </summary>
        [NonSerialized]
        private object _syncRoot;

        #endregion

        #region Constructors

        public SwiftSortedList(): this(0) { }

        /// <summary>
        /// Initializes a new, empty instance of <see cref="SwiftSortedList{T}"/> uisng the specified <see cref="IComparer{T}"/>.
        /// </summary>
        public SwiftSortedList(IComparer<T> comparer) : this(0, comparer) { }

        /// <summary>
        /// Initializes a new, empty instance of <see cref="SwiftSortedList{T}"/> with the specified initial capacity and <see cref="IComparer{T}"/>.
        /// </summary>
        /// <param name="capacity">The starting initial capacity.</param>
        /// <param name="comparer">The comparer to use. If null, the default comparer is used.</param>
        public SwiftSortedList(int capacity, IComparer<T> comparer = null)
        {
            _comparer = comparer ?? Comparer<T>.Default;

            if (capacity == 0)
            {
                _innerArray = _emptyArray;
                _offset = 0;
            }
            else
            {
                capacity = capacity <= DefaultCapacity ? DefaultCapacity : HashHelper.NextPowerOfTwo(capacity);
                _innerArray = new T[capacity];
                _offset = capacity >> 1; // initial offset half of capacity
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SwiftList{T}"/> class with elements from the specified collection.
        /// The collection must have a known count for optimized memory allocation.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown if the input collection does not have a known count.</exception>
        public SwiftSortedList(IEnumerable<T> items, IComparer<T> comparer = null)
        {
            if (items == null) ThrowHelper.ThrowArgumentNullException(nameof(items));

            _comparer = comparer ?? Comparer<T>.Default;

            if (items is ICollection<T> collection)
            {
                int count = collection.Count;
                if (count == 0)
                {
                    _innerArray = _emptyArray;
                    _offset = 0;
                }
                else
                {
                    int capacity = HashHelper.NextPowerOfTwo(count <= DefaultCapacity ? DefaultCapacity : count);
                    _innerArray = new T[capacity];
                    collection.CopyTo(_innerArray, 0);
                    Array.Sort(_innerArray, 0, _innerArray.Length, _comparer);
                    _offset = capacity >> 1; // initial offset half of capacity
                }
            }
            else
            {
                _innerArray = new T[DefaultCapacity];
                _offset = DefaultCapacity >> 1;
                AddRange(items); // Will handle capacity increases as needed
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the number of elements in the sorter.
        /// </summary>
        public int Count => _count;

        /// <summary>
        /// Gets the current capacity of the internal array.
        /// </summary>
        public int Capacity => _innerArray.Length;

        /// <summary>
        /// The comparer used to sort elements in the collection.
        /// </summary>
        public IComparer<T> Comparer => _comparer;

        bool ICollection<T>.IsReadOnly => false;
        public bool IsSynchronized => false;
        object ICollection.SyncRoot => _syncRoot ??= new object();

        /// <summary>
        /// Gets the element at the specified arrayIndex.
        /// </summary>
        public T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if ((uint)index >= (uint)_count) ThrowHelper.ThrowArgumentOutOfRangeException();
                return _innerArray[GetPhysicalIndex(index)];
            }
        }

        #endregion

        #region Collection Manipulation

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(T item)
        {
            int index = Search(item);
            if (index < 0) index = ~index;
            Insert(item, index);
        }

        /// <summary>
        /// Inserts an item into the internal array at the specified arrayIndex, shifting elements as needed.
        /// </summary>
        public void Insert(T item, int index)
        {
            if (_offset + _count + 1 > _innerArray.Length)
                Resize(_innerArray.Length * 2);

            int physicalIndex = GetPhysicalIndex(index);

            if (index < (uint)_count)
            {
                int distanceToHead = physicalIndex - _offset;
                int distanceToTail = (_offset + _count - 1) - physicalIndex;

                if (distanceToHead >= (uint)distanceToTail)
                {
                    if ((uint)_offset == 0)
                    {
                        // Ensure capacity for recentering
                        Resize(_innerArray.Length * 2);
                        RecenterArray();
                        physicalIndex = GetPhysicalIndex(index);
                    }

                    _offset--;
                    physicalIndex--;
                    // Shift elements towards the head (left)
                    Array.Copy(_innerArray, _offset + 1, _innerArray, _offset, distanceToHead + 1);
                }
                else  // Shift elements towards the tail (right) to make space
                    Array.Copy(_innerArray, physicalIndex, _innerArray, physicalIndex + 1, _count - index);
            }

            _innerArray[physicalIndex] = item;
            _count++;

            _version++;
        }

        /// <summary>
        /// Adds a range of elements to the collection, ensuring they are sorted and merged efficiently.
        /// </summary>
        /// <remarks>
        /// This will compact the array for efficiency.
        /// </remarks>
        public void AddRange(IEnumerable<T> items)
        {
            if (items == null) ThrowHelper.ThrowArgumentNullException(nameof(items));

            // Convert items to a sorted array
            T[] sortedItems;

            if (items is ICollection<T> collection)
            {
                if (collection.Count == 0) return;

                sortedItems = new T[collection.Count];
                collection.CopyTo(sortedItems, 0);
            }
            else
            {
                sortedItems = items.ToArray();
                if (sortedItems.Length == 0) return;
            }

            Array.Sort(sortedItems, 0, sortedItems.Length, _comparer);

            if ((uint)_count == 0)
            {
                // If the list is empty, initialize with the sorted items
                int initialCapacity = HashHelper.NextPowerOfTwo(sortedItems.Length);
                int newOffset = (initialCapacity - sortedItems.Length) >> 1;
                if ((uint)_innerArray.Length < initialCapacity) _innerArray = new T[initialCapacity];
                Array.Copy(sortedItems, 0, _innerArray, newOffset, sortedItems.Length);
                _offset = newOffset;
                _count = sortedItems.Length;
                _version++;
                return;
            }

            // Merge the sorted arrays
            int newCount = _count + sortedItems.Length;
            int totalRequiredCapacity = newCount + _offset;

            // Determine new capacity and offset
            int newCapacity = HashHelper.NextPowerOfTwo(totalRequiredCapacity);
            int mergedOffset = (newCapacity - newCount) >> 1;

            // Create a new array with the new capacity
            T[] newArray = new T[newCapacity];

            // Merge existing items and new items into newArray
            int existingIndex = 0;
            int newItemsIndex = 0;
            int mergedIndex = mergedOffset;

            while (existingIndex < _count && newItemsIndex < sortedItems.Length)
            {
                T existingItem = _innerArray[_offset + existingIndex];
                T newItem = sortedItems[newItemsIndex];

                if (_comparer.Compare(existingItem, newItem) <= 0)
                {
                    newArray[mergedIndex++] = existingItem;
                    existingIndex++;
                }
                else
                {
                    newArray[mergedIndex++] = newItem;
                    newItemsIndex++;
                }
            }

            // Copy any remaining existing items
            while (existingIndex < _count)
                newArray[mergedIndex++] = _innerArray[_offset + existingIndex++];

            // Copy any remaining new items
            while (newItemsIndex < sortedItems.Length)
                newArray[mergedIndex++] = sortedItems[newItemsIndex++];

            // Set the new array and update the offset
            _innerArray = newArray;
            _offset = mergedOffset;
            _count = newCount;

            _version++;
        }

        /// <summary>
        /// Removes and returns the minimum element in the sorter.
        /// </summary>
        /// <returns>The minimum element.</returns>
        public T PopMin()
        {
            if ((uint)_count == 0) ThrowHelper.ThrowIndexOutOfRangeException();

            int index = GetPhysicalIndex(0);
            T ret = _innerArray[index];
            _innerArray[index] = default;
            _count--;
            // Increment _offset as we remove from the front
            _offset = _count == 0 ? _innerArray.Length >> 1 : _offset++;

            _version++;

            return ret;
        }

        /// <summary>
        /// Removes and returns the maximum element in the sorter.
        /// </summary>
        /// <returns>The maximum element.</returns>
        public T PopMax()
        {
            if ((uint)_count == 0) ThrowHelper.ThrowIndexOutOfRangeException();

            int index = GetPhysicalIndex(--_count);
            T ret = _innerArray[index];
            _innerArray[index] = default;
            if ((uint)_count == 0) _offset = _innerArray.Length >> 1;

            _version++;

            return ret;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(T item)
        {
            if ((uint)_count == 0) return false;

            int index = Search(item);
            if (index < 0) return false;

            RemoveAt(index);
            return true;
        }

        /// <summary>
        /// Removes the element at the specified arrayIndex from the sorted list.
        /// Shifts elements as needed to maintain the sorted order and efficient space utilization.
        /// </summary>
        /// <param name="index">The zero-based arrayIndex of the element to remove.</param>
        public void RemoveAt(int index)
        {
            if ((uint)index >= (uint)_count) ThrowHelper.ThrowArgumentOutOfRangeException();

            int physicalIndex = GetPhysicalIndex(index);

            _count--;
            if ((uint)index < (uint)_count)
            {
                int distanceToHead = physicalIndex - _offset;
                int distanceToTail = (_offset + _count) - physicalIndex;

                if (distanceToHead < distanceToTail)
                {
                    // Shift elements towards the tail (right) to fill the gap
                    Array.Copy(_innerArray, _offset, _innerArray, _offset + 1, distanceToHead);
                    _offset++;  // Adjust offset since we've moved elements towards the tail
                }
                else
                {
                    // Shift elements towards the head (left) to fill the gap
                    Array.Copy(_innerArray, physicalIndex + 1, _innerArray, physicalIndex, distanceToTail);
                    _innerArray[GetPhysicalIndex(_count)] = default; // Clear the last element
                }
            }
            else  // Removing the last element; simply clear it
                _innerArray[GetPhysicalIndex(_count)] = default;

            // Only recenter if offset is non-zero and count is less than 25% of capacity
            if (_offset != 0 && (uint)_count < _innerArray.Length * 0.25)
            {
                if ((uint)_count == 0)
                    _offset = _innerArray.Length >> 1; // Reset offset to the middle when list is empty
                else
                    RecenterArray();
            }

            _version++;
        }

        public void Clear()
        {
            if ((uint)_count == 0) return;
            Array.Clear(_innerArray, _offset, _count);
            _count = 0;
            _offset = _innerArray.Length == 0 ? 0 : _innerArray.Length >> 1; // Reset _offset to the middle of the array

            _version++;
        }

        /// <summary>
        /// Quickly clears the list by resetting the count and offset without modifying the internal array.
        /// Note: This leaves references in the internal array, which may prevent garbage collection of reference types.
        /// Use when performance is critical and you are certain that residual references are acceptable.
        /// </summary>
        public void FastClear()
        {
            if ((uint)_count == 0) return;
            _count = 0;
            _offset = _innerArray.Length == 0 ? 0 : _innerArray.Length >> 1; // Reset offset to middle

            _version++;
        }

        #endregion

        #region Capacity Management

        /// <summary>
        /// Ensures that the capacity of <see cref="SwiftSortedList{T}"/> is sufficient to accommodate the specified number of elements.
        /// The capacity can increase by double to balance memory allocation efficiency and space.
        /// </summary>
        public void EnsureCapacity(int capacity)
        {
            capacity = HashHelper.NextPowerOfTwo(capacity);
            if (capacity > _innerArray.Length)
                Resize(capacity);
        }

        /// <summary>
        /// Ensures that the capacity of <see cref="SwiftSortedList{T}"/> is sufficient to accommodate the specified number of elements.
        /// The capacity can increase by double to balance memory allocation efficiency and space.
        /// </summary>
        private void Resize(int newSize)
        {
            int newCapacity = newSize <= DefaultCapacity ? DefaultCapacity : newSize;

            T[] newArray = new T[newCapacity];
            int newOffset = (newArray.Length - _count) >> 1; // Center the elements in the new array
            if ((uint)_count > 0)
                Array.Copy(_innerArray, _offset, newArray, newOffset, _count);

            _innerArray = newArray;
            _offset = newOffset;

            _version++;
        }

        /// <summary>
        /// Recenters the elements within the internal array to balance available space on both ends.
        /// This minimizes the need for shifting elements during future insertions and deletions.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RecenterArray()
        {
            int newOffset = (_innerArray.Length - _count) >> 1;
            Array.Copy(_innerArray, _offset, _innerArray, newOffset, _count);
            _offset = newOffset;

            _version++;
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Converts a logical arrayIndex within the list to the corresponding physical arrayIndex in the internal array.
        /// </summary>
        /// <param name="logicalIndex">The logical arrayIndex within the list.</param>
        /// <returns>The physical arrayIndex within the internal array.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetPhysicalIndex(int logicalIndex) => _offset + logicalIndex;

        /// <summary>
        /// Returns the minimum element in the sorter without removing it.
        /// </summary>
        /// <returns>The minimum element.</returns>
        public T PeekMin()
        {
            if ((uint)_count == 0) ThrowHelper.ThrowIndexOutOfRangeException();
            return _innerArray[GetPhysicalIndex(0)];
        }

        /// <summary>
        /// Returns the maximum element in the sorter without removing it.
        /// </summary>
        /// <returns>The maximum element.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T PeekMax()
        {
            if ((uint)_count == 0) ThrowHelper.ThrowIndexOutOfRangeException();
            return _innerArray[GetPhysicalIndex(_count - 1)];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(T item) => Search(item) >= 0;

        /// <summary>
        /// Searches for the specified item in the sorted collection and returns the arrayIndex of the first occurrence.
        /// </summary>
        /// <param name="item">The item to search for.</param>
        /// <returns>The zero-based arrayIndex of the item if found; otherwise, -1.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int IndexOf(T item)
        {
            int index = Search(item);
            return index >= 0 ? index : -1;
        }

        /// <summary>
        /// Determines the insertion point for a specified item in the collection.
        /// The insertion point is the arrayIndex where the item would be inserted if it were not already present.
        /// </summary>
        /// <param name="item">The item for which to find the insertion point.</param>
        /// <returns>The insertion point as a zero-based arrayIndex.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int InsertionPoint(T item)
        {
            int index = Search(item);
            return index >= 0 ? index : ~index;
        }

        /// <summary>
        /// Searches for the specified item in the sorted collection.
        /// </summary>
        /// <param name="item">The item to search for.</param>
        /// <returns>
        /// The arrayIndex of the item if found or where the item should be inserted if not found.
        /// </returns>
        public int Search(T item)
        {
            int low = 0;
            int high = _count - 1;
            while ((uint)low <= high)
            {
                int mid = low + ((high - low) >> 1);
                T midItem = _innerArray[GetPhysicalIndex(mid)];
                int cmp = _comparer.Compare(midItem, item);
                if ((uint)cmp == 0)
                    return mid; // Exact match found

                if (cmp < 0)
                    low = mid + 1; // Search in the right half
                else
                    high = mid - 1; // Search in the left half
            }
            return ~low; // Item not found, should be inserted at arrayIndex low
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            if (array == null) ThrowHelper.ThrowArgumentNullException(nameof(array));
            if ((uint)arrayIndex > array.Length) ThrowHelper.ThrowArgumentOutOfRangeException();
            if (array.Length - arrayIndex < _count) ThrowHelper.ThrowArgumentException("The target array is too small.");

            Array.Copy(_innerArray, _offset, array, arrayIndex, _count);
        }

        public void CopyTo(Array array, int arrayIndex)
        {
            if (array == null) ThrowHelper.ThrowArgumentNullException(nameof(array));
            if ((uint)arrayIndex > array.Length) ThrowHelper.ThrowArgumentOutOfRangeException();
            if (array.Length - arrayIndex < _count) ThrowHelper.ThrowArgumentException("The target array is too small.");

            Array.Copy(_innerArray, _offset, array, arrayIndex, _count);
        }

        public void CloneTo(ICollection<T> output)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            output.Clear();

            foreach (var item in this)
                output.Add(item);
        }

        #endregion

        #region Enumerators

        /// <summary>
        /// Returns an enumerator that iterates through <see cref="SwiftSortedList{T}"/>.
        /// </summary>
        public SwiftSorterEnumerator GetEnumerator() => new SwiftSorterEnumerator(this);
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public struct SwiftSorterEnumerator : IEnumerator<T>, IEnumerator, IDisposable
        {
            private readonly SwiftSortedList<T> _list;
            private readonly uint _count;
            private readonly uint _version;
            private int _index;

            private T _current;

            public SwiftSorterEnumerator(SwiftSortedList<T> sortedList)
            {
                _list = sortedList;
                _count = (uint)sortedList._count;
                _version = sortedList._version;
                _index = 0;
                _current = default;
            }

            public T Current => _current;

            object IEnumerator.Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    if ((uint)_index > _count) ThrowHelper.ThrowInvalidOperationException("Bad enumeration");
                    return _current;
                }

            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                if (_version != _list._version)
                    ThrowHelper.ThrowInvalidOperationException("Enumerator modified outside of enumeration!");

                if (_index < _count)
                {
                    _current = _list._innerArray[_list.GetPhysicalIndex(_index)];
                    _index++;
                    return true;
                }

                _index = _list._count + 1;
                _current = default;
                return false;
            }

            public void Reset()
            {
                if (_version != _list._version)
                    ThrowHelper.ThrowInvalidOperationException("Enumerator modified outside of enumeration!");

                _index = 0;
                _current = default;
            }

            public void Dispose() { }
        }

        #endregion
    }
}
