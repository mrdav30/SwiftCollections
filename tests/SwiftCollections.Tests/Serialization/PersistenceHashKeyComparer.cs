using System.Collections.Generic;

namespace SwiftCollections.Tests;

public sealed class PersistenceHashKeyComparer : IEqualityComparer<PersistenceHashKey>
{
    public PersistenceHashKeyComparer(int seed) => Seed = seed;
    public int Seed { get; }
    public bool Equals(PersistenceHashKey left, PersistenceHashKey right)
        => ReferenceEquals(left, right) || (left != null && right != null && left.Group == right.Group && left.Identity == right.Identity);
    public int GetHashCode(PersistenceHashKey value)
        => value == null ? 0 : unchecked((value.Group * 397 ^ value.Identity) ^ Seed);
}
