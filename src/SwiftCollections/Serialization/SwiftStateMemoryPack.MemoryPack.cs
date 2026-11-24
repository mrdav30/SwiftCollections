//=======================================================================
// SwiftStateMemoryPack.MemoryPack.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using Chronicler;
using MemoryPack;

namespace SwiftCollections.Serialization;

/// <summary>Reads the shared one-state ticket-owner envelope without snapshotting a replaced owner.</summary>
internal static class SwiftStateMemoryPack
{
    internal static bool Read<TOwner, TState>(ref MemoryPackReader reader, TOwner? previous, out TState state)
        where TOwner : class, IStateBacked<TState>
        where TState : struct, IMemoryPackable<TState>
    {
        if (!reader.TryReadObjectHeader(out byte count))
        {
            state = default;
            return false;
        }
        if (count > 1)
            MemoryPackSerializationException.ThrowInvalidPropertyCount(typeof(TOwner), 1, count);

        // Only an omitted owner member inherits prior state. A present readonly DTO is decoded afresh.
        state = count == 0 ? previous == null ? default : previous.State : reader.ReadPackable<TState>();
        return true;
    }
}
