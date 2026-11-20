using System;

namespace SwiftCollections.Tests;

public struct PersistenceGenericComparable : IComparable<PersistenceGenericComparable>
{
    public int Key { get; set; }
    public int CompareTo(PersistenceGenericComparable other) => Key.CompareTo(other.Key);
}
