using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SwiftCollections;

/// <summary>
/// Provides a custom JSON converter for serializing and deserializing a collection type using a state-based approach.
/// </summary>
/// <remarks>
/// This converter serializes the collection as a JSON object containing a single 'State' property, which
/// holds the serialized state. 
/// During deserialization, the collection is reconstructed from the state using the provided factory function. 
/// This approach is useful for types where the full collection can be represented and restored from a state object.</remarks>
/// <typeparam name="TCollection">The collection type to convert. Must be a reference type.</typeparam>
/// <typeparam name="TState">The type representing the serializable state of the collection.</typeparam>
public sealed class SwiftStateJsonConverter<TCollection, TState> : JsonConverter<TCollection> where TCollection : class
{
    private readonly Func<TState, TCollection> _factory;
    private readonly Func<TCollection, TState> _stateGetter;

    /// <summary>
    /// Initializes a new instance of the SwiftStateJsonConverter class with the specified factory and state getter functions.
    /// </summary>
    /// <remarks>
    /// Both factory and stateGetter are required to enable conversion between state 
    /// and collection representations during JSON serialization and deserialization.
    /// </remarks>
    /// <param name="factory">A function that creates a collection of type TCollection from a state of type TState. Cannot be null.</param>
    /// <param name="stateGetter">A function that creates a state of type TState from a collection of type TCollection. Cannot be null.</param>
    public SwiftStateJsonConverter(
        Func<TState, TCollection> factory,
        Func<TCollection, TState> stateGetter)
    {
        _factory = factory;
        _stateGetter = stateGetter;
    }

    /// <summary>
    /// Reads and converts a JSON object into an instance of the collection type represented by <typeparamref name="TCollection"/>.
    /// </summary>
    /// <remarks>
    /// The JSON input must be an object containing a single 'State' property. 
    /// The method expects the reader to be positioned at the start of this object.
    /// </remarks>
    /// <param name="reader">
    /// The reader positioned at the JSON to read. 
    /// The reader must be at the start of a JSON object containing a 'State' property.
    /// </param>
    /// <param name="typeToConvert">
    /// The type of the collection to convert the JSON to. 
    /// This parameter is provided by the serialization infrastructure.
    /// </param>
    /// <param name="options">Options to control the behavior of the deserialization process.</param>
    /// <returns>An instance of <typeparamref name="TCollection"/> deserialized from the JSON input.</returns>
    /// <exception cref="JsonException">Thrown if the JSON is not in the expected format or if a deserialization error occurs.</exception>
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

        var state = JsonSerializer.Deserialize<TState>(ref reader, options) ?? throw new JsonException();
        if (!reader.Read() || reader.TokenType != JsonTokenType.EndObject)
            throw new JsonException();

        return _factory(state);
    }

    /// <summary>
    /// Writes the JSON representation of the specified collection object using the provided Utf8JsonWriter.
    /// </summary>
    /// <remarks>
    /// The method writes a JSON object containing a single property named "State", which holds the serialized state of the collection. 
    /// The serialization behavior may be influenced by the provided <see cref="JsonSerializerOptions"/>.
    /// </remarks>
    /// <param name="writer">The Utf8JsonWriter to which the JSON output is written. Must not be null.</param>
    /// <param name="value">The collection object to serialize. Must not be null.</param>
    /// <param name="options">The options to use when serializing the collection object.</param>
    public override void Write(Utf8JsonWriter writer, TCollection value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        var state = _stateGetter(value);

        writer.WritePropertyName("State");
        JsonSerializer.Serialize(writer, state, options);

        writer.WriteEndObject();
    }
}
