#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
using MemoryPack;
#endif

namespace SwiftCollections.Tests;

#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
[MemoryPackable]
#endif
public partial class TicketStorePersistenceWorld
{
    public SwiftDenseTicketMap<int> Agents { get; set; }
    public SwiftTicketStore<int> Positions { get; set; }
    public SwiftTicketStore<int> Velocities { get; set; }
    public SwiftTicketStore<int> Health { get; set; }
}
