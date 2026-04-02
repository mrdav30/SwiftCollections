using MemoryPack;
using System;
using System.Collections;
using System.Collections.Generic;

#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#endif
#if !NET8_0_OR_GREATER
using System.Text.Json.Serialization.Shim;
#endif

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

    public SwiftArray3D() : this(0, 0, 0) { }

    public SwiftArray3D(int width, int height, int length)
    {
        Initialize(width, height, length);
    }

    public SwiftArray3D(int width, int height, int length, T defaultValue) : this(width, height, length)
    {
        Fill(defaultValue);
    }

    [MemoryPackConstructor]
    public SwiftArray3D(Array3DState<T> state)
    {
        State = state;
    }

    #endregion

    #region Properties


    [JsonIgnore]
    [MemoryPackIgnore]
    public int Width => _width;

    [JsonIgnore]
    [MemoryPackIgnore]
    public int Height => _height;

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
    /// Initializes the 3D array with the specified dimensions.
    /// </summary>
    private void Initialize(int width, int height, int depth)
    {
        _width = width;
        _height = height;
        _depth = depth;
        _innerArray = new T[width * height * depth];
    }

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
