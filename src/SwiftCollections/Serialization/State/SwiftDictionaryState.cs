using MemoryPack;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SwiftCollections;

/// <summary>
/// Represents an immutable snapshot of the key-value pairs contained in a dictionary at a specific point in time.
/// </summary>
/// <remarks>
/// This struct is typically used to serialize or transfer the state of a dictionary. 
/// The contents are read-only and reflect the state of the dictionary when the snapshot was taken.
/// </remarks>
/// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
/// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
[Serializable]
[MemoryPackable]
public readonly partial struct SwiftDictionaryState<TKey, TValue>
{
    /// <summary>
    /// Gets the collection of key/value pairs contained in the object.
    /// </summary>
    [JsonInclude]
    [MemoryPackInclude]
    public readonly KeyValuePair<TKey, TValue>[] Items;

    /// <summary>
    /// Initializes a new instance of the SwiftDictionaryState class with the specified key-value pairs.
    /// </summary>
    /// <remarks>
    /// This constructor is typically used during deserialization to restore the state of the dictionary from a serialized array of key-value pairs.
    /// </remarks>
    /// <param name="items">An array of key-value pairs to initialize the dictionary state. Cannot be null.</param>
    [JsonConstructor]
    [MemoryPackConstructor]
    public SwiftDictionaryState(KeyValuePair<TKey, TValue>[] items)
    {
        Items = items;
    }
}
