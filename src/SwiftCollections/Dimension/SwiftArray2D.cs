using MemoryPack;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SwiftCollections.Dimensions;

/// <summary>
/// Represents a flattened 2D array with dynamic resizing and efficient access.
/// </summary>
/// <typeparam name="T">The type of elements in the array.</typeparam>
[Serializable]
[JsonConverter(typeof(SwiftStateJsonConverterFactory))]
[MemoryPackable]
public partial class SwiftArray2D<T> : IEnumerable<T>, IEnumerable
{
    #region Fields

    private T[] _innerArray;

    private int _width;

    private int _height;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="SwiftArray2D{T}"/> class.
    /// </summary>
    public SwiftArray2D() : this(0, 0) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SwiftArray2D{T}"/> class with specified dimensions.
    /// </summary>
    public SwiftArray2D(int width, int height)
    {
        Initialize(width, height);

        // Ensure that the inner array is not null after initialization
        SwiftThrowHelper.ThrowIfNull(_innerArray, nameof(_innerArray));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SwiftArray2D{T}"/> class with specified dimensions and default value.
    /// </summary>
    public SwiftArray2D(int width, int height, T defaultValue) : this(width, height)
    {
        Fill(defaultValue);
    }

    /// <summary>
    /// Initializes a new instance of the SwiftArray2D class by copying the contents of the specified two-dimensional array.
    /// </summary>
    /// <remarks>
    /// The dimensions of the new SwiftArray2D instance match those of the source array. 
    /// Changes to the source array after construction do not affect the SwiftArray2D instance.
    /// </remarks>
    /// <param name="source">The two-dimensional array whose elements are copied to the new SwiftArray2D instance. Cannot be null.</param>
    public SwiftArray2D(T[,] source)
    {
        int width = source.GetLength(0);
        int height = source.GetLength(1);

        Initialize(width, height);

        SwiftThrowHelper.ThrowIfNull(_innerArray, nameof(_innerArray));

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                this[x, y] = source[x, y];
    }

    /// <summary>
    /// Initializes a new instance of the SwiftArray2D class with the specified array state.
    /// </summary>
    /// <param name="state">The state object that encapsulates the underlying data and configuration for the two-dimensional array. Cannot be null.</param>
    [MemoryPackConstructor]
    public SwiftArray2D(Array2DState<T> state)
    {
        State = state;

        SwiftThrowHelper.ThrowIfNull(_innerArray, nameof(_innerArray));
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the width of the 2D array.
    /// </summary>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Width => _width;

    /// <summary>
    /// Gets the height of the 2D array.
    /// </summary>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Height => _height;

    /// <summary>
    /// Gets the underlying flattened array for direct access.
    /// </summary>
    [JsonIgnore]
    [MemoryPackIgnore]
    public T[] InnerArray => _innerArray;

    /// <summary>
    /// Total size of the array
    /// </summary>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Size => _width * _height;

    /// <inheritdoc cref="Array.Length" />
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Length => _innerArray.Length;

    /// <summary>
    /// Gets or sets the element at the specified position in the 2D array.
    /// </summary>
    /// <param name="x">The zero-based X coordinate.</param>
    /// <param name="y">The zero-based Y coordinate.</param>
    [JsonIgnore]
    [MemoryPackIgnore]
    public T this[int x, int y]
    {
        get
        {
            ValidateIndex(x, y);
            return _innerArray[GetIndex(x, y)];
        }
        set
        {
            ValidateIndex(x, y);
            _innerArray[GetIndex(x, y)] = value;
        }
    }

    /// <summary>
    /// Gets or sets the current state of the two-dimensional array, including its dimensions and data.
    /// </summary>
    /// <remarks>
    /// The returned state is a snapshot and is independent of future changes to the array. 
    /// Setting this property replaces the entire contents and dimensions of the array with those from the provided state.
    /// This property is intended for serialization and deserialization scenarios.
    /// </remarks>
    [JsonInclude]
    [MemoryPackInclude]
    public Array2DState<T> State
    {
        get
        {
            var data = new T[_innerArray.Length];
            Array.Copy(_innerArray, data, data.Length);

            return new Array2DState<T>(
                _width,
                _height,
                data
            );
        }

        internal set
        {
            SwiftThrowHelper.ThrowIfNull(value.Data, nameof(value.Data));

            _width = value.Width;
            _height = value.Height;

            int capacity = value.Data.Length;

            _innerArray = new T[capacity];
            Array.Copy(value.Data, _innerArray, capacity);
        }
    }

    #endregion

    #region Collection Management

    /// <summary>
    /// Adds the provides source into the current 2D Array.
    /// </summary>
    /// <remarks>
    /// Will overwrite current values.
    /// </remarks>
    /// <param name="source"></param>
    public void AddRange(T[,] source)
    {
        Resize(source.GetLength(0), source.GetLength(1));
        for (int i = 0; i < Width; i++)
        {
            for (int j = 0; j < Height; j++)
                this[i, j] = source[i, j];
        }
    }

    /// <summary>
    /// Fills the array with the specified value.
    /// </summary>
    public void Fill(T value)
    {
        for (int i = 0; i < _innerArray.Length; i++)
            _innerArray[i] = value;
    }

    /// <summary>
    /// Shifts the elements in the array by the specified X and Y offsets.
    /// </summary>
    /// <param name="xShift">The offset to apply along the X-axis.</param>
    /// <param name="yShift">The offset to apply along the Y-axis.</param>
    /// <param name="wrap">
    /// Specifies whether to wrap elements that exceed the array's boundaries. 
    /// If <c>true</c>, values wrap around to the other side of the array.
    /// If <c>false</c>, values that exceed boundaries are discarded.
    /// </param>
    public void Shift(int xShift, int yShift, bool wrap = true)
    {
        T[] newArray = new T[_innerArray.Length];

        for (int x = 0; x < _width; x++)
        {
            int newX = wrap ? (x + xShift + _width) % _width : x + xShift;
            if (!wrap && (newX < 0 || newX >= _width)) continue;

            for (int y = 0; y < _height; y++)
            {
                int newY = wrap ? (y + yShift + _height) % _height : y + yShift;
                if (!wrap && (newY < 0 || newY >= _height)) continue;

                newArray[newX * _height + newY] = this[x, y];
            }
        }

        _innerArray = newArray;
    }

    /// <summary>
    /// Clears all elements in the array.
    /// </summary>
    public void Clear() => Array.Clear(_innerArray, 0, _innerArray.Length);

    #endregion

    #region Capacity Management

    /// <summary>
    /// Resizes the 2D array to new dimensions, preserving existing values within the new bounds.
    /// </summary>
    public void Resize(int newWidth, int newHeight)
    {
        SwiftThrowHelper.ThrowIfNegative(newWidth, nameof(newWidth));
        SwiftThrowHelper.ThrowIfNegative(newHeight, nameof(newHeight));

        if (newWidth == _width && newHeight == _height)
            return;

        T[] newArray = new T[newWidth * newHeight];
        int minWidth = Math.Min(_width, newWidth);
        int minHeight = Math.Min(_height, newHeight);

        for (int x = 0; x < minWidth; x++)
            for (int y = 0; y < minHeight; y++)
                newArray[x * newHeight + y] = this[x, y];

        _innerArray = newArray;
        _width = newWidth;
        _height = newHeight;
    }

    #endregion

    #region Utility Methods

    private void Initialize(int width, int height)
    {
        _width = Math.Max(0, width);
        _height = Math.Max(0, height);
        _innerArray = new T[_width * _height];
    }

    /// <summary>
    /// Validates the specified index coordinates.
    /// </summary>
    public virtual void ValidateIndex(int x, int y)
    {
        if (x < 0 || x >= _width || y < 0 || y >= _height)
            throw new IndexOutOfRangeException($"Invalid index: ({x}, {y})");
    }

    /// <summary>
    /// Checks if the specified index is valid in the current array dimensions.
    /// </summary>
    public virtual bool IsValidIndex(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

    /// <summary>
    /// Converts 2D coordinates to a flattened index.
    /// </summary>
    public virtual int GetIndex(int x, int y) => x * _height + y;

    /// <summary>
    /// Converts the flattened array back to a 2D array representation.
    /// </summary>
    public T[,] ToArray()
    {
        T[,] result = new T[_width, _height];
        for (int x = 0; x < _width; x++)
            for (int y = 0; y < _height; y++)
                result[x, y] = this[x, y];
        return result;
    }

    /// <summary>
    /// Clones a 2D array into a new instance of Array2D.
    /// </summary>
    public SwiftArray2D<T> Clone()
    {
        var array2D = new SwiftArray2D<T>(Width, Height);
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
                array2D[x, y] = this[x, y];
        }
        return array2D;
    }

    #endregion

    #region IEnumerator Implementation

    /// <summary>
    /// Returns an enumerator that iterates through all elements in the 2D array.
    /// </summary>
    /// <returns>An enumerator for the 2D array.</returns>
    public IEnumerator<T> GetEnumerator()
    {
        for (int x = 0; x < _width; x++)
        {
            for (int y = 0; y < _height; y++)
            {
                yield return this[x, y];
            }
        }
    }

    /// <summary>
    /// Returns an enumerator that iterates through all elements in the 2D array (non-generic).
    /// </summary>
    /// <returns>An enumerator for the 2D array.</returns>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    #endregion
}
