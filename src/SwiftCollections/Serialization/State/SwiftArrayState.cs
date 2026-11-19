//=======================================================================
// SwiftArrayState.cs
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
/// <typeparam name="T">The type of elements contained in the array.</typeparam>
/// <remarks>
/// Readonly fields retain mutable arrays; this struct is not deeply immutable. Constructors retain arrays without
/// copying or validation. default(TState) bypasses constructors and leaves array fields null. Collection State
/// getters allocate shallow snapshots; restore behavior and validation belong to each collection.
/// This state contains no comparer. Sorted-list items must satisfy the restoring owner's ordering contract;
/// set uniqueness is interpreted using that owner's equality rules.
/// </remarks>
[Serializable]
[MemoryPackable]
public readonly partial struct SwiftArrayState<T>
{
    /// <summary>
    /// Stores retained element data; array contents remain mutable and default state may contain null.
    /// </summary>
    [JsonInclude]
    [MemoryPackInclude]
    public readonly T[] Items;

    /// <summary>
    /// Creates a state value by retaining the supplied arrays and metadata.
    /// </summary>
    /// <param name="items">The items array, retained without copying. Null is stored as supplied.</param>
    /// <remarks>
    /// Performs no consistency checks and allocates no snapshot copy. Mutating a supplied array mutates this
    /// state.
    /// </remarks>
    [JsonConstructor]
    [MemoryPackConstructor]
    public SwiftArrayState(T[] items)
    {
        Items = items;
    }
}
