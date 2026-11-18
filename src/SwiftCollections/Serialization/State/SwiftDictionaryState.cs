//=======================================================================
// SwiftDictionaryState.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using MemoryPack;

namespace SwiftCollections;

/// <summary>
/// Stores explicit serialized state for the owning collection.
/// </summary>
/// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
/// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
/// <remarks>
/// Readonly fields retain mutable arrays; this struct is not deeply immutable. Constructors retain arrays without
/// copying or validation. default(TState) bypasses constructors and leaves array fields null. Collection State
/// getters allocate shallow snapshots; restore behavior and validation belong to each collection.
/// </remarks>
[Serializable]
[MemoryPackable]
public readonly partial struct SwiftDictionaryState<TKey, TValue>
{
    /// <summary>
    /// Stores retained element data; array contents remain mutable and default state may contain null.
    /// </summary>
    [JsonInclude]
    [MemoryPackInclude]
    public readonly KeyValuePair<TKey, TValue>[] Items;

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
    public SwiftDictionaryState(KeyValuePair<TKey, TValue>[] items)
    {
        Items = items;
    }
}
