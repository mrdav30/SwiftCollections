//=======================================================================
// SwiftComparerDescriptor.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

namespace SwiftCollections.Serialization;

/// <summary>
/// Carries a comparer codec identifier, version and configuration bytes across persistence boundaries.
/// </summary>
/// <remarks>
/// Capture and wire readers validate metadata and supply independently owned configuration storage.
/// </remarks>
internal readonly struct SwiftComparerDescriptor
{
    internal readonly string Id;
    internal readonly ushort Version;
    internal readonly byte[] Config;

    internal SwiftComparerDescriptor(string id, ushort version, byte[] config)
    {
        Id = id;
        Version = version;
        Config = config;
    }
}
