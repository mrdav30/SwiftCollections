using MemoryPack;
using System;
using System.Text.Json.Serialization;

namespace SwiftCollections;

/// <summary>
/// Represents the immutable state of a generational bucket, including item storage, allocation status, generation tracking, and free index management.
/// </summary>
/// <remarks>
/// This struct is typically used to capture or transfer the complete state of a generational bucket for serialization, persistence, or inspection purposes. 
/// All arrays are expected to be of the same length, and the struct is designed for efficient value-type usage. 
/// The state is read-only and does not provide mutation operations.</remarks>
/// <typeparam name="T">The type of elements stored in the bucket.</typeparam>
[Serializable]
[MemoryPackable]
public readonly partial struct SwiftGenerationalBucketState<T>
{
    /// <summary>
    /// Gets the array of items contained in the collection.
    /// </summary>
    [JsonInclude]
    [MemoryPackInclude]
    public readonly T[] Items;

    /// <summary>
    /// Indicates which items in the collection are currently allocated.
    /// </summary>
    [JsonInclude]
    [MemoryPackInclude]
    public readonly bool[] Allocated;

    /// <summary>
    /// Gets the collection of generation values associated with this instance.
    /// </summary>
    [JsonInclude]
    [MemoryPackInclude]
    public readonly uint[] Generations;

    /// <summary>
    /// Gets the collection of indices that are currently available for allocation.
    /// </summary>
    [JsonInclude]
    [MemoryPackInclude]
    public readonly int[] FreeIndices;

    /// <summary>
    /// Gets the peak value recorded during the measurement period.
    /// </summary>
    [JsonInclude]
    [MemoryPackInclude]
    public readonly int Peak;

    /// <summary>
    /// Initializes a new instance of the SwiftGenerationalBucketState class with the specified items, allocation states, 
    /// generation counters, free indices, and peak index.
    /// </summary>
    /// <param name="items">The array of items to be managed by the bucket. Cannot be null; an empty array is used if null is provided.</param>
    /// <param name="allocated">
    /// An array indicating which slots in the bucket are currently allocated.
    /// Each element corresponds to an item in the items array. 
    /// Cannot be null; an empty array is used if null is provided.
    /// </param>
    /// <param name="generations">
    /// An array of generation counters for each slot in the bucket. 
    /// Used to track the version or state of each item.
    /// Cannot be null; an empty array is used if null is provided.
    /// </param>
    /// <param name="freeIndices">
    /// An array of indices representing the free slots available for allocation. 
    /// Cannot be null; an empty array is used if null is provided.
    /// </param>
    /// <param name="peak">The highest index that has been allocated in the bucket. Must be greater than or equal to zero.</param>
    [JsonConstructor]
    [MemoryPackConstructor]
    public SwiftGenerationalBucketState(
        T[] items,
        bool[] allocated,
        uint[] generations,
        int[] freeIndices,
        int peak)
    {
        Items = items ?? Array.Empty<T>();
        Allocated = allocated ?? Array.Empty<bool>();
        Generations = generations ?? Array.Empty<uint>();
        FreeIndices = freeIndices ?? Array.Empty<int>();
        Peak = peak;
    }
}
