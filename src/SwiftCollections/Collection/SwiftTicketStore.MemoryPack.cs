//=======================================================================
// SwiftTicketStore.MemoryPack.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Buffers;
using MemoryPack;
using MemoryPack.Formatters;
using MemoryPack.Internal;
using SwiftCollections.Serialization;

namespace SwiftCollections;

public sealed partial class SwiftTicketStore<T> : IMemoryPackable<SwiftTicketStore<T>>
{
    static SwiftTicketStore()
    {
#if NET7_0_OR_GREATER
        MemoryPackFormatterProvider.Register<SwiftTicketStore<T>>();
#else
        RegisterFormatter();
#endif
    }

    /// <summary>Registers this closed owner and its arrays for MemoryPack serialization.</summary>
    /// <remarks>Use explicit closed-type bootstrap on hosts that cannot discover formatters reflectively.</remarks>
    [Preserve]
#if NET7_0_OR_GREATER
    static void IMemoryPackFormatterRegister.RegisterFormatter()
#else
    public static void RegisterFormatter()
#endif
    {
        if (!MemoryPackFormatterProvider.IsRegistered<SwiftTicketStore<T>>())
#if NET7_0_OR_GREATER
            MemoryPackFormatterProvider.Register(new MemoryPackableFormatter<SwiftTicketStore<T>>());
#else
            MemoryPackFormatterProvider.Register(new OwnerFormatter());
#endif
        if (!MemoryPackFormatterProvider.IsRegistered<SwiftTicketStore<T>[]>())
            MemoryPackFormatterProvider.Register(new ArrayFormatter<SwiftTicketStore<T>>());
    }

    /// <summary>Appends a null owner or the existing one-member state envelope.</summary>
    /// <typeparam name="TBufferWriter">The destination byte-buffer writer type.</typeparam>
    /// <param name="writer">The writer to append to; failures may leave partial output.</param>
    /// <param name="value">The owner to snapshot, or null; callers must prevent concurrent mutation.</param>
    [Preserve]
#if NET7_0_OR_GREATER
    static void IMemoryPackable<SwiftTicketStore<T>>.Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer,
        scoped ref SwiftTicketStore<T>? value)
#else
    public static void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer,
        scoped ref SwiftTicketStore<T>? value) where TBufferWriter : class, IBufferWriter<byte>
#endif
    {
        if (value == null) { writer.WriteNullObjectHeader(); return; }
        writer.WriteObjectHeader(1);
        writer.WritePackable(value.State);
    }

    /// <summary>Reads and validates state before publishing a replacement owner.</summary>
    /// <param name="reader">The reader to consume; failure may advance its position.</param>
    /// <param name="value">Replaced only after success, or set to null for a null owner record.</param>
    /// <exception cref="MemoryPackSerializationException">The record is truncated or its owner header is invalid.</exception>
    /// <exception cref="ArgumentException">The decoded logical state is invalid.</exception>
    /// <remarks>A zero-member owner reconstructs prior logical state, or empty state when value is null.
    /// Complete records read fresh arrays. The previous owner remains unchanged on failure; custom payload
    /// callback side effects are not rolled back.</remarks>
    [Preserve]
#if NET7_0_OR_GREATER
    static void IMemoryPackable<SwiftTicketStore<T>>.Deserialize(ref MemoryPackReader reader, scoped ref SwiftTicketStore<T>? value)
#else
    public static void Deserialize(ref MemoryPackReader reader, scoped ref SwiftTicketStore<T>? value)
#endif
    {
        value = SwiftStateMemoryPack.Read(ref reader, value, out SwiftTicketStoreState<T> state) ? new(state) : null;
    }

#if !NET7_0_OR_GREATER
    [Preserve]
    private sealed class OwnerFormatter : MemoryPackFormatter<SwiftTicketStore<T>>
    {
        [Preserve]
        public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref SwiftTicketStore<T>? value)
            => SwiftTicketStore<T>.Serialize(ref writer, ref value);

        [Preserve]
        public override void Deserialize(ref MemoryPackReader reader, scoped ref SwiftTicketStore<T>? value)
            => SwiftTicketStore<T>.Deserialize(ref reader, ref value);
    }
#endif
}
