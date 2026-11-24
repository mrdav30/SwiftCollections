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
    /// Negative dimensions are clamped before checking the exact dimension product. A zero dimension creates
    /// empty storage.
    /// </remarks>
    /// <exception cref="T:System.OverflowException">The exact product of the clamped dimensions exceeds
    /// Int32.MaxValue.</exception>
    public SwiftShortArray2D(int width, int height) : base(width, height) { }

    /// <summary>
    /// Initializes a new instance of the SwiftShortArray2D class with the specified dimensions and default value.
    /// </summary>
    /// <param name="width">The requested width in cells; negative values are clamped to zero.</param>
    /// <param name="height">The requested height in cells; negative values are clamped to zero.</param>
    /// <param name="defaultValue">The value assigned to every selected cell; reference-type values are shared,
    /// not cloned.</param>
    /// <remarks>
    /// Negative dimensions are clamped before checking the exact dimension product. A zero dimension creates
    /// empty storage.
    /// </remarks>
    /// <exception cref="T:System.OverflowException">The exact product of the clamped dimensions exceeds
    /// Int32.MaxValue.</exception>
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
    /// <param name="state">The state to restore. Dimensions must be non-negative and their exact product must
    /// equal the non-null data array's length. Data is copied into independently owned storage.</param>
    /// <exception cref="T:System.ArgumentNullException">The state data array is null.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">A state dimension is negative.</exception>
    /// <exception cref="T:System.OverflowException">The exact dimension product exceeds Int32.MaxValue.</exception>
    /// <exception cref="T:System.ArgumentException">The data length differs from the dimension product.</exception>
    /// <remarks>
    /// A zero dimension requires empty data. Unlike dimensional construction, restoration rejects negative
    /// dimensions instead of clamping them. Validation and copying complete before the restored state is installed.
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
    /// Rescales the stored values in place using deterministic integer interpolation.
    /// </summary>
    /// <param name="min">The value assigned to the smallest input, or to every cell when the input is constant.</param>
    /// <param name="max">The value assigned to the largest non-constant input. May be less than min for a
    /// descending mapping, or equal to min for a constant output.</param>
    /// <remarks>
    /// Maps the observed input range linearly onto the requested endpoints, truncating the final exact result
    /// toward zero. All Int16 inputs and endpoints are supported without overflow or floating-point arithmetic.
    /// Empty storage is unchanged. Constant input is filled with min. Dimensions and backing storage are preserved.
    /// </remarks>
    public void Normalize(short min, short max)
    {
        short[] data = InnerArray;
        short currentMin = short.MaxValue;
        short currentMax = short.MinValue;

        foreach (var value in data)
        {
            if (value < currentMin) currentMin = value;
            if (value > currentMax) currentMax = value;
        }

        if (currentMin == currentMax)
        {
            Fill(min);
            return;
        }

        int range = currentMax - currentMin;
        for (int i = 0; i < data.Length; i++)
        {
            int value = data[i];
            // Nonnegative weights sum to range, so the weighted sum fits Int32 for every Int16 endpoint.
            int numerator = min * (currentMax - value) + max * (value - currentMin);
            data[i] = (short)(numerator / range);
        }
    }

    #endregion
}
