using System.Text.Json.Serialization;

namespace SwiftCollections.Tests;

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(SwiftTicketStore<int>))]
[JsonSerializable(typeof(SwiftTicketStoreState<int>))]
[JsonSerializable(typeof(SwiftTicketStore<int?>))]
[JsonSerializable(typeof(SwiftTicketStoreState<int?>))]
[JsonSerializable(typeof(SwiftTicketStore<string>))]
[JsonSerializable(typeof(SwiftTicketStoreState<string>))]
[JsonSerializable(typeof(SwiftTicketStore<DenseTicketPersistencePayload>))]
[JsonSerializable(typeof(SwiftTicketStoreState<DenseTicketPersistencePayload>))]
[JsonSerializable(typeof(SwiftTicketStore<DenseTicketPersistenceValue>))]
[JsonSerializable(typeof(SwiftTicketStoreState<DenseTicketPersistenceValue>))]
[JsonSerializable(typeof(SwiftDenseTicketMap<int>))]
[JsonSerializable(typeof(SwiftDenseTicketMapState<int>))]
[JsonSerializable(typeof(TicketStorePersistenceEnvelope))]
[JsonSerializable(typeof(TicketStorePersistenceWorld))]
internal partial class SwiftTicketStoreJsonContext : JsonSerializerContext { }
