//=======================================================================
// SwiftSlotMapState.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Text.Json.Serialization;
using MemoryPack;

namespace SwiftCollections;

/// <summary>
/// Stores explicit serialized state for the owning collection.
/// </summary>
/// <typeparam name="T">The type of items stored in the map.</typeparam>
/// <remarks>
/// Readonly fields retain mutable arrays; this struct is not deeply immutable. Constructors retain arrays without
/// copying, normalizing null array arguments to empty arrays. default(TState) bypasses constructors and leaves
/// array fields null. Collection State getters allocate shallow snapshots; restore behavior and validation belong
/// to each collection.
/// </remarks>
[Serializable]
[MemoryPackable]
public readonly partial struct SwiftSlotMapState<T>
{
    /// <summary>
    /// Stores retained element data; array contents remain mutable and default state may contain null.
    /// </summary>
    [JsonInclude]
    [MemoryPackInclude]
    public readonly T[] Items;

    /// <summary>
    /// Stores per-slot allocation flags; absent flags are treated as false during restore.
    /// </summary>
    [JsonInclude]
    [MemoryPackInclude]
    public readonly bool[] Allocated;

    /// <summary>
    /// Stores the free-slot stack in bottom-to-top order; arrays may differ in length from item storage. Entries
    /// must be unique and disjoint from allocated slots.
    /// </summary>
    [JsonInclude]
    [MemoryPackInclude]
    public readonly int[] FreeIndices;

    /// <summary>
    /// Stores the exclusive high-water slot boundary, not the historical maximum live Count. Restore normalizes
    /// it into capacity.
    /// </summary>
    [JsonInclude]
    [MemoryPackInclude]
    public readonly int PeakCount;

    /// <summary>
    /// Creates a state value by retaining the supplied arrays and metadata.
    /// </summary>
    /// <param name="items">The items array, retained without copying. Null is normalized to an empty array;
    /// contents are not validated.</param>
    /// <param name="allocated">The allocated array, retained without copying. Null is normalized to an empty
    /// array; contents are not validated.</param>
    /// <param name="freeIndices">The freeIndices array, retained without copying. Null is normalized to an empty
    /// array; contents are not validated.</param>
    /// <param name="peakCount">The exclusive slot high-water boundary, stored without validation.</param>
    /// <remarks>
    /// Performs no consistency checks and allocates no snapshot copy. Mutating a supplied array mutates this
    /// state.
    /// </remarks>
    [JsonConstructor]
    [MemoryPackConstructor]
    public SwiftSlotMapState(T[] items, bool[] allocated, int[] freeIndices, int peakCount)
    {
        Items = items ?? Array.Empty<T>();
        Allocated = allocated ?? Array.Empty<bool>();
        FreeIndices = freeIndices ?? Array.Empty<int>();
        PeakCount = peakCount;
    }
}
