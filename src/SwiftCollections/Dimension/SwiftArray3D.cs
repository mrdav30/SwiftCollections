using MemoryPack;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SwiftCollections.Dimensions;

/// <summary>
/// Represents a generic, flattened 3D array with efficient indexing and resizing capabilities.
/// Optimized for use in performance-critical applications like game grids.
/// </summary>
/// <typeparam name="T">The type of elements in the 3D array.</typeparam>
[Serializable]
[JsonConverter(typeof(SwiftStateJsonConverterFactory))]
[MemoryPackable]
public partial class SwiftArray3D<T> : IEnumerable<T>, IEnumerable
{
    #region Fields

    private T[] _innerArray;

    private int _width;

    private int _height;

    private int _depth;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the SwiftArray3D class with zero dimensions.
    /// </summary>
    /// <remarks>
    /// This constructor creates an empty three-dimensional array. 
    /// Use this overload when you intend to set the dimensions later or create an empty array.
    /// </remarks>
    public SwiftArray3D() : this(0, 0, 0) { }

    /// <summary>
    /// Initializes a new instance of the SwiftArray3D class with the specified dimensions.
    /// </summary>
    /// <param name="width">The number of elements in the first dimension. Must be greater than zero.</param>
    /// <param name="height">The number of elements in the second dimension. Must be greater than zero.</param>
    /// <param name="depth">The number of elements in the third dimension. Must be greater than zero.</param>
    public SwiftArray3D(int width, int height, int depth)
    {
        _width = width;
        _height = height;
        _depth = depth;
        _innerArray = new T[width * height * depth];
    }

    /// <summary>
    /// Initializes a new instance of the SwiftArray3D class with the specified dimensions and fills all elements with
    /// the provided default value.
    /// </summary>
    /// <param name="width">The number of elements in the first dimension. Must be greater than zero.</param>
    /// <param name="height">The number of elements in the second dimension. Must be greater than zero.</param>
    /// <param name="depth">The number of elements in the third dimension. Must be greater than zero.</param>
    /// <param name="defaultValue">The value to assign to each element in the array upon initialization.</param>
    public SwiftArray3D(int width, int height, int depth, T defaultValue) : this(width, height, depth)
    {
        Fill(defaultValue);
    }

    /// <summary>
    /// Initializes a new instance of the SwiftArray3D class with the specified array state.
    /// </summary>
    /// <param name="state">
    /// The state object that encapsulates the underlying data and configuration for the three-dimensional array. 
    /// Cannot be null.
    /// </param>
    [MemoryPackConstructor]
    public SwiftArray3D(Array3DState<T> state)
    {
        State = state;
        _innerArray ??= Array.Empty<T>(); // Ensure _innerArray is initialized to avoid null reference issues.
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the width of the object.
    /// </summary>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Width => _width;

    /// <summary>
    /// Gets the height value associated with the current instance.
    /// </summary>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Height => _height;

    /// <summary>
    /// Gets the current depth value for this instance.
    /// </summary>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Depth => _depth;

    /// <summary>
    /// Total size of the array.
    /// </summary>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Size => _width * _height * _depth;

    /// <inheritdoc cref="Array.Length" />
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Length => _innerArray.Length;

    /// <summary>
    /// Gets or sets the element at the specified three-dimensional indices.
    /// </summary>
    /// <remarks>An exception is thrown if any index is outside the valid range for its dimension.</remarks>
    /// <param name="x">The zero-based index along the first dimension.</param>
    /// <param name="y">The zero-based index along the second dimension.</param>
    /// <param name="z">The zero-based index along the third dimension.</param>
    /// <returns>The element located at the specified indices.</returns>
    [JsonIgnore]
    [MemoryPackIgnore]
    public T this[int x, int y, int z]
    {
        get
        {
            ValidateIndex(x, y, z);
            return _innerArray[GetIndex(x, y, z)];
        }
        set
        {
            ValidateIndex(x, y, z);
            _innerArray[GetIndex(x, y, z)] = value;
        }
    }

    /// <summary>
    /// Gets or sets the complete state of the 3D array, including its dimensions and data contents.
    /// </summary>
    /// <remarks>
    /// Setting this property replaces the current array's dimensions and data with those from the specified state. 
    /// Getting this property returns a snapshot of the current array state. 
    /// This property is intended for serialization and deserialization scenarios.
    /// </remarks>
    [JsonInclude]
    [MemoryPackInclude]
    public Array3DState<T> State
    {
        get
        {
            var data = new T[_innerArray.Length];
            Array.Copy(_innerArray, data, data.Length);

            return new Array3DState<T>(
                _width,
                _height,
                _depth,
                data
            );
        }

        internal set
        {
            _width = value.Width;
            _height = value.Height;
            _depth = value.Depth;

            _innerArray = new T[value.Data.Length];
            Array.Copy(value.Data, _innerArray, value.Data.Length);
        }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Resizes the 3D array to the specified dimensions.
    /// Retains existing data where possible.
    /// </summary>
    public void Resize(int newWidth, int newHeight, int newDepth)
    {
        var newArray = new T[newWidth * newHeight * newDepth];

        int minWidth = Math.Min(Width, newWidth);
        int minHeight = Math.Min(Height, newHeight);
        int minDepth = Math.Min(Depth, newDepth);

        for (int x = 0; x < minWidth; x++)
        {
            for (int y = 0; y < minHeight; y++)
            {
                for (int z = 0; z < minDepth; z++)
                {
                    int srcIndex = GetIndex(x, y, z);
                    int dstIndex = x * (newHeight * newDepth) + y * newDepth + z;
                    newArray[dstIndex] = _innerArray[srcIndex];
                }
            }
        }

        _innerArray = newArray;
        _width = newWidth;
        _height = newHeight;
        _depth = newDepth;
    }

    /// <summary>
    /// Shifts the elements in the array by the specified offsets along each axis.
    /// </summary>
    /// <param name="xOffset">The offset to apply along the X-axis.</param>
    /// <param name="yOffset">The offset to apply along the Y-axis.</param>
    /// <param name="zOffset">The offset to apply along the Z-axis.</param>
    /// <param name="wrap">
    /// Specifies whether to wrap elements that exceed the array's boundaries. 
    /// If <c>true</c>, values wrap around to the other side of the array.
    /// If <c>false</c>, values that exceed boundaries are discarded.
    /// </param>
    /// <remarks>
    /// - Wrapping behavior ensures that no data is lost during shifts.
    /// - Non-wrapping behavior discards elements that move out of bounds.
    /// </remarks>
    public void Shift(int xOffset, int yOffset, int zOffset, bool wrap = true)
    {
        var newArray = new T[Width * Height * Depth];

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                for (int z = 0; z < Depth; z++)
                {
                    int newX = wrap ? (x + xOffset + Width) % Width : x + xOffset;
                    int newY = wrap ? (y + yOffset + Height) % Height : y + yOffset;
                    int newZ = wrap ? (z + zOffset + Depth) % Depth : z + zOffset;

                    if (IsValidIndex(newX, newY, newZ))
                    {
                        int srcIndex = GetIndex(x, y, z);
                        int dstIndex = GetIndex(newX, newY, newZ);
                        newArray[dstIndex] = _innerArray[srcIndex];
                    }
                }
            }
        }

        _innerArray = newArray;
    }

    /// <summary>
    /// Clears all elements in the array.
    /// </summary>
    public void Clear() => Array.Clear(_innerArray, 0, _innerArray.Length);

    /// <summary>
    /// Fills the entire array with the specified value.
    /// </summary>
    public void Fill(T value)
    {
        for (int i = 0; i < _innerArray.Length; i++)
            _innerArray[i] = value;
    }

    /// <summary>
    /// Calculates the one-dimensional array index corresponding to the specified three-dimensional coordinates.
    /// </summary>
    /// <remarks>
    /// Use this method to map three-dimensional coordinates to a linear array index when working with flattened 3D data structures. 
    /// The valid ranges for x, y, and z depend on the dimensions of the underlying data structure.
    /// </remarks>
    /// <param name="x">The zero-based X coordinate to convert.</param>
    /// <param name="y">The zero-based Y coordinate to convert.</param>
    /// <param name="z">The zero-based Z coordinate to convert.</param>
    /// <returns>The zero-based index in the underlying one-dimensional array that corresponds to the specified (x, y, z) coordinates.</returns>
    public virtual int GetIndex(int x, int y, int z)
    {
        return x * (Height * Depth) + y * Depth + z;
    }

    /// <summary>
    /// Validates the specified indices.
    /// Throws an exception if the indices are out of bounds.
    /// </summary>
    public virtual void ValidateIndex(int x, int y, int z)
    {
        if (!IsValidIndex(x, y, z))
            throw new IndexOutOfRangeException($"Invalid index ({x}, {y}, {z}) for dimensions ({Width}, {Height}, {Depth}).");
    }

    /// <summary>
    /// Checks if the specified indices are within bounds.
    /// </summary>
    public virtual bool IsValidIndex(int x, int y, int z) =>
        x >= 0 && x < Width && y >= 0 && y < Height && z >= 0 && z < Depth;

    #endregion

    #region IEnumerator Implementation

    /// <summary>
    /// Returns an enumerator that iterates through all elements in the 3D array.
    /// </summary>
    /// <returns>An enumerator for the 3D array.</returns>
    public IEnumerator<T> GetEnumerator()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                for (int z = 0; z < Depth; z++)
                    yield return this[x, y, z];
            }
        }
    }

    /// <summary>
    /// Returns an enumerator that iterates through all elements in the 3D array (non-generic).
    /// </summary>
    /// <returns>An enumerator for the 3D array.</returns>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


    #endregion
}
