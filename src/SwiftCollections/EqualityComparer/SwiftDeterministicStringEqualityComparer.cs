using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace SwiftCollections;

/// <summary>
/// Provides a deterministic string comparer used by SwiftCollections when no comparer is supplied.
/// </summary>
internal sealed class SwiftDeterministicStringEqualityComparer : IEqualityComparer<string>, IEqualityComparer, ISerializable
{
    private readonly int _seed;

    public SwiftDeterministicStringEqualityComparer() : this(SwiftHashTools.DefaultDeterministicStringHashSeed) { }

    internal SwiftDeterministicStringEqualityComparer(int seed)
    {
        _seed = seed;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(string? x, string? y) => x == y || (x != null && y != null && x.Equals(y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new bool Equals(object? x, object? y)
    {
        return x == y || (x != null && y != null && (x is string a && y is string b ? a.Equals(b) : x.Equals(y)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj) => obj is SwiftDeterministicStringEqualityComparer other && _seed == other._seed;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetHashCode(string obj)
    {
        if (obj == null) return 0;
        return SwiftHashTools.MurmurHash3(obj, _seed);
    }

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
