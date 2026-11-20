using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text.Json;
using SwiftCollections.Serialization;
using SwiftCollections.Utility;
using Xunit;

namespace SwiftCollections.Tests;

public partial class HashComparerPersistenceTests
{
    [Theory]
    [MemberData(nameof(Formats))]
    public void SignedSeedsAndFullEntropyRemainCanonicalAcrossBothHashOwners(bool binary)
    {
        foreach (IEqualityComparer<string> comparer in new IEqualityComparer<string>[]
        {
            new SwiftDeterministicStringEqualityComparer(int.MinValue),
            new SwiftDeterministicStringEqualityComparer(int.MaxValue),
            new SwiftStringEqualityComparer(long.MinValue + 17),
            new SwiftStringEqualityComparer(long.MaxValue),
            SwiftHashTools.GetDeterministicObjectEqualityComparer(-194119),
            new SwiftObjectEqualityComparer(unchecked((long)0xFEDCBA9876543210UL))
        })
        {
            var dictionary = new SwiftDictionary<string, int>(8, comparer) { { "Alpha", 1 }, { "Beta", 2 } };
            var set = new SwiftHashSet<string>(new[] { "Alpha", "Beta" }, comparer);
            var restoredDictionary = RoundTrip(dictionary, binary);
            var restoredSet = RoundTrip(set, binary);

            Assert.Equal(comparer.GetType(), restoredDictionary.Comparer.GetType());
            Assert.Equal(comparer.GetType(), restoredSet.Comparer.GetType());
            Assert.True(comparer.Equals(restoredDictionary.Comparer));
            Assert.True(comparer.Equals(restoredSet.Comparer));
            Assert.Equal(1, restoredDictionary["Alpha"]);
            bool containsBeta = restoredSet.Contains("Beta");
            Assert.True(containsBeta);
            Assert.Equal(DescriptorJson(dictionary), DescriptorJson(restoredDictionary));
            Assert.Equal(DescriptorJson(set), DescriptorJson(restoredSet));
        }
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void RandomizedEntropyDecodedFromWirePreservesHighBitsAndSignedConfiguration(bool binary)
    {
        foreach (string id in new[] { "lsf.swift-string-randomized", "lsf.swift-object-randomized" })
        {
            long entropy = unchecked((long)0xFEDCBA9876543210UL);
            var configuration = new byte[8];
            BinaryPrimitives.WriteInt64LittleEndian(configuration, entropy);
            var state = new SwiftDictionaryState<string, int>(new[] { new KeyValuePair<string, int>("Alpha", 9) });
            byte[] payload = Envelope(binary, state, ((byte)2, id, configuration));

            var result = Decode<SwiftDictionary<string, int>>(payload, binary);

            Assert.Equal(9, result["Alpha"]);
            Assert.Equal(SwiftHashTools.MurmurHash3("Alpha", (int)(entropy & int.MaxValue)), result.Comparer.GetHashCode("Alpha"));
            using var descriptor = JsonDocument.Parse(DescriptorJson(result));
            JsonElement rule = descriptor.RootElement[0];
            Assert.Equal(id, rule.GetProperty("Id").GetString());
            Assert.Equal(configuration, rule.GetProperty("Config").GetBytesFromBase64());
            Assert.Equal(9, RoundTrip(result, binary)["Alpha"]);
        }
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void CollisionHeavyRestoreKeepsItsResolvedSeedWithoutSamplingNewEntropy(bool binary)
    {
        const int seed = -7719;
        string[] values = CollidingStrings(seed);
        var pairs = new KeyValuePair<string, int>[values.Length];
        for (int index = 0; index < pairs.Length; index++) pairs[index] = new(values[index], index);
        var dictionaryState = new SwiftDictionaryState<string, int>(pairs);
        var setState = new SwiftArrayState<string>(values);
        var dictionary = Decode<SwiftDictionary<string, int>>(Envelope(binary, dictionaryState,
            ((byte)2, "lsf.swift-string-seeded", Int32Configuration(seed))), binary);
        var set = Decode<SwiftHashSet<string>>(Envelope(binary, setState,
            ((byte)3, "lsf.swift-string-seeded", Int32Configuration(seed))), binary);

        Assert.Equal(seed, Assert.IsType<SwiftDeterministicStringEqualityComparer>(dictionary.Comparer).Seed);
        Assert.Equal(seed, Assert.IsType<SwiftDeterministicStringEqualityComparer>(set.Comparer).Seed);
        Assert.Equal(128, dictionary.Count);
        Assert.Equal(128, set.Count);
        for (int index = 0; index < values.Length; index++)
        {
            Assert.Equal(index, dictionary[values[index]]);
            bool present = set.Contains(values[index]);
            Assert.True(present);
        }
        Assert.Equal(seed, Assert.IsType<SwiftDeterministicStringEqualityComparer>(RoundTrip(dictionary, binary).Comparer).Seed);
        Assert.Equal(seed, Assert.IsType<SwiftDeterministicStringEqualityComparer>(RoundTrip(set, binary).Comparer).Seed);
        Assert.True(dictionary.Remove(values[0]));
        Assert.True(set.Remove(values[0]));
        Assert.True(dictionary.Add("ordinary-continuation", 123));
        Assert.True(set.Add("ordinary-continuation"));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void BidirectionalConfiguredSeedAndEffectiveRandomizedReverseRuleRemainDistinct(bool binary)
    {
        const int seed = -123;
        const long entropy = long.MinValue + 31;
        var config = new byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(config, entropy);
        var state = new SwiftDictionaryState<int, string>(new[]
        {
            new KeyValuePair<int, string>(1, "One"), new KeyValuePair<int, string>(2, "Two")
        });
        byte[] payload = Envelope(binary, state,
            ((byte)2, "lsf.runtime-equality-default", Array.Empty<byte>()),
            ((byte)4, "lsf.swift-string-seeded", Int32Configuration(seed)),
            ((byte)5, "lsf.swift-string-randomized", config));

        var result = Decode<SwiftBiDictionary<int, string>>(payload, binary);
        var reloaded = RoundTrip(result, binary);

        Assert.Equal(1, reloaded.GetKey("One"));
        Assert.Equal("Two", reloaded[2]);
        using var document = JsonDocument.Parse(DescriptorJson(reloaded));
        JsonElement configured = document.RootElement[1], effective = document.RootElement[2];
        Assert.Equal(4, configured.GetProperty("Role").GetInt32());
        Assert.Equal("lsf.swift-string-seeded", configured.GetProperty("Id").GetString());
        Assert.Equal(Int32Configuration(seed), configured.GetProperty("Config").GetBytesFromBase64());
        Assert.Equal(5, effective.GetProperty("Role").GetInt32());
        Assert.Equal("lsf.swift-string-randomized", effective.GetProperty("Id").GetString());
        Assert.Equal(config, effective.GetProperty("Config").GetBytesFromBase64());
        Assert.True(reloaded.Remove(1));
        Assert.False(reloaded.TryGetKey("One", out _));
        Assert.True(reloaded.Add(3, "Three"));
        Assert.Equal(3, reloaded.GetKey("Three"));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void OrdinaryBidirectionalCollisionFallbackPersistsTheEffectiveReverseEntropy(bool binary)
    {
        const int seed = -7719;
        string[] values = CollidingStrings(seed);
        var source = new SwiftBiDictionary<int, string>(null, new SwiftDeterministicStringEqualityComparer(seed));
        for (int index = 0; index < values.Length; index++) Assert.True(source.Add(index, values[index]));
        string sourceDescriptors = DescriptorJson(source);
        using var descriptors = JsonDocument.Parse(sourceDescriptors);
        JsonElement configured = descriptors.RootElement[1], effective = descriptors.RootElement[2];
        Assert.Equal("lsf.swift-string-seeded", configured.GetProperty("Id").GetString());
        Assert.Equal(Int32Configuration(seed), configured.GetProperty("Config").GetBytesFromBase64());
        Assert.Equal("lsf.swift-string-randomized", effective.GetProperty("Id").GetString());
        Assert.Equal(8, effective.GetProperty("Config").GetBytesFromBase64().Length);

        var restored = RoundTrip(source, binary);

        Assert.Equal(sourceDescriptors, DescriptorJson(restored));
        Assert.Equal(128, restored.Count);
        for (int index = 0; index < values.Length; index++)
        {
            Assert.Equal(values[index], restored[index]);
            Assert.Equal(index, restored.GetKey(values[index]));
        }
        Assert.True(restored.Remove(0));
        Assert.False(restored.TryGetKey(values[0], out _));
        Assert.True(restored.Add(128, "ordinary-continuation"));
        Assert.Equal(128, restored.GetKey("ordinary-continuation"));
        Assert.Equal(128, source.Count);
        Assert.Equal(0, source.GetKey(values[0]));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void BidirectionalObjectFallbackCanBeUsedContravariantlyForStringReverseValues(bool binary)
    {
        var entropy = new byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(entropy, long.MinValue + 9);
        var state = new SwiftDictionaryState<int, string>(new[] { new KeyValuePair<int, string>(1, "One") });
        byte[] payload = Envelope(binary, state,
            ((byte)2, "lsf.runtime-equality-default", Array.Empty<byte>()),
            ((byte)4, "lsf.swift-object-seeded", Int32Configuration(-17)),
            ((byte)5, "lsf.swift-object-randomized", entropy));

        var result = RoundTrip(Decode<SwiftBiDictionary<int, string>>(payload, binary), binary);

        Assert.Equal(1, result.GetKey("One"));
        using var descriptors = JsonDocument.Parse(DescriptorJson(result));
        Assert.Equal("lsf.swift-object-seeded", descriptors.RootElement[1].GetProperty("Id").GetString());
        Assert.Equal("lsf.swift-object-randomized", descriptors.RootElement[2].GetProperty("Id").GetString());
        Assert.Equal(entropy, descriptors.RootElement[2].GetProperty("Config").GetBytesFromBase64());
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void RuntimeDefaultReverseEqualityAcceptsOnlyItsRecognizedRandomizedFallback(bool binary)
    {
        var entropy = new byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(entropy, -19);
        var strings = new SwiftDictionaryState<int, string>(Array.Empty<KeyValuePair<int, string>>());
        var objects = new SwiftDictionaryState<int, object>(Array.Empty<KeyValuePair<int, object>>());
        var forward = ((byte)2, "lsf.runtime-equality-default", Array.Empty<byte>());
        var reverse = ((byte)4, "lsf.runtime-equality-default", Array.Empty<byte>());
        var stringFallback = ((byte)5, "lsf.swift-string-randomized", entropy);
        var objectFallback = ((byte)5, "lsf.swift-object-randomized", entropy);

        var stringOwner = Decode<SwiftBiDictionary<int, string>>(Envelope(binary, strings, forward, reverse, stringFallback), binary);
        var objectOwner = Decode<SwiftBiDictionary<int, object>>(Envelope(binary, objects, forward, reverse, objectFallback), binary);

        Assert.Empty(stringOwner);
        Assert.Empty(objectOwner);
        Assert.True(stringOwner.Add(1, "One"));
        Assert.Equal(1, stringOwner.GetKey("One"));
        Assert.True(objectOwner.Add(1, "One"));
        Assert.Equal(1, objectOwner.GetKey("One"));
        AssertInvalid<SwiftBiDictionary<int, string>>(Envelope(binary, strings, forward, reverse, objectFallback), binary);
        AssertInvalid<SwiftBiDictionary<int, object>>(Envelope(binary, objects, forward, reverse, stringFallback), binary);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void OneInstanceCanRegisterIndependentOrderingAndEqualityRulesUnderTheSameIdentity(bool binary)
    {
        var comparer = new DualRuleComparer();
        var registry = new SwiftComparerRegistryBuilder()
            .RegisterOrdering<int>("test.dual", 1, comparer, static _ => ReadOnlyMemory<byte>.Empty, _ => (comparer, 0))
            .RegisterEquality<int>("test.dual", 1, comparer, static _ => ReadOnlyMemory<byte>.Empty, _ => (comparer, 0)).Build();
        var sorted = new SwiftSortedList<int>(new[] { 1, 2, 3 }, comparer);
        var dictionary = new SwiftDictionary<int, int>(8, comparer) { { 1, 17 } };

        var restoredSorted = RoundTrip(sorted, binary, registry);
        var restoredDictionary = RoundTrip(dictionary, binary, registry);

        Assert.Equal(new[] { 3, 2, 1 }, restoredSorted.AsReadOnlySpan().ToArray());
        Assert.Equal(17, restoredDictionary[11]);
        var orderingOnly = new SwiftComparerRegistryBuilder().RegisterOrdering<int>("test.dual", 1, comparer,
            static _ => ReadOnlyMemory<byte>.Empty, _ => (comparer, 0)).Build();
        Assert.Throws<NotSupportedException>(() => Encode(dictionary, binary, orderingOnly));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void ConfiguredEqualityLambdaCanBeRecapturedWithoutInspectingItsPrivateClosure(bool binary)
    {
        int modulus = 10;
        IEqualityComparer<int> comparer = EqualityComparer<int>.Create((left, right) => left % modulus == right % modulus, value => value % modulus);
        var registry = new SwiftComparerRegistryBuilder().RegisterEquality<int>("test.modulus", 1, comparer,
            _ => new byte[] { (byte)modulus }, static bytes =>
            {
                int decodedModulus = bytes.Span[0];
                return (EqualityComparer<int>.Create((left, right) => left % decodedModulus == right % decodedModulus,
                    value => value % decodedModulus), 1);
            }).Build();
        var source = new SwiftDictionary<int, string>(8, comparer) { { 1, "one" } };

        var result = RoundTrip(RoundTrip(source, binary, registry), binary, registry);

        Assert.Equal("one", result[11]);
        Assert.False(result.Add(21, "duplicate"));
        Assert.True(result.Add(2, "two"));
        Assert.Equal("two", result[12]);
        var unrelated = EqualityComparer<int>.Create(static (left, right) => left == right, static value => value);
        Assert.Equal(comparer.GetType(), unrelated.GetType());
        Assert.Throws<NotSupportedException>(() => Encode(new SwiftHashSet<int>(unrelated), binary, registry));
    }

    private static string[] CollidingStrings(int seed)
    {
        var values = new List<string>(128);
        for (int candidate = 0; candidate < 1_000_000 && values.Count < 128; candidate++)
        {
            string value = candidate.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if ((SwiftHashTools.MurmurHash3(value, seed) & 1023) == 0) values.Add(value);
        }
        Assert.Equal(128, values.Count);
        return values.ToArray();
    }

    private static string DescriptorJson<T>(T owner)
    {
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(owner));
        return json.RootElement.GetProperty("Comparers").GetRawText();
    }

    private sealed class DualRuleComparer : IComparer<int>, IEqualityComparer<int>
    {
        public int Compare(int left, int right) => right.CompareTo(left);
        public bool Equals(int left, int right) => left % 10 == right % 10;
        public int GetHashCode(int value) => value % 10;
    }
}
