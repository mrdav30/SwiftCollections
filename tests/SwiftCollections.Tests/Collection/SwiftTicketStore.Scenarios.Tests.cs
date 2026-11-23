using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Xunit;

namespace SwiftCollections.Tests;

public class SwiftTicketStoreScenarioTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IndependentComponents_JoinByTicketAndContinueAfterCoordinatedRawRestore(bool restore)
    {
        var agents = new SwiftDenseTicketMap<int>(8);
        SwiftTicket a = agents.Add(101);
        SwiftTicket b = agents.Add(202);
        SwiftTicket c = agents.Add(303);
        var positions = new SwiftTicketStore<int>();
        var velocities = new SwiftTicketStore<int>();
        var health = new SwiftTicketStore<int>();
        positions.Add(a, 0);
        positions.Add(b, 10);
        positions.Add(c, 100);
        velocities.Add(c, 3);
        velocities.Add(a, 2);
        velocities.Add(b, -1);
        health.Add(a, 100);
        health.Add(c, 50);

        TickPositions(positions, velocities);
        AssertAssociations(positions, [a, b, c], [2, 9, 103]);
        Assert.True(health.Remove(a));
        health.Add(a, 80);
        AssertAssociations(health, [c, a], [50, 80]);
        Assert.True(agents.IsValid(a));
        Assert.Equal(2, positions.GetRef(a));
        Assert.Equal(2, velocities.GetRef(a));

        if (restore)
        {
            var agentState = agents.State;
            var positionState = positions.State;
            var velocityState = velocities.State;
            var healthState = health.State;
            var restoredAgents = new SwiftDenseTicketMap<int>(agentState);
            var restoredPositions = new SwiftTicketStore<int>(positionState);
            var restoredVelocities = new SwiftTicketStore<int>(velocityState);
            var restoredHealth = new SwiftTicketStore<int>(healthState);
            foreach (var componentStore in new[] { restoredPositions, restoredVelocities, restoredHealth })
                for (int i = 0; i < componentStore.Count; i++)
                    Assert.True(restoredAgents.IsValid(componentStore.GetTicketAt(i)));
            agents = restoredAgents;
            positions = restoredPositions;
            velocities = restoredVelocities;
            health = restoredHealth;
        }

        Assert.True(positions.Remove(b));
        Assert.True(velocities.Remove(b));
        Assert.False(health.Remove(b));
        Assert.True(agents.Remove(b));
        SwiftTicket d = agents.Add(404);
        Assert.Equal(new SwiftTicket(1, 2), d);
        positions.Add(d, -5);
        velocities.Add(d, 4);
        health.Add(d, 25);
        Assert.False(positions.Remove(b));
        Assert.False(velocities.Remove(b));
        Assert.False(health.Remove(b));
        TickPositions(positions, velocities);
        TickPositions(positions, velocities);

        AssertAssociations(positions, [a, c, d], [6, 109, 3]);
        AssertAssociations(velocities, [c, a, d], [3, 2, 4]);
        AssertAssociations(health, [c, a, d], [50, 80, 25]);
        Assert.Equal(new[] { 101, 303, 404 }, agents.Values.ToArray());
        Assert.Equal(101, agents.GetRef(a));
        Assert.Equal(303, agents.GetRef(c));
        Assert.Equal(404, agents.GetRef(d));
    }

    [Fact]
    public void OrphanedComponent_RemainsLocalUntilExplicitCleanupBeforeReplacement()
    {
        var agents = new SwiftDenseTicketMap<int>();
        SwiftTicket old = agents.Add(101);
        var components = new SwiftTicketStore<int>();
        components.Add(old, 11);

        Assert.True(agents.Remove(old));
        SwiftTicket replacement = agents.Add(202);

        Assert.False(agents.IsValid(old));
        Assert.True(agents.IsValid(replacement));
        Assert.Equal(old.Index, replacement.Index);
        Assert.True(components.ContainsKey(old));
        Assert.False(components.TryAdd(replacement, 22));
        Assert.False(components.Remove(replacement));
        Assert.Throws<KeyNotFoundException>(() => components.GetRef(replacement));
        Assert.Equal(11, components.GetRef(old));
        Assert.True(components.Remove(old));
        Assert.True(agents.IsValid(replacement));
        Assert.True(components.TryAdd(replacement, 22));
        Assert.False(components.TryGet(old, out int stale));
        Assert.Equal(0, stale);
        Assert.False(components.Remove(old));
        Assert.Throws<KeyNotFoundException>(() => components.GetRef(old));
        Assert.Equal(22, components.GetRef(replacement));
    }

    [Fact]
    public void LocalMembership_DoesNotProveIssuerLivenessOrWorldIdentity()
    {
        var firstWorld = new SwiftDenseTicketMap<int>();
        var secondWorld = new SwiftDenseTicketMap<int>();
        SwiftTicket first = firstWorld.Add(101);
        SwiftTicket foreign = secondWorld.Add(202);
        var components = new SwiftTicketStore<int>();
        components.Add(first, 7);

        Assert.Equal(first, foreign);
        Assert.True(secondWorld.IsValid(foreign));
        Assert.True(components.TryGet(foreign, out int aliased));
        Assert.Equal(7, aliased);
        Assert.True(firstWorld.Remove(first));
        Assert.True(components.ContainsKey(first));
        var bare = new SwiftTicketStore<int>();
        Assert.True(bare.TryAdd(first, 9));
        Assert.False(firstWorld.IsValid(first));
        var guarded = new SwiftTicketStore<int>();
        if (firstWorld.IsValid(first))
            guarded.Add(first, 9);
        Assert.Empty(guarded);
    }

    [Fact]
    public void ComponentRemoval_DoesNotGiveTheAgentTicketAComponentIncarnation()
    {
        var agents = new SwiftDenseTicketMap<int>();
        SwiftTicket agent = agents.Add(101);
        var health = new SwiftTicketStore<int>();
        var position = new SwiftTicketStore<int>();
        health.Add(agent, 100);
        position.Add(agent, 12);
        SwiftTicket delayedHealthCommand = agent;

        Assert.True(health.Remove(agent));
        health.Add(agent, 80);
        health.GetRef(delayedHealthCommand) -= 10;

        Assert.True(agents.IsValid(agent));
        Assert.Equal(101, agents.GetRef(agent));
        Assert.Equal(12, position.GetRef(agent));
        Assert.Equal(70, health.GetRef(agent));
    }

    [Fact]
    public void LegacyZeroAndWrappedGenerations_RemainValidExternalKeyData()
    {
        var fresh = new SwiftTicketMap<int>();
        SwiftTicket initial = fresh.Add(101);
        Assert.Equal(default, initial);
        var store = new SwiftTicketStore<int>();
        store.Add(initial, 11);
        Assert.Equal(11, store.GetRef(default));

        var legacy = new SwiftTicketMap<int>(new SwiftTicketMapState<int>(
            [202], [true], [uint.MaxValue], [], 1));
        var terminal = new SwiftTicket(0, uint.MaxValue);
        Assert.True(legacy.IsValid(terminal));
        Assert.True(store.Remove(initial));
        store.Add(terminal, 22);
        Assert.True(store.Remove(terminal));
        Assert.True(legacy.Remove(terminal));
        SwiftTicket wrapped = legacy.Add(303);

        Assert.Equal(default, wrapped);
        Assert.True(store.TryAdd(wrapped, 33));
        Assert.Equal(33, store.GetRef(wrapped));
        Assert.False(store.ContainsKey(terminal));
        Assert.True(legacy.IsValid(wrapped));
    }

    [Fact]
    public void DenseIssuerRetirement_DoesNotBecomeAStoreGenerationPolicy()
    {
        var agents = new SwiftDenseTicketMap<int>(new SwiftDenseTicketMapState<int>(
            [101], [0], [uint.MaxValue], []));
        var terminal = new SwiftTicket(0, uint.MaxValue);
        var store = new SwiftTicketStore<int>();
        store.Add(terminal, 11);

        Assert.True(agents.Remove(terminal));
        SwiftTicket next = agents.Add(202);

        Assert.Equal(new SwiftTicket(1, 1), next);
        Assert.True(store.ContainsKey(terminal));
        Assert.True(store.Remove(terminal));
        Assert.True(store.TryAdd(default, 22));
        Assert.False(agents.IsValid(default));
        Assert.Equal(22, store.GetRef(default));
    }

    [Fact]
    public void IndividuallyValidSnapshots_CanRequireAnIssuerCrossCheckBeforePublication()
    {
        var agents = new SwiftDenseTicketMap<int>();
        SwiftTicket old = agents.Add(101);
        var oldIssuerState = agents.State;
        agents.Remove(old);
        SwiftTicket replacement = agents.Add(202);
        var components = new SwiftTicketStore<int>();
        components.Add(replacement, 7);

        var restoredIssuer = new SwiftDenseTicketMap<int>(oldIssuerState);
        var restoredComponents = new SwiftTicketStore<int>(components.State);

        Assert.True(restoredComponents.ContainsKey(replacement));
        Assert.True(restoredIssuer.IsValid(old));
        Assert.False(restoredIssuer.IsValid(restoredComponents.GetTicketAt(0)));
    }

    [Fact]
    public void SeededMutations_MatchIndependentLogicalMembershipAndPayloadModel()
    {
        var store = new SwiftTicketStore<int>(0, 0);
        var model = new Dictionary<int, (uint Generation, int Value)>();
        uint random = 0x0C013A55;

        for (int step = 0; step < 3000; step++)
        {
            random ^= random << 13;
            random ^= random >> 17;
            random ^= random << 5;
            int index = (int)(random % 129);
            uint generation = ((random >> 16) % 3) switch { 0 => 0, 1 => 1, _ => uint.MaxValue };
            var ticket = new SwiftTicket(index, generation);
            bool present = model.TryGetValue(index, out var entry);
            bool matches = present && entry.Generation == generation;

            switch (random % 7)
            {
                case 0:
                case 1:
                    Assert.Equal(!present, store.TryAdd(ticket, step));
                    if (!present)
                        model.Add(index, (generation, step));
                    break;
                case 2:
                    Assert.Equal(matches, store.Remove(ticket));
                    if (matches)
                        model.Remove(index);
                    break;
                case 3:
                    Assert.Equal(matches, store.ContainsKey(ticket));
                    Assert.Equal(matches, store.TryGet(ticket, out int value));
                    Assert.Equal(matches ? entry.Value : 0, value);
                    break;
                case 4:
                    if (matches)
                    {
                        store.GetRef(ticket) += 3;
                        model[index] = (generation, entry.Value + 3);
                    }
                    break;
                case 5:
                    store = new SwiftTicketStore<int>(store.State);
                    break;
                case 6:
                    if ((step & 63) == 0)
                    {
                        store.Clear();
                        model.Clear();
                    }
                    break;
            }

            Assert.Equal(model.Count, store.Count);
            var seen = new HashSet<int>();
            for (int i = 0; i < store.Count; i++)
            {
                SwiftTicket actual = store.GetTicketAt(i);
                Assert.True(seen.Add(actual.Index));
                Assert.True(model.TryGetValue(actual.Index, out var expected));
                Assert.Equal(expected.Generation, actual.Generation);
                Assert.Equal(expected.Value, store.Values[i]);
            }
        }
    }

    [Fact]
    public void ReservedTypedOperations_AllocateNoBytesAndPreserveAllAssociations()
    {
        var store = new SwiftTicketStore<int>(512, 128);
        var keys = new SwiftTicket[64];
        for (int i = 0; i < keys.Length; i++)
        {
            keys[i] = new SwiftTicket(i * 4, i % 2 == 0 ? 0 : uint.MaxValue);
            store.Add(keys[i], i);
        }
        ExerciseReservedStore(store, keys, 256);
        _ = GC.GetAllocatedBytesForCurrentThread();

        long before = GC.GetAllocatedBytesForCurrentThread();
        long checksum = ExerciseReservedStore(store, keys, 512);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
        Assert.True(checksum > 0);
        Assert.Equal(keys.Length, store.Count);
        for (int i = 0; i < keys.Length; i++)
            Assert.True(store.ContainsKey(keys[i]));
    }

    [Fact]
    public void JoinedPositionVelocityAndHealthTick_AllocatesNoReservedBatchBytes()
    {
        const int count = 128;
        var agents = new SwiftDenseTicketMap<int>(count);
        var positions = new SwiftTicketStore<int>(count, count);
        var velocities = new SwiftTicketStore<int>(count, count);
        var health = new SwiftTicketStore<int>(count, count / 4);
        var keys = new SwiftTicket[count];
        for (int i = 0; i < count; i++)
        {
            keys[i] = agents.Add(1000 + i);
            positions.Add(keys[i], i * 10);
            if (i % 4 == 0)
                health.Add(keys[i], 100);
        }
        for (int i = count - 1; i >= 0; i--)
            velocities.Add(keys[i], i % 5 - 2);
        ExerciseJoinedStores(positions, velocities, health, keys[0], 128);
        _ = GC.GetAllocatedBytesForCurrentThread();

        long before = GC.GetAllocatedBytesForCurrentThread();
        long checksum = ExerciseJoinedStores(positions, velocities, health, keys[0], 256);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
        Assert.True(checksum > 0);
        Assert.Equal(count, positions.Count);
        Assert.Equal(count, velocities.Count);
        Assert.Equal(count / 4, health.Count);
        for (int i = 0; i < count; i++)
            Assert.Equal(i * 10 + (i % 5 - 2) * 384, positions.GetRef(keys[i]));
        Assert.True(agents.IsValid(keys[0]));
    }

    [Fact]
    public void InterfaceEnumeration_AccountsForItsBoxSeparatelyFromTypedIteration()
    {
        var store = new SwiftTicketStore<int>();
        store.Add(default, 7);
        GetInterfaceEnumerator(store).Dispose();
        _ = GC.GetAllocatedBytesForCurrentThread();

        long before = GC.GetAllocatedBytesForCurrentThread();
        IEnumerator<int> enumerator = GetInterfaceEnumerator(store);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated > 0);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(7, enumerator.Current);
        Assert.False(enumerator.MoveNext());
        enumerator.Dispose();
        GC.KeepAlive(enumerator);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void RemoveAndClear_ReleaseDiscardedReferencesWhileOwnerRemainsAlive(bool clear, bool containingStruct)
    {
        var pair = CreateReleasedStore(clear, containingStruct);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(pair.Target.IsAlive);
        GC.KeepAlive(pair.Owner);
    }

    private static void TickPositions(SwiftTicketStore<int> positions, SwiftTicketStore<int> velocities)
    {
        Span<int> values = positions.Values;
        for (int i = 0; i < values.Length; i++)
            if (velocities.TryGet(positions.GetTicketAt(i), out int velocity))
                values[i] += velocity;
    }

    private static void AssertAssociations(SwiftTicketStore<int> store, SwiftTicket[] keys, int[] values)
    {
        Assert.Equal(keys.Length, store.Count);
        Assert.Equal(values, store.Values.ToArray());
        for (int i = 0; i < keys.Length; i++)
        {
            Assert.Equal(keys[i], store.GetTicketAt(i));
            Assert.Equal(values[i], store.GetRef(keys[i]));
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long ExerciseReservedStore(SwiftTicketStore<int> store, SwiftTicket[] keys, int iterations)
    {
        long checksum = 0;
        for (int round = 0; round < iterations; round++)
        {
            SwiftTicket ticket = keys[round % keys.Length];
            store.GetRef(ticket)++;
            if (!store.TryGet(ticket, out int value) || !store.ContainsKey(ticket))
                throw new InvalidOperationException("Reserved live lookup failed.");
            checksum += value;
            var stale = new SwiftTicket(ticket.Index, ticket.Generation ^ 0x80000000U);
            if (store.TryAdd(stale, -1) || store.Remove(stale) || store.TryGet(stale, out int missing) || missing != 0)
                throw new InvalidOperationException("Occupied-generation operation mutated membership.");
            store.EnsureCapacity(512, 128);
            foreach (int item in store)
                checksum += item;
            foreach (int item in store.Values)
                checksum += item;
            checksum += store.GetTicketAt(0).Generation;
            if (!store.Remove(ticket) || !store.TryAdd(ticket, value))
                throw new InvalidOperationException("Reserved reinsertion failed.");
            if ((round & 15) == 15)
            {
                store.Clear();
                for (int i = 0; i < keys.Length; i++)
                    store.Add(keys[i], i);
            }
        }
        return checksum;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long ExerciseJoinedStores(SwiftTicketStore<int> positions, SwiftTicketStore<int> velocities,
        SwiftTicketStore<int> health, SwiftTicket churn, int iterations)
    {
        long checksum = 0;
        for (int round = 0; round < iterations; round++)
        {
            Span<int> values = positions.Values;
            for (int i = 0; i < values.Length; i++)
            {
                SwiftTicket key = positions.GetTicketAt(i);
                if (velocities.TryGet(key, out int velocity))
                    values[i] += velocity;
                if (health.TryGet(key, out int hitPoints))
                    checksum += hitPoints;
                checksum += values[i];
            }
            if (!health.Remove(churn) || !health.TryAdd(churn, 100))
                throw new InvalidOperationException("Optional component churn changed agent identity.");
        }
        return checksum;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IEnumerator<int> GetInterfaceEnumerator(SwiftTicketStore<int> store) =>
        ((IEnumerable<int>)store).GetEnumerator();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (object Owner, WeakReference Target) CreateReleasedStore(bool clear, bool containingStruct)
    {
        var payload = new object();
        var weak = new WeakReference(payload);
        if (containingStruct)
        {
            var store = new SwiftTicketStore<ScenarioReferenceValue>();
            store.Add(default, new ScenarioReferenceValue { Resource = new object(), Tag = 101 });
            var moved = new SwiftTicket(7, uint.MaxValue);
            store.Add(moved, new ScenarioReferenceValue { Resource = payload, Tag = 202 });
            store.Remove(default);
            Assert.Same(payload, store.GetRef(moved).Resource);
            Assert.Equal(202, store.GetRef(moved).Tag);
            if (clear)
                store.Clear();
            else
                store.Remove(moved);
            return (store, weak);
        }
        else
        {
            var store = new SwiftTicketStore<object>();
            store.Add(default, new object());
            var moved = new SwiftTicket(7, uint.MaxValue);
            store.Add(moved, payload);
            store.Remove(default);
            Assert.Same(payload, store.GetRef(moved));
            if (clear)
                store.Clear();
            else
                store.Remove(moved);
            return (store, weak);
        }
    }

    private struct ScenarioReferenceValue
    {
        public object Resource;
        public int Tag;
    }
}
