//=======================================================================
// SwiftHashCollectionJsonConverter.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SwiftCollections.Serialization;

// Callbacks run once per persistence boundary, never during collection lookup or population.
internal sealed class SwiftHashCollectionJsonConverter<TCollection, TState, TComparers> : JsonConverter<TCollection>
    where TCollection : class
{
    private readonly SwiftComparerRegistry _registry;
    private readonly byte[] _roles;
    private readonly Func<TCollection, SwiftComparerRegistry, (SwiftComparerDescriptor First, SwiftComparerDescriptor Second, SwiftComparerDescriptor Third)> _capture;
    private readonly Func<TCollection, TState> _snapshot;
    private readonly Func<(SwiftComparerDescriptor First, SwiftComparerDescriptor Second, SwiftComparerDescriptor Third), SwiftComparerRegistry, TComparers> _resolve;
    private readonly Func<TState, TComparers, TCollection> _restore;

    internal SwiftHashCollectionJsonConverter(SwiftComparerRegistry registry, byte[] roles,
        Func<TCollection, SwiftComparerRegistry, (SwiftComparerDescriptor First, SwiftComparerDescriptor Second, SwiftComparerDescriptor Third)> capture,
        Func<TCollection, TState> snapshot,
        Func<(SwiftComparerDescriptor First, SwiftComparerDescriptor Second, SwiftComparerDescriptor Third), SwiftComparerRegistry, TComparers> resolve,
        Func<TState, TComparers, TCollection> restore)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _roles = roles;
        _capture = capture;
        _snapshot = snapshot;
        _resolve = resolve;
        _restore = restore;
    }

    public override TCollection? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        SwiftComparerJsonMetadata.ReadStart(ref reader);
        SwiftComparerDescriptor first = SwiftComparerJsonMetadata.ReadDescriptor(ref reader, _roles[0]);
        SwiftComparerDescriptor second = default, third = default;
        if (_roles.Length == 3)
        {
            second = SwiftComparerJsonMetadata.ReadDescriptor(ref reader, _roles[1]);
            third = SwiftComparerJsonMetadata.ReadDescriptor(ref reader, _roles[2]);
        }
        SwiftComparerJsonMetadata.ReadToken(ref reader, JsonTokenType.EndArray);
        try
        {
            TComparers comparers = _resolve((first, second, third), _registry);
            SwiftComparerJsonMetadata.ReadProperty(ref reader, "State");
            TState state = JsonSerializer.Deserialize<TState>(ref reader, options)!;
            SwiftComparerJsonMetadata.ReadToken(ref reader, JsonTokenType.EndObject);
            return _restore(state, comparers);
        }
        catch (Exception exception) when (exception is not JsonException)
        {
            throw new JsonException("The collection comparison rule or item state could not be restored.", exception);
        }
    }

    public override void Write(Utf8JsonWriter writer, TCollection value, JsonSerializerOptions options)
    {
        if (value == null) { writer.WriteNullValue(); return; }
        var descriptors = _capture(value, _registry);
        TState state = _snapshot(value);
        SwiftComparerJsonMetadata.WriteStart(writer);
        SwiftComparerJsonMetadata.WriteDescriptor(writer, _roles[0], descriptors.First);
        if (_roles.Length == 3)
        {
            SwiftComparerJsonMetadata.WriteDescriptor(writer, _roles[1], descriptors.Second);
            SwiftComparerJsonMetadata.WriteDescriptor(writer, _roles[2], descriptors.Third);
        }
        writer.WriteEndArray();
        writer.WritePropertyName("State");
        JsonSerializer.Serialize(writer, state, options);
        writer.WriteEndObject();
    }
}
