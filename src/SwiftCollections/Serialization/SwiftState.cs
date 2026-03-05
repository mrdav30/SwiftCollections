using MemoryPack;
using System;
using System.Collections.Generic;

#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#endif
#if !NET8_0_OR_GREATER
using System.Text.Json.Serialization.Shim;
#endif

namespace SwiftCollections;

[Serializable]
[MemoryPackable]
public readonly partial struct SwiftArrayState<T>
{
    [JsonInclude]
    [MemoryPackInclude]
    public readonly T[] Items;

    [JsonConstructor]
    [MemoryPackConstructor]
    public SwiftArrayState(T[] items)
    {
        Items = items;
    }
}

[Serializable]
[MemoryPackable]
public readonly partial struct SwiftDictionaryState<TKey, TValue>
{
    [JsonInclude]
    [MemoryPackInclude]
    public readonly KeyValuePair<TKey, TValue>[] Items;

    [JsonConstructor]
    [MemoryPackConstructor]
    public SwiftDictionaryState(KeyValuePair<TKey, TValue>[] items)
    {
        Items = items;
    }
}

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
        Items = items;
        Allocated = allocated;
        FreeIndices = freeIndices;
        PeakCount = peakCount;
    }
}

[Serializable]
[MemoryPackable]
public readonly partial struct Array2DState<T>
{
    [JsonInclude]
    [MemoryPackInclude]
    public readonly int Width;

    [JsonInclude]
    [MemoryPackInclude]
    public readonly int Height;

    [JsonInclude]
    [MemoryPackInclude]
    public readonly T[] Data;

    [JsonConstructor]
    [MemoryPackConstructor]
    public Array2DState(int width, int height, T[] data)
    {
        Width = width;
        Height = height;
        Data = data;
    }
}

[Serializable]
[MemoryPackable]
public readonly partial struct Array3DState<T>
{
    [JsonInclude]
    [MemoryPackInclude]
    public readonly int Width;

    [JsonInclude]
    [MemoryPackInclude]
    public readonly int Height;

    [JsonInclude]
    [MemoryPackInclude]
    public readonly int Depth;

    [JsonInclude]
    [MemoryPackInclude]
    public readonly T[] Data;

    [JsonConstructor]
    [MemoryPackConstructor]
    public Array3DState(int width, int height, int depth, T[] data)
    {
        Width = width;
        Height = height;
        Depth = depth;
        Data = data;
    }
}
