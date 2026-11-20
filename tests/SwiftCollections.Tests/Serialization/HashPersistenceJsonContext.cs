using System.Text.Json.Serialization;

namespace SwiftCollections.Tests;

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(HashPersistenceBox))]
[JsonSerializable(typeof(SwiftDictionaryState<PersistenceHashKey, int>))]
[JsonSerializable(typeof(SwiftDictionaryState<PersistenceHashKey, string>))]
[JsonSerializable(typeof(SwiftArrayState<PersistenceHashKey>))]
internal partial class HashPersistenceJsonContext : JsonSerializerContext { }
