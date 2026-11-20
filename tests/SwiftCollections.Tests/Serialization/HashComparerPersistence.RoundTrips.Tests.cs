using System;
using System.Buffers.Binary;
using System.Collections.Generic;
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
    public static TheoryData<bool> Formats => new()
    {
        false,
#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
        true
#endif
    };

    [Theory]
    [MemberData(nameof(Formats))]
    public void CustomDistinctEntriesResolveEqualityBeforePopulationAndSupportContinuation(bool binary)
    {
        var comparer = new PersistenceHashKeyComparer(int.MinValue);
        var registry = KeyRegistry(comparer);
        PersistenceHashKey first = Key(1), second = Key(2);
        Assert.Equal(first, second);
        var dictionary = new SwiftDictionary<PersistenceHashKey, int>(8, comparer) { { first, 10 }, { second, 20 } };
        var set = new SwiftHashSet<PersistenceHashKey>(new[] { first, second }, comparer);

        var restoredDictionary = RoundTrip(dictionary, binary, registry);
        var restoredSet = RoundTrip(set, binary, registry);

        Assert.Equal(2, restoredDictionary.Count);
        Assert.Equal(10, restoredDictionary[Key(1)]);
        Assert.Equal(20, restoredDictionary[Key(2)]);
        Assert.Equal(int.MinValue, Assert.IsType<PersistenceHashKeyComparer>(restoredDictionary.Comparer).Seed);
        Assert.NotSame(comparer, restoredDictionary.Comparer);
        Assert.Equal(2, restoredSet.Count);
        bool containsSecond = restoredSet.Contains(Key(2));
        Assert.True(containsSecond);
        Assert.Equal(int.MinValue, Assert.IsType<PersistenceHashKeyComparer>(restoredSet.Comparer).Seed);
        Assert.True(restoredDictionary.Add(Key(3), 30));
        Assert.True(restoredDictionary.Remove(Key(1)));
        Assert.Equal(30, restoredDictionary[Key(3)]);
        Assert.True(restoredSet.Add(Key(3)));
        Assert.True(restoredSet.Remove(Key(1)));
        Assert.Equal(2, restoredSet.Count);
        Assert.Equal(2, dictionary.Count);
        Assert.Equal(2, set.Count);
        Assert.Equal(2, RoundTrip(restoredDictionary, binary, registry).Count);
        Assert.Equal(2, RoundTrip(restoredSet, binary, registry).Count);
        Assert.Throws<NotSupportedException>(() => Encode(restoredDictionary, binary));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void BuiltInEqualityRulesPreserveLookupSemanticsAndNullRejection(bool binary)
    {
        foreach (IEqualityComparer<string> comparer in new IEqualityComparer<string>[]
        {
            StringComparer.Ordinal, StringComparer.OrdinalIgnoreCase,
            EqualityComparer<string>.Default, new SwiftDeterministicStringEqualityComparer(-189377)
        })
        {
            var sourceDictionary = new SwiftDictionary<string, int>(8, comparer) { { "Alpha", 17 } };
            var sourceSet = new SwiftHashSet<string>(new[] { "Alpha" }, comparer);

            var dictionary = RoundTrip(sourceDictionary, binary);
            var set = RoundTrip(sourceSet, binary);

            Assert.Equal(comparer.Equals("Alpha", "ALPHA"), dictionary.ContainsKey("ALPHA"));
            Assert.Equal(17, dictionary["Alpha"]);
            Assert.Equal(comparer.Equals("Alpha", "ALPHA"), set.Contains("ALPHA"));
            bool containsNull = set.Contains(null);
            Assert.False(containsNull);
            Assert.Equal(comparer.GetHashCode("Alpha"), dictionary.Comparer.GetHashCode("Alpha"));
            Assert.Equal(comparer.GetHashCode("Alpha"), set.Comparer.GetHashCode("Alpha"));
            Assert.Throws<ArgumentNullException>(() => set.Add(null));
            Assert.Throws<ArgumentNullException>(() => set.Remove(null));
            Assert.True(dictionary.Add("Beta", 23));
            Assert.Equal(2, dictionary.Count);
        }
        Assert.Null(RoundTrip<SwiftDictionary<int, int>>(null, binary));
        Assert.Null(RoundTrip<SwiftHashSet<int>>(null, binary));
        Assert.Empty(RoundTrip(new SwiftDictionary<int, int>(), binary));
        Assert.Empty(RoundTrip(new SwiftHashSet<int>(), binary));
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void BidirectionalAndObservableOwnersPreserveAllRulesWithoutRestorationEvents(bool binary)
    {
        var comparer = new PersistenceHashKeyComparer(37);
        var registry = KeyRegistry(comparer);
        var bidirectional = new SwiftBiDictionary<PersistenceHashKey, string>(comparer, StringComparer.OrdinalIgnoreCase);
        Assert.True(bidirectional.Add(Key(1), "First"));
        Assert.True(bidirectional.Add(Key(2), "Second"));
        var observable = new SwiftObservableDictionary<PersistenceHashKey, int>();
        observable.SetComparer(comparer);
        observable.Add(Key(1), 10);
        observable.Add(Key(2), 20);
        int sourceEvents = 0;
        observable.CollectionChanged += (_, _) => sourceEvents++;
        observable.PropertyChanged += (_, _) => sourceEvents++;

        var restoredBidirectional = RoundTrip(bidirectional, binary, registry);
        var restoredObservable = RoundTrip(observable, binary, registry);

        Assert.Equal(0, sourceEvents);
        Assert.Equal(2, restoredBidirectional.Count);
        Assert.Equal(1, restoredBidirectional.GetKey("FIRST").Identity);
        Assert.Equal(2, restoredBidirectional.GetKey("second").Identity);
        Assert.Equal("First", restoredBidirectional[Key(1)]);
        Assert.False(restoredBidirectional.Add(Key(3), "FIRST"));
        Assert.True(restoredBidirectional.Remove(Key(1)));
        Assert.False(restoredBidirectional.TryGetKey("first", out _));
        Assert.Equal(2, restoredObservable.Count);
        Assert.Equal(20, restoredObservable[Key(2)]);
        int restoredEvents = 0;
        restoredObservable.CollectionChanged += (_, _) => restoredEvents++;
        restoredObservable.Add(Key(3), 30);
        Assert.Equal(1, restoredEvents);
        Assert.Equal(0, sourceEvents);
        Assert.IsType<SwiftObservableDictionary<PersistenceHashKey, int>>(restoredObservable);
        Assert.Single(RoundTrip(restoredBidirectional, binary, registry));
        Assert.Equal(3, RoundTrip(restoredObservable, binary, registry).Count);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void NestedOwnersAndPackableContainersShareTheRegisteredEqualityContract(bool binary)
    {
        var comparer = new PersistenceHashKeyComparer(-41);
        var registry = KeyRegistry(comparer);
        var dictionary = new SwiftDictionary<PersistenceHashKey, int>(8, comparer) { { Key(1), 10 }, { Key(2), 20 } };
        var set = new SwiftHashSet<PersistenceHashKey>(new[] { Key(1), Key(2) }, comparer);
        var bidirectional = new SwiftBiDictionary<PersistenceHashKey, string>(comparer, StringComparer.OrdinalIgnoreCase);
        bidirectional.Add(Key(1), "One");
        var observable = new SwiftObservableDictionary<PersistenceHashKey, int>();
        observable.SetComparer(comparer);
        observable.Add(Key(1), 10);
        var box = new HashPersistenceBox
        {
            Dictionary = dictionary,
            Set = set,
            Bidirectional = bidirectional,
            Observable = observable,
            Array = new[] { dictionary, null },
            List = new List<SwiftHashSet<PersistenceHashKey>> { set, null }
        };

        HashPersistenceBox result;
        if (binary) result = RoundTrip(box, true, registry);
        else
        {
            var context = new HashPersistenceJsonContext(JsonOptions(registry));
            string json = JsonSerializer.Serialize(box, context.HashPersistenceBox);
            result = JsonSerializer.Deserialize(json, context.HashPersistenceBox);
        }

        Assert.Equal(20, result.Dictionary[Key(2)]);
        Assert.Equal(2, result.Set.Count);
        Assert.Equal(1, result.Bidirectional.GetKey("ONE").Identity);
        Assert.Equal(10, result.Observable[Key(1)]);
        Assert.Equal(20, result.Array[0][Key(2)]);
        Assert.Null(result.Array[1]);
        Assert.Equal(2, result.List[0].Count);
        Assert.Null(result.List[1]);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void EqualityRegistrationCanBeRecapturedAcrossAllEqualityRoles(bool binary)
    {
        var comparer = new PersistenceHashKeyComparer(-7);
        var registry = KeyRegistry(comparer);
        var source = new SwiftBiDictionary<PersistenceHashKey, PersistenceHashKey>(comparer, comparer);
        source.Add(Key(1), Key(11));
        source.Add(Key(2), Key(12));

        var once = RoundTrip(source, binary, registry);
        var twice = RoundTrip(once, binary, registry);

        Assert.Equal(2, twice.Count);
        Assert.Equal(12, twice[Key(2)].Identity);
        Assert.Equal(1, twice.GetKey(Key(11)).Identity);
        Assert.Equal(-7, Assert.IsType<PersistenceHashKeyComparer>(twice.Comparer).Seed);
    }

    private static PersistenceHashKey Key(int identity) => new() { Group = 42, Identity = identity };

    private static SwiftComparerRegistry KeyRegistry(PersistenceHashKeyComparer comparer)
        => new SwiftComparerRegistryBuilder().RegisterEquality<PersistenceHashKey>("test.key.identity", 1, comparer,
            static rule => Int32Configuration(((PersistenceHashKeyComparer)rule).Seed),
            static bytes => (new PersistenceHashKeyComparer(BinaryPrimitives.ReadInt32LittleEndian(bytes.Span)), 4)).Build();

    private static byte[] Int32Configuration(int value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        return bytes;
    }

    private static JsonSerializerOptions JsonOptions(SwiftComparerRegistry registry)
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new SwiftCollectionJsonConverterFactory(registry));
        return options;
    }

    private static T RoundTrip<T>(T source, bool binary, SwiftComparerRegistry registry = null)
        => Decode<T>(Encode(source, binary, registry), binary, registry);

    private static byte[] Encode<T>(T source, bool binary, SwiftComparerRegistry registry = null)
    {
#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
        if (binary) return MemoryPackSerializer.Serialize(source, registry == null ? null : new MemoryPackSerializerOptions { ServiceProvider = registry });
#endif
        return JsonSerializer.SerializeToUtf8Bytes(source, registry == null ? null : JsonOptions(registry));
    }

    private static T Decode<T>(byte[] bytes, bool binary, SwiftComparerRegistry registry = null)
    {
#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
        if (binary) return MemoryPackSerializer.Deserialize<T>(bytes, registry == null ? null : new MemoryPackSerializerOptions { ServiceProvider = registry });
#endif
        return JsonSerializer.Deserialize<T>(bytes, registry == null ? null : JsonOptions(registry));
    }
}
