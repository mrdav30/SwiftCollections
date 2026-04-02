using MemoryPack;
using System;
using System.Text.Json.Serialization;

namespace SwiftCollections;

[Serializable]
[MemoryPackable]
public readonly partial struct SwiftGenerationalBucketState<T>
{
    [JsonInclude]
    [MemoryPackInclude]
    public readonly T[] Items;

    [JsonInclude]
    [MemoryPackInclude]
    public readonly bool[] Allocated;

    [JsonInclude]
    [MemoryPackInclude]
    public readonly uint[] Generations;

    [JsonInclude]
    [MemoryPackInclude]
    public readonly int[] FreeIndices;

    [JsonInclude]
    [MemoryPackInclude]
    public readonly int Peak;

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
