//=======================================================================
// SwiftSparseMapState.cs
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
/// <typeparam name="T">The type of values stored in the sparse map.</typeparam>
/// <remarks>
/// Readonly fields retain mutable arrays; this struct is not deeply immutable. Constructors retain arrays without
/// copying, normalizing null array arguments to empty arrays. default(TState) bypasses constructors and leaves
/// array fields null. Collection State getters allocate shallow snapshots; restore behavior and validation belong
/// to each collection.
/// </remarks>
[Serializable]
[MemoryPackable]
public readonly partial struct SwiftSparseMapState<T>
{
    /// <summary>
    /// Stores retained IDs in dense order; valid restored IDs must be distinct, non-negative and below
    /// Int32.MaxValue.
    /// </summary>
    [JsonInclude]
    [MemoryPackInclude]
    public readonly int[] DenseKeys;

    /// <summary>
    /// Stores retained values parallel to DenseKeys; restored array lengths must agree.
    /// </summary>
    [JsonInclude]
    [MemoryPackInclude]
    public readonly T[] DenseValues;

    /// <summary>
    /// Creates a state value by retaining the supplied arrays and metadata.
    /// </summary>
    /// <param name="denseKeys">The denseKeys array, retained without copying. Null is normalized to an empty
    /// array; contents are not validated.</param>
    /// <param name="denseValues">The denseValues array, retained without copying. Null is normalized to an empty
    /// array; contents are not validated.</param>
    /// <remarks>
    /// Performs no consistency checks and allocates no snapshot copy. Mutating a supplied array mutates this
    /// state.
    /// </remarks>
    [JsonConstructor]
    [MemoryPackConstructor]
    public SwiftSparseMapState(int[] denseKeys, T[] denseValues)
    {
        DenseKeys = denseKeys ?? Array.Empty<int>();
        DenseValues = denseValues ?? Array.Empty<T>();
    }
}
