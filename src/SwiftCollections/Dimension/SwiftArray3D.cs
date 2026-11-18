//=======================================================================
// SwiftArray3D.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Chronicler;
using Chronicler.Diagnostics;
using Chronicler.Serialization;
using MemoryPack;

namespace SwiftCollections.Dimensions;

/// <summary>
/// Stores a flattened 3D array with coordinate-based indexing and resizing.
/// </summary>
/// <typeparam name="T">The type of elements in the 3D array.</typeparam>
/// <remarks>
/// Coordinates are zero-based cells; the last coordinate varies fastest in flattened storage. Element copies are
/// shallow. Mutable instances and their enumerators require external synchronization. Restore constructors trust
/// dimension/data consistency.
/// </remarks>
[Serializable]
[JsonConverter(typeof(StateJsonConverterFactory))]
[MemoryPackable]
public partial class SwiftArray3D<T> : IStateBacked<Array3DState<T>>, IEnumerable<T>, IEnumerable
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
    /// This constructor creates an empty three-dimensional array. Use this overload when you intend to set the
    /// dimensions later or create an empty array.
    /// </remarks>
    public SwiftArray3D() : this(0, 0, 0) { }

    /// <summary>
    /// Initializes a new instance of the SwiftArray3D class with the specified dimensions.
    /// </summary>
    /// <param name="width">The new width in cells. Use a non-negative value and a dimension product representable
    /// as an array length.</param>
    /// <param name="height">The new height in cells. Use a non-negative value and a dimension product
    /// representable as an array length.</param>
    /// <param name="depth">The new depth in cells. Use a non-negative value and a dimension product representable
    /// as an array length.</param>
    /// <remarks>
    /// Unlike the 2D constructor, dimensions are stored verbatim without a non-negative guard. Negative or
    /// overflowing products can throw or create inconsistent shapes. The dimension product uses unchecked Int32
    /// arithmetic; provide dimensions whose product fits the supported array length. Zero dimensions create empty
    /// storage.
    /// </remarks>
    /// <exception cref="T:System.OverflowException">The unchecked dimension product is negative when used as an
    /// array length. Positive wrapped products are not detected; keep the full product representable.</exception>
    public SwiftArray3D(int width, int height, int depth)
    {
        _width = width;
        _height = height;
        _depth = depth;
        _innerArray = new T[width * height * depth];
    }

    /// <summary>
    /// Initializes a new instance of the SwiftArray3D class with the specified dimensions and fills all elements
    /// with the provided default value.
    /// </summary>
    /// <param name="width">The new width in cells. Use a non-negative value and a dimension product representable
    /// as an array length.</param>
    /// <param name="height">The new height in cells. Use a non-negative value and a dimension product
    /// representable as an array length.</param>
    /// <param name="depth">The new depth in cells. Use a non-negative value and a dimension product representable
    /// as an array length.</param>
    /// <param name="defaultValue">The value assigned to every selected cell; reference-type values are shared,
    /// not cloned.</param>
    /// <remarks>
    /// Unlike the 2D constructor, dimensions are stored verbatim without a non-negative guard. Negative or
    /// overflowing products can throw or create inconsistent shapes. The dimension product uses unchecked Int32
    /// arithmetic; provide dimensions whose product fits the supported array length. Zero dimensions create empty
    /// storage.
    /// </remarks>
    /// <exception cref="T:System.OverflowException">The unchecked dimension product is negative when used as an
    /// array length. Positive wrapped products are not detected; keep the full product representable.</exception>
    public SwiftArray3D(int width, int height, int depth, T defaultValue) : this(width, height, depth)
    {
        Fill(defaultValue);
    }

    /// <summary>
    /// Initializes a new instance of the SwiftArray3D class with the specified array state.
    /// </summary>
    /// <param name="state">The state to restore. Data is shallow-copied; dimensions and data length are not
    /// checked for consistency.</param>
    /// <exception cref="T:System.NullReferenceException">The state data array is null.</exception>
    /// <remarks>
    /// The state is a value type and may be default. Its data must be non-null; shape consistency is the caller's
    /// responsibility.
    /// </remarks>
    [MemoryPackConstructor]
    public SwiftArray3D(Array3DState<T> state)
    {
        State = state;
        ChronicleGuard.ThrowIfNull(_innerArray, nameof(_innerArray));
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the width of the object.
    /// </summary>
    /// <value>The stored width in cells; restored states are not validated.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Width => _width;

    /// <summary>
    /// Gets the height value associated with the current instance.
    /// </summary>
    /// <value>The stored height in cells; restored states are not validated.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Height => _height;

    /// <summary>
    /// Gets the current depth value for this instance.
    /// </summary>
    /// <value>The stored depth in cells; restored states are not validated.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Depth => _depth;

    /// <summary>
    /// Gets the unchecked product of the stored dimensions.
    /// </summary>
    /// <value>The logical cell count for valid dimensions; malformed restored dimensions or arithmetic overflow
    /// can make it differ from Length.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Size => _width * _height * _depth;

    /// <summary>
    /// Gets the length of the backing array.
    /// </summary>
    /// <value>The actual number of allocated cells, including all cells of a valid shape.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Length => _innerArray.Length;

    /// <summary>
    /// Gets or sets the element at the specified three-dimensional indices.
    /// </summary>
    /// <param name="x">The zero-based X coordinate; valid from zero through the corresponding dimension minus
    /// one.</param>
    /// <param name="y">The zero-based Y coordinate; valid from zero through the corresponding dimension minus
    /// one.</param>
    /// <param name="z">The zero-based Z coordinate; valid from zero through the corresponding dimension minus
    /// one.</param>
    /// <returns>The element located at the specified indices.</returns>
    /// <value>The stored element; assigning replaces that cell without cloning.</value>
    /// <remarks>
    /// An exception is thrown if any index is outside the valid range for its dimension.
    /// </remarks>
    /// <exception cref="T:System.IndexOutOfRangeException">A coordinate is outside its dimension, or inconsistent
    /// restored storage cannot hold the flattened offset.</exception>
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
    /// Gets a shallow snapshot of the dimensions and cell data.
    /// </summary>
    /// <value>A new state with independently allocated data storage; referenced elements remain shared.</value>
    /// <remarks>
    /// Restoration through the state constructor copies data but trusts dimensions and data length. Validate
    /// untrusted states before restoring. The setter is internal.
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
    /// Resizes the 3D array to the specified dimensions. Retains existing data where possible.
    /// </summary>
    /// <param name="newWidth">The new width in cells. Use a non-negative value and a dimension product
    /// representable as an array length.</param>
    /// <param name="newHeight">The new height in cells. Use a non-negative value and a dimension product
    /// representable as an array length.</param>
    /// <param name="newDepth">The new depth in cells. Use a non-negative value and a dimension product
    /// representable as an array length.</param>
    /// <remarks>
    /// Preserves cells at coordinates inside both old and new shapes; new cells contain default(T). Storage is
    /// replaced even for an unchanged shape. Dimension products use unchecked Int32 arithmetic; keep the product
    /// within the supported array length.
    /// </remarks>
    /// <exception cref="T:System.OverflowException">The unchecked dimension product is negative when used as an
    /// array length. Positive wrapped products are not detected; keep the full product representable.</exception>
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
    /// <param name="wrap"> Specifies whether to wrap elements that exceed the array's boundaries. If <c>true</c>,
    /// values wrap around to the other side of the array. If <c>false</c>, values that exceed boundaries are
    /// discarded. </param>
    /// <param name="xOffset">Signed displacement in cells along the X axis; positive values move toward larger
    /// coordinates.</param>
    /// <param name="yOffset">Signed displacement in cells along the Y axis; positive values move toward larger
    /// coordinates.</param>
    /// <param name="zOffset">Signed displacement in cells along the Z axis; positive values move toward larger
    /// coordinates.</param>
    /// <remarks>
    /// Allocates replacement storage even for a zero shift. Wrapping normalizes offsets modulo each dimension;
    /// empty dimensions are supported. Without wrapping, cells leaving the shape are discarded and newly exposed
    /// cells contain default(T). Existing enumerators are not version checked.
    /// </remarks>
    public void Shift(int xOffset, int yOffset, int zOffset, bool wrap = true)
    {
        var newArray = new T[Width * Height * Depth];

        if (wrap)
            ShiftWrapped(newArray, xOffset, yOffset, zOffset);
        else
            ShiftClamped(newArray, xOffset, yOffset, zOffset);

        _innerArray = newArray;
    }

    private void ShiftWrapped(T[] newArray, int xOffset, int yOffset, int zOffset)
    {
        int normalizedXOffset = NormalizeShift(xOffset, Width);
        int normalizedYOffset = NormalizeShift(yOffset, Height);
        int normalizedZOffset = NormalizeShift(zOffset, Depth);

        for (int x = 0; x < Width; x++)
        {
            int newX = WrapForwardIndex(x, normalizedXOffset, Width);
            for (int y = 0; y < Height; y++)
            {
                int newY = WrapForwardIndex(y, normalizedYOffset, Height);
                for (int z = 0; z < Depth; z++)
                {
                    int newZ = WrapForwardIndex(z, normalizedZOffset, Depth);
                    newArray[GetIndex(newX, newY, newZ)] = _innerArray[GetIndex(x, y, z)];
                }
            }
        }
    }

    private void ShiftClamped(T[] newArray, int xOffset, int yOffset, int zOffset)
    {
        GetShiftRange(Width, xOffset, out int xStart, out int xEnd);
        GetShiftRange(Height, yOffset, out int yStart, out int yEnd);
        GetShiftRange(Depth, zOffset, out int zStart, out int zEnd);

        for (int x = xStart; x < xEnd; x++)
        {
            int newX = x + xOffset;
            for (int y = yStart; y < yEnd; y++)
            {
                int newY = y + yOffset;
                for (int z = zStart; z < zEnd; z++)
                {
                    int newZ = z + zOffset;
                    newArray[GetIndex(newX, newY, newZ)] = _innerArray[GetIndex(x, y, z)];
                }
            }
        }
    }

    /// <summary>
    /// Clears all elements in the array.
    /// </summary>
    /// <remarks>
    /// Resets every allocated cell to default(T), releases stored references, and preserves dimensions and
    /// backing storage.
    /// </remarks>
    public void Clear() => Array.Clear(_innerArray, 0, _innerArray.Length);

    /// <summary>
    /// Fills the entire array with the specified value.
    /// </summary>
    /// <param name="value">The value assigned to every selected cell; reference-type values are shared, not
    /// cloned.</param>
    /// <remarks>
    /// Overwrites every allocated cell in place; dimensions and storage are preserved.
    /// </remarks>
    public void Fill(T value)
    {
        for (int i = 0; i < _innerArray.Length; i++)
            _innerArray[i] = value;
    }

    /// <summary>
    /// Calculates the one-dimensional array index corresponding to the specified three-dimensional coordinates.
    /// </summary>
    /// <param name="x">The zero-based X coordinate; valid from zero through the corresponding dimension minus
    /// one.</param>
    /// <param name="y">The zero-based Y coordinate; valid from zero through the corresponding dimension minus
    /// one.</param>
    /// <param name="z">The zero-based Z coordinate; valid from zero through the corresponding dimension minus
    /// one.</param>
    /// <returns>The unchecked flattened offset x * (Height * Depth) + y * Depth + z; coordinates are not
    /// validated.</returns>
    /// <remarks>
    /// Call IsValidIndex or ValidateIndex first when coordinates may be outside the shape. Integer arithmetic can
    /// wrap for invalid or excessively large inputs.
    /// </remarks>
    public virtual int GetIndex(int x, int y, int z)
    {
        return x * (Height * Depth) + y * Depth + z;
    }

    /// <summary>
    /// Validates the specified indices. Throws an exception if the indices are out of bounds.
    /// </summary>
    /// <param name="x">The zero-based X coordinate; valid from zero through the corresponding dimension minus
    /// one.</param>
    /// <param name="y">The zero-based Y coordinate; valid from zero through the corresponding dimension minus
    /// one.</param>
    /// <param name="z">The zero-based Z coordinate; valid from zero through the corresponding dimension minus
    /// one.</param>
    /// <exception cref="T:System.IndexOutOfRangeException">Any coordinate is outside its corresponding
    /// dimension.</exception>
    public virtual void ValidateIndex(int x, int y, int z)
    {
        if (!IsValidIndex(x, y, z))
            throw new IndexOutOfRangeException($"Invalid index ({x}, {y}, {z}) for dimensions ({Width}, {Height}, {Depth}).");
    }

    private static int NormalizeShift(int shift, int length)
    {
        if (length == 0)
            return 0;

        int normalized = shift % length;
        return normalized < 0 ? normalized + length : normalized;
    }

    private static int WrapForwardIndex(int index, int normalizedShift, int length)
    {
        int shifted = index + normalizedShift;
        return shifted >= length ? shifted - length : shifted;
    }

    private static void GetShiftRange(int length, int shift, out int start, out int end)
    {
        long startValue = shift < 0 ? -(long)shift : 0L;
        long endValue = shift > 0 ? (long)length - shift : length;

        start = (int)Math.Min(startValue, length);
        end = (int)Math.Max(0L, Math.Min(endValue, length));
    }

    /// <summary>
    /// Checks if the specified indices are within bounds.
    /// </summary>
    /// <param name="x">The zero-based X coordinate; valid from zero through the corresponding dimension minus
    /// one.</param>
    /// <param name="y">The zero-based Y coordinate; valid from zero through the corresponding dimension minus
    /// one.</param>
    /// <param name="z">The zero-based Z coordinate; valid from zero through the corresponding dimension minus
    /// one.</param>
    /// <returns>true if every coordinate is non-negative and below its corresponding dimension; otherwise false.
    /// No state changes.</returns>
    public virtual bool IsValidIndex(int x, int y, int z) =>
        x >= 0 && x < Width && y >= 0 && y < Height && z >= 0 && z < Depth;

    #endregion

    #region IEnumerator Implementation

    /// <summary>
    /// Returns an enumerator that iterates through all elements in the 3D array.
    /// </summary>
    /// <returns>An enumerator in increasing X, then Y, then Z order; the last coordinate varies
    /// fastest.</returns>
    /// <remarks>
    /// Enumeration reads the current instance and is not version checked. Do not mutate or resize during
    /// enumeration; changes can alter the remaining sequence.
    /// </remarks>
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
    /// Creates an enumerator through the implemented interface.
    /// </summary>
    /// <returns>An enumerator with the concrete collection's ordering and mutation rules. Interface dispatch may
    /// box the value-type enumerator.</returns>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


    #endregion
}
