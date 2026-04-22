using MemoryPack;
using System;
using System.Text.Json.Serialization;

namespace SwiftCollections;


/// <summary>
/// Represents the immutable state of a three-dimensional array, including its dimensions and underlying data.
/// </summary>
/// <remarks>
/// This struct is intended for scenarios where a snapshot of a 3D array's state is needed, such as
/// serialization or state management. The data is stored in a one-dimensional array in row-major order. The struct is
/// serializable and compatible with supported serialization frameworks.
/// </remarks>
/// <typeparam name="T">The type of elements stored in the array.</typeparam>
[Serializable]
[MemoryPackable]
public readonly partial struct Array3DState<T>
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
    /// Gets the depth value represented by this field.
    /// </summary>
    [JsonInclude]
    [MemoryPackInclude]
    public readonly int Depth;

    /// <summary>
    /// Gets the underlying array of data elements contained in the collection.
    /// </summary>
    [JsonInclude]
    [MemoryPackInclude]
    public readonly T[] Data;

    /// <summary>
    /// Initializes a new instance of the Array3DState class with the specified dimensions and data.
    /// </summary>
    /// <param name="width">The number of elements along the X-axis. Must be greater than zero.</param>
    /// <param name="height">The number of elements along the Y-axis. Must be greater than zero.</param>
    /// <param name="depth">The number of elements along the Z-axis. Must be greater than zero.</param>
    /// <param name="data">
    /// A one-dimensional array containing the elements of the 3D array, stored in row-major order. 
    /// The length must equal width × height × depth.
    /// </param>
    [JsonConstructor]
    [MemoryPackConstructor]
    public Array3DState(int width, int height, int depth, T[] data)
    {
        Width = width;
        Height = height;
        Depth = depth;
        Data = data;
    }
}
