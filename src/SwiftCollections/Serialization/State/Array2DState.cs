using MemoryPack;
using System;
using System.Text.Json.Serialization;

namespace SwiftCollections;

/// <summary>
/// Represents the immutable state of a two-dimensional array, including its dimensions and underlying data.
/// </summary>
/// <remarks>
/// This struct is intended for scenarios where a snapshot of a 2D array's state is needed, such as
/// serialization or state management. The data is stored in a one-dimensional array in row-major order. The struct is
/// serializable and compatible with supported serialization frameworks.
/// </remarks>
/// <typeparam name="T">The type of elements stored in the array.</typeparam>
[Serializable]
[MemoryPackable]
public readonly partial struct Array2DState<T>
{
    /// <summary>
    /// Gets the width value represented by this field.
    /// </summary>
    [JsonInclude]
    [MemoryPackInclude]
    public readonly int Width;

    /// <summary>
    /// Gets the height value represented by this field.
    /// </summary>
    [JsonInclude]
    [MemoryPackInclude]
    public readonly int Height;

    /// <summary>
    /// Gets the underlying array of data elements contained in the collection.
    /// </summary>
    [JsonInclude]
    [MemoryPackInclude]
    public readonly T[] Data;

    /// <summary>
    /// Initializes a new instance of the Array2DState class with the specified dimensions and data array.
    /// </summary>
    /// <param name="width">The number of columns in the two-dimensional array. Must be greater than zero.</param>
    /// <param name="height">The number of rows in the two-dimensional array. Must be greater than zero.</param>
    /// <param name="data">
    /// The one-dimensional array containing the elements of the two-dimensional array, stored in row-major order. 
    /// The length must be equal to width multiplied by height.
    /// </param>
    [JsonConstructor]
    [MemoryPackConstructor]
    public Array2DState(int width, int height, T[] data)
    {
        Width = width;
        Height = height;
        Data = data;
    }
}
