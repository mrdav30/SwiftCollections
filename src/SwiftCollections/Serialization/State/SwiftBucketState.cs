using MemoryPack;
using System;
using System.Text.Json.Serialization;

namespace SwiftCollections;

[Serializable]
[MemoryPackable]
public readonly partial struct SwiftBucketState<T>
{
    [JsonInclude]
    [MemoryPackInclude]
    public readonly T[] Items;

    [JsonInclude]
    [MemoryPackInclude]
    public readonly bool[] Allocated;

    [JsonInclude]
    [MemoryPackInclude]
    public readonly int[] FreeIndices;

    [JsonInclude]
    [MemoryPackInclude]
    public readonly int PeakCount;

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
