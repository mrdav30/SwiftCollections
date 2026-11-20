//=======================================================================
// SwiftBiDictionary.MemoryPack.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Buffers;
using MemoryPack;
using SwiftCollections.Serialization;

namespace SwiftCollections;

public partial class SwiftBiDictionary<T1, T2> : IMemoryPackable<SwiftBiDictionary<T1, T2>> where T1 : notnull where T2 : notnull
{
    static SwiftBiDictionary() => RegisterFormatter();

    /// <summary>Registers the closed collection type's versioned MemoryPack formatter for explicit host bootstrap.</summary>
    /// <remarks>Comparer definitions remain per-operation services; no application comparer is installed globally.</remarks>
    public new static void RegisterFormatter()
    {
        if (!MemoryPackFormatterProvider.IsRegistered<SwiftBiDictionary<T1, T2>>())
            MemoryPackFormatterProvider.Register(new BiDictionaryFormatter());
    }

    /// <summary>Appends a version-one comparison envelope and shallow item snapshot.</summary>
    /// <typeparam name="TBufferWriter">The destination buffer writer type.</typeparam>
    /// <param name="writer">The writer; its service provider can supply a SwiftComparerRegistry.</param>
    /// <param name="value">The owner to snapshot without mutation, or null to write a null record.</param>
    /// <exception cref="NotSupportedException">A comparison rule or runtime owner contract is unsupported.</exception>
    /// <exception cref="InvalidOperationException">Registry services or comparison-role configuration are invalid.</exception>
    /// <remarks>Capture and snapshot precede the prefix. Later failures may leave partial output. Callers must prevent concurrent owner/comparer mutation.</remarks>
    public static void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref SwiftBiDictionary<T1, T2>? value)
#if NET7_0_OR_GREATER
        where TBufferWriter : IBufferWriter<byte>
#else
        where TBufferWriter : class, IBufferWriter<byte>
#endif
    {
        if (value == null) { writer.WriteNullObjectHeader(); return; }
        SwiftComparerRegistry registry = SwiftComparerWire.GetRegistry(writer.Options.ServiceProvider);
        SwiftComparerDescriptor forward = registry.CaptureEquality(value.Comparer);
        SwiftComparerDescriptor configured = registry.CaptureEquality(value.ConfiguredReverseComparer);
        SwiftComparerDescriptor effective = registry.CaptureEquality(value.EffectiveReverseComparer);
        SwiftComparerRegistry.ValidateReverseEquality<T2>(configured, effective);
        SwiftDictionaryState<T1, T2> state = value.State;
        SwiftComparerWire.WriteEnvelopeHeader(ref writer, 3);
        SwiftComparerWire.WriteDescriptor(ref writer, 2, forward);
        SwiftComparerWire.WriteDescriptor(ref writer, 4, configured);
        SwiftComparerWire.WriteDescriptor(ref writer, 5, effective);
        writer.WriteValue(state);
    }

    /// <summary>Resolves comparison rules before reconstructing and publishing a new owner.</summary>
    /// <param name="reader">The reader to consume; failures can advance it.</param>
    /// <param name="value">Replaced only after success, or set to null for a null record; unchanged on failure.</param>
    /// <exception cref="MemoryPackSerializationException">The envelope, comparison rules or unique-item state are invalid.</exception>
    /// <exception cref="InvalidOperationException">The service provider supplies an incompatible registry service.</exception>
    /// <remarks>Fresh-table restoration uses no new entropy. Replacement detaches prior views and subscribers; callback side effects on shared references are not rolled back.</remarks>
    public static void Deserialize(ref MemoryPackReader reader, scoped ref SwiftBiDictionary<T1, T2>? value)
    {
        if (!SwiftComparerWire.ReadEnvelopeHeader(ref reader, 3)) { value = null; return; }
        SwiftComparerRegistry registry = SwiftComparerWire.GetRegistry(reader.Options.ServiceProvider);
        try
        {
            SwiftComparerDescriptor forwardDescriptor = SwiftComparerWire.ReadDescriptor(ref reader, 2);
            SwiftComparerDescriptor configuredDescriptor = SwiftComparerWire.ReadDescriptor(ref reader, 4);
            SwiftComparerDescriptor effectiveDescriptor = SwiftComparerWire.ReadDescriptor(ref reader, 5);
            SwiftComparerRegistry.ValidateReverseEquality<T2>(configuredDescriptor, effectiveDescriptor);
            var forward = SwiftComparerWire.RestoreEquality<T1>(registry, forwardDescriptor);
            var configured = SwiftComparerWire.RestoreEquality<T2>(registry, configuredDescriptor);
            var effective = SwiftComparerWire.RestoreEquality<T2>(registry, effectiveDescriptor);
            SwiftDictionaryState<T1, T2> state = reader.ReadValue<SwiftDictionaryState<T1, T2>>();
            value = RestoreTrusted(state, forward, configured, effective);
        }
        catch (Exception exception) when (exception is not MemoryPackSerializationException)
        {
            throw new MemoryPackSerializationException("The collection comparison rule or state is invalid.", exception);
        }
    }

    private sealed class BiDictionaryFormatter : MemoryPackFormatter<SwiftBiDictionary<T1, T2>>
    {
        public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref SwiftBiDictionary<T1, T2>? value)
            => SwiftBiDictionary<T1, T2>.Serialize(ref writer, ref value);
        public override void Deserialize(ref MemoryPackReader reader, scoped ref SwiftBiDictionary<T1, T2>? value)
            => SwiftBiDictionary<T1, T2>.Deserialize(ref reader, ref value);
    }
}
