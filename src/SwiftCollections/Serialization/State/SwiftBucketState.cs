using MemoryPack;
using System;
using System.Text.Json.Serialization;

namespace SwiftCollections;

/// <summary>
/// Represents the state of a bucket in a fast allocation pool, including the items, allocation status, free indices, and peak usage count.
/// </summary>
/// <remarks>
/// This struct is typically used to capture or transfer the current state of a bucket-based memory pool.
/// It provides direct access to the underlying arrays representing the items, their allocation status, and the indices of free slots. 
/// The struct is serializable and designed for efficient state management in high-performance scenarios.
/// </remarks>
/// <typeparam name="T">The type of items stored in the bucket.</typeparam>
[Serializable]
[MemoryPackable]
public readonly partial struct SwiftBucketState<T>
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
    /// Gets the collection of indices that are currently available for allocation.
    /// </summary>
    [JsonInclude]
    [MemoryPackInclude]
    public readonly int[] FreeIndices;

    /// <summary>
    /// Gets the maximum number of peaks detected or recorded.
    /// </summary>
    [JsonInclude]
    [MemoryPackInclude]
    public readonly int PeakCount;

    /// <summary>
    /// Initializes a new instance of the SwiftBucketState class with the specified items, allocation states, free indices, and peak count.
    /// </summary>
    /// <param name="items">The array of items contained in the bucket. Cannot be null; an empty array is used if null is provided.</param>
    /// <param name="allocated">
    /// An array indicating which items in the bucket are currently allocated. 
    /// Cannot be null; an empty array is used if null is provided.
    /// </param>
    /// <param name="freeIndices">
    /// An array of indices representing the positions of free items in the bucket. 
    /// Cannot be null; an empty array is used if null is provided.
    /// </param>
    /// <param name="peakCount">The highest number of items that have been allocated in the bucket at any point.</param>
    [JsonConstructor]
    [MemoryPackConstructor]
    public SwiftBucketState(T[] items, bool[] allocated, int[] freeIndices, int peakCount)
    {
        Items = items ?? Array.Empty<T>();
        Allocated = allocated ?? Array.Empty<bool>();
        FreeIndices = freeIndices ?? Array.Empty<int>();
        PeakCount = peakCount;
    }
}
