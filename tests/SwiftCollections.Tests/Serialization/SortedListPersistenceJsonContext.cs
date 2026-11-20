using System.Text.Json.Serialization;

namespace SwiftCollections.Tests;

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(SortedListPersistenceBox))]
[JsonSerializable(typeof(SwiftArrayState<PersistenceToken>))]
internal partial class SortedListPersistenceJsonContext : JsonSerializerContext { }
