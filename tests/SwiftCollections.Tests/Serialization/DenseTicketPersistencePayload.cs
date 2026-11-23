#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
using MemoryPack;
#endif

namespace SwiftCollections.Tests;

#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
[MemoryPackable]
#endif
public partial class DenseTicketPersistencePayload
{
    public int Id { get; set; }
    public string Label { get; set; }
}
