//=======================================================================
// SwiftDenseTicketMapState.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Text.Json.Serialization;
using MemoryPack;

namespace SwiftCollections;

/// <summary>
/// Stores dense values, slot identity history and deterministic reuse order for a dense ticket map.
/// </summary>
/// <typeparam name="T">The type of values stored in the map.</typeparam>
/// <remarks>
/// Readonly fields retain mutable arrays without copying or validation. Constructor null arrays become empty;
/// default construction bypasses that normalization and leaves fields null. The owner validates and copies arrays
/// when restoring, and its State getter returns independent shallow array snapshots. Referenced payload objects
/// remain shared through raw snapshots and restores. Do not mutate input arrays concurrently with restoration.
/// </remarks>
[Serializable]
[MemoryPackable]
public readonly partial struct SwiftDenseTicketMapState<T>
{
    /// <summary>
    /// Stores live values in exact dense order, parallel to DenseSlots.
    /// </summary>
    [JsonInclude]
    [MemoryPackInclude]
    public readonly T[] DenseValues;

    /// <summary>
    /// Stores the unique live slot IDs parallel to DenseValues; each ID is below SlotGenerations.Length.
    /// </summary>
    [JsonInclude]
    [MemoryPackInclude]
    public readonly int[] DenseSlots;

    /// <summary>
    /// Stores every historically issued slot generation; length is the issued boundary and zero marks retirement.
    /// </summary>
    [JsonInclude]
    [MemoryPackInclude]
    public readonly uint[] SlotGenerations;

    /// <summary>
    /// Stores reusable slot IDs in bottom-to-top order; the final ID is reused first.
    /// </summary>
    /// <remarks>
    /// IDs must be unique, disjoint from live slots and have nonzero generations. Every nonzero issued generation
    /// belongs to exactly one live or free slot. Retired slots appear in neither list.
    /// </remarks>
    [JsonInclude]
    [MemoryPackInclude]
    public readonly int[] FreeSlots;

    /// <summary>
    /// Creates a raw state by retaining its supplied arrays and normalizing null arrays to empty.
    /// </summary>
    /// <param name="denseValues">Live values in dense order, retained without cloning or structural validation.</param>
    /// <param name="denseSlots">Parallel live slot IDs, retained without copying or validation.</param>
    /// <param name="slotGenerations">Issued-slot generation history, retained without copying or validation.</param>
    /// <param name="freeSlots">Free IDs in bottom-to-top order, retained without copying or validation.</param>
    /// <remarks>
    /// Null arguments become empty arrays. Mutating a supplied array changes this state; the owning map's
    /// constructor, rather than this DTO, validates structural consistency and creates owned storage.
    /// </remarks>
    [JsonConstructor]
    [MemoryPackConstructor]
    public SwiftDenseTicketMapState(T[]? denseValues, int[]? denseSlots, uint[]? slotGenerations, int[]? freeSlots)
    {
        DenseValues = denseValues ?? Array.Empty<T>();
        DenseSlots = denseSlots ?? Array.Empty<int>();
        SlotGenerations = slotGenerations ?? Array.Empty<uint>();
        FreeSlots = freeSlots ?? Array.Empty<int>();
    }
}
