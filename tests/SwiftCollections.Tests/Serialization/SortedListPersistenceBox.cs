using System.Collections.Generic;
#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
using MemoryPack;
#endif

namespace SwiftCollections.Tests;

#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
[MemoryPackable]
#endif
public partial class SortedListPersistenceBox
{
    public SwiftSortedList<PersistenceToken> Single { get; set; }
    public SwiftSortedList<PersistenceToken>[] Array { get; set; }
    public List<SwiftSortedList<PersistenceToken>> List { get; set; }
}
