using System.Text.Json.Serialization;

namespace SwiftCollections.Tests;

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(SwiftDenseTicketMap<int>))]
[JsonSerializable(typeof(SwiftDenseTicketMapState<int>))]
[JsonSerializable(typeof(SwiftDenseTicketMap<int?>))]
[JsonSerializable(typeof(SwiftDenseTicketMapState<int?>))]
[JsonSerializable(typeof(SwiftDenseTicketMap<string>))]
[JsonSerializable(typeof(SwiftDenseTicketMapState<string>))]
[JsonSerializable(typeof(SwiftDenseTicketMap<DenseTicketPersistencePayload>))]
[JsonSerializable(typeof(SwiftDenseTicketMapState<DenseTicketPersistencePayload>))]
[JsonSerializable(typeof(SwiftDenseTicketMap<DenseTicketPersistenceValue>))]
[JsonSerializable(typeof(SwiftDenseTicketMapState<DenseTicketPersistenceValue>))]
[JsonSerializable(typeof(DenseTicketPersistenceEnvelope))]
internal partial class SwiftDenseTicketMapJsonContext : JsonSerializerContext { }
