using System.Collections.Generic;
#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
using MemoryPack;
#endif

namespace SwiftCollections.Tests;

#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
[MemoryPackable]
#endif
public partial class DenseTicketPersistenceEnvelope
{
    public SwiftDenseTicketMap<DenseTicketPersistencePayload> ReferenceMap { get; set; }
    public SwiftDenseTicketMap<DenseTicketPersistenceValue> ValueMap { get; set; }
    public SwiftDenseTicketMap<int>[] Array { get; set; }
    public List<SwiftDenseTicketMap<int>> List { get; set; }
}
