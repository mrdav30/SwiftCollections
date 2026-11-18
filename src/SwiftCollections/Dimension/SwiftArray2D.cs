//=======================================================================
// SwiftArray2D.cs
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
/// Stores a flattened 2D array with coordinate-based indexing and resizing.
/// </summary>
/// <typeparam name="T">The type of elements in the array.</typeparam>
/// <remarks>
/// Coordinates are zero-based cells; the last coordinate varies fastest in flattened storage. Element copies are
/// shallow. Mutable instances and their enumerators require external synchronization. Restore constructors trust
/// dimension/data consistency.
/// </remarks>
[Serializable]
[JsonConverter(typeof(StateJsonConverterFactory))]
[MemoryPackable]
public partial class SwiftArray2D<T> : IStateBacked<Array2DState<T>>, IEnumerable<T>, IEnumerable
{
    #region Fields

    private T[] _innerArray;

    private int _width;

    private int _height;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.Dimensions.SwiftArray2D`1" /> class.
    /// </summary>
    public SwiftArray2D() : this(0, 0) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.Dimensions.SwiftArray2D`1" /> class with
    /// specified dimensions.
    /// </summary>
    /// <param name="width">The requested width in cells; negative values are clamped to zero.</param>
    /// <param name="height">The requested height in cells; negative values are clamped to zero.</param>
    /// <remarks>
    /// The dimension product uses unchecked Int32 arithmetic; provide dimensions whose product fits the supported
    /// array length. Zero dimensions create empty storage.
    /// </remarks>
    /// <exception cref="T:System.OverflowException">The unchecked dimension product is negative when used as an
    /// array length. Positive wrapped products are not detected; keep the full product representable.</exception>
    public SwiftArray2D(int width, int height)
    {
        Initialize(width, height);

        // Ensure that the inner array is not null after initialization
        ChronicleGuard.ThrowIfNull(_innerArray, nameof(_innerArray));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.Dimensions.SwiftArray2D`1" /> class with
    /// specified dimensions and default value.
    /// </summary>
    /// <param name="width">The requested width in cells; negative values are clamped to zero.</param>
    /// <param name="height">The requested height in cells; negative values are clamped to zero.</param>
    /// <param name="defaultValue">The value assigned to every selected cell; reference-type values are shared,
    /// not cloned.</param>
    /// <remarks>
    /// The dimension product uses unchecked Int32 arithmetic; provide dimensions whose product fits the supported
    /// array length. Zero dimensions create empty storage.
    /// </remarks>
    /// <exception cref="T:System.OverflowException">The unchecked dimension product is negative when used as an
    /// array length. Positive wrapped products are not detected; keep the full product representable.</exception>
    public SwiftArray2D(int width, int height, T defaultValue) : this(width, height)
    {
        Fill(defaultValue);
    }

    /// <summary>
    /// Initializes a new instance of the SwiftArray2D class by copying the contents of the specified
    /// two-dimensional array.
    /// </summary>
    /// <param name="source">The source rectangular array. Elements are shallow-copied; the source storage is not
    /// retained.</param>
    /// <exception cref="T:System.NullReferenceException">source is null; this constructor does not use the
    /// AddRange null guard.</exception>
    /// <remarks>
    /// The dimensions of the new SwiftArray2D instance match those of the source array. Changes to the source
    /// array after construction do not affect the SwiftArray2D instance.
    /// </remarks>
    public SwiftArray2D(T[,] source)
    {
        int width = source.GetLength(0);
        int height = source.GetLength(1);

        Initialize(width, height);

        ChronicleGuard.ThrowIfNull(_innerArray, nameof(_innerArray));

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                this[x, y] = source[x, y];
    }

    /// <summary>
    /// Initializes a new instance of the SwiftArray2D class with the specified array state.
    /// </summary>
    /// <param name="state">The state to restore. Data is shallow-copied; dimensions and data length are not
    /// checked for consistency.</param>
    /// <exception cref="T:System.ArgumentNullException">The state data array is null.</exception>
    /// <remarks>
    /// The state is a value type and may be default. Its data must be non-null; shape consistency is the caller's
    /// responsibility.
    /// </remarks>
    [MemoryPackConstructor]
    public SwiftArray2D(Array2DState<T> state)
    {
        State = state;

        ChronicleGuard.ThrowIfNull(_innerArray, nameof(_innerArray));
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the width of the 2D array.
    /// </summary>
    /// <value>The stored width in cells; restored states are not validated.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Width => _width;

    /// <summary>
    /// Gets the height of the 2D array.
    /// </summary>
    /// <value>The stored height in cells; restored states are not validated.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Height => _height;

    /// <summary>
    /// Gets the underlying flattened array for direct access.
    /// </summary>
    /// <value>The live mutable backing array, not a copy.</value>
    /// <remarks>
    /// Writes affect this instance. Resize when dimensions change, and Shift, replace storage; AddRange replaces
    /// storage only when its length changes. Previously obtained arrays then remain detached. No synchronization
    /// or mutation detection is provided.
    /// </remarks>
    [JsonIgnore]
    [MemoryPackIgnore]
    public T[] InnerArray => _innerArray;

    /// <summary>
    /// Gets the unchecked product of the stored dimensions.
    /// </summary>
    /// <value>The logical cell count for valid dimensions; malformed restored dimensions or arithmetic overflow
    /// can make it differ from Length.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Size => _width * _height;

    /// <summary>
    /// Gets the length of the backing array.
    /// </summary>
    /// <value>The actual number of allocated cells, including all cells of a valid shape.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Length => _innerArray.Length;

    /// <summary>
    /// Gets or sets the element at the specified position in the 2D array.
    /// </summary>
    /// <param name="x">The zero-based X coordinate; valid from zero through the corresponding dimension minus
    /// one.</param>
    /// <param name="y">The zero-based Y coordinate; valid from zero through the corresponding dimension minus
    /// one.</param>
    /// <value>The stored element; assigning replaces that cell without cloning.</value>
    /// <exception cref="T:System.IndexOutOfRangeException">A coordinate is outside its dimension, or inconsistent
    /// restored storage cannot hold the flattened offset.</exception>
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
    /// Gets a shallow snapshot of the dimensions and cell data.
    /// </summary>
    /// <value>A new state with independently allocated data storage; referenced elements remain shared.</value>
    /// <remarks>
    /// Restoration through the state constructor copies data but trusts dimensions and data length. Validate
    /// untrusted states before restoring. The setter is internal.
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
            ChronicleGuard.ThrowIfNull(value.Data, nameof(value.Data));

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
    /// Replaces the shape and all cells with a shallow copy of a rectangular array.
    /// </summary>
    /// <param name="source">The source rectangular array. Elements are shallow-copied; the source storage is not
    /// retained.</param>
    /// <exception cref="T:System.ArgumentNullException">source is null.</exception>
    /// <remarks>
    /// This operation overwrites rather than appends. Equal flattened lengths reuse storage even when the shape
    /// changes; otherwise storage is replaced.
    /// </remarks>
    public void AddRange(T[,] source)
    {
        ChronicleGuard.ThrowIfNull(source, nameof(source));

        int width = source.GetLength(0);
        int height = source.GetLength(1);
        int length = source.Length;

        if (_innerArray.Length != length)
            _innerArray = new T[length];

        _width = width;
        _height = height;

        int index = 0;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
                _innerArray[index++] = source[x, y];
        }
    }

    /// <summary>
    /// Fills the array with the specified value.
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
    /// Shifts the elements in the array by the specified X and Y offsets.
    /// </summary>
    /// <param name="wrap"> Specifies whether to wrap elements that exceed the array's boundaries. If <c>true</c>,
    /// values wrap around to the other side of the array. If <c>false</c>, values that exceed boundaries are
    /// discarded. </param>
    /// <param name="xShift">Signed displacement in cells along the X axis; positive values move toward larger
    /// coordinates.</param>
    /// <param name="yShift">Signed displacement in cells along the Y axis; positive values move toward larger
    /// coordinates.</param>
    /// <remarks>
    /// Allocates replacement storage even for a zero shift. Wrapping normalizes offsets modulo each dimension;
    /// empty dimensions are supported. Without wrapping, cells leaving the shape are discarded and newly exposed
    /// cells contain default(T). Existing enumerators are not version checked.
    /// </remarks>
    public void Shift(int xShift, int yShift, bool wrap = true)
    {
        T[] newArray = new T[_innerArray.Length];

        if (wrap)
            ShiftWrapped(newArray, xShift, yShift);
        else
            ShiftClamped(newArray, xShift, yShift);

        _innerArray = newArray;
    }

    private void ShiftWrapped(T[] newArray, int xShift, int yShift)
    {
        int normalizedXShift = NormalizeShift(xShift, _width);
        int normalizedYShift = NormalizeShift(yShift, _height);

        for (int x = 0; x < _width; x++)
        {
            int newX = WrapForwardIndex(x, normalizedXShift, _width);
            int sourceBase = x * _height;
            int targetBase = newX * _height;

            for (int y = 0; y < _height; y++)
            {
                int newY = WrapForwardIndex(y, normalizedYShift, _height);
                newArray[targetBase + newY] = _innerArray[sourceBase + y];
            }
        }
    }

    private void ShiftClamped(T[] newArray, int xShift, int yShift)
    {
        GetShiftRange(_width, xShift, out int xStart, out int xEnd);
        GetShiftRange(_height, yShift, out int yStart, out int yEnd);

        for (int x = xStart; x < xEnd; x++)
        {
            int newX = x + xShift;
            int sourceBase = x * _height;
            int targetBase = newX * _height;

            for (int y = yStart; y < yEnd; y++)
            {
                int newY = y + yShift;
                newArray[targetBase + newY] = _innerArray[sourceBase + y];
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

    #endregion

    #region Capacity Management

    /// <summary>
    /// Resizes the 2D array to new dimensions, preserving existing values within the new bounds.
    /// </summary>
    /// <param name="newWidth">The new width in cells. Use a non-negative value and a dimension product
    /// representable as an array length.</param>
    /// <param name="newHeight">The new height in cells. Use a non-negative value and a dimension product
    /// representable as an array length.</param>
    /// <exception cref="T:System.ArgumentOutOfRangeException">A new dimension is negative.</exception>
    /// <remarks>
    /// Preserves cells at coordinates inside both old and new shapes; new cells contain default(T). Storage is
    /// replaced only when the dimensions change. Negative dimensions are rejected before mutation. Dimension
    /// products use unchecked Int32 arithmetic; keep the product within the supported array length.
    /// </remarks>
    /// <exception cref="T:System.OverflowException">The unchecked dimension product is negative when used as an
    /// array length. Positive wrapped products are not detected; keep the full product representable.</exception>
    public void Resize(int newWidth, int newHeight)
    {
        ChronicleGuard.ThrowIfNegative(newWidth, nameof(newWidth));
        ChronicleGuard.ThrowIfNegative(newHeight, nameof(newHeight));

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
    /// Validates the specified index coordinates.
    /// </summary>
    /// <param name="x">The zero-based X coordinate; valid from zero through the corresponding dimension minus
    /// one.</param>
    /// <param name="y">The zero-based Y coordinate; valid from zero through the corresponding dimension minus
    /// one.</param>
    /// <exception cref="T:System.IndexOutOfRangeException">Any coordinate is outside its corresponding
    /// dimension.</exception>
    public virtual void ValidateIndex(int x, int y)
    {
        if (x < 0 || x >= _width || y < 0 || y >= _height)
            throw new IndexOutOfRangeException($"Invalid index: ({x}, {y})");
    }

    /// <summary>
    /// Checks if the specified index is valid in the current array dimensions.
    /// </summary>
    /// <param name="x">The zero-based X coordinate; valid from zero through the corresponding dimension minus
    /// one.</param>
    /// <param name="y">The zero-based Y coordinate; valid from zero through the corresponding dimension minus
    /// one.</param>
    /// <returns>true if every coordinate is non-negative and below its corresponding dimension; otherwise false.
    /// No state changes.</returns>
    public virtual bool IsValidIndex(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

    /// <summary>
    /// Converts 2D coordinates to a flattened index.
    /// </summary>
    /// <param name="x">The zero-based X coordinate; valid from zero through the corresponding dimension minus
    /// one.</param>
    /// <param name="y">The zero-based Y coordinate; valid from zero through the corresponding dimension minus
    /// one.</param>
    /// <returns>The unchecked flattened offset x * Height + y; coordinates are not validated.</returns>
    /// <remarks>
    /// Call IsValidIndex or ValidateIndex first when coordinates may be outside the shape. Integer arithmetic can
    /// wrap for invalid or excessively large inputs.
    /// </remarks>
    public virtual int GetIndex(int x, int y) => x * _height + y;

    /// <summary>
    /// Converts the flattened array back to a 2D array representation.
    /// </summary>
    /// <returns>A new rectangular array with the same shape and shallow-copied elements.</returns>
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
    /// <returns>A new array instance with independent storage and shallow-copied elements.</returns>
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
    /// <returns>An enumerator in increasing X, then Y order; the last coordinate varies fastest.</returns>
    /// <remarks>
    /// Enumeration reads the current instance and is not version checked. Do not mutate or resize during
    /// enumeration; changes can alter the remaining sequence.
    /// </remarks>
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
    /// Creates an enumerator through the implemented interface.
    /// </summary>
    /// <returns>An enumerator with the concrete collection's ordering and mutation rules. Interface dispatch may
    /// box the value-type enumerator.</returns>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    #endregion
}
