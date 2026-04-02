using MemoryPack;
using System;
using System.Text.Json.Serialization;

namespace SwiftCollections;

[Serializable]
[MemoryPackable]
public readonly partial struct SwiftSparseSetState<T>
{
    [JsonInclude]
    [MemoryPackInclude]
    public readonly int[] DenseKeys;

    [JsonInclude]
    [MemoryPackInclude]
    public readonly T[] DenseValues;

    [JsonConstructor]
    [MemoryPackConstructor]
    public SwiftSparseSetState(int[] denseKeys, T[] denseValues)
    {
        DenseKeys = denseKeys ?? Array.Empty<int>();
        DenseValues = denseValues ?? Array.Empty<T>();
    }
}
