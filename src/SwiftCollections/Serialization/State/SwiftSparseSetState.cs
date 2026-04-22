using MemoryPack;
using System;
using System.Text.Json.Serialization;

namespace SwiftCollections;

/// <summary>
/// Represents the immutable state of a sparse set, containing the dense keys and associated values.
/// </summary>
/// <remarks>
/// This structure is typically used to serialize or inspect the contents of a sparse set. 
/// The arrays are guaranteed to be non-null, but may be empty if the set contains no elements.
/// </remarks>
/// <typeparam name="T">The type of values stored in the sparse set.</typeparam>
[Serializable]
[MemoryPackable]
public readonly partial struct SwiftSparseSetState<T>
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
    /// Initializes a new instance of the SwiftSparseSetState class with the specified dense keys and values.
    /// </summary>
    /// <param name="denseKeys">
    /// An array of integers representing the dense keys to initialize the set with. 
    /// If null, an empty array is used.
    /// </param>
    /// <param name="denseValues">
    /// An array of values of type T corresponding to the dense keys. 
    /// If null, an empty array is used.
    /// </param>
    [JsonConstructor]
    [MemoryPackConstructor]
    public SwiftSparseSetState(int[] denseKeys, T[] denseValues)
    {
        DenseKeys = denseKeys ?? Array.Empty<int>();
        DenseValues = denseValues ?? Array.Empty<T>();
    }
}
