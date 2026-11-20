//=======================================================================
// SwiftComparerWire.MemoryPack.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using MemoryPack;

namespace SwiftCollections.Serialization;

internal static class SwiftComparerWire
{
    internal static SwiftComparerRegistry GetRegistry(IServiceProvider? provider)
    {
        object? service = provider?.GetService(typeof(SwiftComparerRegistry));
        return service == null ? SwiftComparerRegistry.Empty : service as SwiftComparerRegistry
            ?? throw new InvalidOperationException("The comparer service must be a SwiftComparerRegistry.");
    }

    internal static void WriteEnvelopeHeader<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, int count)
#if NET7_0_OR_GREATER
        where TBufferWriter : IBufferWriter<byte>
#else
        where TBufferWriter : class, IBufferWriter<byte>
#endif
    {
        writer.WriteObjectHeader(3);
        writer.WriteUnmanaged((ushort)1);
        writer.WriteCollectionHeader(count);
    }

    internal static bool ReadEnvelopeHeader(ref MemoryPackReader reader, int count)
    {
        if (!reader.TryReadObjectHeader(out byte members)) return false;
        if (members != 3)
            throw new MemoryPackSerializationException("Expected a versioned comparison envelope; state-only legacy records are unsupported.");
        reader.ReadUnmanaged(out ushort version);
        if (version != 1) throw new MemoryPackSerializationException("Unsupported comparison format version.");
        if (!reader.TryReadCollectionHeader(out int descriptors) || descriptors != count)
            throw new MemoryPackSerializationException("The comparison descriptor count does not match the collection roles.");
        return true;
    }

    internal static void WriteDescriptor<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer,
        byte role, SwiftComparerDescriptor descriptor)
#if NET7_0_OR_GREATER
        where TBufferWriter : IBufferWriter<byte>
#else
        where TBufferWriter : class, IBufferWriter<byte>
#endif
    {
        writer.WriteObjectHeader(4);
        writer.WriteUnmanaged(role);
        writer.WriteString(descriptor.Id);
        writer.WriteUnmanaged(descriptor.Version);
        writer.WriteArray(descriptor.Config);
    }

    internal static SwiftComparerDescriptor ReadDescriptor(ref MemoryPackReader reader, byte expectedRole)
    {
        if (!reader.TryReadObjectHeader(out byte members) || members != 4)
            throw new MemoryPackSerializationException("Invalid ordering descriptor layout.");
        reader.ReadUnmanaged(out byte role);
        if (role != expectedRole) throw new MemoryPackSerializationException("Unexpected comparison descriptor role.");
        string id = ReadId(ref reader);
        reader.ReadUnmanaged(out ushort version);
        if (version == 0) throw new MemoryPackSerializationException("Codec versions start at one.");
        if (!reader.TryPeekCollectionHeader(out int length) || length < 0
            || length > SwiftComparerRegistry.MaxConfigurationLength)
            throw new MemoryPackSerializationException("Invalid or oversized comparer configuration.");
        byte[] configuration = reader.ReadArray<byte>()!;
        return new SwiftComparerDescriptor(id, version, configuration);
    }

    internal static IEqualityComparer<T> RestoreEquality<T>(SwiftComparerRegistry registry, SwiftComparerDescriptor descriptor)
    {
        try { return registry.RestoreEquality<T>(descriptor); }
        catch (InvalidOperationException exception)
        {
            throw new MemoryPackSerializationException("The equality rule could not be restored.", exception);
        }
    }

    private static string ReadId(ref MemoryPackReader reader)
    {
        // Inspect both string headers before ReadString can allocate, including segmented input.
        if (!reader.TryPeekCollectionHeader(out int length) || length == 0)
            throw new MemoryPackSerializationException("A comparer identifier is required.");
        if (length > 0)
        {
            if (length > SwiftComparerRegistry.MaxIdLength)
                throw new MemoryPackSerializationException("The comparer identifier exceeds 128 characters.");
        }
        else
        {
            int bytes = ~length;
            if (bytes > SwiftComparerRegistry.MaxIdLength)
                throw new MemoryPackSerializationException("The comparer identifier exceeds 128 ASCII bytes.");
            ReadOnlySpan<byte> header = MemoryMarshal.CreateReadOnlySpan(ref reader.GetSpanReference(8), 8);
            int characters = BinaryPrimitives.ReadInt32LittleEndian(header.Slice(4));
            if (characters != bytes)
                throw new MemoryPackSerializationException("An ASCII comparer identifier requires matching positive character and byte counts.");
        }
        string id = reader.ReadString()!;
        try { SwiftComparerRegistry.ValidateId(id); }
        catch (ArgumentException exception)
        {
            throw new MemoryPackSerializationException("Invalid comparer identifier.", exception);
        }
        return id;
    }
}
