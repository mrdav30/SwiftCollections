using MemoryPack;
using System;
using System.Text.Json.Serialization;

namespace SwiftCollections;

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
