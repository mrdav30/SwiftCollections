using System;
using System.Text.Json;
#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
using MemoryPack;
#endif
using Xunit;

namespace SwiftCollections.Tests;

public class SlotMapMigrationTests
{
    // Captured from published SwiftCollections 7.0.0 (16aec0cb6a9a75c4a8609d817c1bf61d7674a0f9).
    private const string SlotJson = "{\"State\":{\"Items\":[\"alpha\",null,\"gamma\",null,null,null,null,null],\"Allocated\":[true,false,true,false,false,false,false,false],\"FreeIndices\":[1],\"PeakCount\":3}}";
    private const string TicketJson = "{\"State\":{\"Items\":[null,\"bravo\",null,null,null,null,null,null],\"Allocated\":[false,true,false,false,false,false,false,false],\"Generations\":[1,1,0,0,0,0,0,0],\"FreeIndices\":[0],\"Peak\":2}}";
    private const string SlotMemoryPack = "AQQIAAAA+v///wUAAABhbHBoYf/////6////BQAAAGdhbW1h//////////////////////////8IAAAAAQABAAAAAAABAAAAAQAAAAMAAAA=";
    private const string TicketMemoryPack = "AQUIAAAA//////r///8FAAAAYnJhdm////////////////////////////////8IAAAAAAEAAAAAAAAIAAAAAQAAAAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABAAAAAAAAAAIAAAA=";

    [Fact]
    public void V7SlotJson_RestoresSlotsAndContinuesAllocation()
    {
        var map = JsonSerializer.Deserialize<SwiftSlotMap<string>>(SlotJson);
        Assert.Equal(SlotJson, JsonSerializer.Serialize(map));
        Assert.Equal(2, map.Count);
        Assert.Equal("alpha", map[0]);
        Assert.Equal("gamma", map[2]);
        Assert.False(map.IsAllocated(1));
        Assert.Equal(1, map.Add("delta"));
        Assert.Equal("delta", map[1]);
    }

    [Fact]
    public void V7TicketJson_RestoresGenerationsAndContinuesAllocation()
    {
        var map = JsonSerializer.Deserialize<SwiftTicketMap<string>>(TicketJson);
        Assert.Equal(TicketJson, JsonSerializer.Serialize(map));
        Assert.Equal(1, map.Count);
        Assert.True(map.TryGet(new SwiftTicket(1, 1), out string value));
        Assert.Equal("bravo", value);
        Assert.False(map.IsValid(new SwiftTicket(1, 0)));
        Assert.Equal(new SwiftTicket(0, 1), map.Add("charlie"));
        Assert.False(map.IsValid(new SwiftTicket(0, 0)));
    }

#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
    [Fact]
    public void V7SlotMemoryPack_RestoresSlotsAndContinuesAllocation()
    {
        byte[] payload = Convert.FromBase64String(SlotMemoryPack);
        var map = MemoryPackSerializer.Deserialize<SwiftSlotMap<string>>(payload);
        Assert.Equal(payload, MemoryPackSerializer.Serialize(map));
        Assert.Equal(2, map.Count);
        Assert.Equal("alpha", map[0]);
        Assert.Equal("gamma", map[2]);
        Assert.False(map.IsAllocated(1));
        Assert.Equal(1, map.Add("delta"));
        Assert.Equal("delta", map[1]);
    }

    [Fact]
    public void V7TicketMemoryPack_RestoresGenerationsAndContinuesAllocation()
    {
        byte[] payload = Convert.FromBase64String(TicketMemoryPack);
        var map = MemoryPackSerializer.Deserialize<SwiftTicketMap<string>>(payload);
        Assert.Equal(payload, MemoryPackSerializer.Serialize(map));
        Assert.Equal(1, map.Count);
        Assert.True(map.TryGet(new SwiftTicket(1, 1), out string value));
        Assert.Equal("bravo", value);
        Assert.False(map.IsValid(new SwiftTicket(1, 0)));
        Assert.Equal(new SwiftTicket(0, 1), map.Add("charlie"));
        Assert.False(map.IsValid(new SwiftTicket(0, 0)));
    }
#endif
}
