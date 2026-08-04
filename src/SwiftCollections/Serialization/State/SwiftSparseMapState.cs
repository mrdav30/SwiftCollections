//=======================================================================
// SwiftSparseMapState.cs
//=======================================================================
// MIT License, Copyright (c) 2024–present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Text.Json.Serialization;
using MemoryPack;

namespace SwiftCollections;

/// <summary>
/// Represents the immutable state of a sparse map, containing the dense keys and associated values.
/// </summary>
/// <remarks>
/// This structure is typically used to serialize or inspect the contents of a sparse map. 
/// The arrays are guaranteed to be non-null, but may be empty if the map contains no elements.
/// </remarks>
/// <typeparam name="T">The type of values stored in the sparse map.</typeparam>
[Serializable]
[MemoryPackable]
public readonly partial struct SwiftSparseMapState<T>
{
    /// <summary>
    /// Gets the collection of dense keys associated with this instance.
    /// </summary>
    [JsonInclude]
    [MemoryPackInclude]
    public readonly int[] DenseKeys;

    /// <summary>
    /// Gets the array containing the dense values for this instance.
    /// </summary>
    [JsonInclude]
    [MemoryPackInclude]
    public readonly T[] DenseValues;

    /// <summary>
    /// Initializes a new instance of the SwiftSparseMapState class with the specified dense keys and values.
    /// </summary>
    /// <param name="denseKeys">
    /// An array of integers representing the dense keys to initialize the map with. 
    /// If null, an empty array is used.
    /// </param>
    /// <param name="denseValues">
    /// An array of values of type T corresponding to the dense keys. 
    /// If null, an empty array is used.
    /// </param>
    [JsonConstructor]
    [MemoryPackConstructor]
    public SwiftSparseMapState(int[] denseKeys, T[] denseValues)
    {
        DenseKeys = denseKeys ?? Array.Empty<int>();
        DenseValues = denseValues ?? Array.Empty<T>();
    }
}
