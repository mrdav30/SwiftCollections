//=======================================================================
// SwiftOrderingRegistration.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Collections.Generic;

namespace SwiftCollections.Serialization;

internal sealed class SwiftOrderingRegistration<T>
{
    internal readonly string Id;
    internal readonly ushort Version;
    internal readonly Func<IComparer<T>, ReadOnlyMemory<byte>> Encode;
    internal readonly Func<ReadOnlyMemory<byte>, (IComparer<T> Comparer, int BytesConsumed)> Decode;

    internal SwiftOrderingRegistration(string id, ushort version,
        Func<IComparer<T>, ReadOnlyMemory<byte>> encode,
        Func<ReadOnlyMemory<byte>, (IComparer<T> Comparer, int BytesConsumed)> decode)
    {
        Id = id;
        Version = version;
        Encode = encode;
        Decode = decode;
    }
}
