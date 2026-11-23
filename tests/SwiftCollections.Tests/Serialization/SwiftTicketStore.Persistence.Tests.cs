using System;
using System.Text.Json;
#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
using MemoryPack;
#endif
using Xunit;

namespace SwiftCollections.Tests;

public class SwiftTicketStorePersistenceTests
{
    [Theory]
    [InlineData("raw")]
    [InlineData("json")]
    [InlineData("generated-json")]
#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
    [InlineData("memorypack")]
#endif
    public void CoordinatedSnapshot_RestoresEveryStoreAndReplaysTheSameAgentTimeline(string codec)
    {
        var original = CreateWorld();
        var restored = RoundTrip(original, codec);
        Assert.NotSame(original.Agents, restored.Agents);
        Assert.NotSame(original.Positions, restored.Positions);
        Assert.NotSame(original.Velocities, restored.Velocities);
        Assert.NotSame(original.Health, restored.Health);
        AssertCoherent(restored);
        AssertEntries(restored.Positions, [new(0, 1), new(1, 1), new(2, 1)], [2, 9, 103]);
        AssertEntries(restored.Velocities, [new(2, 1), new(0, 1), new(1, 1)], [3, 2, -1]);
        AssertEntries(restored.Health, [new(2, 1), new(0, 1)], [50, 80]);

        Continue(original);
        // Restored values were captured before the live world's continuation.
        Assert.Equal(9, restored.Positions.GetRef(new SwiftTicket(1, 1)));
        Continue(restored);
        AssertGoldenFinal(original);
        AssertGoldenFinal(restored);
        Assert.Equal(original.Agents.State.SlotGenerations, restored.Agents.State.SlotGenerations);
        Assert.Equal(original.Agents.State.FreeSlots, restored.Agents.State.FreeSlots);
    }

    [Theory]
    [InlineData("raw")]
    [InlineData("json")]
    [InlineData("generated-json")]
#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
    [InlineData("memorypack")]
#endif
    public void IndividuallyValidSerializedStores_StillNeedIssuerValidationBeforeAggregatePublication(string codec)
    {
        var published = CreateWorld();
        var previous = published;
        var mismatched = CreateWorld();
        // Individually valid state can still contain a ticket from a different timeline.
        Assert.True(mismatched.Agents.Remove(new SwiftTicket(1, 1)));
        Assert.Equal(new SwiftTicket(1, 2), mismatched.Agents.Add(404));
        var candidate = RoundTrip(mismatched, codec);
        Assert.True(candidate.Positions.ContainsKey(new SwiftTicket(1, 1)));
        Assert.False(candidate.Agents.IsValid(new SwiftTicket(1, 1)));

        Assert.Throws<InvalidOperationException>(() =>
        {
            RequireCoherent(candidate);
            published = candidate;
        });

        Assert.Same(previous, published);
        Assert.Equal(202, published.Agents.GetRef(new SwiftTicket(1, 1)));
        Assert.Equal(9, published.Positions.GetRef(new SwiftTicket(1, 1)));
        AssertCoherent(published);
    }

    private static TicketStorePersistenceWorld CreateWorld()
    {
        var world = new TicketStorePersistenceWorld
        {
            Agents = new SwiftDenseTicketMap<int>(),
            Positions = new SwiftTicketStore<int>(),
            Velocities = new SwiftTicketStore<int>(),
            Health = new SwiftTicketStore<int>()
        };
        SwiftTicket a = world.Agents.Add(101);
        SwiftTicket b = world.Agents.Add(202);
        SwiftTicket c = world.Agents.Add(303);
        Assert.Equal(new SwiftTicket(0, 1), a);
        Assert.Equal(new SwiftTicket(1, 1), b);
        Assert.Equal(new SwiftTicket(2, 1), c);
        world.Positions.Add(a, 0);
        world.Positions.Add(b, 10);
        world.Positions.Add(c, 100);
        world.Velocities.Add(c, 3);
        world.Velocities.Add(a, 2);
        world.Velocities.Add(b, -1);
        world.Health.Add(a, 100);
        world.Health.Add(c, 50);
        Tick(world);
        Assert.True(world.Health.Remove(a));
        world.Health.Add(a, 80);
        return world;
    }

    private static TicketStorePersistenceWorld RoundTrip(TicketStorePersistenceWorld world, string codec)
    {
        if (codec == "raw")
            return new TicketStorePersistenceWorld
            {
                Agents = new SwiftDenseTicketMap<int>(world.Agents.State),
                Positions = new SwiftTicketStore<int>(world.Positions.State),
                Velocities = new SwiftTicketStore<int>(world.Velocities.State),
                Health = new SwiftTicketStore<int>(world.Health.State)
            };
#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
        if (codec == "memorypack")
            return MemoryPackSerializer.Deserialize<TicketStorePersistenceWorld>(MemoryPackSerializer.Serialize(world));
#endif
        var options = SwiftTicketStoreJsonTests.Options(codec == "generated-json");
        return JsonSerializer.Deserialize<TicketStorePersistenceWorld>(JsonSerializer.Serialize(world, options), options);
    }

    private static void Continue(TicketStorePersistenceWorld world)
    {
        var b = new SwiftTicket(1, 1);
        Assert.True(world.Positions.Remove(b));
        Assert.True(world.Velocities.Remove(b));
        Assert.False(world.Health.Remove(b));
        Assert.True(world.Agents.Remove(b));
        SwiftTicket d = world.Agents.Add(404);
        Assert.Equal(new SwiftTicket(1, 2), d);
        world.Positions.Add(d, -5);
        world.Velocities.Add(d, 4);
        world.Health.Add(d, 25);
        Assert.False(world.Positions.Remove(b));
        Assert.False(world.Velocities.Remove(b));
        Assert.False(world.Health.Remove(b));
        Tick(world);
        Tick(world);
    }

    private static void Tick(TicketStorePersistenceWorld world)
    {
        for (int i = 0; i < world.Positions.Count; i++)
        {
            SwiftTicket ticket = world.Positions.GetTicketAt(i);
            if (world.Velocities.TryGet(ticket, out int velocity))
                world.Positions.Values[i] += velocity;
        }
    }

    private static void RequireCoherent(TicketStorePersistenceWorld world)
    {
        foreach (var store in new[] { world.Positions, world.Velocities, world.Health })
            for (int i = 0; i < store.Count; i++)
                if (!world.Agents.IsValid(store.GetTicketAt(i)))
                    throw new InvalidOperationException("Component ticket is absent from the restored issuer.");
    }

    private static void AssertCoherent(TicketStorePersistenceWorld world) => RequireCoherent(world);

    private static void AssertGoldenFinal(TicketStorePersistenceWorld world)
    {
        var a = new SwiftTicket(0, 1);
        var b = new SwiftTicket(1, 1);
        var c = new SwiftTicket(2, 1);
        var d = new SwiftTicket(1, 2);
        AssertCoherent(world);
        Assert.Equal(new[] { 101, 303, 404 }, world.Agents.Values.ToArray());
        Assert.Equal(new[] { 0, 2, 1 }, world.Agents.State.DenseSlots);
        Assert.Equal(new uint[] { 1, 2, 1 }, world.Agents.State.SlotGenerations);
        Assert.Empty(world.Agents.State.FreeSlots);
        AssertEntries(world.Positions, [a, c, d], [6, 109, 3]);
        AssertEntries(world.Velocities, [c, a, d], [3, 2, 4]);
        AssertEntries(world.Health, [c, a, d], [50, 80, 25]);
        Assert.False(world.Agents.IsValid(b));
        foreach (var store in new[] { world.Positions, world.Velocities, world.Health })
            Assert.False(store.ContainsKey(b));
    }

    private static void AssertEntries(SwiftTicketStore<int> store, SwiftTicket[] tickets, int[] values)
    {
        Assert.Equal(tickets.Length, store.Count);
        Assert.Equal(values, store.Values.ToArray());
        for (int i = 0; i < tickets.Length; i++)
        {
            Assert.Equal(tickets[i], store.GetTicketAt(i));
            Assert.True(store.TryGet(tickets[i], out int actual));
            Assert.Equal(values[i], actual);
        }
    }
}
