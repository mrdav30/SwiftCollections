using System.Collections.Generic;

namespace SwiftCollections.Tests;

internal sealed class NonWellKnownStringComparer : IEqualityComparer<string>
{
    private readonly IEqualityComparer<string> _inner;

    public NonWellKnownStringComparer(IEqualityComparer<string> inner)
    {
        _inner = inner;
    }

    public bool Equals(string x, string y) => _inner.Equals(x, y);

    public int GetHashCode(string obj) => _inner.GetHashCode(obj);
}
