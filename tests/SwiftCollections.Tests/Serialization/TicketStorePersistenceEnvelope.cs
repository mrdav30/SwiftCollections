using System.Collections.Generic;
#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
using MemoryPack;
#endif

namespace SwiftCollections.Tests;

#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
[MemoryPackable]
#endif
public partial class TicketStorePersistenceEnvelope
{
    public SwiftTicketStore<DenseTicketPersistencePayload> ReferenceStore { get; set; }
    public SwiftTicketStore<DenseTicketPersistenceValue> ValueStore { get; set; }
    public SwiftTicketStore<int>[] Array { get; set; }
    public List<SwiftTicketStore<int>> List { get; set; }
}
