using System.Collections.Generic;

namespace SwiftCollections.Tests;

internal sealed class SelectiveIntHashComparer : IEqualityComparer<int>
{
    private readonly SwiftDictionary<int, int> _hashOverrides;

    public SelectiveIntHashComparer(params (int Value, int Hash)[] hashOverrides)
    {
        _hashOverrides = new SwiftDictionary<int, int>(hashOverrides.Length);

        foreach (var (value, hash) in hashOverrides)
            _hashOverrides[value] = hash;
    }

    public bool Equals(int x, int y) => x == y;

    public int GetHashCode(int obj)
        => _hashOverrides.TryGetValue(obj, out int hash) ? hash : obj;
}
