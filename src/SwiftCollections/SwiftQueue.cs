using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

#if UNITY_EDITOR
using UnityEngine;
#endif

namespace SwiftCollections
{
    /// <summary>
    /// <c>SwiftQueue&lt;T&gt;</c> is a high-performance, circular buffer-based queue designed for ultra-low-latency enqueue and dequeue operations.
    /// <para>
    /// It leverages power-of-two capacities and bitwise arithmetic to eliminate expensive modulo operations, enhancing performance.
    /// By managing memory efficiently with a wrap-around technique and custom capacity growth strategies, SwiftQueue minimizes allocations and resizing.
    /// Aggressive inlining and optimized exception handling further reduce overhead, making SwiftQueue outperform traditional queues,
    /// especially in scenarios with high-frequency additions and removals.
    /// </para>
    /// <para>
    /// This implementation is optimized for performance and does not perform versioning checks.
    /// Modifying the queue during enumeration may result in undefined behavior.
    /// </para>
    /// </summary>
    /// <typeparam name="T">Specifies the type of elements in the queue.</typeparam>
    [Serializable]
    public sealed class SwiftQueue<T> : ISwiftCloneable<T>, IEnumerable<T>, IEnumerable, ICollection
    {
        #region Constants

        /// <summary>
        /// The default initial capacity of the SwiftQueue if none is specified.
        /// Used to allocate a reasonable starting size to minimize resizing operations.
        /// </summary>
        public const int DefaultCapacity = 4;

        private static readonly T[] _emptyArray = new T[0];

        #endregion

        #region Fields

        /// <summary>
        /// The internal array that stores elements of the SwiftQueue. Resized as needed to
        /// accommodate additional elements. Not directly exposed outside the queue.
        /// </summary>
#if UNITY_EDITOR
        [SerializeField]
#endif
        private T[] _innerArray;

        /// <summary>
        /// The current number of elements in the SwiftQueue. Represents the total count of
        /// valid elements stored in the queue, also indicating the arrayIndex of the next insertion point.
        /// </summary>
        private int _count;

        /// <summary>
        /// The arrayIndex of the first element in the queue. Adjusts as elements are dequeued.
        /// </summary>
        private int _head;

        /// <summary>
        /// The arrayIndex at which the next element will be enqueued, wrapping around as needed.
        /// </summary>
        private int _tail;

        [NonSerialized]
        private object _syncRoot;

        /// <summary>
        /// Gets the element at the specified arrayIndex.
        /// </summary>
        public T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if ((uint)index >= (uint)_count) ThrowHelper.ThrowArgumentOutOfRangeException();
                return _innerArray[(_head + index) & _mask];
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if ((uint)index >= (uint)_count) ThrowHelper.ThrowArgumentOutOfRangeException();
                _innerArray[(_head + index) & _mask] = value;
            }
        }

        private int _mask;

        private uint _version;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new, empty instance of SwiftQueue.
        /// </summary>
        public SwiftQueue() : this(0) { }

        /// <summary>
        /// Initializes a new, empty instance of SwiftQueue with the specified initial capacity.
        /// </summary>
        public SwiftQueue(int capacity)
        {
            if (capacity == 0)
            {
                _innerArray = _emptyArray;
                _mask = 0;
            }
            else
            {
                capacity = capacity < DefaultCapacity ? DefaultCapacity : HashHelper.NextPowerOfTwo(capacity);
                _innerArray = new T[capacity];
                _mask = _innerArray.Length - 1;
            }
        }

        /// <summary>
        /// Initializes a new instance of SwiftQueue that contains elements copied from the provided items.
        /// </summary>
        public SwiftQueue(IEnumerable<T> items)
        {
            if (items == null) ThrowHelper.ThrowArgumentNullException(nameof(items));
            if (items is ICollection<T> collection)
            {
                int capacity = collection.Count < DefaultCapacity ? DefaultCapacity : HashHelper.NextPowerOfTwo(collection.Count);
                _innerArray = new T[capacity];
            }
            else
                _innerArray = new T[DefaultCapacity];

            _mask = _innerArray.Length - 1;
            foreach (T item in items)
                Enqueue(item);
        }

        #endregion

        #region Properties

        /// <inheritdoc cref="_count"/>
        public int Count => _count;

        /// <summary>
        /// Gets the total number of elements the SwiftQueue can hold without resizing.
        /// Reflects the current allocated size of the internal array.
        /// </summary>
        public int Capacity => _innerArray.Length;

        public bool IsSynchronized => false;
        object ICollection.SyncRoot => _syncRoot ??= new object();

        #endregion

        #region Collection Management

        /// <summary>
        /// Adds an item to the end of the queue. Automatically resizes the queue if the capacity is exceeded.
        /// </summary>
        /// <param name="item">The item to add to the queue.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enqueue(T item)
        {
            if ((uint)_count >= (uint)_innerArray.Length)
                Resize(_innerArray.Length * 2);
            _innerArray[_tail] = item;
            _tail = (_tail + 1) & _mask;
            _count++;
            _version++;
        }

        /// <summary>
        /// Removes and returns the item at the front of the queue. 
        /// Throws an InvalidOperationException if the queue is empty.
        /// </summary>
        /// <returns>The item at the front of the queue.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Dequeue()
        {
            if ((uint)_count == 0) ThrowHelper.ThrowInvalidOperationException("Queue is Empty");
            T item = _innerArray[_head];
            _innerArray[_head] = default;
            _head = (_head + 1) & _mask;
            _count--;
            _version++;
            return item;
        }

        /// <summary>
        /// Returns the item at the front of the queue without removing it. 
        /// Throws an InvalidOperationException if the queue is empty.
        /// </summary>
        /// <returns>The item at the front of the queue.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Peek()
        {
            if ((uint)_count == 0) ThrowHelper.ThrowInvalidOperationException("Queue is Empty");
            return _innerArray[_head];
        }

        /// <summary>
        /// Returns the item at the end of the queue without removing it. 
        /// Throws an InvalidOperationException if the queue is empty.
        /// </summary>
        /// <returns>The item at the end of the queue.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T PeekTail()
        {
            if ((uint)_count == 0) ThrowHelper.ThrowInvalidOperationException("Queue is Empty");
            int tailIndex = (_tail - 1) & _mask;
            return _innerArray[tailIndex];
        }

        /// <summary>
        /// Removes all elements from the SwiftQueue, resetting its count to zero.
        /// </summary>
        public void Clear()
        {
            if (_count == 0) return;

            if ((uint)_head < (uint)_tail)
                Array.Clear(_innerArray, _head, _count);
            else
            {
                Array.Clear(_innerArray, _head, _innerArray.Length - _head);
                Array.Clear(_innerArray, 0, _tail);
            }

            _count = 0;
            _head = 0;
            _tail = 0;
            _version++;
        }

        /// <summary>
        /// Clears the SwiftQueue without releasing the reference to the stored elements.
        /// Use FastClear() when you want to quickly reset the list without reallocating memory.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FastClear()
        {
            _count = 0;
            _tail = 0;
            _head = 0;
            _version++;
        }

        #endregion

        #region Capacity Management

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureCapacity(int capacity)
        {
            capacity = HashHelper.NextPowerOfTwo(capacity);  // Capacity must be a power of 2 for proper masking
            if (capacity > _innerArray.Length)
                Resize(capacity);
        }

        /// <summary>
        /// Ensures that the capacity of the queue is sufficient to accommodate the specified number of elements.
        /// The capacity increases to the next power of two greater than or equal to the required minimum capacity.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Resize(int newSize)
        {
            var newArray = new T[newSize <= DefaultCapacity ? DefaultCapacity : newSize];
            if ((uint)_count > 0)
            {
                // If we are not wrapped around...
                if ((uint)_head < (uint)_tail)
                {
                    // ...copy from head to tail into new array starting at arrayIndex 0
                    Array.Copy(_innerArray, _head, newArray, 0, _count);
                }
                // Else if we are wrapped around...
                else
                {
                    // ...copy from head to end of old array to beginning of new array
                    Array.Copy(_innerArray, _head, newArray, 0, _innerArray.Length - _head);
                    // ...copy from start of old array to tail into new array
                    Array.Copy(_innerArray, 0, newArray, _innerArray.Length - _head, _tail);
                }
            }

            _innerArray = newArray;
            _mask = _innerArray.Length - 1;
            _head = 0;
            _tail = _count & _mask;
            _version++;
        }

        /// <summary>
        /// Reduces the capacity of the SwiftQueue if the element count is significantly less than the current capacity.
        /// This method resizes the internal array to the next power of two greater than or equal to the current count,
        /// optimizing memory usage.
        /// </summary>
        public void TrimExcessCapacity()
        {
            int newSize = _count < DefaultCapacity ? DefaultCapacity : HashHelper.NextPowerOfTwo(_count);
            if (newSize >= _innerArray.Length) return;

            var newArray = new T[newSize];

            if ((uint)_count != 0)
            {
                if ((uint)_head < (uint)_tail)
                {
                    // No wrap-around, simple copy
                    Array.Copy(_innerArray, _head, newArray, 0, _count);
                }
                else
                {
                    // Wrap-around, copy in two parts
                    Array.Copy(_innerArray, _head, newArray, 0, _innerArray.Length - _head);
                    Array.Copy(_innerArray, 0, newArray, _innerArray.Length - _head, _tail);
                }
            }

            _innerArray = newArray;
            _mask = _innerArray.Length - 1;
            _head = 0;
            _tail = _count & _mask;
            _version++;
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Copies the elements of the SwiftQueue to a new array.
        /// </summary>
        public T[] ToArray()
        {
            var result = new T[_count];
            if ((uint)_count == 0) return result;
            if ((uint)_head < (uint)_tail)
                Array.Copy(_innerArray, _head, result, 0, _count);
            else
            {
                int firstPartLength = _innerArray.Length - _head;
                Array.Copy(_innerArray, _head, result, 0, firstPartLength);
                Array.Copy(_innerArray, 0, result, firstPartLength, _tail);
            }
            return result;
        }

        public void CopyTo(Array array, int arrayIndex)
        {
            if (array == null) ThrowHelper.ThrowArgumentNullException(nameof(array));
            if ((uint)array.Rank != 1) ThrowHelper.ThrowArgumentException("Array must be single dimensional.");
            if ((uint)array.GetLowerBound(0) != 0) ThrowHelper.ThrowArgumentException("Array must have zero-based indexing.");
            if ((uint)arrayIndex > array.Length) ThrowHelper.ThrowArgumentOutOfRangeException();
            if ((uint)(array.Length - arrayIndex) < _count) ThrowHelper.ThrowArgumentException("Destination array is not long enough.");

            if ((uint)_count == 0)
                return;

            try
            {
                if ((uint)_head < (uint)_tail)
                    Array.Copy(_innerArray, _head, array, arrayIndex, _count);
                else
                {
                    Array.Copy(_innerArray, _head, array, arrayIndex, _innerArray.Length - _head);
                    Array.Copy(_innerArray, 0, array, arrayIndex + _innerArray.Length - _head, _tail);
                }
            }
            catch (ArrayTypeMismatchException)
            {
                ThrowHelper.ThrowArgumentException("Invalid array type.");
            }
        }

        public void CloneTo(ICollection<T> output)
        {
            if (output == null) ThrowHelper.ThrowArgumentNullException(nameof(output));
            output.Clear();
            foreach (var item in this)
            {
                output.Add(item);
            }
        }

        #endregion

        #region Enumerators

        /// <summary>
        /// Returns an enumerator that iterates through the SwiftList.
        /// </summary>
        public SwiftQueueEnumerator GetEnumerator() => new SwiftQueueEnumerator(this);
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public struct SwiftQueueEnumerator : IEnumerator<T>, IEnumerator, IDisposable
        {
            private readonly SwiftQueue<T> _queue;
            private readonly T[] _array;
            private readonly uint _version;
            private uint _index;
            private uint _currentIndex;

            private T _current;

            public SwiftQueueEnumerator(SwiftQueue<T> queue)
            {
                _queue = queue;
                _array = queue._innerArray;
                _version = queue._version;
                _index = 0;
                _currentIndex = (uint)queue._head - 1;
                _current = default;
            }

            public T Current => _current;

            object IEnumerator.Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    if (_index >= (uint)_queue._count) ThrowHelper.ThrowInvalidOperationException("Bad enumeration");
                    return _current;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                if (_version != _queue._version)
                    throw new InvalidOperationException("Collection was modified during enumeration.");
                _index++;
                if (_index > (uint)_queue._count) return false;
                _currentIndex++;
                if (_currentIndex == _array.Length) _currentIndex = 0;
                _current = _array[_currentIndex];
                return true;
            }

            public void Reset()
            {
                if (_version != _queue._version)
                    throw new InvalidOperationException("Collection was modified during enumeration.");

                _index = 0;
                _currentIndex = 0;
                _current = default;
            }

            public void Dispose() { }
        }

        #endregion
    }
}