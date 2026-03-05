using System;
using System.Runtime.CompilerServices;

namespace SwiftCollections;

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
    public int[] Array { get; private set; }

    /// <summary>
    /// The current count of elements in the stack.
    /// </summary>
    public int Count { get; private set; }

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
        Array = capacity == 0 ? new int[DefaultCapacity] : new int[capacity];
        Count = 0;
    }

    /// <summary>
    /// Initializes a new instance of the IntStack class using the specified array as the underlying storage and sets
    /// the initial number of elements in the stack.
    /// </summary>
    /// <remarks>This constructor allows advanced scenarios where the stack is initialized with a pre-existing
    /// array and a specific element count. The caller is responsible for ensuring that the array and count accurately
    /// represent the intended stack state.</remarks>
    /// <param name="array">The array that provides the underlying storage for the stack. This parameter cannot be null.</param>
    /// <param name="count">The number of elements initially contained in the stack. Must be a non-negative integer and cannot exceed the
    /// length of the array.</param>
    public IntStack(int[] array, int count)
    {
        Array = array;
        Count = count;
    }

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
        EnsureCapacity(Count + 1);
        Array[Count++] = value;
    }

    /// <summary>
    /// Removes and returns the top integer from the stack.
    /// </summary>
    /// <returns>The top integer from the stack.</returns>
    /// <exception cref="InvalidOperationException">Thrown when attempting to pop from an empty stack.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Pop() => Array[--Count];

    /// <summary>
    /// Returns the top integer from the stack without removing it.
    /// </summary>
    /// <returns>The top integer from the stack.</returns>
    /// <exception cref="InvalidOperationException">Thrown when attempting to peek an empty stack.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Peek() => Array[Count - 1];

    /// <summary>
    /// Resets the stack to it's initial state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        Array = new int[DefaultCapacity];
        Count = 0;
    }

    /// <summary>
    /// Removes all elements from the stack.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear() => Count = 0;

    /// <summary>
    /// Ensures the stack has at least the specified capacity.
    /// Expands the internal array if necessary.
    /// </summary>
    /// <param name="capacity">The minimum capacity to ensure.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureCapacity(int capacity)
    {
        if (capacity >= Array.Length)
        {
            int[] newArray = new int[Array.Length * 2];
            System.Array.Copy(Array, 0, newArray, 0, Count);
            Array = newArray;
        }
    }

    #endregion
}
