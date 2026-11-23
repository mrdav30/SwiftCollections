using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Chronicler.Serialization;
using Xunit;

namespace SwiftCollections.Tests;

public class SwiftTicketStoreJsonTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OwnerAndRawState_UseTheScalarSchemaAndPreserveDenseOrderAcrossContinuation(bool generated)
    {
        var options = Options(generated);
        var original = new SwiftTicketStore<int>(ControlState());
        original.EnsureCapacity(128, 128);

        string json = JsonSerializer.Serialize(original, options);

        Assert.Equal("""{"State":{"DenseValues":[101,202,303],"DenseSlots":[0,7,3],"DenseGenerations":[0,4294967295,9]}}""", json);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(["State"], document.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal(["DenseValues", "DenseSlots", "DenseGenerations"],
            document.RootElement.GetProperty("State").EnumerateObject().Select(property => property.Name));
        var restored = JsonSerializer.Deserialize<SwiftTicketStore<int>>(json, options);
        var raw = JsonSerializer.Deserialize<SwiftTicketStoreState<int>>(
            JsonSerializer.Serialize(original.State, options), options);

        Assert.Equal(8, restored.SparseCapacity);
        Assert.Equal(8, restored.DenseCapacity);
        Assert.Equal(128, original.SparseCapacity);
        Assert.Equal(128, original.DenseCapacity);
        ContinueAndAssert(original);
        ContinueAndAssert(restored);
        ContinueAndAssert(new SwiftTicketStore<int>(raw));
        Assert.Equal(JsonSerializer.Serialize(original, options), JsonSerializer.Serialize(restored, options));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NullOwnersDefaultStateAndOmittedFields_HaveExplicitEmptyResults(bool generated)
    {
        var options = Options(generated);
        Assert.Equal("null", JsonSerializer.Serialize<SwiftTicketStore<int>>(null, options));
        Assert.Null(JsonSerializer.Deserialize<SwiftTicketStore<int>>("null", options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SwiftTicketStoreState<int>>("null", options));
        Assert.Equal("""{"DenseValues":null,"DenseSlots":null,"DenseGenerations":null}""",
            JsonSerializer.Serialize(default(SwiftTicketStoreState<int>), options));

        var raw = JsonSerializer.Deserialize<SwiftTicketStoreState<int>>(
            JsonSerializer.Serialize(default(SwiftTicketStoreState<int>), options), options);
        Assert.Empty(raw.DenseValues);
        Assert.Empty(raw.DenseSlots);
        Assert.Empty(raw.DenseGenerations);
        foreach (string json in new[]
        {
            """{"State":{}}""",
            """{"State":{"DenseValues":null,"DenseSlots":null,"DenseGenerations":null}}""",
            """{"State":{"DenseValues":[]}}""",
            """{"State":{"DenseSlots":[],"DenseGenerations":[]}}"""
        })
        {
            var restored = JsonSerializer.Deserialize<SwiftTicketStore<int>>(json, options);
            Assert.Empty(restored);
            Assert.Equal(0, restored.SparseCapacity);
            Assert.Equal(0, restored.DenseCapacity);
            Assert.True(restored.TryAdd(default, 7));
            Assert.Equal(7, restored.GetRef(default));
            restored.Clear();
            Assert.Equal("""{"State":{"DenseValues":[],"DenseSlots":[],"DenseGenerations":[]}}""",
                JsonSerializer.Serialize(restored, options));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NullableAndStringValues_KeepNullDefaultDuplicatePayloadsAndExactTickets(bool generated)
    {
        var options = Options(generated);
        var nullable = new SwiftTicketStore<int?>();
        nullable.Add(default, null);
        nullable.Add(new SwiftTicket(9, uint.MaxValue), 0);
        nullable.Add(new SwiftTicket(3, 7), 7);
        var nullableCopy = JsonSerializer.Deserialize<SwiftTicketStore<int?>>(JsonSerializer.Serialize(nullable, options), options);
        Assert.True(nullableCopy.TryGet(default, out int? missingValue));
        Assert.Null(missingValue);
        Assert.Equal(0, nullableCopy.GetRef(new SwiftTicket(9, uint.MaxValue)));
        Assert.Equal(new int?[] { null, 0, 7 }, nullableCopy.Values.ToArray());
        Assert.Equal(new uint[] { 0, uint.MaxValue, 7 }, nullableCopy.State.DenseGenerations);

        var strings = new SwiftTicketStore<string>(new SwiftTicketStoreState<string>(
            [null, "duplicate", "duplicate", null], [7, 0, 3, 1], [0, 9, uint.MaxValue, 1]));
        var stringCopy = JsonSerializer.Deserialize<SwiftTicketStore<string>>(JsonSerializer.Serialize(strings, options), options);
        Assert.True(stringCopy.TryGet(new SwiftTicket(7, 0), out string text));
        Assert.Null(text);
        Assert.Equal(new[] { null, "duplicate", "duplicate", null }, stringCopy.Values.ToArray());
        Assert.Equal([7, 0, 3, 1], stringCopy.State.DenseSlots);
        Assert.False(stringCopy.ContainsKey(new SwiftTicket(7, 1)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReferencePayload_RawStateSharesObjectsWhileSerializedCopiesAreIndependent(bool generated)
    {
        var options = Options(generated);
        var payload = new DenseTicketPersistencePayload { Id = 101, Label = "before" };
        var original = new SwiftTicketStore<DenseTicketPersistencePayload>();
        original.Add(default, payload);
        original.Add(new SwiftTicket(7, uint.MaxValue), null);
        original.Add(new SwiftTicket(3, 9), payload);
        var rawCopy = new SwiftTicketStore<DenseTicketPersistencePayload>(original.State);
        var restored = JsonSerializer.Deserialize<SwiftTicketStore<DenseTicketPersistencePayload>>(
            JsonSerializer.Serialize(original, options), options);

        payload.Label = "after";
        Assert.Same(payload, rawCopy.GetRef(default));
        Assert.Equal("after", rawCopy.GetRef(default).Label);
        Assert.NotSame(payload, restored.GetRef(default));
        Assert.Equal(101, restored.GetRef(default).Id);
        Assert.Equal("before", restored.GetRef(default).Label);
        Assert.Null(restored.GetRef(new SwiftTicket(7, uint.MaxValue)));
        Assert.Equal("before", restored.GetRef(new SwiftTicket(3, 9)).Label);
        restored.GetRef(default).Label = "decoded";
        Assert.Equal("after", payload.Label);
        Assert.Equal("before", restored.GetRef(new SwiftTicket(3, 9)).Label);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReferenceContainingValue_PreservesNestedDataAfterSwapBackAndSerializedRestore(bool generated)
    {
        var options = Options(generated);
        var detail = new DenseTicketPersistencePayload { Id = 17, Label = "nested" };
        var original = new SwiftTicketStore<DenseTicketPersistenceValue>();
        original.Add(default, default);
        var moved = new SwiftTicket(7, uint.MaxValue);
        original.Add(moved, new DenseTicketPersistenceValue { Id = 303, Label = "value", Detail = detail });
        Assert.True(original.Remove(default));
        var rawCopy = new SwiftTicketStore<DenseTicketPersistenceValue>(original.State);
        var restored = JsonSerializer.Deserialize<SwiftTicketStore<DenseTicketPersistenceValue>>(
            JsonSerializer.Serialize(original, options), options);

        detail.Label = "changed";
        Assert.Equal(moved, restored.GetTicketAt(0));
        Assert.Equal(303, restored.GetRef(moved).Id);
        Assert.Equal("value", restored.GetRef(moved).Label);
        Assert.Same(detail, rawCopy.GetRef(moved).Detail);
        Assert.Equal("nested", restored.GetRef(moved).Detail.Label);
        Assert.NotSame(detail, restored.GetRef(moved).Detail);
        Assert.False(restored.ContainsKey(default));
        Assert.True(restored.TryAdd(default, default));
        Assert.Equal(moved, restored.GetTicketAt(0));
        Assert.Equal(default(SwiftTicket), restored.GetTicketAt(1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NestedOwnersArraysAndLists_PreserveLocalAssociationsAndNullOwners(bool generated)
    {
        var integers = new SwiftTicketStore<int>(ControlState());
        var references = new SwiftTicketStore<DenseTicketPersistencePayload>();
        references.Add(new SwiftTicket(7, 0), new DenseTicketPersistencePayload { Id = 101, Label = "reference" });
        var values = new SwiftTicketStore<DenseTicketPersistenceValue>();
        values.Add(new SwiftTicket(3, uint.MaxValue), new DenseTicketPersistenceValue { Id = 303, Label = "value" });
        var envelope = new TicketStorePersistenceEnvelope
        {
            ReferenceStore = references,
            ValueStore = values,
            Array = [integers, null],
            List = new List<SwiftTicketStore<int>> { null, integers }
        };
        var options = Options(generated);

        var restored = JsonSerializer.Deserialize<TicketStorePersistenceEnvelope>(JsonSerializer.Serialize(envelope, options), options);

        Assert.Equal(101, restored.ReferenceStore.GetRef(new SwiftTicket(7, 0)).Id);
        Assert.Equal("reference", restored.ReferenceStore.GetRef(new SwiftTicket(7, 0)).Label);
        Assert.Equal(303, restored.ValueStore.GetRef(new SwiftTicket(3, uint.MaxValue)).Id);
        Assert.Null(restored.Array[1]);
        Assert.Null(restored.List[0]);
        ContinueAndAssert(restored.Array[0]);
        ContinueAndAssert(restored.List[1]);
        Assert.Equal([101, 202, 303], integers.Values.ToArray());
        Assert.Equal([0, 7, 3], integers.State.DenseSlots);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NamingOptions_ApplyInsideStateWithoutChangingTheCaseSensitiveEnvelope(bool generated)
    {
        var options = Options(generated);
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.PropertyNameCaseInsensitive = true;
        string json = JsonSerializer.Serialize(new SwiftTicketStore<int>(ControlState()), options);
        using var document = JsonDocument.Parse(json);

        Assert.Equal(["State"], document.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal(["denseValues", "denseSlots", "denseGenerations"],
            document.RootElement.GetProperty("State").EnumerateObject().Select(property => property.Name));
        Assert.Equal(202, JsonSerializer.Deserialize<SwiftTicketStore<int>>(json, options).GetRef(new SwiftTicket(7, uint.MaxValue)));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SwiftTicketStore<int>>(
            json.Replace("\"State\":", "\"state\":"), options));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("""{"state":{}}""")]
    [InlineData("""{"Other":{}}""")]
    [InlineData("""{"State":null}""")]
    [InlineData("""{"State":{},"Extra":0}""")]
    [InlineData("""{"State":{},"State":{}}""")]
    [InlineData("""{"State":""")]
    public void InvalidOuterEnvelopes_DoNotReplaceTheExistingOwner(string json)
    {
        foreach (bool generated in new[] { false, true })
        {
            var owner = new SwiftTicketStore<int>(ControlState());
            var original = owner;

            Assert.Throws<JsonException>(() => owner = JsonSerializer.Deserialize<SwiftTicketStore<int>>(json, Options(generated)));

            Assert.Same(original, owner);
            Assert.Equal([101, 202, 303], owner.Values.ToArray());
            Assert.Equal([0, 7, 3], owner.State.DenseSlots);
            Assert.Equal(new uint[] { 0, uint.MaxValue, 9 }, owner.State.DenseGenerations);
            ContinueAndAssert(owner);
        }
    }

    [Theory]
    [InlineData("""{"DenseValues":[1],"DenseSlots":[],"DenseGenerations":[]}""")]
    [InlineData("""{"DenseValues":[1],"DenseSlots":[0]}""")]
    [InlineData("""{"DenseValues":[1],"DenseSlots":[0],"DenseGenerations":null}""")]
    [InlineData("""{"DenseValues":[1,2],"DenseSlots":[0,0],"DenseGenerations":[1,1]}""")]
    [InlineData("""{"DenseValues":[1,2],"DenseSlots":[0,0],"DenseGenerations":[0,4294967295]}""")]
    [InlineData("""{"DenseValues":[1],"DenseSlots":[-1],"DenseGenerations":[0]}""")]
    [InlineData("""{"DenseValues":[1],"DenseSlots":[1073741824],"DenseGenerations":[1]}""")]
    public void InvalidDecodedStructure_PreservesDestinationAndReportsTheStateArgument(string stateJson)
    {
        foreach (bool generated in new[] { false, true })
        {
            var options = Options(generated);
            var owner = new SwiftTicketStore<int>(ControlState());
            var original = owner;

            var error = Assert.Throws<ArgumentException>(() =>
                owner = JsonSerializer.Deserialize<SwiftTicketStore<int>>("""{"State":""" + stateJson + "}", options));

            Assert.Equal("state", error.ParamName);
            Assert.Same(original, owner);
            Assert.Equal([101, 202, 303], owner.Values.ToArray());
            Assert.Equal([0, 7, 3], owner.State.DenseSlots);
            var raw = JsonSerializer.Deserialize<SwiftTicketStoreState<int>>(stateJson, options);
            Assert.Equal("state", Assert.Throws<ArgumentException>(() => new SwiftTicketStore<int>(raw)).ParamName);
        }
    }

    [Theory]
    [InlineData("""{"DenseValues":[7],"DenseSlots":[0],"DenseGenerations":[-1]}""")]
    [InlineData("""{"DenseValues":[7],"DenseSlots":[0],"DenseGenerations":[4294967296]}""")]
    [InlineData("""{"DenseValues":[7],"DenseSlots":[2147483648],"DenseGenerations":[0]}""")]
    [InlineData("""{"DenseValues":{},"DenseSlots":[],"DenseGenerations":[]}""")]
    [InlineData("""{"DenseValues":[7],"DenseSlots":[0],"DenseGenerations":"invalid"}""")]
    [InlineData("""{"DenseValues":[7],"DenseSlots":[0],"DenseGenerations":[0]""")]
    public void UndecodableNumbersShapesAndTruncation_DoNotReplaceTheExistingOwner(string stateJson)
    {
        foreach (bool generated in new[] { false, true })
        {
            var owner = new SwiftTicketStore<int>(ControlState());
            var original = owner;

            Assert.Throws<JsonException>(() =>
                owner = JsonSerializer.Deserialize<SwiftTicketStore<int>>("""{"State":""" + stateJson + "}", Options(generated)));

            Assert.Same(original, owner);
            Assert.Equal([101, 202, 303], owner.Values.ToArray());
            Assert.Equal([0, 7, 3], owner.State.DenseSlots);
            Assert.Equal(new uint[] { 0, uint.MaxValue, 9 }, owner.State.DenseGenerations);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedReplacement_DoesNotMutateExistingReferencePayloadOrItsAssociations(bool generated)
    {
        var options = Options(generated);
        var payload = new DenseTicketPersistencePayload { Id = 101, Label = "original" };
        var owner = new SwiftTicketStore<DenseTicketPersistencePayload>();
        owner.Add(default, payload);
        owner.Add(new SwiftTicket(7, uint.MaxValue), payload);
        var original = owner;
        var enumerator = owner.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        string invalid = """{"State":{"DenseValues":[{"Id":999,"Label":"changed"},{"Id":202,"Label":"other"}],"DenseSlots":[0,0],"DenseGenerations":[0,1]}}""";

        var error = Assert.Throws<ArgumentException>(() =>
            owner = JsonSerializer.Deserialize<SwiftTicketStore<DenseTicketPersistencePayload>>(invalid, options));

        Assert.Equal("state", error.ParamName);
        Assert.Same(original, owner);
        Assert.Same(payload, owner.GetRef(default));
        Assert.Same(payload, owner.GetRef(new SwiftTicket(7, uint.MaxValue)));
        Assert.Equal(101, payload.Id);
        Assert.Equal("original", payload.Label);
        Assert.Equal([0, 7], owner.State.DenseSlots);
        Assert.Equal(new uint[] { 0, uint.MaxValue }, owner.State.DenseGenerations);
        Assert.True(enumerator.MoveNext());
        Assert.Same(payload, enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NestedState_UsesOrdinaryUnknownAndDuplicateFieldHandlingBeforeOwnerValidation(bool generated)
    {
        var owner = JsonSerializer.Deserialize<SwiftTicketStore<int>>(
            """{"State":{"DenseValues":[999],"DenseValues":[7],"DenseSlots":[3],"DenseGenerations":[0],"FutureField":17}}""",
            Options(generated));

        Assert.Equal(7, owner.GetRef(new SwiftTicket(3, 0)));
        Assert.Equal(new SwiftTicket(3, 0), owner.GetTicketAt(0));
        Assert.False(owner.TryAdd(new SwiftTicket(3, uint.MaxValue), 8));
        Assert.Equal(7, owner.GetRef(new SwiftTicket(3, 0)));
        owner.Add(new SwiftTicket(0, uint.MaxValue), 9);
        Assert.Equal([3, 0], owner.State.DenseSlots);
    }

    [Fact]
    public void GeneratedResolver_DoesNotFallBackToReflectionForAnUnregisteredClosedOwner()
    {
        var options = Options(true);
        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(new SwiftTicketStore<decimal>(), options));
    }

    internal static JsonSerializerOptions Options(bool generated)
    {
        if (!generated) return new JsonSerializerOptions();
        var options = new JsonSerializerOptions { TypeInfoResolver = SwiftTicketStoreJsonContext.Default };
        AddClosed<int>(options);
        AddClosed<int?>(options);
        AddClosed<string>(options);
        AddClosed<DenseTicketPersistencePayload>(options);
        AddClosed<DenseTicketPersistenceValue>(options);
        options.Converters.Add(new StateJsonConverter<SwiftDenseTicketMap<int>, SwiftDenseTicketMapState<int>>(
            static state => new SwiftDenseTicketMap<int>(state)));
        return options;
    }

    private static void AddClosed<T>(JsonSerializerOptions options) =>
        options.Converters.Add(new StateJsonConverter<SwiftTicketStore<T>, SwiftTicketStoreState<T>>(
            static state => new SwiftTicketStore<T>(state)));

    private static SwiftTicketStoreState<int> ControlState() =>
        new([101, 202, 303], [0, 7, 3], [0, uint.MaxValue, 9]);

    private static void ContinueAndAssert(SwiftTicketStore<int> owner)
    {
        Assert.True(owner.Remove(new SwiftTicket(7, uint.MaxValue)));
        Assert.False(owner.ContainsKey(new SwiftTicket(7, uint.MaxValue)));
        Assert.True(owner.TryAdd(new SwiftTicket(7, 0), 404));
        owner.Add(new SwiftTicket(17, 1), 505);
        owner.GetRef(default)++;
        Assert.False(owner.Remove(new SwiftTicket(7, uint.MaxValue)));
        Assert.False(owner.TryAdd(new SwiftTicket(7, 1), -1));
        Assert.Equal([102, 303, 404, 505], owner.Values.ToArray());
        Assert.Equal([0, 3, 7, 17], owner.State.DenseSlots);
        Assert.Equal(new uint[] { 0, 9, 0, 1 }, owner.State.DenseGenerations);
        Assert.Equal(new SwiftTicket(7, 0), owner.GetTicketAt(2));
    }
}
