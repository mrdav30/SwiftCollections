//=======================================================================
// SwiftTicketStoreState.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Text.Json.Serialization;
using MemoryPack;

namespace SwiftCollections;

/// <summary>Stores the live values and exact external tickets of a dense ticket store.</summary>
/// <typeparam name="T">The payload type.</typeparam>
/// <remarks>Readonly fields retain mutable arrays without validation or copying. Constructor nulls normalize
/// to empty; default construction leaves null fields. The owner validates and copies arrays on reconstruction.
/// Referenced payload objects remain shared. No issuer, liveness, sparse table, capacity or generation history
/// is stored. Do not mutate arrays concurrently with owner reconstruction.</remarks>
[Serializable]
[MemoryPackable]
public readonly partial struct SwiftTicketStoreState<T>
{
    /// <summary>Stores live payloads in exact dense order, parallel to the ticket arrays.</summary>
    [JsonInclude, MemoryPackInclude]
    public readonly T[] DenseValues;

    /// <summary>Stores the unique external indices parallel to DenseValues.</summary>
    [JsonInclude, MemoryPackInclude]
    public readonly int[] DenseSlots;

    /// <summary>Stores exact external generations, including zero and UInt32.MaxValue, parallel to DenseValues.</summary>
    [JsonInclude, MemoryPackInclude]
    public readonly uint[] DenseGenerations;

    /// <summary>Creates a raw state by retaining arrays and normalizing nulls without structural validation.</summary>
    /// <param name="denseValues">Payloads in dense order, retained without copying.</param>
    /// <param name="denseSlots">Parallel external indices, retained without copying.</param>
    /// <param name="denseGenerations">Parallel generation values, retained without copying or interpretation.</param>
    [JsonConstructor, MemoryPackConstructor]
    public SwiftTicketStoreState(T[]? denseValues, int[]? denseSlots, uint[]? denseGenerations)
    {
        DenseValues = denseValues ?? Array.Empty<T>();
        DenseSlots = denseSlots ?? Array.Empty<int>();
        DenseGenerations = denseGenerations ?? Array.Empty<uint>();
    }
}
