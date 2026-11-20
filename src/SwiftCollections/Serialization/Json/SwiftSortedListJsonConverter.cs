//=======================================================================
// SwiftSortedListJsonConverter.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SwiftCollections.Serialization;

/// <summary>
/// Persists a sorted list's trusted item order and supported comparison rule in a versioned JSON envelope.
/// </summary>
/// <typeparam name="T">The element type whose ordering the registry resolves.</typeparam>
/// <remarks>
/// Reading copies the state into a new owner without comparing or sorting items. Snapshot items must already
/// obey the recorded ordering. Register this converter in serializer options for custom rules; hosts using
/// generated JSON metadata must also supply metadata for the raw state and element types. Null owners retain
/// the serializer's normal null representation. State-only legacy payloads are rejected.
/// </remarks>
public sealed class SwiftSortedListJsonConverter<T> : JsonConverter<SwiftSortedList<T>>
{
    private readonly SwiftComparerRegistry _registry;

    /// <summary>
    /// Creates a converter supporting the built-in comparison rules.
    /// </summary>
    public SwiftSortedListJsonConverter() : this(SwiftComparerRegistry.Empty) { }

    /// <summary>
    /// Creates a converter using the supplied registry at each persistence boundary.
    /// </summary>
    /// <param name="registry">The immutable comparison definitions retained by the converter.</param>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> is null.</exception>
    public SwiftSortedListJsonConverter(SwiftComparerRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <summary>
    /// Reads strict version-one metadata before restoring an owned copy of the raw items.
    /// </summary>
    /// <param name="reader">The reader positioned at the owner value; success leaves it on the ending object token.</param>
    /// <param name="typeToConvert">The closed sorted-list type requested by the serializer.</param>
    /// <param name="options">The options used to deserialize the unchanged raw state and its elements.</param>
    /// <returns>A newly restored owner, or null for a JSON null value.</returns>
    /// <exception cref="JsonException">The envelope is malformed, exceeds metadata limits, or its comparison rule cannot be restored.</exception>
    /// <remarks>
    /// Metadata and envelope properties are ordered and case sensitive regardless of serializer property settings.
    /// Resolving a custom rule can invoke its registered factory before raw state parsing; no collection is
    /// published when any later parse or restoration step fails. Raw state parsing follows <paramref name="options"/>.
    /// </remarks>
    public override SwiftSortedList<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("Expected a sorted-list comparison envelope.");

        SwiftComparerJsonMetadata.ReadProperty(ref reader, "FormatVersion");
        if (SwiftComparerJsonMetadata.ReadVersion(ref reader) != 1) throw new JsonException("Unsupported sorted-list envelope version.");
        SwiftComparerJsonMetadata.ReadProperty(ref reader, "Comparers");
        if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("Expected one ordering descriptor.");
        SwiftComparerJsonMetadata.ReadToken(ref reader, JsonTokenType.StartObject);
        SwiftComparerJsonMetadata.ReadProperty(ref reader, "Role");
        if (reader.TokenType != JsonTokenType.Number || !reader.TryGetByte(out byte role) || role != 1)
            throw new JsonException("A sorted list requires ordering role 1.");
        SwiftComparerJsonMetadata.ReadProperty(ref reader, "Id");
        string id = SwiftComparerJsonMetadata.ReadId(ref reader);
        SwiftComparerJsonMetadata.ReadProperty(ref reader, "Version");
        ushort version = SwiftComparerJsonMetadata.ReadVersion(ref reader);
        SwiftComparerJsonMetadata.ReadProperty(ref reader, "Config");
        byte[] configuration = SwiftComparerJsonMetadata.ReadConfiguration(ref reader);
        SwiftComparerJsonMetadata.ReadToken(ref reader, JsonTokenType.EndObject);
        SwiftComparerJsonMetadata.ReadToken(ref reader, JsonTokenType.EndArray);

        IComparer<T> comparer;
        try
        {
            comparer = _registry.RestoreOrdering<T>(new SwiftComparerDescriptor(id, version, configuration));
        }
        catch (InvalidOperationException exception)
        {
            throw new JsonException("The sorted-list comparison rule could not be restored.", exception);
        }

        SwiftComparerJsonMetadata.ReadProperty(ref reader, "State");
        SwiftArrayState<T> state = JsonSerializer.Deserialize<SwiftArrayState<T>>(ref reader, options);
        if (state.Items == null) throw new JsonException("A sorted-list snapshot requires a non-null items array.");
        SwiftComparerJsonMetadata.ReadToken(ref reader, JsonTokenType.EndObject);
        return SwiftSortedList<T>.RestoreTrusted(state, comparer);
    }

    /// <summary>
    /// Writes supported comparison metadata followed by a shallow item snapshot in the current order.
    /// </summary>
    /// <param name="writer">The destination writer, advanced as the envelope and elements are written.</param>
    /// <param name="value">The owner to snapshot; the owner and its comparer are not mutated.</param>
    /// <param name="options">The options used to serialize the unchanged raw state and its elements.</param>
    /// <exception cref="NotSupportedException">The active comparer cannot be captured by the registry.</exception>
    /// <remarks>
    /// Comparer capture and state copying finish before writing the envelope prefix. A later writer or element
    /// serialization failure can leave a partial envelope in the destination. Copies are shallow and concurrent
    /// owner mutation requires caller synchronization.
    /// </remarks>
    public override void Write(Utf8JsonWriter writer, SwiftSortedList<T> value, JsonSerializerOptions options)
    {
        if (value == null) { writer.WriteNullValue(); return; }
        SwiftComparerDescriptor descriptor = _registry.CaptureOrdering(value.Comparer);
        SwiftArrayState<T> state = value.State;
        writer.WriteStartObject();
        writer.WriteNumber("FormatVersion", 1);
        writer.WriteStartArray("Comparers");
        writer.WriteStartObject();
        writer.WriteNumber("Role", 1);
        writer.WriteString("Id", descriptor.Id);
        writer.WriteNumber("Version", descriptor.Version);
        writer.WriteBase64String("Config", descriptor.Config);
        writer.WriteEndObject();
        writer.WriteEndArray();
        writer.WritePropertyName("State");
        JsonSerializer.Serialize(writer, state, options);
        writer.WriteEndObject();
    }
}
