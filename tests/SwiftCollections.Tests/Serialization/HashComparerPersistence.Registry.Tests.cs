using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using SwiftCollections.Observable;
using SwiftCollections.Serialization;
using SwiftCollections.Utility;
using Xunit;

namespace SwiftCollections.Tests;

public partial class HashComparerPersistenceTests
{
    [Fact]
    public void EqualityBuiltInsCannotAcquireApplicationAliasesAtRegistration()
    {
        foreach (IEqualityComparer<string> comparer in new IEqualityComparer<string>[]
        {
            EqualityComparer<string>.Default, StringComparer.Ordinal, StringComparer.OrdinalIgnoreCase,
            new SwiftDeterministicStringEqualityComparer(-7), new SwiftStringEqualityComparer(-7),
            new SwiftDeterministicObjectEqualityComparer(-7), new SwiftObjectEqualityComparer(-7)
        })
        {
            Assert.Throws<ArgumentException>(() => new SwiftComparerRegistryBuilder().RegisterEquality<string>(
                "test.alias", 1, comparer, static _ => ReadOnlyMemory<byte>.Empty, _ => (comparer, 0)));
        }
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void EqualityFactoriesMustReturnNonCanonicalProductsAndConsumeAllConfiguration(bool binary)
    {
        var comparer = EqualityComparer<int>.Create(static (left, right) => left % 10 == right % 10, static value => value % 10);
        var source = new SwiftDictionary<int, int>(8, comparer) { { 1, 17 } };
        foreach (string failure in new[] { "null", "trailing", "built-in", "throw" })
        {
            var registry = new SwiftComparerRegistryBuilder().RegisterEquality<int>("test.bad-factory", 1, comparer,
                static _ => new byte[] { 1 }, _ => failure switch
                {
                    "null" => (null, 1),
                    "trailing" => (comparer, 0),
                    "built-in" => (EqualityComparer<int>.Default, 1),
                    _ => throw new ArithmeticException("factory failed")
                }).Build();

            AssertInvalid<SwiftDictionary<int, int>>(Encode(source, binary, registry), binary, registry);

            Assert.Same(comparer, source.Comparer);
            Assert.Equal(17, source[11]);
        }
    }

    [Fact]
    public void EqualityBuiltInsRejectWrongConfigurationLengthsAndDeclaredTypes()
    {
        var strings = new SwiftDictionaryState<string, int>(Array.Empty<KeyValuePair<string, int>>());
        var integers = new SwiftDictionaryState<int, int>(Array.Empty<KeyValuePair<int, int>>());
        foreach (string id in new[]
        {
            "lsf.string-ordinal", "lsf.string-ordinal-ignore-case", "lsf.swift-string-seeded",
            "lsf.swift-string-randomized", "lsf.swift-object-seeded", "lsf.swift-object-randomized", "lsf.unknown"
        })
        {
            byte[] invalidConfiguration = id.Contains("seeded", StringComparison.Ordinal) || id.Contains("randomized", StringComparison.Ordinal)
                ? Array.Empty<byte>() : new byte[] { 1 };
            AssertInvalid<SwiftDictionary<string, int>>(Envelope(false, strings, ((byte)2, id, invalidConfiguration)), false);
            byte[] validLength = id.Contains("randomized", StringComparison.Ordinal) ? new byte[8]
                : id.Contains("seeded", StringComparison.Ordinal) ? new byte[4] : Array.Empty<byte>();
            AssertInvalid<SwiftDictionary<int, int>>(Envelope(false, integers, ((byte)2, id, validLength)), false);
        }
    }

    [Fact]
    public void ClosedJsonCreatorsPreserveTheSameOwnerContractsAndRequireARegistry()
    {
        var registry = new SwiftComparerRegistryBuilder().Build();
        var options = new JsonSerializerOptions();
        options.Converters.Add(SwiftCollectionJsonConverters.ForDictionary<int, int>(registry));
        options.Converters.Add(SwiftCollectionJsonConverters.ForHashSet<int>(registry));
        options.Converters.Add(SwiftCollectionJsonConverters.ForBiDictionary<int, int>(registry));
        options.Converters.Add(SwiftCollectionJsonConverters.ForObservableDictionary<int, int>(registry));
        Assert.Equal(2, JsonSerializer.Deserialize<SwiftDictionary<int, int>>(
            JsonSerializer.Serialize(new SwiftDictionary<int, int> { { 1, 2 } }, options), options)[1]);
        var set = JsonSerializer.Deserialize<SwiftHashSet<int>>(JsonSerializer.Serialize(new SwiftHashSet<int>(new[] { 1 }), options), options);
        Assert.Equal(new[] { 1 }, set);
        Assert.Throws<ArgumentNullException>(() => SwiftCollectionJsonConverters.ForDictionary<int, int>(null));
        Assert.Throws<ArgumentNullException>(() => SwiftCollectionJsonConverters.ForHashSet<int>(null));
        Assert.Throws<ArgumentNullException>(() => SwiftCollectionJsonConverters.ForBiDictionary<int, int>(null));
        Assert.Throws<ArgumentNullException>(() => SwiftCollectionJsonConverters.ForObservableDictionary<int, int>(null));
    }

    [Fact]
    public void ClosedJsonConverterHandlesNullWhenInvokedDirectly()
    {
        var options = new JsonSerializerOptions();
        var converter = SwiftCollectionJsonConverters.ForDictionary<int, int>(SwiftComparerRegistry.Empty);
        var reader = new Utf8JsonReader("null"u8);
        Assert.True(reader.Read());

        var result = converter.Read(ref reader, typeof(SwiftDictionary<int, int>), options);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            converter.Write(writer, null, options);
            writer.Flush();
        }

        Assert.Null(result);
        Assert.Equal(JsonTokenType.Null, reader.TokenType);
        Assert.Equal("null", Encoding.UTF8.GetString(buffer.WrittenSpan));
    }

    [Fact]
    public void PersistenceCapacityRejectsOversizedCountsBeforeAllocatingTables()
    {
        Assert.Throws<InvalidOperationException>(() => SwiftHashTools.GetPersistenceCapacity(int.MaxValue, 8, 82));
        Assert.Throws<InvalidOperationException>(() => SwiftHashTools.GetPersistenceCapacity(int.MaxValue, 8, 85));
    }

    [Fact]
    public void ObservableRawStateConstructorKeepsItsCopyOnlyDefaultEqualityContract()
    {
        var items = new[] { new KeyValuePair<int, string>(1, "one"), new KeyValuePair<int, string>(2, "two") };
        var source = new SwiftDictionaryState<int, string>(items);

        var result = new SwiftObservableDictionary<int, string>(source);
        items[0] = new KeyValuePair<int, string>(1, "changed");

        Assert.Equal(2, result.Count);
        Assert.Equal("one", result[1]);
        Assert.Equal("two", result[2]);
        int changes = 0;
        result.CollectionChanged += (_, _) => changes++;
        result.Add(3, "three");
        Assert.Equal(1, changes);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void RegisteredEqualitySingletonCannotClaimContradictoryRecordedConfiguration(bool binary)
    {
        var comparer = new PersistenceHashKeyComparer(-7);
        var registry = new SwiftComparerRegistryBuilder().RegisterEquality<PersistenceHashKey>("test.singleton", 1, comparer,
            static rule => Int32Configuration(((PersistenceHashKeyComparer)rule).Seed), _ => (comparer, 4)).Build();
        var state = new SwiftDictionaryState<PersistenceHashKey, int>(Array.Empty<KeyValuePair<PersistenceHashKey, int>>());

        AssertInvalid<SwiftDictionary<PersistenceHashKey, int>>(Envelope(binary, state,
            ((byte)2, "test.singleton", Int32Configuration(-8))), binary, registry);

        Assert.Equal(-7, comparer.Seed);
        var valid = new SwiftDictionary<PersistenceHashKey, int>(8, comparer);
        Assert.Same(comparer, RoundTrip(valid, binary, registry).Comparer);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void EqualityFactoryCannotReturnASingletonBoundToAnOlderCodecVersion(bool binary)
    {
        var first = new PersistenceHashKeyComparer(1);
        var second = new PersistenceHashKeyComparer(2);
        var registry = new SwiftComparerRegistryBuilder()
            .RegisterEquality<PersistenceHashKey>("test.versioned", 1, first,
                static rule => Int32Configuration(((PersistenceHashKeyComparer)rule).Seed), _ => (first, 4))
            .RegisterEquality<PersistenceHashKey>("test.versioned", 2, second,
                static rule => Int32Configuration(((PersistenceHashKeyComparer)rule).Seed), _ => (first, 4)).Build();
        var source = new SwiftDictionary<PersistenceHashKey, int>(8, second);

        AssertInvalid<SwiftDictionary<PersistenceHashKey, int>>(Encode(source, binary, registry), binary, registry);

        Assert.Same(second, source.Comparer);
        Assert.Same(first, RoundTrip(new SwiftDictionary<PersistenceHashKey, int>(8, first), binary, registry).Comparer);
    }

    [Fact]
    public void ExplicitHostRawStateConverterRetainsItsOwnComparerLosingContract()
    {
        var comparer = new PersistenceHashKeyComparer(7);
        var source = new SwiftDictionary<PersistenceHashKey, int>(8, comparer) { { Key(1), 11 }, { Key(2), 22 } };
        var options = new JsonSerializerOptions();
        options.Converters.Add(new Chronicler.Serialization.StateJsonConverterFactory());

        string json = JsonSerializer.Serialize(source, options);
        var result = JsonSerializer.Deserialize<SwiftDictionary<PersistenceHashKey, int>>(json, options);

        Assert.StartsWith("{\"State\":", json);
        Assert.DoesNotContain("Comparers", json);
        Assert.Single(result);
        Assert.Equal(2, source.Count);
    }
}
