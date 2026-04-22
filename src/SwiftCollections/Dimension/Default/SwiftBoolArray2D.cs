using MemoryPack;
using System;
using System.Text.Json.Serialization;

namespace SwiftCollections.Dimensions;

/// <summary>
/// Represents a 2D array specifically designed to handle boolean values.
/// </summary>
[Serializable]
[JsonConverter(typeof(SwiftStateJsonConverterFactory))]
[MemoryPackable]
public partial class SwiftBoolArray2D : SwiftArray2D<bool>
{
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the SwiftBoolArray2D class.
    /// </summary>
    public SwiftBoolArray2D() { }

    /// <summary>
    /// Initializes a new instance of the SwiftBoolArray2D class with the specified dimensions.
    /// </summary>
    /// <param name="width">The number of columns in the two-dimensional array. Must be greater than zero.</param>
    /// <param name="height">The number of rows in the two-dimensional array. Must be greater than zero.</param>
    public SwiftBoolArray2D(int width, int height) : base(width, height) { }

    /// <summary>
    /// Initializes a new instance of the SwiftBoolArray2D class with the specified dimensions and default value.
    /// </summary>
    /// <param name="width">The number of columns in the two-dimensional array. Must be greater than zero.</param>
    /// <param name="height">The number of rows in the two-dimensional array. Must be greater than zero.</param>
    /// <param name="defaultValue">The initial boolean value assigned to each element in the array.</param>
    public SwiftBoolArray2D(int width, int height, bool defaultValue) : base(width, height, defaultValue) { }

    /// <summary>
    /// Initializes a new instance of the SwiftBoolArray2D class using the specified two-dimensional Boolean array as the source data.
    /// </summary>
    /// <param name="source">A two-dimensional array of Boolean values to initialize the array with. Cannot be null.</param>
    public SwiftBoolArray2D(bool[,] source) : base(source) { }

    /// <summary>
    /// Initializes a new instance of the SwiftBoolArray2D class using the specified array state.
    /// </summary>
    /// <param name="state">The state object containing the two-dimensional array data and configuration to initialize the array.</param>
    [MemoryPackConstructor]
    public SwiftBoolArray2D(Array2DState<bool> state) : base(state) { }

    #endregion

    #region Collection Management

    /// <summary>
    /// Toggles the value at the specified position in the array.
    /// </summary>
    public void Toggle(int x, int y)
    {
        this[x, y] = !this[x, y];
    }

    /// <summary>
    /// Sets all values within a rectangular region to the specified value.
    /// </summary>
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
