using System.Collections.Generic;
using SwiftCollections.Observable;
#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
using MemoryPack;
#endif

namespace SwiftCollections.Tests;

#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
[MemoryPackable]
#endif
public partial class HashPersistenceBox
{
    public SwiftDictionary<PersistenceHashKey, int> Dictionary { get; set; }
    public SwiftHashSet<PersistenceHashKey> Set { get; set; }
    public SwiftBiDictionary<PersistenceHashKey, string> Bidirectional { get; set; }
    public SwiftObservableDictionary<PersistenceHashKey, int> Observable { get; set; }
    public SwiftDictionary<PersistenceHashKey, int>[] Array { get; set; }
    public List<SwiftHashSet<PersistenceHashKey>> List { get; set; }
}
