using System;

namespace SwiftCollections.Tests;

public class PersistenceComparableBase : IComparable<PersistenceComparableBase>
{
    public int CompareTo(PersistenceComparableBase other) => 0;
}
