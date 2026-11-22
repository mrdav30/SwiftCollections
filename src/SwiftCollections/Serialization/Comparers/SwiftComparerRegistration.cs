//=======================================================================
// SwiftComparerRegistration.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;

namespace SwiftCollections.Serialization;

/// <summary>
/// Stores the identifier, version and encode/decode callbacks for a registered comparer codec.
/// </summary>
internal sealed class SwiftComparerRegistration<TComparer> where TComparer : class
{
    internal readonly string Id;
    internal readonly ushort Version;
    internal readonly Func<TComparer, ReadOnlyMemory<byte>> Encode;
    internal readonly Func<ReadOnlyMemory<byte>, (TComparer Comparer, int BytesConsumed)> Decode;

    internal SwiftComparerRegistration(string id, ushort version,
        Func<TComparer, ReadOnlyMemory<byte>> encode,
        Func<ReadOnlyMemory<byte>, (TComparer Comparer, int BytesConsumed)> decode)
    {
        Id = id;
        Version = version;
        Encode = encode;
        Decode = decode;
    }
}
