using MemoryPack;
using System;
using System.Text.Json.Serialization;

namespace SwiftCollections.Dimensions;

/// <summary>
/// Represents a 2D array specifically designed to handle short integer values.
/// </summary>
[Serializable]
[JsonConverter(typeof(SwiftStateJsonConverterFactory))]
[MemoryPackable]
public partial class SwiftShortArray2D : SwiftArray2D<short>
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the SwiftShortArray2D class.
    /// </summary>
    public SwiftShortArray2D() { }

    /// <summary>
    /// Initializes a new instance of the SwiftShortArray2D class with the specified dimensions.
    /// </summary>
    /// <param name="width">The number of columns in the two-dimensional array. Must be greater than zero.</param>
    /// <param name="height">The number of rows in the two-dimensional array. Must be greater than zero.</param>
    public SwiftShortArray2D(int width, int height) : base(width, height) { }

    /// <summary>
    /// Initializes a new instance of the SwiftShortArray2D class with the specified dimensions and default value.
    /// </summary>
    /// <param name="width">The number of columns in the two-dimensional array. Must be greater than zero.</param>
    /// <param name="height">The number of rows in the two-dimensional array. Must be greater than zero.</param>
    /// <param name="defaultValue">The value to assign to each element in the array upon initialization.</param>
    public SwiftShortArray2D(int width, int height, short defaultValue) : base(width, height, defaultValue) { }

    /// <summary>
    /// Initializes a new instance of the SwiftShortArray2D class using the specified two-dimensional array of short
    /// values.
    /// </summary>
    /// <param name="source">A two-dimensional array of short values that provides the initial data for the array.</param>
    public SwiftShortArray2D(short[,] source) : base(source) { }

    /// <summary>
    /// Initializes a new instance of the SwiftShortArray2D class using the specified array state.
    /// </summary>
    /// <param name="state">The state object that provides the initial data and configuration for the two-dimensional short array. Cannot be null.</param>
    [MemoryPackConstructor]
    public SwiftShortArray2D(Array2DState<short> state) : base(state) { }

    #endregion

    #region Collection Management

    /// <summary>
    /// Scales all elements in the array by the specified factor.
    /// </summary>
    public void Scale(short factor)
    {
        for (int i = 0; i < InnerArray.Length; i++)
            InnerArray[i] = (short)(InnerArray[i] * factor);
    }

    /// <summary>
    /// Normalizes all elements in the array to a specified range.
    /// </summary>
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
