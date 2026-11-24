#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
using System;
using System.Buffers;
using System.Collections.Generic;
using MemoryPack;
using Xunit;

namespace SwiftCollections.Tests;

public class SwiftTicketStoreMemoryPackTests
{
    [Fact]
    public void OwnerAndRawState_UseTheFrozenThreeArrayWireShapeAndExactGenerations()
    {
        var owner = new SwiftTicketStore<int>(ControlState());
        owner.EnsureCapacity(128, 256);
        byte[] bytes = MemoryPackSerializer.Serialize(owner);
        byte[] stateBytes = MemoryPackSerializer.Serialize(owner.State);

        Assert.Equal(StateEnvelope(ControlState()), bytes);
        Assert.Equal(Convert.FromHexString(
            "01030300000065000000CA0000002F010000030000000000000007000000030000000300000000000000FFFFFFFF09000000"), bytes);
        Assert.Equal(50, bytes.Length);
        Assert.Equal(1, bytes[0]);
        Assert.Equal(3, stateBytes[0]);
        Assert.Equal(bytes.AsSpan(1).ToArray(), stateBytes);
        var restored = MemoryPackSerializer.Deserialize<SwiftTicketStore<int>>(bytes);
        var state = MemoryPackSerializer.Deserialize<SwiftTicketStoreState<int>>(stateBytes);

        AssertLogicalState(ControlState(), restored.State);
        AssertLogicalState(ControlState(), state);
        Assert.NotSame(owner, restored);
        Assert.Equal(8, restored.SparseCapacity);
        Assert.Equal(8, restored.DenseCapacity);
        Assert.Equal(bytes, MemoryPackSerializer.Serialize(restored));
        ContinueAndAssert(owner);
        ContinueAndAssert(restored);
        ContinueAndAssert(new SwiftTicketStore<int>(state));
    }

    [Fact]
    public void ExplicitBootstrapAndPackableWriterReader_AgreeWithOrdinaryOwnerSerialization()
    {
#if NET7_0_OR_GREATER
        MemoryPackFormatterProvider.Register<SwiftTicketStore<int>>();
        MemoryPackFormatterProvider.Register<SwiftTicketStoreState<int>>();
        MemoryPackFormatterProvider.Register<SwiftTicketStore<int>>();
#else
        SwiftTicketStore<int>.RegisterFormatter();
        SwiftTicketStoreState<int>.RegisterFormatter();
        SwiftTicketStore<int>.RegisterFormatter();
#endif
        var owner = new SwiftTicketStore<int>(ControlState());
        byte[] ordinary = MemoryPackSerializer.Serialize(owner);
        var buffer = new ArrayBufferWriter<byte>();
        using (var optionalState = MemoryPackWriterOptionalStatePool.Rent(null))
        {
            var writer = new MemoryPackWriter<ArrayBufferWriter<byte>>(ref buffer, optionalState);
            writer.WritePackable(owner);
            writer.Flush();
        }
        Assert.Equal(ordinary, buffer.WrittenSpan.ToArray());

        SwiftTicketStore<int> restored = null;
        using (var optionalState = MemoryPackReaderOptionalStatePool.Rent(null))
        {
            var reader = new MemoryPackReader(ordinary, optionalState);
            reader.ReadPackable(ref restored);
            Assert.Equal(0, reader.Remaining);
        }
        ContinueAndAssert(restored);
    }

    [Fact]
    public void NullOwnerAndDefaultState_HaveDistinctRawAndNormalizedSemantics()
    {
        byte[] nullOwner = MemoryPackSerializer.Serialize<SwiftTicketStore<int>>(null);
        Assert.Equal(new byte[] { 255 }, nullOwner);
        Assert.Null(MemoryPackSerializer.Deserialize<SwiftTicketStore<int>>(nullOwner));
        var original = new SwiftTicketStore<int>(ControlState());
        var overwritten = original;
        MemoryPackSerializer.Deserialize(nullOwner, ref overwritten);
        Assert.Null(overwritten);
        AssertLogicalState(ControlState(), original.State);

        var normalized = MemoryPackSerializer.Deserialize<SwiftTicketStoreState<int>>(
            MemoryPackSerializer.Serialize(default(SwiftTicketStoreState<int>)));
        Assert.Empty(normalized.DenseValues);
        Assert.Empty(normalized.DenseSlots);
        Assert.Empty(normalized.DenseGenerations);
        Assert.Empty(new SwiftTicketStore<int>(normalized));

        var rawNull = MemoryPackSerializer.Deserialize<SwiftTicketStoreState<int>>(new byte[] { 255 });
        Assert.Null(rawNull.DenseValues);
        Assert.Null(rawNull.DenseSlots);
        Assert.Null(rawNull.DenseGenerations);
        Assert.Empty(new SwiftTicketStore<int>(rawNull));
    }

    [Fact]
    public void ZeroMemberOwner_PreservesPriorLogicalStateByRefButDoesNotRetainOwnerIdentity()
    {
        var empty = MemoryPackSerializer.Deserialize<SwiftTicketStore<int>>(new byte[] { 0 });
        Assert.Empty(empty);
        Assert.Equal(0, empty.SparseCapacity);
        Assert.Equal(0, empty.DenseCapacity);

        var payload = new DenseTicketPersistencePayload { Id = 7, Label = "original" };
        var original = new SwiftTicketStore<DenseTicketPersistencePayload>();
        var ticket = new SwiftTicket(3, 0);
        original.Add(ticket, payload);
        var overwritten = original;
        var enumerator = original.GetEnumerator();

        MemoryPackSerializer.Deserialize(new byte[] { 0 }, ref overwritten);

        Assert.NotSame(original, overwritten);
        Assert.Same(payload, overwritten.GetRef(ticket));
        Assert.Same(payload, original.GetRef(ticket));
        Assert.True(enumerator.MoveNext());
        Assert.Same(payload, enumerator.Current);
        Assert.True(overwritten.Remove(ticket));
        Assert.True(original.ContainsKey(ticket));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(255)]
    public void ExplicitEmptyOrNullNestedState_ClearsByRefRatherThanInheritingPreviousEntries(byte stateHeader)
    {
        var original = new SwiftTicketStore<int>(ControlState());
        var overwritten = original;

        MemoryPackSerializer.Deserialize(new byte[] { 1, stateHeader }, ref overwritten);

        Assert.NotSame(original, overwritten);
        Assert.Empty(overwritten);
        Assert.Equal(0, overwritten.SparseCapacity);
        Assert.Equal(0, overwritten.DenseCapacity);
        AssertLogicalState(ControlState(), original.State);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void OmittedTrailingStateArrays_AreNormalizedToEmptyInsteadOfRetainingPreviousArrays(byte memberCount)
    {
        var original = ControlState();
        var overwritten = original;
        byte[] bytes = PartialStateEnvelope(memberCount, false);
        MemoryPackSerializer.Deserialize(bytes.AsSpan(1), ref overwritten);

        Assert.Empty(overwritten.DenseValues);
        Assert.Empty(overwritten.DenseSlots);
        Assert.Empty(overwritten.DenseGenerations);
        AssertLogicalState(ControlState(), original);
        Assert.Empty(MemoryPackSerializer.Deserialize<SwiftTicketStore<int>>(bytes));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void OmittedNonemptyStateFields_FailStructuralValidationWithoutReplacingTheOwner(byte memberCount)
    {
        var original = new SwiftTicketStore<int>(ControlState());
        var overwritten = original;

        var error = Assert.Throws<ArgumentException>(() => MemoryPackSerializer.Deserialize(
            PartialStateEnvelope(memberCount, true), ref overwritten));

        Assert.Equal("state", error.ParamName);
        Assert.Same(original, overwritten);
        AssertLogicalState(ControlState(), original.State);
    }

    [Fact]
    public void ReferenceNullableAndReferenceContainingValues_RoundTripWithoutSharingOriginalObjects()
    {
        var first = new SwiftTicket(3, 0);
        var second = new SwiftTicket(7, uint.MaxValue);
        var payload = new DenseTicketPersistencePayload { Id = 101, Label = "first" };
        var references = new SwiftTicketStore<DenseTicketPersistencePayload>();
        references.Add(first, payload);
        references.Add(second, null);
        var restored = MemoryPackSerializer.Deserialize<SwiftTicketStore<DenseTicketPersistencePayload>>(
            MemoryPackSerializer.Serialize(references));

        Assert.Equal(101, restored.GetRef(first).Id);
        Assert.Equal("first", restored.GetRef(first).Label);
        Assert.NotSame(payload, restored.GetRef(first));
        Assert.True(restored.TryGet(second, out var nullValue));
        Assert.Null(nullValue);
        restored.GetRef(first).Label = "decoded";
        Assert.Equal("first", payload.Label);
        Assert.Equal(first, restored.GetTicketAt(0));
        Assert.Equal(second, restored.GetTicketAt(1));

        var values = new SwiftTicketStore<DenseTicketPersistenceValue>();
        values.Add(first, new DenseTicketPersistenceValue { Id = 202, Label = "value", Detail = payload });
        values.Add(second, default);
        var decodedValues = MemoryPackSerializer.Deserialize<SwiftTicketStore<DenseTicketPersistenceValue>>(
            MemoryPackSerializer.Serialize(values));
        Assert.Equal(202, decodedValues.GetRef(first).Id);
        Assert.Equal("value", decodedValues.GetRef(first).Label);
        Assert.Equal(101, decodedValues.GetRef(first).Detail.Id);
        Assert.NotSame(payload, decodedValues.GetRef(first).Detail);
        Assert.Null(decodedValues.GetRef(second).Detail);
        Assert.Null(decodedValues.GetRef(second).Label);
        Assert.Equal(0, decodedValues.GetRef(second).Id);

        var nullable = new SwiftTicketStore<int?>();
        nullable.Add(first, null);
        nullable.Add(second, 17);
        var decodedNullable = MemoryPackSerializer.Deserialize<SwiftTicketStore<int?>>(
            MemoryPackSerializer.Serialize(nullable));
        Assert.True(decodedNullable.TryGet(first, out int? missing));
        Assert.Null(missing);
        Assert.Equal(17, decodedNullable.GetRef(second));
    }

    [Fact]
    public void NestedArrayAndListOwners_DecodeIndependentStoresAndPayloads()
    {
        var store = new SwiftTicketStore<int>(ControlState());
        var ticket = new SwiftTicket(4, uint.MaxValue);
        var references = new SwiftTicketStore<DenseTicketPersistencePayload>();
        references.Add(ticket, new DenseTicketPersistencePayload { Id = 707, Label = "nested" });
        var values = new SwiftTicketStore<DenseTicketPersistenceValue>();
        values.Add(ticket, new DenseTicketPersistenceValue { Id = 808, Label = "struct" });
        var envelope = new TicketStorePersistenceEnvelope
        {
            ReferenceStore = references,
            ValueStore = values,
            Array = [store, null],
            List = new List<SwiftTicketStore<int>> { null, store }
        };
        var restored = MemoryPackSerializer.Deserialize<TicketStorePersistenceEnvelope>(MemoryPackSerializer.Serialize(envelope));

        Assert.Null(restored.Array[1]);
        Assert.Null(restored.List[0]);
        Assert.NotSame(restored.Array[0], restored.List[1]);
        Assert.Equal(707, restored.ReferenceStore.GetRef(ticket).Id);
        Assert.NotSame(references.GetRef(ticket), restored.ReferenceStore.GetRef(ticket));
        Assert.Equal(808, restored.ValueStore.GetRef(ticket).Id);
        ContinueAndAssert(restored.Array[0]);
        ContinueAndAssert(restored.List[1]);
        AssertLogicalState(ControlState(), store.State);
    }

    [Fact]
    public void SuccessfulRefRestore_ReplacesOwnerAndPayloadWithoutChangingPriorAssociationsOrEnumeration()
    {
        var ticket = new SwiftTicket(2, 8);
        var payload = new DenseTicketPersistencePayload { Id = 7, Label = "original" };
        var original = new SwiftTicketStore<DenseTicketPersistencePayload>();
        original.Add(ticket, payload);
        var incoming = new SwiftTicketStore<DenseTicketPersistencePayload>();
        incoming.Add(new SwiftTicket(5, 0), new DenseTicketPersistencePayload { Id = 999, Label = "incoming" });
        var overwritten = original;
        var enumerator = original.GetEnumerator();

        MemoryPackSerializer.Deserialize(MemoryPackSerializer.Serialize(incoming), ref overwritten);

        Assert.NotSame(original, overwritten);
        Assert.False(overwritten.ContainsKey(ticket));
        Assert.Equal(999, overwritten.GetRef(new SwiftTicket(5, 0)).Id);
        Assert.NotSame(incoming.GetRef(new SwiftTicket(5, 0)), overwritten.GetRef(new SwiftTicket(5, 0)));
        Assert.Same(payload, original.GetRef(ticket));
        Assert.Equal(7, payload.Id);
        Assert.Equal("original", payload.Label);
        Assert.True(enumerator.MoveNext());
        Assert.Same(payload, enumerator.Current);
    }

    [Fact]
    public void CompleteRefReplacement_DoesNotAllocateForPreviousEntries()
    {
        const int population = 4096;
        var original = new SwiftTicketStore<int>(population, population);
        for (int i = 0; i < population; i++) original.Add(new SwiftTicket(i, (uint)i), i + 1);
        var originalState = original.State;
        byte[] bytes = MemoryPackSerializer.Serialize(new SwiftTicketStore<int>(0, 0));

        long freshBytes = MeasureEmptyReplacement(null, bytes, out var fresh);
        long replacementBytes = MeasureEmptyReplacement(original, bytes, out var replacement);

        Assert.Equal(freshBytes, replacementBytes);
        Assert.NotSame(original, replacement);
        Assert.Empty(fresh);
        Assert.Empty(replacement);
        Assert.Empty(replacement.State.DenseSlots);
        AssertLogicalState(originalState, original.State);
        replacement.Add(default, -1);
        Assert.Equal(-1, replacement.GetRef(default));
        AssertLogicalState(originalState, original.State);
    }

    [Theory]
    [InlineData("slot-length")]
    [InlineData("generation-length")]
    [InlineData("negative-index")]
    [InlineData("maximum-index")]
    [InlineData("duplicate-same-generation")]
    [InlineData("duplicate-different-generation")]
    public void StructuralWireFailures_PropagateStateArgumentAndPreserveTheOwner(string scenario)
    {
        var invalid = scenario switch
        {
            "slot-length" => new SwiftTicketStoreState<int>([1], [], [0]),
            "generation-length" => new SwiftTicketStoreState<int>([1], [0], []),
            "negative-index" => new SwiftTicketStoreState<int>([1], [-1], [0]),
            "maximum-index" => new SwiftTicketStoreState<int>([1], [1 << 30], [0]),
            "duplicate-same-generation" => new SwiftTicketStoreState<int>([1, 2], [3, 3], [9, 9]),
            _ => new SwiftTicketStoreState<int>([1, 2], [3, 3], [0, uint.MaxValue])
        };
        byte[] bytes = StateEnvelope(invalid);
        var original = new SwiftTicketStore<int>(ControlState());
        var overwritten = original;
        var enumerator = original.GetEnumerator();

        var error = Assert.Throws<ArgumentException>(() => MemoryPackSerializer.Deserialize(bytes, ref overwritten));

        Assert.Equal("state", error.ParamName);
        Assert.Same(original, overwritten);
        AssertLogicalState(ControlState(), original.State);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(101, enumerator.Current);
        Assert.False(original.TryAdd(new SwiftTicket(7, 0), 999));
        Assert.Equal(202, original.GetRef(new SwiftTicket(7, uint.MaxValue)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedRefDecode_DoesNotReuseSameLengthMutablePayloadsOrTheirArrays(bool truncate)
    {
        var ticket = new SwiftTicket(2, 8);
        var payload = new DenseTicketPersistencePayload { Id = 7, Label = "original" };
        var original = new SwiftTicketStore<DenseTicketPersistencePayload>();
        original.Add(ticket, payload);
        var incoming = new SwiftTicketStoreState<DenseTicketPersistencePayload>(
            [new DenseTicketPersistencePayload { Id = 999, Label = "incoming" }],
            truncate ? [2] : [-1], [0]);
        byte[] bytes = StateEnvelope(incoming);
        if (truncate) bytes = bytes.AsSpan(0, bytes.Length - 1).ToArray();
        var overwritten = original;

        if (truncate)
            Assert.Throws<MemoryPackSerializationException>(() => MemoryPackSerializer.Deserialize(bytes, ref overwritten));
        else
            Assert.Equal("state", Assert.Throws<ArgumentException>(() => MemoryPackSerializer.Deserialize(bytes, ref overwritten)).ParamName);

        Assert.Same(original, overwritten);
        Assert.Same(payload, original.GetRef(ticket));
        Assert.Equal(7, payload.Id);
        Assert.Equal("original", payload.Label);
        Assert.Equal(new uint[] { 8 }, original.State.DenseGenerations);
        Assert.False(original.ContainsKey(new SwiftTicket(2, 0)));
        Assert.Equal(ticket, original.GetTicketAt(0));
    }

    [Fact]
    public void RawStateRefDecode_UsesFreshArraysAndPreservesPreviousMutablePayloadOnTruncation()
    {
        var payload = new DenseTicketPersistencePayload { Id = 7, Label = "original" };
        var original = new SwiftTicketStoreState<DenseTicketPersistencePayload>([payload], [2], [8]);
        var incoming = new SwiftTicketStoreState<DenseTicketPersistencePayload>(
            [new DenseTicketPersistencePayload { Id = 999, Label = "incoming" }], [5], [0]);
        byte[] bytes = MemoryPackSerializer.Serialize(incoming);
        byte[] truncated = bytes.AsSpan(0, bytes.Length - 1).ToArray();
        var overwritten = original;

        Assert.Throws<MemoryPackSerializationException>(() => MemoryPackSerializer.Deserialize(truncated, ref overwritten));

        Assert.Same(original.DenseValues, overwritten.DenseValues);
        Assert.Same(original.DenseSlots, overwritten.DenseSlots);
        Assert.Same(original.DenseGenerations, overwritten.DenseGenerations);
        Assert.Same(payload, overwritten.DenseValues[0]);
        Assert.Equal(7, payload.Id);
        Assert.Equal("original", payload.Label);
        Assert.Equal([2], overwritten.DenseSlots);
        Assert.Equal(new uint[] { 8 }, overwritten.DenseGenerations);

        MemoryPackSerializer.Deserialize(bytes, ref overwritten);

        Assert.NotSame(original.DenseValues, overwritten.DenseValues);
        Assert.NotSame(original.DenseSlots, overwritten.DenseSlots);
        Assert.NotSame(original.DenseGenerations, overwritten.DenseGenerations);
        Assert.NotSame(incoming.DenseValues[0], overwritten.DenseValues[0]);
        Assert.Equal(999, overwritten.DenseValues[0].Id);
        Assert.Equal("incoming", overwritten.DenseValues[0].Label);
        Assert.Equal([5], overwritten.DenseSlots);
        Assert.Equal(new uint[] { 0 }, overwritten.DenseGenerations);
        Assert.Equal(7, payload.Id);
    }

    [Fact]
    public void EveryTruncatedPrefixAndOversizedObjectHeaders_RejectBeforeReplacingTheOwner()
    {
        byte[] valid = MemoryPackSerializer.Serialize(new SwiftTicketStore<int>(ControlState()));
        var original = new SwiftTicketStore<int>(ControlState());
        for (int length = 0; length < valid.Length; length++)
        {
            byte[] truncated = valid.AsSpan(0, length).ToArray();
            var overwritten = original;
            Assert.Throws<MemoryPackSerializationException>(() => MemoryPackSerializer.Deserialize(truncated, ref overwritten));
            Assert.Same(original, overwritten);
            AssertLogicalState(ControlState(), original.State);
        }
        foreach (byte[] malformed in new[] { new byte[] { 2 }, new byte[] { 1, 4 } })
        {
            var overwritten = original;
            Assert.Throws<MemoryPackSerializationException>(() => MemoryPackSerializer.Deserialize(malformed, ref overwritten));
            Assert.Same(original, overwritten);
            AssertLogicalState(ControlState(), original.State);
        }
        var state = ControlState();
        Assert.Throws<MemoryPackSerializationException>(() => MemoryPackSerializer.Deserialize(new byte[] { 4 }, ref state));
        AssertLogicalState(ControlState(), state);
    }

    private static SwiftTicketStoreState<int> ControlState() =>
        new([101, 202, 303], [0, 7, 3], [0, uint.MaxValue, 9]);

    private static long MeasureEmptyReplacement(SwiftTicketStore<int> previous, byte[] bytes,
        out SwiftTicketStore<int> replacement)
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

    private static void AssertLogicalState(SwiftTicketStoreState<int> expected, SwiftTicketStoreState<int> actual)
    {
        Assert.Equal(expected.DenseValues, actual.DenseValues);
        Assert.Equal(expected.DenseSlots, actual.DenseSlots);
        Assert.Equal(expected.DenseGenerations, actual.DenseGenerations);
    }

    private static void ContinueAndAssert(SwiftTicketStore<int> store)
    {
        Assert.Equal(101, store.GetRef(default));
        Assert.Equal(202, store.GetRef(new SwiftTicket(7, uint.MaxValue)));
        Assert.Equal(303, store.GetRef(new SwiftTicket(3, 9)));
        Assert.False(store.TryAdd(new SwiftTicket(3, 10), 999));
        Assert.True(store.Remove(new SwiftTicket(7, uint.MaxValue)));
        store.Add(new SwiftTicket(7, 0), 404);
        Assert.False(store.TryGet(new SwiftTicket(7, uint.MaxValue), out _));
        store.GetRef(default) += 5;
        Assert.Equal(106, store.GetRef(default));
        Assert.True(store.Remove(default));
        store.Add(new SwiftTicket(8, 1), 505);
        Assert.Equal([404, 303, 505], store.Values.ToArray());
        Assert.Equal([7, 3, 8], store.State.DenseSlots);
        Assert.Equal(new uint[] { 0, 9, 1 }, store.State.DenseGenerations);
    }

    private static byte[] StateEnvelope<T>(SwiftTicketStoreState<T> state)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var optionalState = MemoryPackWriterOptionalStatePool.Rent(null);
        var writer = new MemoryPackWriter<ArrayBufferWriter<byte>>(ref buffer, optionalState);
        writer.WriteObjectHeader(1);
        writer.WriteObjectHeader(3);
        writer.WriteArray(state.DenseValues);
        writer.WriteUnmanagedArray(state.DenseSlots);
        writer.WriteUnmanagedArray(state.DenseGenerations);
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    private static byte[] PartialStateEnvelope(byte memberCount, bool nonempty)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var optionalState = MemoryPackWriterOptionalStatePool.Rent(null);
        var writer = new MemoryPackWriter<ArrayBufferWriter<byte>>(ref buffer, optionalState);
        writer.WriteObjectHeader(1);
        writer.WriteObjectHeader(memberCount);
        if (memberCount > 0) writer.WriteArray(nonempty ? new[] { 101 } : Array.Empty<int>());
        if (memberCount > 1) writer.WriteUnmanagedArray(nonempty ? new[] { 0 } : Array.Empty<int>());
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }
}
#endif
