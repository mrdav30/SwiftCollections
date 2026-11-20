//=======================================================================
// SwiftHashSet.MemoryPack.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Buffers;
using MemoryPack;
using SwiftCollections.Serialization;

namespace SwiftCollections;

public sealed partial class SwiftHashSet<T> : IMemoryPackable<SwiftHashSet<T>> where T : notnull
{
    static SwiftHashSet() => RegisterFormatter();

    /// <summary>Registers the closed collection type's versioned MemoryPack formatter for explicit host bootstrap.</summary>
    /// <remarks>Comparer definitions remain per-operation services; no application comparer is installed globally.</remarks>
    public static void RegisterFormatter()
    {
        if (!MemoryPackFormatterProvider.IsRegistered<SwiftHashSet<T>>())
            MemoryPackFormatterProvider.Register(new HashSetFormatter());
    }

    /// <summary>Appends a version-one comparison envelope and shallow item snapshot.</summary>
    /// <typeparam name="TBufferWriter">The destination buffer writer type.</typeparam>
    /// <param name="writer">The writer; its service provider can supply a SwiftComparerRegistry.</param>
    /// <param name="value">The owner to snapshot without mutation, or null to write a null record.</param>
    /// <exception cref="NotSupportedException">A comparison rule or runtime owner contract is unsupported.</exception>
    /// <exception cref="InvalidOperationException">Registry services or comparison-role configuration are invalid.</exception>
    /// <remarks>Capture and snapshot precede the prefix. Later failures may leave partial output. Callers must prevent concurrent owner/comparer mutation.</remarks>
    public static void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref SwiftHashSet<T>? value)
#if NET7_0_OR_GREATER
        where TBufferWriter : IBufferWriter<byte>
#else
        where TBufferWriter : class, IBufferWriter<byte>
#endif
    {
        if (value == null) { writer.WriteNullObjectHeader(); return; }
        SwiftComparerRegistry registry = SwiftComparerWire.GetRegistry(writer.Options.ServiceProvider);
        SwiftComparerDescriptor descriptor = registry.CaptureEquality(value.Comparer);
        SwiftArrayState<T> state = value.State;
        SwiftComparerWire.WriteEnvelopeHeader(ref writer, 1);
        SwiftComparerWire.WriteDescriptor(ref writer, 3, descriptor);
        writer.WriteValue(state);
    }

    /// <summary>Resolves comparison rules before reconstructing and publishing a new owner.</summary>
    /// <param name="reader">The reader to consume; failures can advance it.</param>
    /// <param name="value">Replaced only after success, or set to null for a null record; unchanged on failure.</param>
    /// <exception cref="MemoryPackSerializationException">The envelope, comparison rules or unique-item state are invalid.</exception>
    /// <exception cref="InvalidOperationException">The service provider supplies an incompatible registry service.</exception>
    /// <remarks>Fresh-table restoration uses no new entropy. Replacement detaches prior views and subscribers; callback side effects on shared references are not rolled back.</remarks>
    public static void Deserialize(ref MemoryPackReader reader, scoped ref SwiftHashSet<T>? value)
    {
        if (!SwiftComparerWire.ReadEnvelopeHeader(ref reader, 1)) { value = null; return; }
        SwiftComparerRegistry registry = SwiftComparerWire.GetRegistry(reader.Options.ServiceProvider);
        try
        {
            SwiftComparerDescriptor descriptor = SwiftComparerWire.ReadDescriptor(ref reader, 3);
            var comparer = SwiftComparerWire.RestoreEquality<T>(registry, descriptor);
            SwiftArrayState<T> state = reader.ReadValue<SwiftArrayState<T>>();
            value = RestoreTrusted(state, comparer);
        }
        catch (Exception exception) when (exception is not MemoryPackSerializationException)
        {
            throw new MemoryPackSerializationException("The collection comparison rule or state is invalid.", exception);
        }
    }

    private sealed class HashSetFormatter : MemoryPackFormatter<SwiftHashSet<T>>
    {
        public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref SwiftHashSet<T>? value)
            => SwiftHashSet<T>.Serialize(ref writer, ref value);
        public override void Deserialize(ref MemoryPackReader reader, scoped ref SwiftHashSet<T>? value)
            => SwiftHashSet<T>.Deserialize(ref reader, ref value);
    }
}
