#if NET8_0_OR_GREATER

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SwiftCollections;

public sealed class SwiftStateJsonConverter<TCollection, TState>
    : JsonConverter<TCollection>
    where TCollection : class
{
    private readonly Func<TState, TCollection> _factory;
    private readonly Func<TCollection, TState> _stateGetter;

    public SwiftStateJsonConverter(
        Func<TState, TCollection> factory,
        Func<TCollection, TState> stateGetter)
    {
        _factory = factory;
        _stateGetter = stateGetter;
    }

    public override TCollection Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException();

        reader.Read();

        if (reader.TokenType != JsonTokenType.PropertyName)
            throw new JsonException();

        if (!reader.ValueTextEquals("State"))
            throw new JsonException();

        reader.Read();

        var state = JsonSerializer.Deserialize<TState>(ref reader, options);

        reader.Read(); // EndObject

        return _factory(state);
    }

    public override void Write(Utf8JsonWriter writer, TCollection value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        var state = _stateGetter(value);

        writer.WritePropertyName("State");
        JsonSerializer.Serialize(writer, state, options);

        writer.WriteEndObject();
    }
}

#endif