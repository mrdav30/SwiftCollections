//=======================================================================
// SwiftComparerJsonMetadata.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Text.Json;

namespace SwiftCollections.Serialization;

/// <summary>
/// Reads and writes the strict versioned JSON envelope metadata shared by comparer-aware converters.
/// </summary>
internal static class SwiftComparerJsonMetadata
{
    private const int MaximumBase64Length = ((SwiftComparerRegistry.MaxConfigurationLength + 2) / 3) * 4;

    internal static void ReadStart(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected a versioned collection comparison envelope.");
        ReadProperty(ref reader, "FormatVersion");
        if (ReadVersion(ref reader) != 1) throw new JsonException("Unsupported collection envelope version.");
        ReadProperty(ref reader, "Comparers");
        if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("Expected comparison descriptors.");
    }

    internal static SwiftComparerDescriptor ReadDescriptor(ref Utf8JsonReader reader, byte expectedRole)
    {
        ReadToken(ref reader, JsonTokenType.StartObject);
        ReadProperty(ref reader, "Role");
        if (reader.TokenType != JsonTokenType.Number || !reader.TryGetByte(out byte role) || role != expectedRole)
            throw new JsonException("Unexpected comparison role or descriptor order.");
        ReadProperty(ref reader, "Id");
        string id = ReadId(ref reader);
        ReadProperty(ref reader, "Version");
        ushort version = ReadVersion(ref reader);
        ReadProperty(ref reader, "Config");
        byte[] config = ReadConfiguration(ref reader);
        ReadToken(ref reader, JsonTokenType.EndObject);
        return new SwiftComparerDescriptor(id, version, config);
    }

    internal static void WriteStart(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteNumber("FormatVersion", 1);
        writer.WriteStartArray("Comparers");
    }

    internal static void WriteDescriptor(Utf8JsonWriter writer, byte role, SwiftComparerDescriptor descriptor)
    {
        writer.WriteStartObject();
        writer.WriteNumber("Role", role);
        writer.WriteString("Id", descriptor.Id);
        writer.WriteNumber("Version", descriptor.Version);
        writer.WriteBase64String("Config", descriptor.Config);
        writer.WriteEndObject();
    }

    internal static void ReadProperty(ref Utf8JsonReader reader, string name)
    {
        if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName || !reader.ValueTextEquals(name) || !reader.Read())
            throw new JsonException($"Expected property '{name}' in the comparison envelope.");
    }

    internal static void ReadToken(ref Utf8JsonReader reader, JsonTokenType token)
    {
        if (!reader.Read() || reader.TokenType != token)
            throw new JsonException($"Expected {token} in the comparison envelope.");
    }

    internal static ushort ReadVersion(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.Number || !reader.TryGetUInt16(out ushort version) || version == 0)
            throw new JsonException("A comparison version must be an integer from 1 through 65535.");
        return version;
    }

    internal static string ReadId(ref Utf8JsonReader reader)
    {
        Span<char> characters = stackalloc char[SwiftComparerRegistry.MaxIdLength];
        int length = CopyBoundedString(ref reader, characters);
        if (length == 0) throw new JsonException("A comparer identifier cannot be empty.");
        for (int i = 0; i < length; i++)
        {
            char character = characters[i];
            if ((character < 'a' || character > 'z') && (character < '0' || character > '9')
                && character != '.' && character != '_' && character != '-' && character != '/')
                throw new JsonException("A comparer identifier must use lowercase ASCII letters, digits, '.', '_', '-' or '/'.");
        }
        return new string(characters[..length]);
    }

    internal static byte[] ReadConfiguration(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.String
            && (reader.HasValueSequence ? reader.ValueSequence.Length : reader.ValueSpan.Length) == 0)
            return Array.Empty<byte>();
        return ReadNonEmptyConfiguration(ref reader);
    }

    private static byte[] ReadNonEmptyConfiguration(ref Utf8JsonReader reader)
    {
        Span<char> characters = stackalloc char[MaximumBase64Length];
        int length = CopyBoundedString(ref reader, characters);
        if ((length & 3) != 0) throw new JsonException("Comparer configuration must be canonical base64.");

        int padding = characters[length - 1] == '=' ? (characters[length - 2] == '=' ? 2 : 1) : 0;
        int decodedLength = (length / 4) * 3 - padding;
        if (decodedLength > SwiftComparerRegistry.MaxConfigurationLength)
            throw new JsonException("Comparer configuration exceeds the supported byte limit.");

        byte[] configuration = new byte[decodedLength];
        if (!Convert.TryFromBase64Chars(characters[..length], configuration, out int written) || written != decodedLength)
            throw new JsonException("Comparer configuration must be canonical base64.");
        // Matching the exact decoded length rejects ignored whitespace. Check
        // unused bits too, so each configuration has a single base64 spelling.
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
        if ((padding == 2 && (alphabet.IndexOf(characters[length - 3]) & 15) != 0)
            || (padding == 1 && (alphabet.IndexOf(characters[length - 2]) & 3) != 0))
            throw new JsonException("Comparer configuration must be canonical base64.");
        return configuration;
    }

    private static int CopyBoundedString(ref Utf8JsonReader reader, scoped Span<char> destination)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Comparison metadata must be a JSON string.");
        long rawLength = reader.HasValueSequence ? reader.ValueSequence.Length : reader.ValueSpan.Length;
        // Every permitted decoded character is ASCII and occupies at most six
        // input bytes as a JSON Unicode escape. Reject excessive input before
        // decoding, including strings split across input segments.
        if (rawLength > destination.Length * 6L)
            throw new JsonException("Comparison metadata exceeds the supported character limit.");
        try
        {
            return reader.CopyString(destination);
        }
        catch (ArgumentException exception)
        {
            throw new JsonException("Comparison metadata exceeds its limit or contains invalid text.", exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new JsonException("Comparison metadata contains invalid text.", exception);
        }
    }

}
