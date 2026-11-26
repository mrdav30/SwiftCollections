using System;
using System.Linq;
using System.Text.Json;
using Xunit;

#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
using MemoryPack;
#endif

namespace SwiftCollections.Tests;

public class SwiftDenseTicketMapStateTests
{
    [Fact]
    public void StateConstructor_RetainsArraysWithoutValidatingOrCopying()
    {
        int[] values = [101];
        int[] slots = [0];
        uint[] generations = [1];
        int[] free = [0]; // Deliberately overlaps: the DTO itself is not a validating owner.

        var state = new SwiftDenseTicketMapState<int>(values, slots, generations, free);

        Assert.Same(values, state.DenseValues);
        Assert.Same(slots, state.DenseSlots);
        Assert.Same(generations, state.SlotGenerations);
        Assert.Same(free, state.FreeSlots);
        values[0] = 202;
        Assert.Equal(202, state.DenseValues[0]);
    }

    [Fact]
    public void StateConstructor_NormalizesNullArraysWhileDefaultStructRemainsNull()
    {
        var state = new SwiftDenseTicketMapState<int>(null, null, null, null);
        Assert.Empty(state.DenseValues);
        Assert.Empty(state.DenseSlots);
        Assert.Empty(state.SlotGenerations);
        Assert.Empty(state.FreeSlots);

        var uninitialized = default(SwiftDenseTicketMapState<int>);
        Assert.Null(uninitialized.DenseValues);
        Assert.Null(uninitialized.DenseSlots);
        Assert.Null(uninitialized.SlotGenerations);
        Assert.Null(uninitialized.FreeSlots);
    }

    [Fact]
    public void OwnerConstructor_AcceptsDefaultAndNormalizedEmptyState()
    {
        var fromDefault = new SwiftDenseTicketMap<int>(default(SwiftDenseTicketMapState<int>));
        var fromConstructor = new SwiftDenseTicketMap<int>(new SwiftDenseTicketMapState<int>(null, null, null, null));

        Assert.Empty(fromDefault);
        Assert.Empty(fromConstructor);
        Assert.Empty(fromDefault.State.DenseValues);
        Assert.Empty(fromDefault.State.DenseSlots);
        Assert.Empty(fromDefault.State.SlotGenerations);
        Assert.Empty(fromDefault.State.FreeSlots);
        Assert.Equal(new SwiftTicket(0, 1), fromDefault.Add(101));
        Assert.Equal(new SwiftTicket(0, 1), fromConstructor.Add(101));
    }

    [Theory]
    [InlineData("values-longer")]
    [InlineData("slots-longer")]
    [InlineData("missing-generations")]
    [InlineData("negative-live")]
    [InlineData("out-of-range-live")]
    [InlineData("duplicate-live")]
    [InlineData("retired-live")]
    [InlineData("negative-free")]
    [InlineData("out-of-range-free")]
    [InlineData("duplicate-free")]
    [InlineData("live-free-overlap")]
    [InlineData("retired-free")]
    [InlineData("unowned-generation")]
    [InlineData("partially-unowned-generation")]
    public void OwnerConstructor_RejectsMalformedPartitionsWithStateArgument(string scenario)
    {
        var state = scenario switch
        {
            "values-longer" => new SwiftDenseTicketMapState<int>([101], [], [], []),
            "slots-longer" => new SwiftDenseTicketMapState<int>([], [0], [1], []),
            "missing-generations" => new SwiftDenseTicketMapState<int>([101], [0], [], []),
            "negative-live" => new SwiftDenseTicketMapState<int>([101], [-1], [1], []),
            "out-of-range-live" => new SwiftDenseTicketMapState<int>([101], [1], [1], []),
            "duplicate-live" => new SwiftDenseTicketMapState<int>([101, 202], [0, 0], [1], []),
            "retired-live" => new SwiftDenseTicketMapState<int>([101], [0], [0], []),
            "negative-free" => new SwiftDenseTicketMapState<int>([], [], [1], [-1]),
            "out-of-range-free" => new SwiftDenseTicketMapState<int>([], [], [1], [1]),
            "duplicate-free" => new SwiftDenseTicketMapState<int>([], [], [1], [0, 0]),
            "live-free-overlap" => new SwiftDenseTicketMapState<int>([101], [0], [1], [0]),
            "retired-free" => new SwiftDenseTicketMapState<int>([], [], [0], [0]),
            "unowned-generation" => new SwiftDenseTicketMapState<int>([], [], [1], []),
            "partially-unowned-generation" => new SwiftDenseTicketMapState<int>([101], [0], [1, 1], []),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };

        var error = Assert.Throws<ArgumentException>(() => new SwiftDenseTicketMap<int>(state));
        Assert.Equal("state", error.ParamName);
    }

    [Fact]
    public void OwnerConstructor_CopiesEveryInputArrayBeforeCallerMutation()
    {
        int[] values = [101, 303];
        int[] slots = [0, 2];
        uint[] generations = [1, 2, 1];
        int[] free = [1];
        var state = new SwiftDenseTicketMapState<int>(values, slots, generations, free);
        var map = new SwiftDenseTicketMap<int>(state);

        values[0] = 999;
        slots[0] = 2;
        generations[0] = 0;
        free[0] = -1;

        Assert.Equal([101, 303], map.Values.ToArray());
        Assert.Equal(101, map.GetRef(new SwiftTicket(0, 1)));
        Assert.Equal(new SwiftTicket(1, 2), map.Add(202));
        Assert.Equal([101, 303, 202], map.Values.ToArray());
    }

    [Fact]
    public void StateGetter_ReturnsIndependentArraysAndPreservesReferencePayloadSharing()
    {
        var payload = new Payload { Value = 101 };
        var map = new SwiftDenseTicketMap<Payload>();
        var live = map.Add(payload);
        var removed = map.Add(new Payload { Value = 202 });
        map.Remove(removed);
        var first = map.State;
        var second = map.State;

        Assert.NotSame(first.DenseValues, second.DenseValues);
        Assert.NotSame(first.DenseSlots, second.DenseSlots);
        Assert.NotSame(first.SlotGenerations, second.SlotGenerations);
        Assert.NotSame(first.FreeSlots, second.FreeSlots);
        Assert.Same(payload, first.DenseValues[0]);

        var restored = new SwiftDenseTicketMap<Payload>(first);
        first.DenseValues[0] = new Payload { Value = 999 };
        first.DenseSlots[0] = -1;
        first.SlotGenerations[0] = 0;
        first.FreeSlots[0] = -1;
        payload.Value = 303;

        Assert.Same(payload, map.GetRef(live));
        Assert.Same(payload, restored.GetRef(live));
        Assert.Equal(303, restored.GetRef(live).Value);
        Assert.Equal(new SwiftTicket(1, 2), map.Add(null));
        Assert.Equal(new SwiftTicket(1, 2), restored.Add(null));
    }

    [Fact]
    public void Snapshot_ReconstructsEveryPartitionAndExactFutureIssuanceOrder()
    {
        var map = new SwiftDenseTicketMap<int>(PartitionedState());
        var snapshot = map.State;
        var restored = new SwiftDenseTicketMap<int>(snapshot);

        Assert.Equal([101, 303], snapshot.DenseValues);
        Assert.Equal([0, 2], snapshot.DenseSlots);
        Assert.Equal(new uint[] { 1, 2, 1, 0, 3 }, snapshot.SlotGenerations);
        Assert.Equal([1, 4], snapshot.FreeSlots);
        ContinueAndAssert(map);
        ContinueAndAssert(restored);
    }

    [Fact]
    public void LastGeneration_FreeSlotIsIssuedOnceThenRetiredOnRemoval()
    {
        var map = new SwiftDenseTicketMap<int>(new SwiftDenseTicketMapState<int>([], [], [uint.MaxValue], [0]));

        Assert.False(map.IsValid(new SwiftTicket(0, uint.MaxValue)));
        Assert.False(map.TryGet(new SwiftTicket(0, uint.MaxValue), out int unissuedValue));
        Assert.Equal(0, unissuedValue);
        var final = map.Add(101);
        Assert.Equal(new SwiftTicket(0, uint.MaxValue), final);
        Assert.True(map.Remove(final));
        Assert.False(map.Remove(final));
        Assert.False(map.TryGet(final, out int missing));
        Assert.Equal(0, missing);
        Assert.Equal(new uint[] { 0 }, map.State.SlotGenerations);
        Assert.Empty(map.State.FreeSlots);
        Assert.Equal(new SwiftTicket(1, 1), map.Add(202));
    }

    [Fact]
    public void Clear_RetiresTerminalSlotsAndPreservesOlderFreeOrder()
    {
        var map = new SwiftDenseTicketMap<int>(new SwiftDenseTicketMapState<int>([101, 303], [0, 2], [1, 2, uint.MaxValue, 0], [1]));

        map.Clear();
        Assert.Empty(map);
        Assert.Equal([1, 0], map.State.FreeSlots);
        Assert.Equal(new uint[] { 2, 2, 0, 0 }, map.State.SlotGenerations);
        Assert.Equal(new SwiftTicket(0, 2), map.Add(404));
        Assert.Equal(new SwiftTicket(1, 2), map.Add(505));
        Assert.Equal(new SwiftTicket(4, 1), map.Add(606));
        Assert.False(map.IsValid(new SwiftTicket(2, uint.MaxValue)));
    }

    [Fact]
    public void RetiredOnlyHistory_RemainsReservedAndExhaustionRejectsBeforeGrowth()
    {
        var map = new SwiftDenseTicketMap<int>(new SwiftDenseTicketMapState<int>(null, null, [0], null));
        int denseCapacity = map.DenseCapacity;
        int slotCapacity = map.SlotCapacity;

        Assert.False(map.IsValid(new SwiftTicket(0, 0)));
        Assert.False(map.IsValid(new SwiftTicket(0, uint.MaxValue)));
        Assert.False(map.TryGet(new SwiftTicket(0, 0), out int retiredValue));
        Assert.Equal(0, retiredValue);
        Assert.Throws<InvalidOperationException>(() => map.EnsureCapacity(1 << 30));
        Assert.Equal(denseCapacity, map.DenseCapacity);
        Assert.Equal(slotCapacity, map.SlotCapacity);
        Assert.Empty(map);
        Assert.Equal(new uint[] { 0 }, map.State.SlotGenerations);
        Assert.Equal(new SwiftTicket(1, 1), map.Add(101));
    }

    [Fact]
    public void Add_RestoredFullDenseStorageGrowsWithoutGrowingReusableSlotStorage()
    {
        var map = new SwiftDenseTicketMap<int>(new SwiftDenseTicketMapState<int>([10], [0], [1, 2], [1]));
        var enumerator = map.GetEnumerator();
        Assert.Equal(1, map.DenseCapacity);
        Assert.Equal(2, map.SlotCapacity);

        Assert.Equal(new SwiftTicket(1, 2), map.Add(20));

        Assert.Equal([10, 20], map.Values.ToArray());
        Assert.Equal(8, map.DenseCapacity);
        Assert.Equal(2, map.SlotCapacity);
        Assert.Equal(10, map.GetRef(new SwiftTicket(0, 1)));
        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
        Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Add_ReservedUnissuedOrFreeSlotPreservesBothCapacities(bool reuse)
    {
        var map = new SwiftDenseTicketMap<int>(8);
        for (int i = 0; i < (reuse ? 8 : 7); i++) map.Add(i);
        if (reuse) Assert.True(map.Remove(new SwiftTicket(3, 1)));
        var enumerator = map.GetEnumerator();

        Assert.Equal(reuse ? new SwiftTicket(3, 2) : new SwiftTicket(7, 1), map.Add(99));

        int[] expected = reuse ? [0, 1, 2, 7, 4, 5, 6, 99] : [0, 1, 2, 3, 4, 5, 6, 99];
        Assert.Equal(expected, map.Values.ToArray());
        Assert.Equal(8, map.DenseCapacity);
        Assert.Equal(8, map.SlotCapacity);
        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
        Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
    }

    [Fact]
    public void Retirement_ConsumesUsableReservationEvenWhenLiveCountReturnsToSameSize()
    {
        var state = new SwiftDenseTicketMapState<int>(Enumerable.Range(0, 16).ToArray(), Enumerable.Range(0, 16).ToArray(), Enumerable.Repeat(uint.MaxValue, 16).ToArray(), []);
        var map = new SwiftDenseTicketMap<int>(state);
        map.EnsureCapacity(16);
        int initialSlotCapacity = map.SlotCapacity;

        Assert.True(map.Remove(new SwiftTicket(0, uint.MaxValue)));
        var enumerator = map.GetEnumerator();
        Assert.Equal(new SwiftTicket(16, 1), map.Add(101));
        Assert.Equal(16, map.Count);
        Assert.Equal(16, initialSlotCapacity);
        Assert.Equal(16, map.DenseCapacity);
        Assert.Equal(32, map.SlotCapacity);
        Assert.Equal([15, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 101], map.Values.ToArray());
        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
        Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
        for (int slot = 1; slot < 16; slot++)
            Assert.Equal(slot, map.GetRef(new SwiftTicket(slot, uint.MaxValue)));
    }

    [Fact]
    public void Restore_RewindsIdentityAndDoesNotClaimForeignOwnerProtection()
    {
        var original = new SwiftDenseTicketMap<int>();
        var old = original.Add(101);
        var snapshot = original.State;
        original.Remove(old);
        var future = original.Add(202);

        var rewound = new SwiftDenseTicketMap<int>(snapshot);
        Assert.Equal(101, rewound.GetRef(old));
        Assert.False(rewound.IsValid(future));
        Assert.False(original.IsValid(old));
        var foreign = new SwiftDenseTicketMap<int>();
        Assert.Equal(old, foreign.Add(303));
        Assert.Equal(303, foreign.GetRef(old));
    }

    [Fact]
    public void JsonOwnerAndRawState_RoundTripsPreserveExactContinuationInBothVariants()
    {
        var map = new SwiftDenseTicketMap<int>(PartitionedState());

        string ownerJson = JsonSerializer.Serialize(map);
        using var document = JsonDocument.Parse(ownerJson);
        Assert.Equal(["State"], document.RootElement.EnumerateObject().Select(property => property.Name).ToArray());
        var restored = JsonSerializer.Deserialize<SwiftDenseTicketMap<int>>(ownerJson);
        var raw = JsonSerializer.Deserialize<SwiftDenseTicketMapState<int>>(JsonSerializer.Serialize(map.State));

        ContinueAndAssert(restored);
        ContinueAndAssert(new SwiftDenseTicketMap<int>(raw));
    }

    [Fact]
    public void JsonOwner_PropagatesStructuralStateFailure()
    {
        var error = Assert.Throws<ArgumentException>(() => JsonSerializer.Deserialize<SwiftDenseTicketMap<int>>("{\"State\":{\"SlotGenerations\":[1]}}"));
        Assert.Equal("state", error.ParamName);
    }

#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
    [Fact]
    public void MemoryPackOwnerAndRawState_RoundTripsPreserveExactContinuation()
    {
        var map = new SwiftDenseTicketMap<int>(PartitionedState());

        var restored = MemoryPackSerializer.Deserialize<SwiftDenseTicketMap<int>>(MemoryPackSerializer.Serialize(map));
        var state = MemoryPackSerializer.Deserialize<SwiftDenseTicketMapState<int>>(MemoryPackSerializer.Serialize(map.State));

        ContinueAndAssert(restored);
        ContinueAndAssert(new SwiftDenseTicketMap<int>(state));
    }
#endif

    private static SwiftDenseTicketMapState<int> PartitionedState() => new([101, 303], [0, 2], [1, 2, 1, 0, 3], [1, 4]);

    private static void ContinueAndAssert(SwiftDenseTicketMap<int> map)
    {
        Assert.Equal(new SwiftTicket(4, 3), map.Add(404));
        Assert.Equal(new SwiftTicket(1, 2), map.Add(202));
        Assert.True(map.Remove(new SwiftTicket(0, 1)));
        Assert.Equal(new SwiftTicket(0, 2), map.Add(505));
        Assert.Equal([202, 303, 404, 505], map.Values.ToArray());
        Assert.Equal([1, 2, 4, 0], map.State.DenseSlots);
        Assert.Empty(map.State.FreeSlots);
    }

    private sealed class Payload
    {
        public int Value;
    }
}
