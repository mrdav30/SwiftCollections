using System.Collections.Generic;

namespace SwiftCollections.Tests;

public sealed class PersistenceTokenComparer : IComparer<PersistenceToken>
{
    public PersistenceTokenComparer(int direction) => Direction = direction;
    public int Direction { get; }
    public int Comparisons { get; set; }
    public int Compare(PersistenceToken left, PersistenceToken right)
    {
        Comparisons++;
        return Direction == -1 ? right.Key.CompareTo(left.Key) : left.Key.CompareTo(right.Key);
    }
}
