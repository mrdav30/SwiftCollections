using System;

namespace SwiftCollections.Tests;

public struct PersistenceOtherComparable : IComparable<int>, IDisposable, IEquatable<PersistenceOtherComparable>
{
    public int CompareTo(int other) => 0;
    public void Dispose() { }
    public bool Equals(PersistenceOtherComparable other) => true;
}
