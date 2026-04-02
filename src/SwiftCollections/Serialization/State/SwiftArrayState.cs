using MemoryPack;
using System;
using System.Text.Json.Serialization;

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
