//=======================================================================
// SwiftShortArray2D.cs
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
/// Represents a 2D array specifically designed to handle short integer values.
/// </summary>
[Serializable]
[JsonConverter(typeof(StateJsonConverterFactory))]
[MemoryPackable]
public partial class SwiftShortArray2D : SwiftArray2D<short>, IStateBacked<Array2DState<short>>
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the SwiftShortArray2D class.
    /// </summary>
    public SwiftShortArray2D() { }

    /// <summary>
    /// Initializes a new instance of the SwiftShortArray2D class with the specified dimensions.
    /// </summary>
    /// <param name="width">The requested width in cells; negative values are clamped to zero.</param>
    /// <param name="height">The requested height in cells; negative values are clamped to zero.</param>
    /// <remarks>
    /// The dimension product uses unchecked Int32 arithmetic; provide dimensions whose product fits the supported
    /// array length. Zero dimensions create empty storage.
    /// </remarks>
    /// <exception cref="T:System.OverflowException">The unchecked dimension product is negative when used as an
    /// array length. Positive wrapped products are not detected; keep the full product representable.</exception>
    public SwiftShortArray2D(int width, int height) : base(width, height) { }

    /// <summary>
    /// Initializes a new instance of the SwiftShortArray2D class with the specified dimensions and default value.
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
    public SwiftShortArray2D(int width, int height, short defaultValue) : base(width, height, defaultValue) { }

    /// <summary>
    /// Initializes a new instance of the SwiftShortArray2D class using the specified two-dimensional array of
    /// short values.
    /// </summary>
    /// <param name="source">The source rectangular array. Elements are shallow-copied; the source storage is not
    /// retained.</param>
    /// <exception cref="T:System.NullReferenceException">source is null; this constructor does not use the
    /// AddRange null guard.</exception>
    public SwiftShortArray2D(short[,] source) : base(source) { }

    /// <summary>
    /// Initializes a new instance of the SwiftShortArray2D class using the specified array state.
    /// </summary>
    /// <param name="state">The state to restore. Data is shallow-copied; dimensions and data length are not
    /// checked for consistency.</param>
    /// <exception cref="T:System.ArgumentNullException">The state data array is null.</exception>
    /// <remarks>
    /// The state is a value type and may be default. Its data must be non-null; shape consistency is the caller's
    /// responsibility.
    /// </remarks>
    [MemoryPackConstructor]
    public SwiftShortArray2D(Array2DState<short> state) : base(state) { }

    #endregion

    #region Collection Management

    /// <summary>
    /// Scales all elements in the array by the specified factor.
    /// </summary>
    /// <param name="factor">The signed multiplier applied in place; results narrow to Int16 with unchecked
    /// wraparound.</param>
    public void Scale(short factor)
    {
        for (int i = 0; i < InnerArray.Length; i++)
            InnerArray[i] = (short)(InnerArray[i] * factor);
    }

    /// <summary>
    /// Rescales non-constant Int16 data in place using floating-point interpolation.
    /// </summary>
    /// <param name="min">The requested lower endpoint.</param>
    /// <param name="max">The requested upper endpoint; reversed endpoints are not rejected.</param>
    /// <remarks>
    /// Uses single-precision floating-point interpolation and truncating conversion to Int16 in place. Empty
    /// storage is unchanged. Constant data divides zero by zero and converts NaN; no stable constant-input or
    /// cross-platform deterministic result is promised. Use non-constant data and ordered endpoints when a
    /// meaningful range is required.
    /// </remarks>
    public void Normalize(short min, short max)
    {
        short currentMin = short.MaxValue;
        short currentMax = short.MinValue;

        // Find current min and max
        foreach (var value in InnerArray)
        {
            if (value < currentMin) currentMin = value;
            if (value > currentMax) currentMax = value;
        }

        // Normalize values
        for (int i = 0; i < InnerArray.Length; i++)
            InnerArray[i] = (short)(((InnerArray[i] - currentMin) / (float)(currentMax - currentMin)) * (max - min) + min);
    }

    #endregion
}
