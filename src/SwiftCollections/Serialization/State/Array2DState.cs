//=======================================================================
// Array2DState.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Text.Json.Serialization;
using MemoryPack;

namespace SwiftCollections;

/// <summary>
/// Stores the dimensions and flattened cell data for a 2D array.
/// </summary>
/// <typeparam name="T">The type of elements stored in the array.</typeparam>
/// <remarks>
/// The readonly struct does not make its Data array immutable. Construction retains the array and performs no
/// validation. Layout is x * Height + y. Collection State getters allocate snapshots; directly constructing this
/// state does not copy data.
/// </remarks>
[Serializable]
[MemoryPackable]
public readonly partial struct Array2DState<T>
{
    /// <summary>
    /// Stores the width in cells, without non-negative or product validation.
    /// </summary>

    [JsonInclude]
    [MemoryPackInclude]
    public readonly int Width;

    /// <summary>
    /// Stores the height in cells, without non-negative or product validation.
    /// </summary>

    [JsonInclude]
    [MemoryPackInclude]
    public readonly int Height;

    /// <summary>
    /// Stores retained flattened data; the array is mutable and default state contains null.
    /// </summary>
    [JsonInclude]
    [MemoryPackInclude]
    public readonly T[] Data;

    /// <summary>
    /// Creates a state value by retaining the supplied arrays and metadata.
    /// </summary>
    /// <param name="width">The recorded width; stored without validation.</param>
    /// <param name="height">The recorded height; stored without validation.</param>
    /// <param name="data">The flattened data, retained by reference without validation or copying. Use a non-null
    /// array whose length equals the non-negative dimension product.</param>
    [JsonConstructor]
    [MemoryPackConstructor]
    public Array2DState(int width, int height, T[] data)
    {
        Width = width;
        Height = height;
        Data = data;
    }
}
