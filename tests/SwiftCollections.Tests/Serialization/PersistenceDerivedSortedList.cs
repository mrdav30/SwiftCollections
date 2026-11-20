using System.Collections.Generic;

namespace SwiftCollections.Tests;

public sealed class PersistenceDerivedSortedList : SwiftSortedList<int>
{
    public PersistenceDerivedSortedList(IComparer<int> comparer) : base(comparer) { }
    public int HostValue { get; set; }
}
