//=======================================================================
// SwiftSortedList.MemoryPack.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Buffers;
using System.Collections.Generic;
using MemoryPack;
using SwiftCollections.Serialization;

namespace SwiftCollections;

public partial class SwiftSortedList<T> : IMemoryPackable<SwiftSortedList<T>>
{
    static SwiftSortedList() => RegisterFormatter();

    /// <summary>Registers this closed sorted-list type's versioned MemoryPack formatter.</summary>
    /// <remarks>
    /// Call for closed types during explicit host bootstrap to avoid relying on reflective formatter discovery.
    /// Comparer definitions remain per-operation services; registration does not install application comparers globally.
    /// </remarks>
    public static void RegisterFormatter()
    {
        if (!MemoryPackFormatterProvider.IsRegistered<SwiftSortedList<T>>())
            MemoryPackFormatterProvider.Register(new SortedListFormatter());
    }

    /// <summary>Writes comparison metadata and an item snapshot through the version-one MemoryPack contract.</summary>
    /// <typeparam name="TBufferWriter">The destination byte-buffer writer type.</typeparam>
    /// <param name="writer">The writer to append to; its service provider can supply a SwiftComparerRegistry.</param>
    /// <param name="value">The owner to snapshot without mutation, or null to write a null owner.</param>
    /// <exception cref="NotSupportedException">The comparer has no supported persistence registration.</exception>
    /// <exception cref="InvalidOperationException">The service provider supplies an incompatible registry service.</exception>
    /// <remarks>
    /// Capture and copying precede the envelope prefix. Later element/writer failures may leave partial output.
    /// The caller must prevent concurrent collection and comparer mutation. Referenced elements remain shared.
    /// Serializing a derived instance through this base contract records only its sorted-list items and ordering.
    /// </remarks>
    public static void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer,
        scoped ref SwiftSortedList<T>? value)
#if NET7_0_OR_GREATER
        where TBufferWriter : IBufferWriter<byte>
#else
        where TBufferWriter : class, IBufferWriter<byte>
#endif
    {
        if (value == null) { writer.WriteNullObjectHeader(); return; }
        SwiftComparerRegistry registry = SwiftComparerWire.GetRegistry(writer.Options.ServiceProvider);
        SwiftComparerDescriptor descriptor = registry.CaptureOrdering(value._comparer);
        SwiftArrayState<T> state = value.State;
        writer.WriteObjectHeader(3);
        writer.WriteUnmanaged((ushort)1);
        SwiftComparerWire.WriteOrdering(ref writer, descriptor);
        writer.WriteValue(state);
    }

    /// <summary>Reads strict version-one comparison metadata before publishing a restored owner.</summary>
    /// <param name="reader">The reader to consume; failures can advance it.</param>
    /// <param name="value">Replaced after successful restoration, or set to null for a null record; unchanged on failure.</param>
    /// <exception cref="MemoryPackSerializationException">The record or comparison rule is invalid, unsupported or incomplete.</exception>
    /// <exception cref="InvalidOperationException">The service provider supplies an incompatible registry service.</exception>
    /// <remarks>
    /// Resolves the comparer before reading items, then copies and centers trusted order without comparisons or
    /// sorting. Overwrite can replace instance identity and detach views. Application callback side effects on
    /// shallow references are not rolled back. Missing registry services support built-ins only.
    /// </remarks>
    public static void Deserialize(ref MemoryPackReader reader, scoped ref SwiftSortedList<T>? value)
    {
        if (!reader.TryReadObjectHeader(out byte members)) { value = null; return; }
        if (members != 3)
            throw new MemoryPackSerializationException("Expected a versioned sorted-list comparison envelope; state-only legacy records are unsupported.");
        reader.ReadUnmanaged(out ushort version);
        if (version != 1) throw new MemoryPackSerializationException("Unsupported sorted-list format version.");
        SwiftComparerDescriptor descriptor = SwiftComparerWire.ReadOrdering(ref reader);
        SwiftComparerRegistry registry = SwiftComparerWire.GetRegistry(reader.Options.ServiceProvider);
        IComparer<T> comparer;
        try { comparer = registry.RestoreOrdering<T>(descriptor); }
        catch (InvalidOperationException exception)
        {
            throw new MemoryPackSerializationException("The sorted-list comparison rule could not be restored.", exception);
        }
        SwiftArrayState<T> state = reader.ReadValue<SwiftArrayState<T>>();
        if (state.Items == null) throw new MemoryPackSerializationException("Sorted-list state items must be non-null.");
        value = RestoreTrusted(state, comparer);
    }

    private sealed class SortedListFormatter : MemoryPackFormatter<SwiftSortedList<T>>
    {
        public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer,
            scoped ref SwiftSortedList<T>? value) => SwiftSortedList<T>.Serialize(ref writer, ref value);

        public override void Deserialize(ref MemoryPackReader reader, scoped ref SwiftSortedList<T>? value)
            => SwiftSortedList<T>.Deserialize(ref reader, ref value);
    }
}
