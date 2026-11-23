using System;
using System.Reflection;
using Xunit;

namespace SwiftCollections.Tests;

public class SwiftTicketStoreStateTests
{
    [Fact]
    public void RawState_RetainsArraysWithoutValidatingTheirContents()
    {
        int[] values = [101, 202];
        int[] slots = [3, 3];
        uint[] generations = [0, uint.MaxValue];

        var state = new SwiftTicketStoreState<int>(values, slots, generations);

        Assert.Same(values, state.DenseValues);
        Assert.Same(slots, state.DenseSlots);
        Assert.Same(generations, state.DenseGenerations);
        values[0] = 303;
        slots[1] = -1;
        generations[0] = 7;
        Assert.Equal(303, state.DenseValues[0]);
        Assert.Equal(-1, state.DenseSlots[1]);
        Assert.Equal(7U, state.DenseGenerations[0]);
    }

    [Fact]
    public void RawState_NormalizesConstructorNullsButDefaultFieldsRemainNull()
    {
        var normalized = new SwiftTicketStoreState<int>(null, null, null);
        var uninitialized = default(SwiftTicketStoreState<int>);

        Assert.Empty(normalized.DenseValues);
        Assert.Empty(normalized.DenseSlots);
        Assert.Empty(normalized.DenseGenerations);
        Assert.Null(uninitialized.DenseValues);
        Assert.Null(uninitialized.DenseSlots);
        Assert.Null(uninitialized.DenseGenerations);
    }

    [Fact]
    public void Restore_AcceptsDefaultAndNormalizedEmptyWithoutInventingMembership()
    {
        var fromDefault = new SwiftTicketStore<int>(default(SwiftTicketStoreState<int>));
        var normalized = new SwiftTicketStore<int>(new SwiftTicketStoreState<int>(null, null, null));

        foreach (var store in new[] { fromDefault, normalized })
        {
            Assert.Empty(store);
            Assert.Equal(0, store.SparseCapacity);
            Assert.Equal(0, store.DenseCapacity);
            Assert.Empty(store.State.DenseValues);
            Assert.Empty(store.State.DenseSlots);
            Assert.Empty(store.State.DenseGenerations);
            Assert.False(store.ContainsKey(default));
            Assert.True(store.TryAdd(default, 101));
            Assert.Equal(101, store.GetRef(default));
        }
    }

    [Theory]
    [InlineData("values-only")]
    [InlineData("slots-only")]
    [InlineData("generations-only")]
    [InlineData("missing-generations")]
    [InlineData("missing-slots")]
    [InlineData("negative-index")]
    [InlineData("minimum-index")]
    [InlineData("unsupported-index")]
    [InlineData("maximum-index")]
    [InlineData("duplicate-same-generation")]
    [InlineData("duplicate-different-generation")]
    public void Restore_RejectsMalformedLogicalArraysWithStateParameter(string scenario)
    {
        var state = scenario switch
        {
            "values-only" => new SwiftTicketStoreState<int>([101], null, null),
            "slots-only" => new SwiftTicketStoreState<int>(null, [0], null),
            "generations-only" => new SwiftTicketStoreState<int>(null, null, [0]),
            "missing-generations" => new SwiftTicketStoreState<int>([101], [0], null),
            "missing-slots" => new SwiftTicketStoreState<int>([101], null, [1]),
            "negative-index" => new SwiftTicketStoreState<int>([101], [-1], [1]),
            "minimum-index" => new SwiftTicketStoreState<int>([101], [int.MinValue], [1]),
            "unsupported-index" => new SwiftTicketStoreState<int>([101], [1 << 30], [1]),
            "maximum-index" => new SwiftTicketStoreState<int>([101], [int.MaxValue], [1]),
            "duplicate-same-generation" => new SwiftTicketStoreState<int>([101, 202], [3, 3], [1, 1]),
            "duplicate-different-generation" => new SwiftTicketStoreState<int>([101, 202], [3, 3], [0, uint.MaxValue]),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };

        Assert.Equal("state", Assert.Throws<ArgumentException>(
            () => new SwiftTicketStore<int>(state)).ParamName);
    }

    [Fact]
    public void Restore_RebuildsLookupFromDenseOrderWithoutPersistingSpareCapacity()
    {
        var original = new SwiftTicketStore<int>(128, 64);
        var first = new SwiftTicket(0, 0);
        var middle = new SwiftTicket(8, uint.MaxValue);
        var last = new SwiftTicket(10, 3);
        original.Add(first, 101);
        original.Add(middle, 202);
        original.Add(last, 303);
        original.Remove(middle);

        var state = original.State;
        var restored = new SwiftTicketStore<int>(state);

        Assert.Equal(new[] { 101, 303 }, state.DenseValues);
        Assert.Equal(new[] { 0, 10 }, state.DenseSlots);
        Assert.Equal(new uint[] { 0, 3 }, state.DenseGenerations);
        Assert.Equal(16, restored.SparseCapacity);
        Assert.Equal(8, restored.DenseCapacity);
        Assert.Equal(first, restored.GetTicketAt(0));
        Assert.Equal(last, restored.GetTicketAt(1));
        Assert.Equal(101, restored.GetRef(first));
        Assert.Equal(303, restored.GetRef(last));
        Assert.False(restored.ContainsKey(middle));
        Assert.True(restored.TryAdd(middle, 404));
        Assert.Equal(new[] { 101, 303, 404 }, restored.Values.ToArray());
        Assert.Equal(new[] { 101, 303 }, original.Values.ToArray());
    }

    [Fact]
    public void Restore_RoundsBothAxesWhenLivePopulationExceedsMinimum()
    {
        int[] values = [10, 11, 12, 13, 14, 15, 16, 17, 18];
        int[] slots = [8, 7, 6, 5, 4, 3, 2, 1, 0];
        uint[] generations = [0, uint.MaxValue, 1, 2, 3, 4, 5, 6, 7];
        var store = new SwiftTicketStore<int>(new SwiftTicketStoreState<int>(values, slots, generations));

        Assert.Equal(16, store.SparseCapacity);
        Assert.Equal(16, store.DenseCapacity);
        Assert.Equal(values, store.Values.ToArray());
        for (int i = 0; i < values.Length; i++)
            Assert.Equal(values[i], store.GetRef(new SwiftTicket(slots[i], generations[i])));
    }

    [Fact]
    public void SnapshotAndRestore_CopyArrayStorageButShareReferencedPayloadObjects()
    {
        var payload = new StatePayload { Value = 101 };
        StatePayload[] values = [payload];
        int[] slots = [7];
        uint[] generations = [uint.MaxValue];
        var state = new SwiftTicketStoreState<StatePayload>(values, slots, generations);
        var store = new SwiftTicketStore<StatePayload>(state);
        var key = new SwiftTicket(7, uint.MaxValue);
        var first = store.State;
        var second = store.State;
        var restored = new SwiftTicketStore<StatePayload>(first);

        Assert.NotSame(values, first.DenseValues);
        Assert.NotSame(slots, first.DenseSlots);
        Assert.NotSame(generations, first.DenseGenerations);
        Assert.NotSame(first.DenseValues, second.DenseValues);
        Assert.NotSame(first.DenseSlots, second.DenseSlots);
        Assert.NotSame(first.DenseGenerations, second.DenseGenerations);
        Assert.Same(payload, first.DenseValues[0]);

        values[0] = null;
        slots[0] = -1;
        generations[0] = 0;
        first.DenseValues[0] = new StatePayload { Value = 999 };
        first.DenseSlots[0] = 0;
        first.DenseGenerations[0] = 0;
        payload.Value = 303;

        Assert.Same(payload, store.GetRef(key));
        Assert.Same(payload, restored.GetRef(key));
        Assert.Equal(303, restored.GetRef(key).Value);
        Assert.Equal(key, restored.GetTicketAt(0));
        Assert.Same(payload, second.DenseValues[0]);
        Assert.Equal(303, second.DenseValues[0].Value);
    }

    [Fact]
    public void Snapshot_PreservesEnumerationAndExistingBorrowedStorage()
    {
        var store = new SwiftTicketStore<int>();
        var key = new SwiftTicket(3, 0);
        store.Add(key, 10);
        ref int borrowed = ref store.GetRef(key);
        var enumerator = store.GetEnumerator();

        var state = store.State;
        borrowed = 20;
        state.DenseValues[0] = 999;
        state.DenseSlots[0] = -1;
        state.DenseGenerations[0] = 7;

        Assert.True(enumerator.MoveNext());
        Assert.Equal(20, enumerator.Current);
        Assert.Equal(20, store.GetRef(key));
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void LogicalLengthValidation_EnforcesSupportedCountWithoutHugeAllocation()
    {
        const int maximum = 1 << 30;
        MethodInfo validate = typeof(SwiftTicketStore<int>).GetMethod(
            "ValidateStateLengths", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(validate);

        Assert.Null(validate.Invoke(null, [maximum, maximum, maximum]));
        var failure = Assert.Throws<TargetInvocationException>(
            () => validate.Invoke(null, [maximum + 1, maximum + 1, maximum + 1]));

        Assert.Equal("state", Assert.IsType<ArgumentException>(failure.InnerException).ParamName);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0, 1, 8)]
    [InlineData(8, 0, 8)]
    [InlineData(8, 8, 8)]
    [InlineData(8, 9, 16)]
    [InlineData(8, 33, 64)]
    [InlineData(1 << 29, (1 << 29) + 1, 1 << 30)]
    [InlineData(0, 1 << 30, 1 << 30)]
    [InlineData(1 << 30, 1 << 30, 1 << 30)]
    public void SharedGrowthPolicy_PreservesNoOpDoublingAndInclusiveMaximum(int current, int required, int expected)
    {
        Type policy = typeof(SwiftTicketStore<int>).Assembly.GetType("SwiftCollections.Utility.SwiftDenseCapacity");
        Assert.NotNull(policy);
        MethodInfo grow = policy.GetMethod("Grow", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(grow);

        Assert.Equal(expected, Assert.IsType<int>(grow.Invoke(null, [current, required])));
    }

    private sealed class StatePayload
    {
        public int Value;
    }
}
