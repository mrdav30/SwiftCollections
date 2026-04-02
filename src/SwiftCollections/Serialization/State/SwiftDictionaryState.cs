using MemoryPack;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SwiftCollections;

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
