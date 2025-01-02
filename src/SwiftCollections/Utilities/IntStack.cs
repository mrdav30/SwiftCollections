using System;
using System.Runtime.CompilerServices;

namespace SwiftCollections
{
    /// <summary>
    /// A minimal and efficient stack implementation for integers.
    /// Optimized for internal use in within the SwiftCollections library.
    /// </summary>
    [Serializable]
    internal class IntStack
    {
        #region Constants

        /// <summary>
        /// The default initial capacity of the stack.
        /// </summary>
        public const int DefaultCapacity = 8;

        #endregion

        #region Fields

        /// <summary>
        /// The internal array holding stack elements.
        /// </summary>
        private int[] _array;

        /// <summary>
        /// The current count of elements in the stack.
        /// </summary>
        private int _count;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="IntStack"/> class with the default capacity.
        /// </summary>
        public IntStack() : this(DefaultCapacity) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="IntStack"/> class with a specified initial capacity.
        /// </summary>
        /// <param name="capacity">The initial capacity of the stack.</param>
        public IntStack(int capacity)
        {
            _array = capacity == 0 ? new int[DefaultCapacity] : new int[capacity];
            _count = 0;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the number of elements currently in the stack.
        /// </summary>
        public int Count => _count;

        #endregion

        #region Public Methods

        /// <summary>
        /// Pushes an integer onto the stack.
        /// Expands the stack's capacity if necessary.
        /// </summary>
        /// <param name="value">The integer value to push.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Push(int value)
        {
            EnsureCapacity(_count + 1);
            _array[_count++] = value;
        }

        /// <summary>
        /// Removes and returns the top integer from the stack.
        /// </summary>
        /// <returns>The top integer from the stack.</returns>
        /// <exception cref="InvalidOperationException">Thrown when attempting to pop from an empty stack.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Pop() => _array[--_count];

        /// <summary>
        /// Returns the top integer from the stack without removing it.
        /// </summary>
        /// <returns>The top integer from the stack.</returns>
        /// <exception cref="InvalidOperationException">Thrown when attempting to peek an empty stack.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Peek() => _array[_count - 1];

        /// <summary>
        /// Resets the stack to it's initial state.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            _array = new int[DefaultCapacity];
            _count = 0;
        }

        /// <summary>
        /// Removes all elements from the stack.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear() => _count = 0;

        /// <summary>
        /// Ensures the stack has at least the specified capacity.
        /// Expands the internal array if necessary.
        /// </summary>
        /// <param name="capacity">The minimum capacity to ensure.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureCapacity(int capacity)
        {
            if (capacity >= _array.Length)
            {
                int[] newArray = new int[_array.Length * 2];
                Array.Copy(_array, 0, newArray, 0, _count);
                _array = newArray;
            }
        }

        #endregion
    }
}
