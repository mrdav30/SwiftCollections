using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Chronicler.Serialization;
using Xunit;

namespace SwiftCollections.Tests;

public class SwiftDenseTicketMapJsonTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OwnerAndRawState_UseOnlyLogicalFieldsAndPreserveExactContinuation(bool generated)
    {
        var options = Options(generated);
        var original = new SwiftDenseTicketMap<int>(PartitionedState());
        original.EnsureCapacity(128);
        string json = JsonSerializer.Serialize(original, options);

        Assert.Equal("""{"State":{"DenseValues":[101,303],"DenseSlots":[0,2],"SlotGenerations":[1,2,4294967295,0,3],"FreeSlots":[1,4]}}""", json);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(["State"], document.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal(["DenseValues", "DenseSlots", "SlotGenerations", "FreeSlots"],
            document.RootElement.GetProperty("State").EnumerateObject().Select(property => property.Name));
        var restored = JsonSerializer.Deserialize<SwiftDenseTicketMap<int>>(json, options);
        var raw = JsonSerializer.Deserialize<SwiftDenseTicketMapState<int>>(
            JsonSerializer.Serialize(original.State, options), options);

        ContinueAndAssert(original);
        ContinueAndAssert(restored);
        ContinueAndAssert(new SwiftDenseTicketMap<int>(raw));
        Assert.Equal(JsonSerializer.Serialize(original, options), JsonSerializer.Serialize(restored, options));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SnapshotAndSerializedRestore_ContinueTheSameAgentTimeline(bool generated)
    {
        var options = Options(generated);
        var agents = new SwiftDenseTicketMap<int>();
        SwiftTicket first = agents.Add(0);
        SwiftTicket removed = agents.Add(10);
        SwiftTicket moved = agents.Add(100);
        agents.GetRef(first) += 2;
        agents.GetRef(removed)--;
        agents.GetRef(moved) += 3;
        agents.Remove(removed);
        SwiftTicket replacement = agents.Add(-5);
        var raw = agents.State;
        var resumed = JsonSerializer.Deserialize<SwiftDenseTicketMap<int>>(JsonSerializer.Serialize(agents, options), options);
        var rebuilt = new SwiftDenseTicketMap<int>(raw);

        foreach (var owner in new[] { agents, resumed, rebuilt })
        {
            for (int tick = 2; tick <= 3; tick++)
            {
                owner.GetRef(first) += 2;
                owner.GetRef(moved) += 3;
                owner.GetRef(replacement) += 4;
            }
            Assert.Equal([6, 109, 3], owner.Values.ToArray());
            Assert.Equal([0, 2, 1], owner.State.DenseSlots);
            Assert.False(owner.TryGet(removed, out int discarded));
            Assert.Equal(0, discarded);
            Assert.Equal(new SwiftTicket(1, 2), replacement);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NullOwnersDefaultStateAndExplicitNullArrays_HaveDistinctSupportedResults(bool generated)
    {
        var options = Options(generated);
        Assert.Equal("null", JsonSerializer.Serialize<SwiftDenseTicketMap<int>>(null, options));
        Assert.Null(JsonSerializer.Deserialize<SwiftDenseTicketMap<int>>("null", options));

        var empty = new SwiftDenseTicketMap<int>(default(SwiftDenseTicketMapState<int>));
        Assert.Equal("""{"State":{"DenseValues":[],"DenseSlots":[],"SlotGenerations":[],"FreeSlots":[]}}""",
            JsonSerializer.Serialize(empty, options));
        var rawDefault = default(SwiftDenseTicketMapState<int>);
        Assert.Equal("""{"DenseValues":null,"DenseSlots":null,"SlotGenerations":null,"FreeSlots":null}""",
            JsonSerializer.Serialize(rawDefault, options));
        var decodedDefault = JsonSerializer.Deserialize<SwiftDenseTicketMapState<int>>(JsonSerializer.Serialize(rawDefault, options), options);
        Assert.Empty(decodedDefault.DenseValues);
        Assert.Empty(decodedDefault.DenseSlots);
        Assert.Empty(decodedDefault.SlotGenerations);
        Assert.Empty(decodedDefault.FreeSlots);
        Assert.Equal(new SwiftTicket(0, 1), new SwiftDenseTicketMap<int>(decodedDefault).Add(7));
        foreach (string json in new[]
        {
            """{"State":{}}""",
            """{"State":{"DenseValues":null,"DenseSlots":null,"SlotGenerations":null,"FreeSlots":null}}"""
        })
        {
            var decoded = JsonSerializer.Deserialize<SwiftDenseTicketMap<int>>(json, options);
            Assert.Empty(decoded);
            Assert.Equal(new SwiftTicket(0, 1), decoded.Add(7));
        }
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SwiftDenseTicketMapState<int>>("null", options));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NullableAndStringValues_KeepNullDefaultAndDuplicateEntries(bool generated)
    {
        var options = Options(generated);
        var nullable = new SwiftDenseTicketMap<int?>();
        SwiftTicket missingValue = nullable.Add(null);
        SwiftTicket zero = nullable.Add(0);
        nullable.Add(7);
        var nullableCopy = JsonSerializer.Deserialize<SwiftDenseTicketMap<int?>>(JsonSerializer.Serialize(nullable, options), options);
        Assert.True(nullableCopy.TryGet(missingValue, out int? value));
        Assert.Null(value);
        Assert.Equal(0, nullableCopy.GetRef(zero));
        Assert.Equal(new int?[] { null, 0, 7 }, nullableCopy.Values.ToArray());

        var strings = new SwiftDenseTicketMap<string>();
        SwiftTicket firstNull = strings.Add(null);
        strings.Add("duplicate");
        strings.Add("duplicate");
        strings.Add(null);
        var stringCopy = JsonSerializer.Deserialize<SwiftDenseTicketMap<string>>(JsonSerializer.Serialize(strings, options), options);
        Assert.True(stringCopy.TryGet(firstNull, out string text));
        Assert.Null(text);
        Assert.Equal(new[] { null, "duplicate", "duplicate", null }, stringCopy.Values.ToArray());
        Assert.False(stringCopy.IsValid(default));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyOwnerSerialization_PreservesRetiredHistoryAndClearedFreeOrder(bool generated)
    {
        var options = Options(generated);
        var retired = new SwiftDenseTicketMap<int>(new SwiftDenseTicketMapState<int>([], [], [0], []));
        var retiredCopy = JsonSerializer.Deserialize<SwiftDenseTicketMap<int>>(JsonSerializer.Serialize(retired, options), options);
        Assert.Empty(retiredCopy);
        Assert.Equal(new uint[] { 0 }, retiredCopy.State.SlotGenerations);
        Assert.Equal(new SwiftTicket(1, 1), retiredCopy.Add(7));

        var cleared = new SwiftDenseTicketMap<int>(PartitionedState());
        cleared.Clear();
        var clearedCopy = JsonSerializer.Deserialize<SwiftDenseTicketMap<int>>(JsonSerializer.Serialize(cleared, options), options);
        Assert.Empty(clearedCopy);
        Assert.Equal([1, 4, 0], clearedCopy.State.FreeSlots);
        foreach (SwiftTicket expected in new[] { new SwiftTicket(0, 2), new SwiftTicket(4, 3), new SwiftTicket(1, 2), new SwiftTicket(5, 1) })
            Assert.Equal(expected, clearedCopy.Add(7));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReferencePayload_RawSnapshotsShareObjectsButSerializedCopiesPreserveValuesIndependently(bool generated)
    {
        var options = Options(generated);
        var payload = new DenseTicketPersistencePayload { Id = 101, Label = "before" };
        var map = new SwiftDenseTicketMap<DenseTicketPersistencePayload>();
        SwiftTicket live = map.Add(payload);
        map.Add(null);
        map.Add(payload);
        var raw = new SwiftDenseTicketMap<DenseTicketPersistencePayload>(map.State);
        var decoded = JsonSerializer.Deserialize<SwiftDenseTicketMap<DenseTicketPersistencePayload>>(JsonSerializer.Serialize(map, options), options);

        payload.Label = "after";
        Assert.Same(payload, raw.GetRef(live));
        Assert.Equal("after", raw.GetRef(live).Label);
        Assert.NotSame(payload, decoded.GetRef(live));
        Assert.Equal(101, decoded.GetRef(live).Id);
        Assert.Equal("before", decoded.GetRef(live).Label);
        Assert.Null(decoded.Values[1]);
        Assert.Equal("before", decoded.Values[2].Label);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NestedOwnersArraysAndLists_PreserveEachOwnerStateWithoutSharingTheOriginal(bool generated)
    {
        var integers = new SwiftDenseTicketMap<int>(PartitionedState());
        var references = new SwiftDenseTicketMap<DenseTicketPersistencePayload>();
        SwiftTicket reference = references.Add(new DenseTicketPersistencePayload { Id = 101, Label = "reference" });
        var values = new SwiftDenseTicketMap<DenseTicketPersistenceValue>();
        SwiftTicket value = values.Add(new DenseTicketPersistenceValue { Id = 303, Label = "value" });
        var envelope = new DenseTicketPersistenceEnvelope
        {
            ReferenceMap = references,
            ValueMap = values,
            Array = [integers, null],
            List = new List<SwiftDenseTicketMap<int>> { null, integers }
        };
        var options = Options(generated);

        var decoded = JsonSerializer.Deserialize<DenseTicketPersistenceEnvelope>(JsonSerializer.Serialize(envelope, options), options);

        Assert.Equal(101, decoded.ReferenceMap.GetRef(reference).Id);
        Assert.Equal("reference", decoded.ReferenceMap.GetRef(reference).Label);
        Assert.Equal(303, decoded.ValueMap.GetRef(value).Id);
        Assert.Null(decoded.Array[1]);
        Assert.Null(decoded.List[0]);
        ContinueAndAssert(decoded.Array[0]);
        ContinueAndAssert(decoded.List[1]);
        Assert.Equal([101, 303], integers.Values.ToArray());
        Assert.Equal([1, 4], integers.State.FreeSlots);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NamingOptions_ApplyToStateFieldsWhileOuterStateRemainsCaseSensitive(bool generated)
    {
        var options = Options(generated);
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.PropertyNameCaseInsensitive = true;
        var map = new SwiftDenseTicketMap<int>();
        SwiftTicket live = map.Add(7);
        string json = JsonSerializer.Serialize(map, options);
        using var document = JsonDocument.Parse(json);

        Assert.Equal(["State"], document.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal(["denseValues", "denseSlots", "slotGenerations", "freeSlots"],
            document.RootElement.GetProperty("State").EnumerateObject().Select(property => property.Name));
        Assert.Equal(7, JsonSerializer.Deserialize<SwiftDenseTicketMap<int>>(json, options).GetRef(live));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SwiftDenseTicketMap<int>>(
            json.Replace("\"State\":", "\"state\":"), options));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReferenceContainingValue_RoundTripsNestedDataAndTicketAssociation(bool generated)
    {
        var options = Options(generated);
        var payload = new DenseTicketPersistencePayload { Id = 17, Label = "nested" };
        var map = new SwiftDenseTicketMap<DenseTicketPersistenceValue>();
        SwiftTicket removed = map.Add(default);
        SwiftTicket moved = map.Add(new DenseTicketPersistenceValue { Id = 303, Label = "value", Detail = payload });
        map.Remove(removed);
        var raw = new SwiftDenseTicketMap<DenseTicketPersistenceValue>(map.State);
        var decoded = JsonSerializer.Deserialize<SwiftDenseTicketMap<DenseTicketPersistenceValue>>(JsonSerializer.Serialize(map, options), options);

        payload.Label = "mutated";
        Assert.Equal(moved, decoded.GetTicketAt(0));
        Assert.Equal(303, decoded.GetRef(moved).Id);
        Assert.Equal("value", decoded.GetRef(moved).Label);
        Assert.Same(payload, raw.GetRef(moved).Detail);
        Assert.Equal("nested", decoded.GetRef(moved).Detail.Label);
        Assert.NotSame(payload, decoded.GetRef(moved).Detail);
        Assert.False(decoded.IsValid(removed));
        Assert.Equal(new SwiftTicket(0, 2), decoded.Add(default));
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
    public void InvalidOuterEnvelopes_FailBeforeReplacingAnExistingOwner(string json)
    {
        foreach (bool generated in new[] { false, true })
        {
            var owner = new SwiftDenseTicketMap<int>();
            SwiftTicket live = owner.Add(11);
            var original = owner;
            Assert.Throws<JsonException>(() => owner = JsonSerializer.Deserialize<SwiftDenseTicketMap<int>>(json, Options(generated)));
            Assert.Same(original, owner);
            Assert.Equal(11, owner.GetRef(live));
            Assert.Equal(new SwiftTicket(1, 1), owner.Add(22));
        }
    }

    [Theory]
    [InlineData("""{"DenseValues":[1],"DenseSlots":[],"SlotGenerations":[],"FreeSlots":[]}""")]
    [InlineData("""{"DenseValues":[1],"DenseSlots":[0],"SlotGenerations":[],"FreeSlots":[]}""")]
    [InlineData("""{"DenseValues":[1,2],"DenseSlots":[0,0],"SlotGenerations":[1],"FreeSlots":[]}""")]
    [InlineData("""{"DenseValues":[1],"DenseSlots":[0],"SlotGenerations":[0],"FreeSlots":[]}""")]
    [InlineData("""{"DenseValues":[1],"DenseSlots":[0],"SlotGenerations":[1],"FreeSlots":[0]}""")]
    [InlineData("""{"DenseValues":[],"DenseSlots":[],"SlotGenerations":[1],"FreeSlots":[-1]}""")]
    [InlineData("""{"DenseValues":[],"DenseSlots":[],"SlotGenerations":[1],"FreeSlots":[1]}""")]
    [InlineData("""{"DenseValues":[],"DenseSlots":[],"SlotGenerations":[1],"FreeSlots":[0,0]}""")]
    [InlineData("""{"DenseValues":[],"DenseSlots":[],"SlotGenerations":[0],"FreeSlots":[0]}""")]
    [InlineData("""{"DenseValues":[],"DenseSlots":[],"SlotGenerations":[1],"FreeSlots":[]}""")]
    public void StructurallyInvalidDecodedState_PropagatesTheStateArgumentAndDoesNotReplaceOwner(string stateJson)
    {
        foreach (bool generated in new[] { false, true })
        {
            var options = Options(generated);
            var original = new SwiftDenseTicketMap<int>();
            SwiftTicket live = original.Add(17);
            var owner = original;
            var error = Assert.Throws<ArgumentException>(() =>
                owner = JsonSerializer.Deserialize<SwiftDenseTicketMap<int>>("""{"State":""" + stateJson + "}", options));
            Assert.Equal("state", error.ParamName);
            Assert.Same(original, owner);
            Assert.Equal(17, owner.GetRef(live));
            var raw = JsonSerializer.Deserialize<SwiftDenseTicketMapState<int>>(stateJson, options);
            Assert.Equal("state", Assert.Throws<ArgumentException>(() => new SwiftDenseTicketMap<int>(raw)).ParamName);
        }
    }

    [Theory]
    [InlineData("""{"DenseValues":[7],"DenseSlots":[0],"SlotGenerations":[-1],"FreeSlots":[]}""")]
    [InlineData("""{"DenseValues":[7],"DenseSlots":[0],"SlotGenerations":[4294967296],"FreeSlots":[]}""")]
    [InlineData("""{"DenseValues":[7],"DenseSlots":[2147483648],"SlotGenerations":[1],"FreeSlots":[]}""")]
    [InlineData("""{"DenseValues":{},"DenseSlots":[],"SlotGenerations":[],"FreeSlots":[]}""")]
    public void UndecodableStateNumbersAndShapes_ThrowJsonExceptionWithoutReplacingOwner(string stateJson)
    {
        foreach (bool generated in new[] { false, true })
        {
            var original = new SwiftDenseTicketMap<int>();
            SwiftTicket live = original.Add(17);
            var owner = original;
            Assert.Throws<JsonException>(() =>
                owner = JsonSerializer.Deserialize<SwiftDenseTicketMap<int>>("""{"State":""" + stateJson + "}", Options(generated)));
            Assert.Same(original, owner);
            Assert.Equal(17, owner.GetRef(live));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NestedDto_UsesOrdinaryJsonFieldHandlingAndStillValidatesFinalStructure(bool generated)
    {
        var map = JsonSerializer.Deserialize<SwiftDenseTicketMap<int>>(
            """{"State":{"DenseValues":[999],"DenseValues":[7],"DenseSlots":[0],"SlotGenerations":[1],"FreeSlots":[],"FutureField":17}}""",
            Options(generated));

        Assert.Equal(7, map.GetRef(new SwiftTicket(0, 1)));
        Assert.Equal(new SwiftTicket(1, 1), map.Add(8));
    }

    private static JsonSerializerOptions Options(bool generated)
    {
        if (!generated) return new JsonSerializerOptions();
        // A generated resolver is the entire resolver chain; no reflection fallback is installed.
        var options = new JsonSerializerOptions { TypeInfoResolver = SwiftDenseTicketMapJsonContext.Default };
        AddClosed<int>(options);
        AddClosed<int?>(options);
        AddClosed<string>(options);
        AddClosed<DenseTicketPersistencePayload>(options);
        AddClosed<DenseTicketPersistenceValue>(options);
        return options;
    }

    private static void AddClosed<T>(JsonSerializerOptions options) =>
        options.Converters.Add(new StateJsonConverter<SwiftDenseTicketMap<T>, SwiftDenseTicketMapState<T>>(
            static state => new SwiftDenseTicketMap<T>(state)));

    private static SwiftDenseTicketMapState<int> PartitionedState() =>
        new([101, 303], [0, 2], [1, 2, uint.MaxValue, 0, 3], [1, 4]);

    private static void ContinueAndAssert(SwiftDenseTicketMap<int> map)
    {
        Assert.Equal(new SwiftTicket(4, 3), map.Add(404));
        Assert.True(map.Remove(new SwiftTicket(2, uint.MaxValue)));
        Assert.Equal(new SwiftTicket(1, 2), map.Add(202));
        Assert.True(map.Remove(new SwiftTicket(0, 1)));
        Assert.Equal(new SwiftTicket(0, 2), map.Add(505));
        Assert.Equal([202, 404, 505], map.Values.ToArray());
        Assert.Equal([1, 4, 0], map.State.DenseSlots);
        Assert.Equal(new uint[] { 2, 2, 0, 0, 3 }, map.State.SlotGenerations);
        Assert.Empty(map.State.FreeSlots);
        Assert.False(map.IsValid(new SwiftTicket(2, uint.MaxValue)));
        Assert.Equal(new SwiftTicket(5, 1), map.Add(606));
    }
}
