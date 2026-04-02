using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace SwiftCollections;

/// <summary>
/// Provides a deterministic object comparer used by SwiftCollections when no comparer is supplied.
/// Strings are hashed via <see cref="SwiftHashTools.MurmurHash3(string, int)"/> to avoid runtime-dependent string hashes.
/// </summary>
internal sealed class SwiftDeterministicObjectEqualityComparer : IEqualityComparer<object>, IEqualityComparer, ISerializable
{
    private readonly int _seed;

    public SwiftDeterministicObjectEqualityComparer() : this(SwiftHashTools.DefaultDeterministicObjectHashSeed) { }

    internal SwiftDeterministicObjectEqualityComparer(int seed)
    {
        _seed = seed;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new bool Equals(object x, object y)
    {
        return x == y || (x != null && y != null && x.Equals(y));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object obj) => obj is SwiftDeterministicObjectEqualityComparer other && _seed == other._seed;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetHashCode(object obj)
    {
        if (obj == null) return 0;
        if (obj is string text) return SwiftHashTools.MurmurHash3(text, _seed);
        return obj.GetHashCode() ^ _seed;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public override int GetHashCode() => GetType().Name.GetHashCode() ^ _seed;

    public void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        info.AddValue("Seed", _seed);
    }
}
