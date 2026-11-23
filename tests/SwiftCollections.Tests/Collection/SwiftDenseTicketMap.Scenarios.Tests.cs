using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SwiftCollections.Tests;

public class SwiftDenseTicketMapScenariosTests
{
    [Fact]
    public void RegistrationChurn_DelayedWorkCannotModifyReplacementRegistration()
    {
        var registrations = new SwiftDenseTicketMap<Registration>();
        SwiftTicket first = registrations.Add(new Registration(101, "first"));
        SwiftTicket removed = registrations.Add(new Registration(202, "old"));
        SwiftTicket moved = registrations.Add(new Registration(303, "moving"));
        SwiftTicket queuedCommand = removed;

        registrations.Remove(removed);
        registrations.GetRef(moved).Label = "updated after dense move";
        SwiftTicket replacement = registrations.Add(new Registration(404, "replacement"));
        if (registrations.IsValid(queuedCommand))
            registrations.GetRef(queuedCommand).Label = "stale command";

        Assert.Equal(new SwiftTicket(1, 2), replacement);
        Assert.Equal(new[] { 101, 303, 404 }, registrations.Values.ToArray().Select(value => value.DomainId));
        Assert.Equal(first, registrations.GetTicketAt(0));
        Assert.Equal(moved, registrations.GetTicketAt(1));
        Assert.Equal("updated after dense move", registrations.GetRef(moved).Label);
        Assert.Equal("replacement", registrations.GetRef(replacement).Label);
        Assert.False(registrations.TryGet(queuedCommand, out Registration discarded));
        Assert.Equal(default(Registration), discarded);

        registrations.Clear();
        SwiftTicket next = registrations.Add(new Registration(505, "next session"));
        Assert.False(registrations.IsValid(first));
        Assert.False(registrations.IsValid(replacement));
        Assert.False(registrations.IsValid(moved));
        Assert.Equal(505, registrations.GetRef(next).DomainId);
    }

    [Fact]
    public void AgentTicks_SpawnDespawnAndDenseUpdatesHaveExactReplayableResults()
    {
        AgentTrace first = RunAgentTicks();
        AgentTrace replay = RunAgentTicks();

        Assert.Equal(new[] { 101, 303, 404 }, first.DomainIds);
        Assert.Equal(new[] { 6, 109, 3 }, first.Positions);
        Assert.Equal(new[] { new SwiftTicket(0, 1), new SwiftTicket(2, 1), new SwiftTicket(1, 2) }, first.Tickets);
        Assert.Equal(new[] { 2, 9, 103, 4, 106, -1, 6, 109, 3 }, first.TickPositions);
        Assert.Equal(first.DomainIds, replay.DomainIds);
        Assert.Equal(first.Positions, replay.Positions);
        Assert.Equal(first.Tickets, replay.Tickets);
        Assert.Equal(first.TickPositions, replay.TickPositions);
    }

    [Fact]
    public void Churn_AgreesWithIndependentLogicalMembershipAndValueModel()
    {
        var map = new SwiftDenseTicketMap<int>(0);
        var logical = new Dictionary<SwiftTicket, int>();
        var historicalTickets = new List<SwiftTicket>();
        uint random = 0x051C4B5D;

        for (int step = 0; step < 2000; step++)
        {
            uint operation = Next(ref random) % 11;
            if (operation < 5)
            {
                int value = (int)(Next(ref random) & 65535);
                SwiftTicket ticket = map.Add(value);
                Assert.True(logical.TryAdd(ticket, value));
                historicalTickets.Add(ticket);
            }
            else if (operation == 10)
            {
                map.Clear();
                logical.Clear();
            }
            else if (historicalTickets.Count != 0)
            {
                SwiftTicket ticket = historicalTickets[(int)(Next(ref random) % (uint)historicalTickets.Count)];
                Assert.Equal(logical.Remove(ticket), map.Remove(ticket));
            }

            Assert.Equal(logical.Count, map.Count);
            var visited = new HashSet<SwiftTicket>();
            for (int denseIndex = 0; denseIndex < map.Count; denseIndex++)
            {
                SwiftTicket ticket = map.GetTicketAt(denseIndex);
                Assert.True(visited.Add(ticket));
                Assert.True(logical.TryGetValue(ticket, out int expected));
                Assert.Equal(expected, map.Values[denseIndex]);
                Assert.True(map.TryGet(ticket, out int actual));
                Assert.Equal(expected, actual);
            }
            Assert.True(visited.SetEquals(logical.Keys));
        }
    }

    private static AgentTrace RunAgentTicks()
    {
        var agents = new SwiftDenseTicketMap<AgentRecord>();
        agents.Add(new AgentRecord(101, 0, 2));
        SwiftTicket despawned = agents.Add(new AgentRecord(202, 10, -1));
        agents.Add(new AgentRecord(303, 100, 3));
        var tickPositions = new List<int>();

        AdvanceTick(agents, tickPositions);
        agents.Remove(despawned);
        SwiftTicket spawned = agents.Add(new AgentRecord(404, -5, 4));
        Assert.Equal(new SwiftTicket(1, 2), spawned);
        Assert.False(agents.TryGet(despawned, out _));
        AdvanceTick(agents, tickPositions);
        AdvanceTick(agents, tickPositions);

        AgentRecord[] values = agents.Values.ToArray();
        SwiftTicket[] tickets = Enumerable.Range(0, agents.Count).Select(agents.GetTicketAt).ToArray();
        return new AgentTrace(values.Select(value => value.DomainId).ToArray(),
            values.Select(value => value.Position).ToArray(), tickets, tickPositions.ToArray());
    }

    private static void AdvanceTick(SwiftDenseTicketMap<AgentRecord> agents, List<int> tickPositions)
    {
        Span<AgentRecord> values = agents.Values;
        for (int i = 0; i < values.Length; i++)
        {
            values[i].Position += values[i].Velocity;
            tickPositions.Add(values[i].Position);
        }
    }

    private static uint Next(ref uint value)
    {
        value ^= value << 13;
        value ^= value >> 17;
        value ^= value << 5;
        return value;
    }

    private struct Registration
    {
        public int DomainId;
        public string Label;
        public Registration(int domainId, string label) { DomainId = domainId; Label = label; }
    }

    private struct AgentRecord
    {
        public int DomainId;
        public int Position;
        public int Velocity;
        public AgentRecord(int domainId, int position, int velocity)
        { DomainId = domainId; Position = position; Velocity = velocity; }
    }

    private sealed record AgentTrace(int[] DomainIds, int[] Positions, SwiftTicket[] Tickets, int[] TickPositions);
}
