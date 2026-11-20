using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using SwiftCollections.Observable;
using SwiftCollections.Serialization;
#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
using MemoryPack;
#endif
using Xunit;

namespace SwiftCollections.Tests;

public partial class HashComparerPersistenceTests
{
    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("[{\"Role\":1,\"Id\":\"lsf.runtime-equality-default\",\"Version\":1,\"Config\":\"\"}]")]
    [InlineData("[{\"Role\":2,\"Id\":\"missing.rule\",\"Version\":1,\"Config\":\"\"}]")]
    [InlineData("[{\"Role\":2,\"Id\":\"lsf.runtime-equality-default\",\"Version\":2,\"Config\":\"\"}]")]
    [InlineData("[{\"Role\":2,\"Id\":\"lsf.runtime-equality-default\",\"Version\":1,\"Config\":\"AA==\"}]")]
    [InlineData("[{\"Role\":2,\"Id\":\"lsf.runtime-equality-default\",\"Version\":1,\"Config\":\"\"},{\"Role\":2,\"Id\":\"lsf.runtime-equality-default\",\"Version\":1,\"Config\":\"\"}]")]
    public void Json_InvalidEqualityMetadataIsRejectedEvenForEmptyOwners(string descriptors)
    {
        string dictionary = JsonEnvelope(descriptors, "{\"Items\":[]}");
        string set = JsonEnvelope(descriptors.Replace("\"Role\":2", "\"Role\":3"), "{\"Items\":[]}");

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SwiftDictionary<int, int>>(dictionary));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SwiftObservableDictionary<int, int>>(dictionary));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SwiftHashSet<int>>(set));
    }

    [Theory]
    [InlineData("{\"State\":{\"Items\":[]}}")]
    [InlineData("{\"FormatVersion\":2,\"Comparers\":[],\"State\":{\"Items\":[]}}")]
    [InlineData("{\"State\":{\"Items\":[]},\"FormatVersion\":1,\"Comparers\":[]}")]
    public void Json_LegacyAndOutOfOrderOwnersDoNotFallBackToDefaultEquality(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SwiftDictionary<int, int>>(json));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SwiftHashSet<int>>(json));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SwiftBiDictionary<int, int>>(json));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SwiftObservableDictionary<int, int>>(json));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void DuplicateKeysAndItemsUnderRestoredEqualityAreRejectedInsteadOfDiscarded(bool binary)
    {
        var dictionaryState = new SwiftDictionaryState<string, int>(new[]
        {
            new KeyValuePair<string, int>("Alpha", 1), new KeyValuePair<string, int>("ALPHA", 2)
        });
        var setState = new SwiftArrayState<string>(new[] { "Alpha", "ALPHA" });
        var descriptor = (Role: (byte)2, Id: "lsf.string-ordinal-ignore-case", Config: Array.Empty<byte>());

        AssertInvalid<SwiftDictionary<string, int>>(Envelope(binary, dictionaryState, descriptor), binary);
        AssertInvalid<SwiftObservableDictionary<string, int>>(Envelope(binary, dictionaryState, descriptor), binary);
        descriptor.Role = 3;
        AssertInvalid<SwiftHashSet<string>>(Envelope(binary, setState, descriptor), binary);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void BidirectionalRestoreRejectsDuplicateReverseValuesAndContradictoryRules(bool binary)
    {
        var state = new SwiftDictionaryState<int, string>(new[]
        {
            new KeyValuePair<int, string>(1, "Alpha"), new KeyValuePair<int, string>(2, "ALPHA")
        });
        var forward = ((byte)2, "lsf.runtime-equality-default", Array.Empty<byte>());
        var reverse = ((byte)4, "lsf.string-ordinal-ignore-case", Array.Empty<byte>());
        var effective = ((byte)5, "lsf.string-ordinal-ignore-case", Array.Empty<byte>());

        AssertInvalid<SwiftBiDictionary<int, string>>(Envelope(binary, state, forward, reverse, effective), binary);
        effective.Item2 = "lsf.string-ordinal";
        AssertInvalid<SwiftBiDictionary<int, string>>(Envelope(binary, state, forward, reverse, effective), binary);
        AssertInvalid<SwiftBiDictionary<int, string>>(Envelope(binary, state, forward, reverse), binary);
        AssertInvalid<SwiftBiDictionary<int, string>>(Envelope(binary, state, forward, effective, reverse), binary);
        AssertInvalid<SwiftBiDictionary<int, string>>(Envelope(binary, state, forward, reverse, reverse), binary);
    }

    [Theory]
    [InlineData("configured-version")]
    [InlineData("effective-version")]
    [InlineData("incompatible-equality")]
    [InlineData("short-seed")]
    [InlineData("short-entropy")]
    public void Json_BidirectionalFallbackRequiresSupportedVersionsConfigurationsAndEquality(string failure)
    {
        string configuredId = failure == "incompatible-equality" ? "lsf.string-ordinal-ignore-case" : "lsf.swift-string-seeded";
        var descriptors = new[]
        {
            new { Role = 2, Id = "lsf.runtime-equality-default", Version = 1, Config = "" },
            new
            {
                Role = 4, Id = configuredId, Version = failure == "configured-version" ? 2 : 1,
                Config = failure == "incompatible-equality" ? "" : Convert.ToBase64String(failure == "short-seed" ? new byte[1] : new byte[4])
            },
            new
            {
                Role = 5, Id = "lsf.swift-string-randomized", Version = failure == "effective-version" ? 2 : 1,
                Config = Convert.ToBase64String(failure == "short-entropy" ? new byte[7] : new byte[8])
            }
        };
        string payload = JsonEnvelope(JsonSerializer.Serialize(descriptors), "{\"Items\":[]}");

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SwiftBiDictionary<int, string>>(payload));
    }

    [Fact]
    public void Json_ValueTypeReverseKeysRejectStringAndObjectRandomizedPolicies()
    {
        var state = new SwiftDictionaryState<int, int>(Array.Empty<KeyValuePair<int, int>>());
        var forward = ((byte)2, "lsf.runtime-equality-default", Array.Empty<byte>());
        var reverse = ((byte)4, "lsf.runtime-equality-default", Array.Empty<byte>());

        AssertInvalid<SwiftBiDictionary<int, int>>(Envelope(false, state, forward, reverse,
            ((byte)5, "lsf.swift-string-randomized", new byte[8])), false);
        AssertInvalid<SwiftBiDictionary<int, int>>(Envelope(false, state, forward, reverse,
            ((byte)5, "lsf.swift-object-randomized", new byte[8])), false);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void UnsupportedOrUnknownCustomEqualityDoesNotBecomeSupportedForEmptyOwners(bool binary)
    {
        var comparer = new PersistenceHashKeyComparer(5);
        var registry = KeyRegistry(comparer);
        var dictionary = new SwiftDictionary<PersistenceHashKey, int>(8, comparer);
        var set = new SwiftHashSet<PersistenceHashKey>(comparer);

        Assert.Throws<NotSupportedException>(() => Encode(dictionary, binary));
        Assert.Throws<NotSupportedException>(() => Encode(set, binary));
        AssertInvalid<SwiftDictionary<PersistenceHashKey, int>>(Encode(dictionary, binary, registry), binary);
        AssertInvalid<SwiftHashSet<PersistenceHashKey>>(Encode(set, binary, registry), binary);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void BaseDictionaryDispatchRejectsBidirectionalOwnersInsteadOfDroppingReverseRules(bool binary)
    {
        SwiftDictionary<string, int> source = new SwiftBiDictionary<string, int>(StringComparer.OrdinalIgnoreCase, null);
        source.Add("One", 1);

        Assert.Throws<NotSupportedException>(() => Encode(source, binary));

        Assert.Equal(1, source["ONE"]);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void RawStateAndComparerlessCollectionsRetainTheirExistingFormats(bool binary)
    {
        var state = new SwiftDictionaryState<int, string>(new[] { new KeyValuePair<int, string>(3, "three") });
        var restoredState = RoundTrip(state, binary);
        Assert.Equal(3, Assert.Single(restoredState.Items).Key);
        Assert.Equal("three", restoredState.Items[0].Value);
        var packed = new SwiftPackedSet<int>();
        packed.Add(3);
        packed.Add(1);
        var restored = RoundTrip(packed, binary);
        Assert.Equal(2, restored.Count);
        Assert.Equal(new[] { 3, 1 }, restored.ToArray());
        bool containsThree = restored.Contains(3);
        Assert.True(containsThree);
        if (!binary)
        {
            Assert.StartsWith("{\"State\":", JsonSerializer.Serialize(packed));
            Assert.DoesNotContain("Comparers", JsonSerializer.Serialize(state));
        }
    }

#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
    [Fact]
    public void MemoryPack_FailedHashRestoreKeepsExistingOwnersAndTheirSubscribers()
    {
        var originalDictionary = new SwiftDictionary<int, int> { { 10, 20 } };
        var dictionary = originalDictionary;
        var originalSet = new SwiftHashSet<int>(new[] { 10, 20 });
        var set = originalSet;
        var originalBidirectional = new SwiftBiDictionary<int, int> { { 10, 20 } };
        var bidirectional = originalBidirectional;
        var originalObservable = new SwiftObservableDictionary<int, int> { { 10, 20 } };
        var observable = originalObservable;
        int events = 0;
        originalObservable.CollectionChanged += (_, _) => events++;

        Assert.Throws<MemoryPackSerializationException>(() => MemoryPackSerializer.Deserialize(
            Encode(new SwiftDictionary<int, int> { { 1, 2 } }, true)[..^1], ref dictionary));
        Assert.Throws<MemoryPackSerializationException>(() => MemoryPackSerializer.Deserialize(
            Encode(new SwiftHashSet<int>(new[] { 1, 2 }), true)[..^1], ref set));
        Assert.Throws<MemoryPackSerializationException>(() => MemoryPackSerializer.Deserialize(
            Encode(new SwiftBiDictionary<int, int> { { 1, 2 } }, true)[..^1], ref bidirectional));
        Assert.Throws<MemoryPackSerializationException>(() => MemoryPackSerializer.Deserialize(
            Encode(new SwiftObservableDictionary<int, int> { { 1, 2 } }, true)[..^1], ref observable));

        Assert.Same(originalDictionary, dictionary);
        Assert.Same(originalSet, set);
        Assert.Same(originalBidirectional, bidirectional);
        Assert.Same(originalObservable, observable);
        Assert.Equal(20, dictionary[10]);
        Assert.Equal(2, set.Count);
        Assert.Equal(10, bidirectional.GetKey(20));
        Assert.Equal(20, observable[10]);
        Assert.Equal(0, events);
        observable.Add(30, 40);
        Assert.Equal(1, events);
    }
#endif

    private static void AssertInvalid<T>(byte[] bytes, bool binary, SwiftComparerRegistry registry = null)
    {
#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
        if (binary)
        {
            Assert.Throws<MemoryPackSerializationException>(() => Decode<T>(bytes, true, registry));
            return;
        }
#endif
        Assert.Throws<JsonException>(() => Decode<T>(bytes, false, registry));
    }

    private static string JsonEnvelope(string descriptors, string state)
        => "{\"FormatVersion\":1,\"Comparers\":" + descriptors + ",\"State\":" + state + "}";

    private static byte[] Envelope<TState>(bool binary, TState state, params (byte Role, string Id, byte[] Config)[] descriptors)
    {
#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
        if (binary)
        {
            var buffer = new ArrayBufferWriter<byte>();
            using var optionalState = MemoryPackWriterOptionalStatePool.Rent(null);
            var writer = new MemoryPackWriter<ArrayBufferWriter<byte>>(ref buffer, optionalState);
            writer.WriteObjectHeader(3);
            writer.WriteUnmanaged((ushort)1);
            writer.WriteCollectionHeader(descriptors.Length);
            foreach (var descriptor in descriptors)
            {
                writer.WriteObjectHeader(4);
                writer.WriteUnmanaged(descriptor.Role);
                writer.WriteString(descriptor.Id);
                writer.WriteUnmanaged((ushort)1);
                writer.WriteArray(descriptor.Config);
            }
            writer.WriteValue(state);
            writer.Flush();
            return buffer.WrittenSpan.ToArray();
        }
#endif
        var jsonDescriptors = new List<object>();
        foreach (var descriptor in descriptors)
            jsonDescriptors.Add(new { descriptor.Role, descriptor.Id, Version = 1, Config = Convert.ToBase64String(descriptor.Config) });
        string json = JsonEnvelope(JsonSerializer.Serialize(jsonDescriptors), JsonSerializer.Serialize(state));
        return System.Text.Encoding.UTF8.GetBytes(json);
    }
}
