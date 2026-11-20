#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
using MemoryPack;
#endif

namespace SwiftCollections.Tests;

#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
[MemoryPackable]
#endif
public partial class PersistenceHashKey
{
    public int Group { get; set; }
    public int Identity { get; set; }

    public override bool Equals(object other) => other is PersistenceHashKey key && Group == key.Group;
    public override int GetHashCode() => Group;
}
