//=======================================================================
// SwiftBoolArray2D.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Text.Json.Serialization;
using Chronicler;
using Chronicler.Serialization;
using MemoryPack;

namespace SwiftCollections.Dimensions;

/// <summary>
/// Represents a 2D array specifically designed to handle boolean values.
/// </summary>
[Serializable]
[JsonConverter(typeof(StateJsonConverterFactory))]
[MemoryPackable]
public partial class SwiftBoolArray2D : SwiftArray2D<bool>, IStateBacked<Array2DState<bool>>
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the SwiftBoolArray2D class.
    /// </summary>
    public SwiftBoolArray2D() { }

    /// <summary>
    /// Initializes a new instance of the SwiftBoolArray2D class with the specified dimensions.
    /// </summary>
    /// <param name="width">The requested width in cells; negative values are clamped to zero.</param>
    /// <param name="height">The requested height in cells; negative values are clamped to zero.</param>
    /// <remarks>
    /// The dimension product uses unchecked Int32 arithmetic; provide dimensions whose product fits the supported
    /// array length. Zero dimensions create empty storage.
    /// </remarks>
    /// <exception cref="T:System.OverflowException">The unchecked dimension product is negative when used as an
    /// array length. Positive wrapped products are not detected; keep the full product representable.</exception>
    public SwiftBoolArray2D(int width, int height) : base(width, height) { }

    /// <summary>
    /// Initializes a new instance of the SwiftBoolArray2D class with the specified dimensions and default value.
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
    public SwiftBoolArray2D(int width, int height, bool defaultValue) : base(width, height, defaultValue) { }

    /// <summary>
    /// Initializes a new instance of the SwiftBoolArray2D class using the specified two-dimensional Boolean array
    /// as the source data.
    /// </summary>
    /// <param name="source">The source rectangular array. Elements are shallow-copied; the source storage is not
    /// retained.</param>
    /// <exception cref="T:System.NullReferenceException">source is null; this constructor does not use the
    /// AddRange null guard.</exception>
    public SwiftBoolArray2D(bool[,] source) : base(source) { }

    /// <summary>
    /// Initializes a new instance of the SwiftBoolArray2D class using the specified array state.
    /// </summary>
    /// <param name="state">The state to restore. Data is shallow-copied; dimensions and data length are not
    /// checked for consistency.</param>
    /// <exception cref="T:System.ArgumentNullException">The state data array is null.</exception>
    /// <remarks>
    /// The state is a value type and may be default. Its data must be non-null; shape consistency is the caller's
    /// responsibility.
    /// </remarks>
    [MemoryPackConstructor]
    public SwiftBoolArray2D(Array2DState<bool> state) : base(state) { }

    #endregion

    #region Collection Management

    /// <summary>
    /// Toggles the value at the specified position in the array.
    /// </summary>
    /// <param name="x">The zero-based X coordinate; valid from zero through the corresponding dimension minus
    /// one.</param>
    /// <param name="y">The zero-based Y coordinate; valid from zero through the corresponding dimension minus
    /// one.</param>
    /// <exception cref="T:System.IndexOutOfRangeException">The cell coordinate is outside the shape.</exception>
    public void Toggle(int x, int y)
    {
        this[x, y] = !this[x, y];
    }

    /// <summary>
    /// Sets all values within a rectangular region to the specified value.
    /// </summary>
    /// <param name="width">The number of X cells to visit; non-positive widths visit no cells.</param>
    /// <param name="height">The number of Y cells to visit; non-positive heights visit no cells.</param>
    /// <param name="value">The value assigned to every selected cell; reference-type values are shared, not
    /// cloned.</param>
    /// <param name="xStart">The inclusive starting X coordinate in cells; out-of-bounds cells are
    /// skipped.</param>
    /// <param name="yStart">The inclusive starting Y coordinate in cells; out-of-bounds cells are
    /// skipped.</param>
    /// <remarks>
    /// Only coordinates inside the array are written. Region endpoints use unchecked Int32 addition; keep start
    /// plus extent representable. This is a clipped overwrite, not a resize.
    /// </remarks>
    public void SetRegion(int xStart, int yStart, int width, int height, bool value)
    {
        for (int x = xStart; x < xStart + width; x++)
        {
            for (int y = yStart; y < yStart + height; y++)
            {
                if (IsValidIndex(x, y))
                    this[x, y] = value;
            }
        }
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Counts the number of true values in the array.
    /// </summary>
    /// <returns>The number of true cells in the entire backing array; zero for empty storage.</returns>
    public int CountTrue()
    {
        int count = 0;
        foreach (var cell in InnerArray)
        {
            if (cell) count++;
        }
        return count;
    }

    #endregion
}
