using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SwiftCollections
{
    /// <summary>
    /// Represents a fast, array-based stack (LIFO - Last-In-First-Out) collection of objects.
    /// <para>
    /// The <c>SwiftStack&lt;T&gt;</c> class provides O(1) time complexity for <c>Push</c> and <c>Pop</c> operations,
    /// making it highly efficient for scenarios where performance is critical.
    /// It minimizes memory allocations by reusing internal arrays and offers methods
    /// like <c>FastClear</c> to quickly reset the stack without deallocating memory.
    /// </para>
    /// <para>
    /// This implementation is optimized for performance and does not perform versioning checks.
    /// Modifying the stack during enumeration may result in undefined behavior.
    /// </para>
    /// </summary>
    /// <typeparam name="T">Specifies the type of elements in the stack.</typeparam>
    [Serializable]
    public sealed class SwiftStack<T> : ISwiftCloneable<T>, IEnumerable<T>, IEnumerable, ICollection<T>, ICollection
    {
        #region Constants

        /// <summary>
        /// The default initial capacity of the SwiftStack if none is specified.
        /// Used to allocate a reasonable starting size to minimize resizing operations.
        /// </summary>
        public const int DefaultCapacity = 8;

        private static readonly T[] _emptyArray = new T[0];

        #endregion

        #region Fields

        /// <summary>
        /// The internal array that stores elements of the SwiftStack. Resized as needed to
        /// accommodate additional elements. Not directly exposed outside the stack.
        /// </summary>
        private T[] _innerArray;

        /// <summary>
        /// The current number of elements in the SwiftStack. Represents the total count of
        /// valid elements stored in the stack, also indicating the arrayIndex of the next insertion point.
        /// </summary>
        private int _count;

        [NonSerialized]
        private object _syncRoot;

        private uint _version;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new, empty instance of SwiftStack.
        /// </summary>
        public SwiftStack() : this(0) { }

        /// <summary>
        /// Initializes a new, empty instance of SwiftStack with the specified initial capacity.
        /// </summary>
        public SwiftStack(int capacity)
        {
            if (capacity == 0)
                _innerArray = _emptyArray;
            else
            {
                capacity = capacity <= DefaultCapacity ? DefaultCapacity : HashTools.NextPowerOfTwo(capacity);
                _innerArray = new T[capacity];
            }
        }

        public SwiftStack(IEnumerable<T> items)
        {
            if (items == null) ThrowHelper.ThrowArgumentNullException(nameof(items));

            if (items is ICollection<T> collection)
            {
                int count = collection.Count;
                if (count == 0)
                    _innerArray = _emptyArray;
                else
                {
                    int capacity = HashTools.NextPowerOfTwo(count <= DefaultCapacity ? DefaultCapacity : count);
                    _innerArray = new T[capacity];
                    collection.CopyTo(_innerArray, 0);
                    _count = count;
                }
            }
            else
            {
                _innerArray = new T[DefaultCapacity];
                foreach (T item in items)
                    Push(item);
            }
        }

        #endregion

        #region Properties

        public T[] InnerArray => _innerArray;

        /// <inheritdoc cref="_count"/>
        public int Count => _count;

        /// <summary>
        /// Gets the total number of elements the SwiftQueue can hold without resizing.
        /// Reflects the current allocated size of the internal array.
        /// </summary>
        public int Capacity => _innerArray.Length;

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void ICollection<T>.Add(T item) => Push(item);

        /// <summary>
        /// Inserts an object at the top of the SwiftStack.
        /// </summary>
        /// <param name="item"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Push(T item)
        {
            if ((uint)_count == (uint)_innerArray.Length)
                Resize(_innerArray.Length * 2);
            _innerArray[_count++] = item;
            _version++;
        }

        bool ICollection<T>.Remove(T item)
        {
            throw new NotSupportedException("Remove is not supported on Stack.");
        }

        /// <summary>
        /// Removes and returns the object at the top of the SwiftStack.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Pop()
        {
            if ((uint)_count == 0) ThrowHelper.ThrowInvalidOperationException("Stack is empty.");
            T item = _innerArray[--_count];
            _innerArray[_count] = default;
            _version++;
            return item;
        }

        /// <summary>
        /// Removes all elements from the SwiftStack, resetting its count to zero.
        /// </summary>
        public void Clear()
        {
            if (_count == 0) return;
            Array.Clear(_innerArray, 0, _count);
            _count = 0;
            _version++;
        }

        /// <summary>
        /// Clears the SwiftStack without releasing the reference to the stored elements.
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

        public void EnsureCapacity(int capacity)
        {
            capacity = HashTools.NextPowerOfTwo(capacity);
            if (capacity > _innerArray.Length)
                Resize(capacity);
        }

        /// <summary>
        /// Ensures that the capacity of the stack is sufficient to accommodate the specified number of elements.
        /// The stack capacity can increase by double to balance memory allocation efficiency and space.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Resize(int newSize)
        {
            int newCapacity = newSize <= DefaultCapacity ? DefaultCapacity : newSize;
            T[] newArray = new T[newCapacity];
            if ((uint)_count > 0)
                Array.Copy(_innerArray, 0, newArray, 0, _count);
            _innerArray = newArray;
            _version++;
        }


        /// <summary>
        /// Sets the capacity of a <see cref="SwiftStack{T}"/> to the actual 
        /// number of elements it contains, rounded up to a nearby next power of 2 value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TrimCapacity()
        {
            int newCapacity = _count <= DefaultCapacity ? DefaultCapacity : HashTools.NextPowerOfTwo(_count);
            T[] newArray = new T[newCapacity];
            if ((uint)_count > 0)
                Array.Copy(_innerArray, 0, newArray, 0, _count);
            _innerArray = newArray;
            _version++;
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Returns the object at the top of the SwiftStack without removing it.
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Peek()
        {
            if ((uint)_count == 0) ThrowHelper.ThrowInvalidOperationException("Stack is empty.");
            return _innerArray[_count - 1];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString() => (uint)_count == 0 ? $"{typeof(SwiftStack<T>)}: Empty" : $"{typeof(SwiftStack<T>)}: Count = {_count}";

        public void CopyTo(T[] array, int arrayIndex)
        {
            if (array == null) ThrowHelper.ThrowArgumentNullException(nameof(array));
            if ((uint)arrayIndex > array.Length) ThrowHelper.ThrowArgumentOutOfRangeException();
            if ((uint)(array.Length - arrayIndex) < (uint)_count) ThrowHelper.ThrowArgumentException("Destination array is not long enough.");

            Array.Copy(_innerArray, 0, array, arrayIndex, _count);
        }

        void ICollection.CopyTo(Array array, int arrayIndex)
        {
            if (array == null) ThrowHelper.ThrowArgumentNullException(nameof(array));
            if ((uint)array.Rank != 1) ThrowHelper.ThrowArgumentException("Array must be single dimensional.");
            if ((uint)array.GetLowerBound(0) != 0) ThrowHelper.ThrowArgumentException("Array must have zero-based indexing.");
            if ((uint)arrayIndex > array.Length) ThrowHelper.ThrowArgumentOutOfRangeException();
            if ((uint)(array.Length - arrayIndex) < _count) ThrowHelper.ThrowArgumentException("Destination array is not long enough.");

            try
            {
                for (int i = 0; (uint)i < (uint)_count; i++)
                    array.SetValue(_innerArray[i], arrayIndex++);
            }
            catch (ArrayTypeMismatchException)
            {
                ThrowHelper.ThrowArgumentException("Invalid array type.");
            }
        }

        public void CloneTo(ICollection<T> output)
        {
            output.Clear();
            for (int i = 0; (uint)i < (uint)_count; i++)
                output.Add(_innerArray[i]);
        }

        public bool Contains(T item)
        {
            EqualityComparer<T> comparer = EqualityComparer<T>.Default;
            for (int i = 0; i < _count; i++)
            {
                if (comparer.Equals(_innerArray[i], item))
                    return true;
            }
            return false;
        }

        #endregion

        #region Enumerators
        public SwiftStackEnumerator GetEnumerator() => new SwiftStackEnumerator(this);
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public struct SwiftStackEnumerator : IEnumerator<T>, IEnumerator, IDisposable
        {
            private readonly SwiftStack<T> _stack;
            private readonly T[] _array;
            private readonly uint _version;
            private readonly int _count;
            private int _index;

            private T _current;

            internal SwiftStackEnumerator(SwiftStack<T> stack)
            {
                _stack = stack;
                _array = stack._innerArray;
                _count = stack._count;
                _version = stack._version;
                _index = -2; // Enumerator not started
                _current = default;
            }

            public T Current => _current;

            object IEnumerator.Current
            {
                get
                {
                    if ((uint)_index > _count) ThrowHelper.ThrowInvalidOperationException("Bad enumeration");
                    return _current;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                if (_version != _stack._version)
                    ThrowHelper.ThrowInvalidOperationException("Enumerator modified outside of enumeration!");

                if (_index == -2)
                {
                    _index = _count - 1;
                }
                else
                {
                    _index--;
                }

                if( _index >= 0)
                {
                    _current = _array[_index];
                    return true;
                }

                _index = -1;
                _current = default;
                return false;
            }

            public void Reset()
            {
                if (_version != _stack._version)
                    ThrowHelper.ThrowInvalidOperationException("Enumerator modified outside of enumeration!");

                _index = -2;
                _current = default;
            }

            public void Dispose() => _index = -1;
        }

        #endregion
    }
}
