//=======================================================================
// SwiftDenseTicketMap.MemoryPack.cs
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

public sealed partial class SwiftDenseTicketMap<T> : IMemoryPackable<SwiftDenseTicketMap<T>>
{
    static SwiftDenseTicketMap()
    {
#if NET7_0_OR_GREATER
        MemoryPackFormatterProvider.Register<SwiftDenseTicketMap<T>>();
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
        if (!MemoryPackFormatterProvider.IsRegistered<SwiftDenseTicketMap<T>>())
#if NET7_0_OR_GREATER
            MemoryPackFormatterProvider.Register(new MemoryPackableFormatter<SwiftDenseTicketMap<T>>());
#else
            MemoryPackFormatterProvider.Register(new OwnerFormatter());
#endif
        if (!MemoryPackFormatterProvider.IsRegistered<SwiftDenseTicketMap<T>[]>())
            MemoryPackFormatterProvider.Register(new ArrayFormatter<SwiftDenseTicketMap<T>>());
    }

    /// <summary>Appends a null owner or the existing one-member state envelope.</summary>
    /// <typeparam name="TBufferWriter">The destination byte-buffer writer type.</typeparam>
    /// <param name="writer">The writer to append to; failures may leave partial output.</param>
    /// <param name="value">The owner to snapshot, or null; callers must prevent concurrent mutation.</param>
    [Preserve]
#if NET7_0_OR_GREATER
    static void IMemoryPackable<SwiftDenseTicketMap<T>>.Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer,
        scoped ref SwiftDenseTicketMap<T>? value)
#else
    public static void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer,
        scoped ref SwiftDenseTicketMap<T>? value) where TBufferWriter : class, IBufferWriter<byte>
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
    /// <exception cref="InvalidOperationException">The decoded state exceeds the supported identity budget.</exception>
    /// <remarks>A zero-member owner reconstructs prior logical state, or empty state when value is null.
    /// Complete records read fresh arrays. The previous owner remains unchanged on failure; custom payload
    /// callback side effects are not rolled back.</remarks>
    [Preserve]
#if NET7_0_OR_GREATER
    static void IMemoryPackable<SwiftDenseTicketMap<T>>.Deserialize(ref MemoryPackReader reader, scoped ref SwiftDenseTicketMap<T>? value)
#else
    public static void Deserialize(ref MemoryPackReader reader, scoped ref SwiftDenseTicketMap<T>? value)
#endif
    {
        value = SwiftStateMemoryPack.Read(ref reader, value, out SwiftDenseTicketMapState<T> state) ? new(state) : null;
    }

#if !NET7_0_OR_GREATER
    [Preserve]
    private sealed class OwnerFormatter : MemoryPackFormatter<SwiftDenseTicketMap<T>>
    {
        [Preserve]
        public override void Serialize<TBufferWriter>(ref MemoryPackWriter<TBufferWriter> writer, scoped ref SwiftDenseTicketMap<T>? value)
            => SwiftDenseTicketMap<T>.Serialize(ref writer, ref value);

        [Preserve]
        public override void Deserialize(ref MemoryPackReader reader, scoped ref SwiftDenseTicketMap<T>? value)
            => SwiftDenseTicketMap<T>.Deserialize(ref reader, ref value);
    }
#endif
}
