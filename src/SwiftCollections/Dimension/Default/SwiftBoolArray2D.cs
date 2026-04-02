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

    public SwiftBoolArray2D() { }

    public SwiftBoolArray2D(int width, int height) : base(width, height) { }

    public SwiftBoolArray2D(int width, int height, bool defaultValue) : base(width, height, defaultValue) { }

    public SwiftBoolArray2D(bool[,] source) : base(source) { }

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
