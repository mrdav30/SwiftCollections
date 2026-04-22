using MemoryPack;
using System;
using System.Text.Json.Serialization;

namespace SwiftCollections;

/// <summary>
/// Represents an immutable snapshot of an array of items of the specified type.
/// </summary>
/// <remarks>
/// This struct is intended for scenarios where a value-type representation of an array is needed, such as serialization or state management. 
/// The array referenced by the Items field should not be modified after construction to preserve immutability.
/// </remarks>
/// <typeparam name="T">The type of elements contained in the array.</typeparam>
[Serializable]
[MemoryPackable]
public readonly partial struct SwiftArrayState<T>
{
    /// <summary>
    /// Gets the array of items contained in the collection.
    /// </summary>
    [JsonInclude]
    [MemoryPackInclude]
    public readonly T[] Items;

    /// <summary>
    /// Initializes a new instance of the SwiftArrayState class with the specified items.
    /// </summary>
    /// <param name="items">The array of items to initialize the state with. Cannot be null.</param>
    [JsonConstructor]
    [MemoryPackConstructor]
    public SwiftArrayState(T[] items)
    {
        Items = items;
    }
}
