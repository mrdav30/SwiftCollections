using MemoryPack;
using System;
using System.Text.Json.Serialization;

namespace SwiftCollections;

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
