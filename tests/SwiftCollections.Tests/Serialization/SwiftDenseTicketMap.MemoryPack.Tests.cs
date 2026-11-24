#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
using System;
using System.Buffers;
using System.Collections.Generic;
using MemoryPack;
using Xunit;

namespace SwiftCollections.Tests;

public class SwiftDenseTicketMapMemoryPackTests
{
    [Fact]
    public void OwnerAndRawState_UseTheFrozenFourFieldStateAndPreserveTerminalContinuation()
    {
        var owner = new SwiftDenseTicketMap<int>(PartitionedState());
        owner.EnsureCapacity(128);
        byte[] bytes = MemoryPackSerializer.Serialize(owner);
        byte[] rawBytes = MemoryPackSerializer.Serialize(owner.State);

        Assert.Equal(StateEnvelope(owner.State), bytes);
        Assert.Equal(1, bytes[0]);
        Assert.Equal(4, rawBytes[0]);
        var restored = MemoryPackSerializer.Deserialize<SwiftDenseTicketMap<int>>(bytes);
        var state = MemoryPackSerializer.Deserialize<SwiftDenseTicketMapState<int>>(rawBytes);

        AssertLogicalState(PartitionedState(), restored.State);
        AssertLogicalState(PartitionedState(), state);
        Assert.NotSame(owner, restored);
        Assert.Equal(bytes, MemoryPackSerializer.Serialize(restored));
        ContinueAndAssert(owner);
        ContinueAndAssert(restored);
        ContinueAndAssert(new SwiftDenseTicketMap<int>(state));
    }

    [Fact]
    public void ExplicitBootstrapAndPackableWriterReader_AgreeWithOrdinaryOwnerSerialization()
    {
#if NET7_0_OR_GREATER
        MemoryPackFormatterProvider.Register<SwiftDenseTicketMap<int>>();
        MemoryPackFormatterProvider.Register<SwiftDenseTicketMapState<int>>();
        MemoryPackFormatterProvider.Register<SwiftDenseTicketMap<int>>();
#else
        SwiftDenseTicketMap<int>.RegisterFormatter();
        SwiftDenseTicketMapState<int>.RegisterFormatter();
        SwiftDenseTicketMap<int>.RegisterFormatter();
#endif
        var owner = new SwiftDenseTicketMap<int>(PartitionedState());
        byte[] ordinary = MemoryPackSerializer.Serialize(owner);
        var buffer = new ArrayBufferWriter<byte>();
        using (var optionalState = MemoryPackWriterOptionalStatePool.Rent(null))
        {
            var writer = new MemoryPackWriter<ArrayBufferWriter<byte>>(ref buffer, optionalState);
            writer.WritePackable(owner);
            writer.Flush();
        }
        Assert.Equal(ordinary, buffer.WrittenSpan.ToArray());

        SwiftDenseTicketMap<int> restored = null;
        using (var optionalState = MemoryPackReaderOptionalStatePool.Rent(null))
        {
            var reader = new MemoryPackReader(ordinary, optionalState);
            reader.ReadPackable(ref restored);
            Assert.Equal(0, reader.Remaining);
        }
        ContinueAndAssert(restored);
    }

    [Fact]
    public void NullOwners_DefaultStateAndEmptyHeaders_HaveExplicitEmptySemantics()
    {
        Assert.Null(MemoryPackSerializer.Deserialize<SwiftDenseTicketMap<int>>(
            MemoryPackSerializer.Serialize<SwiftDenseTicketMap<int>>(null)));
        var original = new SwiftDenseTicketMap<int>();
        var ticket = original.Add(7);
        var overwritten = original;
        MemoryPackSerializer.Deserialize(MemoryPackSerializer.Serialize<SwiftDenseTicketMap<int>>(null), ref overwritten);
        Assert.Null(overwritten);
        Assert.Equal(7, original.GetRef(ticket));

        var normalized = MemoryPackSerializer.Deserialize<SwiftDenseTicketMapState<int>>(
            MemoryPackSerializer.Serialize(default(SwiftDenseTicketMapState<int>)));
        Assert.Empty(normalized.DenseValues);
        Assert.Empty(normalized.DenseSlots);
        Assert.Empty(normalized.SlotGenerations);
        Assert.Empty(normalized.FreeSlots);
        Assert.Equal(new SwiftTicket(0, 1), new SwiftDenseTicketMap<int>(normalized).Add(7));
        var emptyOwner = MemoryPackSerializer.Deserialize<SwiftDenseTicketMap<int>>(new byte[] { 0 });
        Assert.Empty(emptyOwner);
        Assert.Equal(new SwiftTicket(0, 1), emptyOwner.Add(7));

        var retained = original;
        MemoryPackSerializer.Deserialize(new byte[] { 0 }, ref retained);
        Assert.NotSame(original, retained);
        Assert.Equal(7, retained.GetRef(ticket));
        Assert.Equal(7, original.GetRef(ticket));
    }

    [Fact]
    public void ReferenceAndReferenceContainingValues_RoundTripWithoutSharingOriginalPayloadObjects()
    {
        var payload = new DenseTicketPersistencePayload { Id = 101, Label = "first" };
        var references = new SwiftDenseTicketMap<DenseTicketPersistencePayload>();
        var first = references.Add(payload);
        var absent = references.Add(null);
        var restored = MemoryPackSerializer.Deserialize<SwiftDenseTicketMap<DenseTicketPersistencePayload>>(
            MemoryPackSerializer.Serialize(references));

        Assert.Equal(101, restored.GetRef(first).Id);
        Assert.Equal("first", restored.GetRef(first).Label);
        Assert.NotSame(payload, restored.GetRef(first));
        Assert.True(restored.TryGet(absent, out var nullValue));
        Assert.Null(nullValue);
        restored.GetRef(first).Label = "decoded";
        Assert.Equal("first", payload.Label);

        var values = new SwiftDenseTicketMap<DenseTicketPersistenceValue>();
        var valueTicket = values.Add(new DenseTicketPersistenceValue { Id = 202, Label = "value", Detail = payload });
        var defaultTicket = values.Add(default);
        Assert.Same(payload, values.State.DenseValues[0].Detail);
        var decodedValues = MemoryPackSerializer.Deserialize<SwiftDenseTicketMap<DenseTicketPersistenceValue>>(
            MemoryPackSerializer.Serialize(values));
        Assert.Equal(202, decodedValues.GetRef(valueTicket).Id);
        Assert.Equal("value", decodedValues.GetRef(valueTicket).Label);
        Assert.Equal(101, decodedValues.GetRef(valueTicket).Detail.Id);
        Assert.NotSame(payload, decodedValues.GetRef(valueTicket).Detail);
        Assert.Null(decodedValues.GetRef(defaultTicket).Detail);
        Assert.Null(decodedValues.GetRef(defaultTicket).Label);
        Assert.Equal(0, decodedValues.GetRef(defaultTicket).Id);

        var nullable = new SwiftDenseTicketMap<int?>();
        var nullTicket = nullable.Add(null);
        var numberTicket = nullable.Add(17);
        var decodedNullable = MemoryPackSerializer.Deserialize<SwiftDenseTicketMap<int?>>(
            MemoryPackSerializer.Serialize(nullable));
        Assert.True(decodedNullable.TryGet(nullTicket, out int? missing));
        Assert.Null(missing);
        Assert.Equal(17, decodedNullable.GetRef(numberTicket));
    }

    [Fact]
    public void NestedArrayAndListOwners_KeepIndependentStorageAndExactReuseSequences()
    {
        var map = new SwiftDenseTicketMap<int>(PartitionedState());
        var references = new SwiftDenseTicketMap<DenseTicketPersistencePayload>();
        references.Add(new DenseTicketPersistencePayload { Id = 707, Label = "nested" });
        var values = new SwiftDenseTicketMap<DenseTicketPersistenceValue>();
        values.Add(new DenseTicketPersistenceValue { Id = 808, Label = "struct" });
        var envelope = new DenseTicketPersistenceEnvelope
        {
            ReferenceMap = references,
            ValueMap = values,
            Array = [map, null],
            List = new List<SwiftDenseTicketMap<int>> { null, map }
        };
        var restored = MemoryPackSerializer.Deserialize<DenseTicketPersistenceEnvelope>(MemoryPackSerializer.Serialize(envelope));

        Assert.Null(restored.Array[1]);
        Assert.Null(restored.List[0]);
        Assert.NotSame(restored.Array[0], restored.List[1]);
        Assert.Equal(707, restored.ReferenceMap.GetRef(new SwiftTicket(0, 1)).Id);
        Assert.Equal(808, restored.ValueMap.GetRef(new SwiftTicket(0, 1)).Id);
        ContinueAndAssert(restored.Array[0]);
        ContinueAndAssert(restored.List[1]);
        AssertLogicalState(PartitionedState(), map.State);
    }

    [Fact]
    public void RepeatedClearRestoreAndGrowth_PreserveFinalGenerationAndNeverReissueRetiredSlots()
    {
        var owner = new SwiftDenseTicketMap<int>(new SwiftDenseTicketMapState<int>([], [], [uint.MaxValue - 1, 0], [0]));
        var penultimate = owner.Add(101);
        Assert.Equal(new SwiftTicket(0, uint.MaxValue - 1), penultimate);
        owner.Clear();
        owner = RoundTrip(owner);
        var terminal = owner.Add(202);
        Assert.Equal(new SwiftTicket(0, uint.MaxValue), terminal);
        owner.Clear();
        owner = RoundTrip(owner);
        owner.Clear();
        owner.EnsureCapacity(16);

        Assert.Equal(new uint[] { 0, 0 }, owner.State.SlotGenerations);
        Assert.Empty(owner.State.FreeSlots);
        Assert.Equal(new SwiftTicket(2, 1), owner.Add(303));
        Assert.False(owner.IsValid(penultimate));
        Assert.False(owner.IsValid(terminal));
        Assert.Equal([303], owner.Values.ToArray());
    }

    [Fact]
    public void SuccessfulRefRestore_ReplacesTheOwnerWithoutMutatingItsPreviousInstance()
    {
        var original = new SwiftDenseTicketMap<int>();
        var ticket = original.Add(7);
        var enumerator = original.GetEnumerator();
        var overwritten = original;

        MemoryPackSerializer.Deserialize(MemoryPackSerializer.Serialize(new SwiftDenseTicketMap<int>(PartitionedState())), ref overwritten);

        Assert.NotSame(original, overwritten);
        Assert.Equal(7, original.GetRef(ticket));
        Assert.True(enumerator.MoveNext());
        Assert.Equal(7, enumerator.Current);
        AssertLogicalState(PartitionedState(), overwritten.State);
        ContinueAndAssert(overwritten);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompleteRefReplacement_DoesNotAllocateForPreviousEntriesOrRetiredHistory(bool retiredHistory)
    {
        const int population = 4096;
        var original = retiredHistory
            ? new SwiftDenseTicketMap<int>(new SwiftDenseTicketMapState<int>([], [], new uint[population], []))
            : new SwiftDenseTicketMap<int>(population);
        if (!retiredHistory)
            for (int i = 0; i < population; i++) original.Add(i + 1);
        var originalState = original.State;
        byte[] bytes = MemoryPackSerializer.Serialize(new SwiftDenseTicketMap<int>(0));

        long freshBytes = MeasureEmptyReplacement(null, bytes, out var fresh);
        long replacementBytes = MeasureEmptyReplacement(original, bytes, out var replacement);

        Assert.Equal(freshBytes, replacementBytes);
        Assert.NotSame(original, replacement);
        Assert.Empty(fresh);
        Assert.Empty(replacement);
        Assert.Empty(replacement.State.SlotGenerations);
        AssertLogicalState(originalState, original.State);
        Assert.Equal(new SwiftTicket(0, 1), replacement.Add(-1));
        Assert.Equal(-1, replacement.GetRef(new SwiftTicket(0, 1)));
        AssertLogicalState(originalState, original.State);
    }

    [Fact]
    public void RawStateRefDecode_UsesFreshArraysAndPreservesPreviousArraysOnTruncation()
    {
        var original = new SwiftDenseTicketMapState<int>([11], [0], [1], []);
        var incoming = PartitionedState();
        byte[] bytes = MemoryPackSerializer.Serialize(incoming);
        byte[] truncated = bytes.AsSpan(0, bytes.Length - 1).ToArray();
        var overwritten = original;

        Assert.Throws<MemoryPackSerializationException>(() => MemoryPackSerializer.Deserialize(truncated, ref overwritten));

        Assert.Same(original.DenseValues, overwritten.DenseValues);
        Assert.Same(original.DenseSlots, overwritten.DenseSlots);
        Assert.Same(original.SlotGenerations, overwritten.SlotGenerations);
        Assert.Same(original.FreeSlots, overwritten.FreeSlots);
        Assert.Equal([11], original.DenseValues);
        Assert.Equal([0], original.DenseSlots);
        Assert.Equal(new uint[] { 1 }, original.SlotGenerations);
        Assert.Empty(original.FreeSlots);

        MemoryPackSerializer.Deserialize(bytes, ref overwritten);
        AssertLogicalState(incoming, overwritten);
        Assert.NotSame(original.DenseValues, overwritten.DenseValues);
        Assert.NotSame(incoming.DenseValues, overwritten.DenseValues);
        incoming.DenseValues[0] = 999;
        Assert.Equal(101, overwritten.DenseValues[0]);
        Assert.Equal([11], original.DenseValues);
    }

    [Theory]
    [InlineData("dense-length")]
    [InlineData("duplicate-live")]
    [InlineData("duplicate-free")]
    [InlineData("live-free")]
    [InlineData("retired-live")]
    [InlineData("retired-free")]
    [InlineData("missing-owner")]
    public void StructuralWireFailures_PreserveExistingOwnerAndPropagateStateArgument(string scenario)
    {
        var state = scenario switch
        {
            "dense-length" => new SwiftDenseTicketMapState<int>([1], [], [1], []),
            "duplicate-live" => new SwiftDenseTicketMapState<int>([1, 2], [0, 0], [1], []),
            "duplicate-free" => new SwiftDenseTicketMapState<int>([], [], [1], [0, 0]),
            "live-free" => new SwiftDenseTicketMapState<int>([1], [0], [1], [0]),
            "retired-live" => new SwiftDenseTicketMapState<int>([1], [0], [0], []),
            "retired-free" => new SwiftDenseTicketMapState<int>([], [], [0], [0]),
            _ => new SwiftDenseTicketMapState<int>([], [], [1], [])
        };
        byte[] bytes = StateEnvelope(state);
        var original = new SwiftDenseTicketMap<int>();
        var ticket = original.Add(7);
        var overwritten = original;
        var enumerator = original.GetEnumerator();

        var error = Assert.Throws<ArgumentException>(() => MemoryPackSerializer.Deserialize(bytes, ref overwritten));

        Assert.Equal("state", error.ParamName);
        Assert.Same(original, overwritten);
        Assert.Equal(7, original.GetRef(ticket));
        Assert.True(enumerator.MoveNext());
        Assert.Equal(7, enumerator.Current);
    }

    [Fact]
    public void StructuralRefFailure_DoesNotReuseOrMutateAnExistingReferencePayload()
    {
        var payload = new DenseTicketPersistencePayload { Id = 7, Label = "original" };
        var original = new SwiftDenseTicketMap<DenseTicketPersistencePayload>();
        var ticket = original.Add(payload);
        var invalid = new SwiftDenseTicketMapState<DenseTicketPersistencePayload>(
            [new DenseTicketPersistencePayload { Id = 999, Label = "incoming" }], [0], [1], [0]);
        byte[] bytes = StateEnvelope(invalid);
        var overwritten = original;

        var error = Assert.Throws<ArgumentException>(() => MemoryPackSerializer.Deserialize(bytes, ref overwritten));

        Assert.Equal("state", error.ParamName);
        Assert.Same(original, overwritten);
        Assert.Same(payload, original.GetRef(ticket));
        Assert.Equal(7, payload.Id);
        Assert.Equal("original", payload.Label);
        Assert.Equal(new uint[] { 1 }, original.State.SlotGenerations);
        Assert.Empty(original.State.FreeSlots);
        Assert.Equal(new SwiftTicket(1, 1), original.Add(null));
    }

    [Fact]
    public void TruncatedAndOversizedHeaders_RejectWithoutReplacingAnExistingOwner()
    {
        byte[] valid = MemoryPackSerializer.Serialize(new SwiftDenseTicketMap<int>(PartitionedState()));
        var original = new SwiftDenseTicketMap<int>();
        var ticket = original.Add(7);
        for (int length = 0; length < valid.Length; length++)
        {
            byte[] truncated = valid.AsSpan(0, length).ToArray();
            var overwritten = original;
            Assert.Throws<MemoryPackSerializationException>(() => MemoryPackSerializer.Deserialize(truncated, ref overwritten));
            Assert.Same(original, overwritten);
            Assert.Equal(7, original.GetRef(ticket));
        }
        var corrupted = (byte[])valid.Clone();
        corrupted[0] = 2;
        var existing = original;
        Assert.Throws<MemoryPackSerializationException>(() => MemoryPackSerializer.Deserialize(corrupted, ref existing));
        Assert.Same(original, existing);
        Assert.Equal(7, existing.GetRef(ticket));
    }

    private static SwiftDenseTicketMap<int> RoundTrip(SwiftDenseTicketMap<int> map)
        => MemoryPackSerializer.Deserialize<SwiftDenseTicketMap<int>>(MemoryPackSerializer.Serialize(map));

    private static long MeasureEmptyReplacement(SwiftDenseTicketMap<int> previous, byte[] bytes,
        out SwiftDenseTicketMap<int> replacement)
    {
        const int iterations = 64;
        replacement = previous;
        for (int i = 0; i < 32; i++)
        {
            replacement = previous;
            MemoryPackSerializer.Deserialize(bytes, ref replacement);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < iterations; i++)
        {
            replacement = previous;
            MemoryPackSerializer.Deserialize(bytes, ref replacement);
        }
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private static SwiftDenseTicketMapState<int> PartitionedState()
        => new([101, 303], [0, 2], [1, 2, uint.MaxValue, 0, 3], [1, 4]);

    private static void AssertLogicalState(SwiftDenseTicketMapState<int> expected, SwiftDenseTicketMapState<int> actual)
    {
        Assert.Equal(expected.DenseValues, actual.DenseValues);
        Assert.Equal(expected.DenseSlots, actual.DenseSlots);
        Assert.Equal(expected.SlotGenerations, actual.SlotGenerations);
        Assert.Equal(expected.FreeSlots, actual.FreeSlots);
    }

    private static void ContinueAndAssert(SwiftDenseTicketMap<int> map)
    {
        Assert.Equal(101, map.GetRef(new SwiftTicket(0, 1)));
        Assert.Equal(303, map.GetRef(new SwiftTicket(2, uint.MaxValue)));
        Assert.Equal(new SwiftTicket(4, 3), map.Add(404));
        Assert.Equal(new SwiftTicket(1, 2), map.Add(202));
        Assert.True(map.Remove(new SwiftTicket(2, uint.MaxValue)));
        Assert.Equal(new SwiftTicket(5, 1), map.Add(505));
        Assert.True(map.Remove(new SwiftTicket(0, 1)));
        Assert.Equal(new SwiftTicket(0, 2), map.Add(606));
        Assert.Equal([505, 202, 404, 606], map.Values.ToArray());
        Assert.Equal([5, 1, 4, 0], map.State.DenseSlots);
        Assert.Equal(new uint[] { 2, 2, 0, 0, 3, 1 }, map.State.SlotGenerations);
        Assert.Empty(map.State.FreeSlots);
    }

    private static byte[] StateEnvelope<T>(SwiftDenseTicketMapState<T> state)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var optionalState = MemoryPackWriterOptionalStatePool.Rent(null);
        var writer = new MemoryPackWriter<ArrayBufferWriter<byte>>(ref buffer, optionalState);
        writer.WriteObjectHeader(1);
        writer.WriteObjectHeader(4);
        writer.WriteArray(state.DenseValues);
        writer.WriteUnmanagedArray(state.DenseSlots);
        writer.WriteUnmanagedArray(state.SlotGenerations);
        writer.WriteUnmanagedArray(state.FreeSlots);
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }
}
#endif
