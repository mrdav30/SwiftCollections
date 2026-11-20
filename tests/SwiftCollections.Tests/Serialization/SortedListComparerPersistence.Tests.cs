using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using SwiftCollections.Serialization;
#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
using MemoryPack;
#endif
using Xunit;

namespace SwiftCollections.Tests;

public class SortedListComparerPersistenceTests
{
    private const string DefaultDescriptor = "{\"Role\":1,\"Id\":\"lsf.runtime-ordering-default\",\"Version\":1,\"Config\":\"\"}";
    // Original items-only output: a5371dc1caa9e1b81b631ac9ce88d5f03933ebc2, Release/net10.0, MemoryPack 1.21.4.
    private const string LegacyDescendingJson = "{\"State\":{\"Items\":[13,0,-7]}}";
    private const string LegacyDescendingMemoryPack = "AQEDAAAADQAAAAAAAAD5////";

    [Fact]
    public void JsonSave_UnregisteredCustomOrderingFailsInsteadOfLosingItsContract()
    {
        var comparer = Comparer<int>.Create(static (left, right) => right.CompareTo(left));
        var source = new SwiftSortedList<int>(new[] { 1, 2, 3 }, comparer);

        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(source));

        Assert.Equal(new[] { 3, 2, 1 }, source.AsReadOnlySpan().ToArray());
        Assert.Same(comparer, source.Comparer);
    }

#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
    [Fact]
    public void MemoryPackSave_UnregisteredCustomOrderingFailsInsteadOfLosingItsContract()
    {
        var comparer = Comparer<int>.Create(static (left, right) => right.CompareTo(left));
        var source = new SwiftSortedList<int>(new[] { 1, 2, 3 }, comparer);

        Assert.Throws<NotSupportedException>(() => MemoryPackSerializer.Serialize(source));

        Assert.Equal(new[] { 3, 2, 1 }, source.AsReadOnlySpan().ToArray());
        Assert.Same(comparer, source.Comparer);
    }
#endif

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(128)]
    public void Json_DefaultOrderingUsesStrictEnvelopeAndSupportsContinuation(int count)
    {
        var source = new SwiftSortedList<int>(Enumerable.Range(0, count));

        string json = JsonSerializer.Serialize(source);
        var result = JsonSerializer.Deserialize<SwiftSortedList<int>>(json);

        Assert.StartsWith("{\"FormatVersion\":1,\"Comparers\":[", json);
        Assert.Contains(DefaultDescriptor, json);
        Assert.Equal(source.AsReadOnlySpan().ToArray(), result.AsReadOnlySpan().ToArray());
        Assert.Same(Comparer<int>.Default, result.Comparer);
        result.Add(-1);
        bool containsAdded = result.Contains(-1);
        Assert.True(containsAdded);
        Assert.True(result.Remove(-1));
        Assert.Equal(count, result.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Json_OrdinalStringRulesRoundTripWithoutHostRegistration(bool ignoreCase)
    {
        StringComparer comparer = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var source = new SwiftSortedList<string>(new[] { "Beta", "alpha", "ALPHA" }, comparer);

        var result = JsonSerializer.Deserialize<SwiftSortedList<string>>(JsonSerializer.Serialize(source));

        Assert.Same(comparer, result.Comparer);
        Assert.Equal(source.AsReadOnlySpan().ToArray(), result.AsReadOnlySpan().ToArray());
        Assert.Equal(ignoreCase, result.Contains("beta"));
        result.Add("gamma");
        Assert.True(result.Remove("gamma"));
    }

    [Fact]
    public void Json_ConfiguredNonComparableElementsRestoreWithoutComparisonsOrSourceMutation()
    {
        var comparer = new PersistenceTokenComparer(-1);
        var registry = CreateTokenRegistry(comparer);
        var options = JsonOptions(registry);
        var values = new[] { new PersistenceToken { Key = -3 }, new PersistenceToken { Key = 9 }, new PersistenceToken { Key = 4 } };
        var source = new SwiftSortedList<PersistenceToken>(values, comparer);
        comparer.Comparisons = 0;

        string json = JsonSerializer.Serialize(source, options);
        var restored = JsonSerializer.Deserialize<SwiftSortedList<PersistenceToken>>(json, options);

        Assert.Equal(0, comparer.Comparisons);
        Assert.Equal(0, Assert.IsType<PersistenceTokenComparer>(restored.Comparer).Comparisons);
        Assert.NotSame(source.InnerArray, restored.InnerArray);
        Assert.NotSame(source[0], restored[0]);
        Assert.Equal(new[] { 9, 4, -3 }, restored.Select(static item => item.Key));
        bool containsFour = restored.Contains(new PersistenceToken { Key = 4 });
        Assert.True(containsFour);
        restored.Add(new PersistenceToken { Key = 7 });
        Assert.True(restored.Remove(new PersistenceToken { Key = -3 }));
        Assert.Equal(new[] { 9, 7, 4 }, restored.Select(static item => item.Key));
        Assert.Equal(new[] { 9, 4, -3 }, source.Select(static item => item.Key));
        Assert.Equal(new[] { -3, 9, 4 }, values.Select(static item => item.Key));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Json_EmptyAndSingleCustomOwnersStillRequireTheirRegisteredRule(int count)
    {
        var comparer = new PersistenceTokenComparer(-1);
        var source = new SwiftSortedList<PersistenceToken>(Enumerable.Range(0, count).Select(static value => new PersistenceToken { Key = value }), comparer);
        var options = JsonOptions(CreateTokenRegistry(comparer));
        comparer.Comparisons = 0;

        string json = JsonSerializer.Serialize(source, options);
        var result = JsonSerializer.Deserialize<SwiftSortedList<PersistenceToken>>(json, options);

        Assert.Equal(count, result.Count);
        Assert.Equal(0, Assert.IsType<PersistenceTokenComparer>(result.Comparer).Comparisons);
        Assert.Equal(0, comparer.Comparisons);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SwiftSortedList<PersistenceToken>>(json));
    }

    [Fact]
    public void Json_SourceGeneratedNestedCollectionsUseTheSuppliedRegistry()
    {
        var comparer = new PersistenceTokenComparer(-1);
        var registry = CreateTokenRegistry(comparer);
        var source = TokenBox(comparer);
        var context = new SortedListPersistenceJsonContext(JsonOptions(registry));

        string json = JsonSerializer.Serialize(source, context.SortedListPersistenceBox);
        var result = JsonSerializer.Deserialize(json, context.SortedListPersistenceBox);

        AssertTokenBox(result);
        Assert.Contains("test.token.direction", json);
        Assert.Equal(json, JsonSerializer.Serialize(result, context.SortedListPersistenceBox));
        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(source));
    }

    [Fact]
    public void Json_ClosedConverterSupportsTheSameStrictContract()
    {
        var comparer = new PersistenceTokenComparer(-1);
        var registry = CreateTokenRegistry(comparer);
        var options = new JsonSerializerOptions();
        options.Converters.Add(SwiftCollectionJsonConverters.ForSortedList<PersistenceToken>(registry));
        var source = TokenList(comparer);

        string json = JsonSerializer.Serialize(source, options);
        var result = JsonSerializer.Deserialize<SwiftSortedList<PersistenceToken>>(json, options);

        Assert.Equal(new[] { 9, 4, -3 }, result.Select(static item => item.Key));
        bool containsFour = result.Contains(new PersistenceToken { Key = 4 });
        Assert.True(containsFour);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SwiftSortedList<PersistenceToken>>(json));
    }

    [Fact]
    public void Json_ExplicitBaseContractProjectsDerivedOwnerItemsAndComparer()
    {
        var comparer = Comparer<int>.Create(static (left, right) => right.CompareTo(left));
        var registry = new SwiftComparerRegistryBuilder().RegisterOrdering<int>("test.base-descending", 1, comparer,
            static _ => ReadOnlyMemory<byte>.Empty, _ => (comparer, 0)).Build();
        var source = new PersistenceDerivedSortedList(comparer) { HostValue = 73 };
        source.AddRange(new[] { 1, 2, 3 });
        var options = JsonOptions(registry);

        string json = JsonSerializer.Serialize<SwiftSortedList<int>>(source, options);
        var result = JsonSerializer.Deserialize<SwiftSortedList<int>>(json, options);

        Assert.IsType<SwiftSortedList<int>>(result);
        Assert.Same(comparer, result.Comparer);
        Assert.Equal(new[] { 3, 2, 1 }, result.AsReadOnlySpan().ToArray());
        Assert.DoesNotContain("HostValue", json);
        Assert.False(new SwiftCollectionJsonConverterFactory(registry).CanConvert(typeof(PersistenceDerivedSortedList)));
        bool containsOne = result.Contains(1);
        Assert.True(containsOne);
        result.Add(4);
        Assert.Equal(new[] { 4, 3, 2, 1 }, result.AsReadOnlySpan().ToArray());
        Assert.Equal(73, source.HostValue);
        Assert.Equal(new[] { 3, 2, 1 }, source.AsReadOnlySpan().ToArray());
    }

    [Fact]
    public void ConfiguredCapturingLambdaCanBeSavedAgainAfterRestore()
    {
        int direction = -1;
        IComparer<int> comparer = Comparer<int>.Create((left, right) => direction * left.CompareTo(right));
        var registry = new SwiftComparerRegistryBuilder().RegisterOrdering<int>("test.int32.direction", 1, comparer,
            _ => new byte[] { 255 }, static bytes =>
            {
                int decodedDirection = bytes.Span[0] == 255 ? -1 : 1;
                return (Comparer<int>.Create((left, right) => decodedDirection * left.CompareTo(right)), 1);
            }).Build();
        var options = JsonOptions(registry);
        var source = new SwiftSortedList<int>(new[] { -3, 4, 9 }, comparer);

        string firstJson = JsonSerializer.Serialize(source, options);
        var firstLoad = JsonSerializer.Deserialize<SwiftSortedList<int>>(firstJson, options);
        string secondJson = JsonSerializer.Serialize(firstLoad, options);
        var secondLoad = JsonSerializer.Deserialize<SwiftSortedList<int>>(secondJson, options);

        Assert.Equal(firstJson, secondJson);
        Assert.Equal(new[] { 9, 4, -3 }, secondLoad.AsReadOnlySpan().ToArray());
        bool containsFour = secondLoad.Contains(4);
        Assert.True(containsFour);
        var unrelated = Comparer<int>.Create(static (left, right) => left.CompareTo(right));
        Assert.Equal(comparer.GetType(), unrelated.GetType());
        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(new SwiftSortedList<int>(unrelated), options));
        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(firstLoad, JsonOptions(new SwiftComparerRegistryBuilder().Build())));
#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
        var binaryOptions = new MemoryPackSerializerOptions { ServiceProvider = registry };
        byte[] firstBinary = MemoryPackSerializer.Serialize(source, binaryOptions);
        var binaryLoad = MemoryPackSerializer.Deserialize<SwiftSortedList<int>>(firstBinary, binaryOptions);
        byte[] secondBinary = MemoryPackSerializer.Serialize(binaryLoad, binaryOptions);
        Assert.Equal(firstBinary, secondBinary);
        var binaryReload = MemoryPackSerializer.Deserialize<SwiftSortedList<int>>(secondBinary, binaryOptions);
        Assert.Equal(new[] { 9, 4, -3 }, binaryReload.AsReadOnlySpan().ToArray());
        bool binaryContainsFour = binaryReload.Contains(4);
        Assert.True(binaryContainsFour);
#endif
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Json_CustomFactoryMustConsumeTheEntireConfiguration(int consumed)
    {
        var comparer = new PersistenceTokenComparer(-1);
        var registry = new SwiftComparerRegistryBuilder().RegisterOrdering<PersistenceToken>("test.token.direction", 1, comparer,
            static _ => new byte[] { 255 }, _ => (new PersistenceTokenComparer(-1), consumed)).Build();
        var options = JsonOptions(registry);
        string json = JsonSerializer.Serialize(TokenList(comparer), options);

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SwiftSortedList<PersistenceToken>>(json, options));
    }

    [Fact]
    public void Registry_RejectsDuplicateIdentitiesAndCanonicalBuiltinAliases()
    {
        var comparer = new PersistenceTokenComparer(-1);
        var builder = new SwiftComparerRegistryBuilder();
        builder.RegisterOrdering<PersistenceToken>("test.token.direction", 1, comparer,
            static _ => new byte[] { 255 }, static _ => (new PersistenceTokenComparer(-1), 1));

        Assert.ThrowsAny<ArgumentException>(() => builder.RegisterOrdering<PersistenceToken>("test.token.direction", 1,
            new PersistenceTokenComparer(-1), static _ => new byte[] { 255 }, static _ => (new PersistenceTokenComparer(-1), 1)));
        Assert.ThrowsAny<ArgumentException>(() => new SwiftComparerRegistryBuilder().RegisterOrdering<int>("test.alias", 1,
            Comparer<int>.Default, static _ => ReadOnlyMemory<byte>.Empty, static _ => (Comparer<int>.Default, 0)));
    }

    [Fact]
    public void Registry_MatchesSourceAndProducedComparersWithoutCallingTheirObjectEqualityOrHashing()
    {
        var descending = new HostileObjectEqualityComparer(-1);
        var ascending = new HostileObjectEqualityComparer(1);
        var registry = new SwiftComparerRegistryBuilder()
            .RegisterOrdering<int>("test.reference.descending", 1, descending,
                static _ => ReadOnlyMemory<byte>.Empty, static _ => (new HostileObjectEqualityComparer(-1), 0))
            .RegisterOrdering<int>("test.reference.ascending", 1, ascending,
                static _ => ReadOnlyMemory<byte>.Empty, static _ => (new HostileObjectEqualityComparer(1), 0))
            .Build();
        var options = JsonOptions(registry);

        foreach (var (id, comparer, expected) in new[]
        {
            ("test.reference.descending", descending, new[] { 9, 4, -3 }),
            ("test.reference.ascending", ascending, new[] { -3, 4, 9 })
        })
        {
            var source = new SwiftSortedList<int>(new[] { -3, 4, 9 }, comparer);
            string json = JsonSerializer.Serialize(source, options);
            using var document = JsonDocument.Parse(json);
            Assert.Equal(id, document.RootElement.GetProperty("Comparers")[0].GetProperty("Id").GetString());

            var restored = JsonSerializer.Deserialize<SwiftSortedList<int>>(json, options);

            Assert.NotSame(comparer, restored.Comparer);
            Assert.Equal(comparer.Direction, Assert.IsType<HostileObjectEqualityComparer>(restored.Comparer).Direction);
            Assert.Equal(expected, restored.AsReadOnlySpan().ToArray());
            Assert.Equal(json, JsonSerializer.Serialize(restored, options));
            bool containsFour = restored.Contains(4);
            Assert.True(containsFour);
            restored.Add(7);
            Assert.Equal(comparer.Direction < 0 ? new[] { 9, 7, 4, -3 } : new[] { -3, 4, 7, 9 }, restored.AsReadOnlySpan().ToArray());
            Assert.True(restored.Remove(7));
            Assert.Equal(expected, source.AsReadOnlySpan().ToArray());
#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
            var binaryOptions = MemoryPackSerializerOptions.Default with { ServiceProvider = registry };
            byte[] binary = MemoryPackSerializer.Serialize(source, binaryOptions);
            var binaryRestored = MemoryPackSerializer.Deserialize<SwiftSortedList<int>>(binary, binaryOptions);
            Assert.Equal(comparer.Direction, Assert.IsType<HostileObjectEqualityComparer>(binaryRestored.Comparer).Direction);
            Assert.Equal(expected, binaryRestored.AsReadOnlySpan().ToArray());
            Assert.Equal(binary, MemoryPackSerializer.Serialize(binaryRestored, binaryOptions));
            binaryRestored.Add(7);
            Assert.Equal(comparer.Direction < 0 ? new[] { 9, 7, 4, -3 } : new[] { -3, 4, 7, 9 }, binaryRestored.AsReadOnlySpan().ToArray());
            Assert.True(binaryRestored.Remove(7));
#endif
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("UPPER")]
    [InlineData("invalid id")]
    [InlineData("test{")]
    [InlineData("lsf.application-alias")]
    public void Registry_RejectsInvalidOrReservedIdentifiers(string id)
    {
        Assert.ThrowsAny<ArgumentException>(() => new SwiftComparerRegistryBuilder().RegisterOrdering<PersistenceToken>(id, 1,
            new PersistenceTokenComparer(-1), static _ => ReadOnlyMemory<byte>.Empty, static _ => (new PersistenceTokenComparer(-1), 0)));
    }

    [Fact]
    public void Registry_SupportsDocumentedIdentifierAlphabetAndMaximumLength()
    {
        foreach (string id in new[] { "a", "test/a_rule-v1", new string('z', 128) })
        {
            var comparer = new PersistenceTokenComparer(-1);
            var registry = new SwiftComparerRegistryBuilder().RegisterOrdering<PersistenceToken>(id, 1, comparer,
                static _ => ReadOnlyMemory<byte>.Empty, static _ => (new PersistenceTokenComparer(-1), 0)).Build();
            var options = JsonOptions(registry);
            string json = JsonSerializer.Serialize(new SwiftSortedList<PersistenceToken>(comparer), options);

            var result = JsonSerializer.Deserialize<SwiftSortedList<PersistenceToken>>(json, options);

            Assert.Empty(result);
            Assert.Equal(-1, Assert.IsType<PersistenceTokenComparer>(result.Comparer).Direction);
            Assert.Contains(id, json);
        }
    }

    [Fact]
    public void Registry_ValidatesRequiredCallbacksVersionsAndDuplicateInstances()
    {
        var comparer = new PersistenceTokenComparer(-1);
        var builder = new SwiftComparerRegistryBuilder();
        Assert.Throws<ArgumentNullException>(() => builder.RegisterOrdering<PersistenceToken>(null, 1, comparer,
            static _ => ReadOnlyMemory<byte>.Empty, static _ => (new PersistenceTokenComparer(-1), 0)));
        Assert.Throws<ArgumentNullException>(() => builder.RegisterOrdering<PersistenceToken>("test.rule", 1, null,
            static _ => ReadOnlyMemory<byte>.Empty, static _ => (new PersistenceTokenComparer(-1), 0)));
        Assert.Throws<ArgumentNullException>(() => builder.RegisterOrdering<PersistenceToken>("test.rule", 1, comparer,
            null, static _ => (new PersistenceTokenComparer(-1), 0)));
        Assert.Throws<ArgumentNullException>(() => builder.RegisterOrdering<PersistenceToken>("test.rule", 1, comparer,
            static _ => ReadOnlyMemory<byte>.Empty, null));
        Assert.ThrowsAny<ArgumentException>(() => builder.RegisterOrdering<PersistenceToken>("test.rule", 0, comparer,
            static _ => ReadOnlyMemory<byte>.Empty, static _ => (new PersistenceTokenComparer(-1), 0)));
        builder.RegisterOrdering<PersistenceToken>("test.first", 1, comparer,
            static _ => ReadOnlyMemory<byte>.Empty, static _ => (new PersistenceTokenComparer(-1), 0));
        Assert.ThrowsAny<ArgumentException>(() => builder.RegisterOrdering<PersistenceToken>("test.second", 1, comparer,
            static _ => ReadOnlyMemory<byte>.Empty, static _ => (new PersistenceTokenComparer(-1), 0)));
        Assert.ThrowsAny<ArgumentException>(() => new SwiftComparerRegistryBuilder().RegisterOrdering<PersistenceToken>(new string('a', 129), 1,
            new PersistenceTokenComparer(-1), static _ => ReadOnlyMemory<byte>.Empty, static _ => (new PersistenceTokenComparer(-1), 0)));
    }

    [Fact]
    public void Registry_BuildIsIndependentAndComposesOnlyItsOwnService()
    {
        var builder = new SwiftComparerRegistryBuilder();
        var beforeRegistration = builder.Build();
        var comparer = new PersistenceTokenComparer(-1);
        builder.RegisterOrdering<PersistenceToken>("test.token.direction", 1, comparer,
            static _ => new byte[] { 255 }, static _ => (new PersistenceTokenComparer(-1), 1));
        var afterRegistration = builder.Build();

        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(TokenList(comparer), JsonOptions(beforeRegistration)));
        Assert.Contains("test.token.direction", JsonSerializer.Serialize(TokenList(comparer), JsonOptions(afterRegistration)));
        Assert.Same(afterRegistration, afterRegistration.GetService(typeof(SwiftComparerRegistry)));
        Assert.Null(afterRegistration.GetService(typeof(string)));
        Assert.Throws<ArgumentNullException>(() => afterRegistration.GetService(null));
        Assert.Throws<ArgumentNullException>(() => new SwiftCollectionJsonConverterFactory(null));
        Assert.Throws<ArgumentNullException>(() => SwiftCollectionJsonConverters.ForSortedList<int>(null));
        var factory = new SwiftCollectionJsonConverterFactory();
        Assert.False(factory.CanConvert(typeof(SwiftSortedList<>)));
        Assert.False(factory.CanConvert(typeof(int)));
        Assert.Throws<NotSupportedException>(() => factory.CreateConverter(typeof(SwiftList<int>), new JsonSerializerOptions()));
    }

    [Fact]
    public void Json_SaveRejectsOversizedConfigurationAndPropagatesEncoderFailure()
    {
        var comparer = new PersistenceTokenComparer(-1);
        var oversized = new SwiftComparerRegistryBuilder().RegisterOrdering<PersistenceToken>("test.large", 1, comparer,
            static _ => new byte[4097], static _ => (new PersistenceTokenComparer(-1), 4097)).Build();
        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(TokenList(comparer), JsonOptions(oversized)));
        var failure = new ArithmeticException("encoder failure");
        var throwing = new SwiftComparerRegistryBuilder().RegisterOrdering<PersistenceToken>("test.throw", 1, comparer,
            _ => throw failure, static _ => (new PersistenceTokenComparer(-1), 0)).Build();

        Assert.Same(failure, Assert.Throws<ArithmeticException>(() => JsonSerializer.Serialize(TokenList(comparer), JsonOptions(throwing))));
    }

    [Fact]
    public void Json_FactoryFailureAndNullProductDoNotChangeTheSource()
    {
        var comparer = new PersistenceTokenComparer(-1);
        var source = TokenList(comparer);
        var failure = new ArithmeticException("factory failure");
        var throwing = new SwiftComparerRegistryBuilder().RegisterOrdering<PersistenceToken>("test.throw", 1, comparer,
            static _ => ReadOnlyMemory<byte>.Empty, _ => throw failure).Build();
        var options = JsonOptions(throwing);
        string json = JsonSerializer.Serialize(source, options);

        Assert.Same(failure, Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SwiftSortedList<PersistenceToken>>(json, options)).GetBaseException());
        var nullFactory = new SwiftComparerRegistryBuilder().RegisterOrdering<PersistenceToken>("test.null", 1, comparer,
            static _ => ReadOnlyMemory<byte>.Empty, static _ => (null, 0)).Build();
        var nullOptions = JsonOptions(nullFactory);
        string nullJson = JsonSerializer.Serialize(source, nullOptions);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SwiftSortedList<PersistenceToken>>(nullJson, nullOptions));
        Assert.Same(comparer, source.Comparer);
        Assert.Equal(new[] { 9, 4, -3 }, source.Select(static item => item.Key));
    }

    [Fact]
    public void Json_SharedFactoryProductRejectsConflictingConfigurationWithoutOverwritingItsBinding()
    {
        var sourceComparer = new PersistenceTokenComparer(-1);
        var singleton = new PersistenceTokenComparer(-1);
        var registry = new SwiftComparerRegistryBuilder().RegisterOrdering<PersistenceToken>("test.shared", 1, sourceComparer,
            static _ => new byte[] { 255 }, _ => (singleton, 1)).Build();
        var options = JsonOptions(registry);
        string json = JsonSerializer.Serialize(TokenList(sourceComparer), options);
        var first = JsonSerializer.Deserialize<SwiftSortedList<PersistenceToken>>(json, options);
        var second = JsonSerializer.Deserialize<SwiftSortedList<PersistenceToken>>(json, options);

        Assert.Same(singleton, first.Comparer);
        Assert.Same(singleton, second.Comparer);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SwiftSortedList<PersistenceToken>>(json.Replace("/w==", "AQ=="), options));
        Assert.Equal(json, JsonSerializer.Serialize(first, options));
    }

    [Fact]
    public void Json_FactoryCannotReturnAnotherRegisteredRulesSourceInstance()
    {
        var first = new PersistenceTokenComparer(-1);
        var second = new PersistenceTokenComparer(1);
        var registry = new SwiftComparerRegistryBuilder()
            .RegisterOrdering<PersistenceToken>("test.first", 1, first, static _ => new byte[] { 255 }, _ => (second, 1))
            .RegisterOrdering<PersistenceToken>("test.second", 1, second, static _ => new byte[] { 1 }, _ => (second, 1))
            .Build();
        var options = JsonOptions(registry);
        string json = JsonSerializer.Serialize(TokenList(first), options);

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SwiftSortedList<PersistenceToken>>(json, options));

        Assert.Equal(1, second.Direction);
        string secondJson = JsonSerializer.Serialize(new SwiftSortedList<PersistenceToken>(second), options);
        Assert.Contains("test.second", secondJson);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Json_SharedProductCannotAcquireASecondCodecIdentityOrVersion(bool changeVersion)
    {
        var first = new PersistenceTokenComparer(-1);
        var second = new PersistenceTokenComparer(-1);
        var shared = new PersistenceTokenComparer(-1);
        string secondId = changeVersion ? "test.first" : "test.second";
        ushort secondVersion = changeVersion ? (ushort)2 : (ushort)1;
        var registry = new SwiftComparerRegistryBuilder()
            .RegisterOrdering<PersistenceToken>("test.first", 1, first, static _ => ReadOnlyMemory<byte>.Empty, _ => (shared, 0))
            .RegisterOrdering<PersistenceToken>(secondId, secondVersion, second, static _ => ReadOnlyMemory<byte>.Empty, _ => (shared, 0))
            .Build();
        var options = JsonOptions(registry);
        string firstJson = JsonSerializer.Serialize(TokenList(first), options);
        var restored = JsonSerializer.Deserialize<SwiftSortedList<PersistenceToken>>(firstJson, options);
        string secondJson = JsonSerializer.Serialize(TokenList(second), options);

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SwiftSortedList<PersistenceToken>>(secondJson, options));

        Assert.Equal(firstJson, JsonSerializer.Serialize(restored, options));
    }

    [Fact]
    public void Json_ContravariantProductBindingDoesNotAuthorizeAnotherDeclaredElementType()
    {
        var sourceComparer = new PersistenceTokenComparer(-1);
        IComparer<object> product = Comparer<object>.Create(static (_, _) => 0);
        var registry = new SwiftComparerRegistryBuilder().RegisterOrdering<PersistenceToken>("test.typed", 1, sourceComparer,
            static _ => ReadOnlyMemory<byte>.Empty, _ => (product, 0)).Build();
        var options = JsonOptions(registry);
        var tokenSource = new SwiftSortedList<PersistenceToken>(sourceComparer);
        string json = JsonSerializer.Serialize(tokenSource, options);
        var restored = JsonSerializer.Deserialize<SwiftSortedList<PersistenceToken>>(json, options);

        Assert.Same(product, restored.Comparer);
        Assert.Equal(json, JsonSerializer.Serialize(restored, options));
        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(new SwiftSortedList<string>(product), options));
    }

    [Theory]
    [InlineData("AB==")]
    [InlineData("AAB=")]
    [InlineData("AAA")]
    [InlineData("A A=")]
    [InlineData("A===")]
    [InlineData("====")]
    public void Json_NonCanonicalConfigurationIsRejectedBeforeFactoryInvocation(string configuration)
    {
        int factories = 0;
        var comparer = new PersistenceTokenComparer(-1);
        var registry = new SwiftComparerRegistryBuilder().RegisterOrdering<PersistenceToken>("test.token.direction", 1, comparer,
            static _ => new byte[] { 255 }, _ => { factories++; return (new PersistenceTokenComparer(-1), 1); }).Build();
        string descriptor = "{\"Role\":1,\"Id\":\"test.token.direction\",\"Version\":1,\"Config\":\"" + configuration + "\"}";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SwiftSortedList<PersistenceToken>>(Envelope(descriptor), JsonOptions(registry)));

        Assert.Equal(0, factories);
    }

    [Fact]
    public void MaximumCanonicalConfigurationRoundTripsAndIsOwnedAfterCapture()
    {
        byte[] configuration = new byte[4096];
        configuration[0] = 255;
        var comparer = new PersistenceTokenComparer(-1);
        var registry = new SwiftComparerRegistryBuilder().RegisterOrdering<PersistenceToken>("test.max-config", 1, comparer,
            _ => configuration, static bytes => (new PersistenceTokenComparer(unchecked((sbyte)bytes.Span[0])), bytes.Length)).Build();
        var options = JsonOptions(registry);
        string json = JsonSerializer.Serialize(TokenList(comparer), options);
        configuration[0] = 1;

        var result = JsonSerializer.Deserialize<SwiftSortedList<PersistenceToken>>(json, options);

        Assert.Equal(-1, Assert.IsType<PersistenceTokenComparer>(result.Comparer).Direction);
        Assert.Equal(json, JsonSerializer.Serialize(result, options));
#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
        var binaryOptions = new MemoryPackSerializerOptions { ServiceProvider = registry };
        byte[] binary = MemoryPackSerializer.Serialize(result, binaryOptions);
        var binaryResult = MemoryPackSerializer.Deserialize<SwiftSortedList<PersistenceToken>>(binary, binaryOptions);
        Assert.Equal(-1, Assert.IsType<PersistenceTokenComparer>(binaryResult.Comparer).Direction);
        Assert.Equal(binary, MemoryPackSerializer.Serialize(binaryResult, binaryOptions));
#endif
    }

    [Fact]
    public void FactoryVisibleConfigurationCannotChangeTheRecordedRuleAfterRestore()
    {
        ArraySegment<byte> factoryBuffer = default;
        var comparer = new HostileObjectEqualityComparer(-1);
        var registry = new SwiftComparerRegistryBuilder().RegisterOrdering<int>("test.factory-buffer", 1, comparer,
            static _ => new byte[] { 255 }, configuration =>
            {
                Assert.True(MemoryMarshal.TryGetArray(configuration, out factoryBuffer));
                return (new HostileObjectEqualityComparer(unchecked((sbyte)configuration.Span[0])), configuration.Length);
            }).Build();
        var options = JsonOptions(registry);
        var source = new SwiftSortedList<int>(new[] { -3, 4, 9 }, comparer);
        string json = JsonSerializer.Serialize(source, options);

        var restored = JsonSerializer.Deserialize<SwiftSortedList<int>>(json, options);
        Assert.NotNull(factoryBuffer.Array);
        factoryBuffer.Array[factoryBuffer.Offset] = 1;

        Assert.Equal(-1, Assert.IsType<HostileObjectEqualityComparer>(restored.Comparer).Direction);
        Assert.Equal(json, JsonSerializer.Serialize(restored, options));
        bool containsFour = restored.Contains(4);
        Assert.True(containsFour);
        restored.Add(7);
        Assert.Equal(new[] { 9, 7, 4, -3 }, restored.AsReadOnlySpan().ToArray());
        Assert.True(restored.Remove(7));
        Assert.Equal(json, JsonSerializer.Serialize(restored, options));
#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
        var binaryOptions = MemoryPackSerializerOptions.Default with { ServiceProvider = registry };
        byte[] binary = MemoryPackSerializer.Serialize(source, binaryOptions);
        var binaryRestored = MemoryPackSerializer.Deserialize<SwiftSortedList<int>>(binary, binaryOptions);
        Assert.NotNull(factoryBuffer.Array);
        factoryBuffer.Array[factoryBuffer.Offset] = 1;

        Assert.Equal(-1, Assert.IsType<HostileObjectEqualityComparer>(binaryRestored.Comparer).Direction);
        Assert.Equal(binary, MemoryPackSerializer.Serialize(binaryRestored, binaryOptions));
        bool binaryContainsFour = binaryRestored.Contains(4);
        Assert.True(binaryContainsFour);
        binaryRestored.Add(7);
        Assert.Equal(new[] { 9, 7, 4, -3 }, binaryRestored.AsReadOnlySpan().ToArray());
        Assert.True(binaryRestored.Remove(7));
        Assert.Equal(binary, MemoryPackSerializer.Serialize(binaryRestored, binaryOptions));
#endif
        Assert.Equal(new[] { 9, 4, -3 }, source.AsReadOnlySpan().ToArray());
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void Json_CanonicalConfigurationSupportsOnePaddingCharacterAndNoPadding(int length)
    {
        byte[] configuration = new byte[length];
        configuration[0] = 255;
        var comparer = new PersistenceTokenComparer(-1);
        var registry = new SwiftComparerRegistryBuilder().RegisterOrdering<PersistenceToken>("test.padding", 1, comparer,
            _ => configuration, static bytes => (new PersistenceTokenComparer(unchecked((sbyte)bytes.Span[0])), bytes.Length)).Build();
        var options = JsonOptions(registry);
        string json = JsonSerializer.Serialize(TokenList(comparer), options);

        var result = JsonSerializer.Deserialize<SwiftSortedList<PersistenceToken>>(json, options);

        Assert.Equal(-1, Assert.IsType<PersistenceTokenComparer>(result.Comparer).Direction);
        Assert.Equal(json, JsonSerializer.Serialize(result, options));
    }

    [Fact]
    public void Json_EmptyConfigurationAcrossSegmentsUsesTheBuiltinRule()
    {
        string json = Envelope(DefaultDescriptor);
        int split = json.IndexOf("\"Config\":\"", StringComparison.Ordinal) + "\"Config\":\"".Length;
        byte[] input = Encoding.UTF8.GetBytes(json);
        var first = new JsonInputSegment(input.AsMemory(0, split));
        var last = first.Append(input.AsMemory(split));
        var reader = new Utf8JsonReader(new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length));

        var result = JsonSerializer.Deserialize<SwiftSortedList<int>>(ref reader);

        Assert.Same(Comparer<int>.Default, result.Comparer);
        Assert.Equal(new[] { 1, 2 }, result.AsReadOnlySpan().ToArray());
    }

    [Fact]
    public void Json_NonemptyConfigurationAcrossSegmentsPreservesTheCustomRule()
    {
        var comparer = new PersistenceTokenComparer(-1);
        var options = JsonOptions(CreateTokenRegistry(comparer));
        string json = JsonSerializer.Serialize(TokenList(comparer), options);
        int split = json.IndexOf("\"Config\":\"", StringComparison.Ordinal) + "\"Config\":\"".Length + 1;
        byte[] input = Encoding.UTF8.GetBytes(json);
        var first = new JsonInputSegment(input.AsMemory(0, split));
        var last = first.Append(input.AsMemory(split));
        var reader = new Utf8JsonReader(new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length));

        var result = JsonSerializer.Deserialize<SwiftSortedList<PersistenceToken>>(ref reader, options);

        Assert.Equal(-1, Assert.IsType<PersistenceTokenComparer>(result.Comparer).Direction);
        Assert.Equal(new[] { 9, 4, -3 }, result.Select(static item => item.Key));
        Assert.Equal(json, JsonSerializer.Serialize(result, options));
    }

    [Fact]
    public void Json_EscapedMetadataWorksWhenTheTokenCrossesInputSegments()
    {
        string escaped = Envelope(DefaultDescriptor.Replace("lsf.runtime-ordering-default", "\\u006csf.runtime-ordering-default"));
        byte[] input = Encoding.UTF8.GetBytes(escaped);
        int split = escaped.IndexOf("006c", StringComparison.Ordinal) + 2;
        var first = new JsonInputSegment(input.AsMemory(0, split));
        var last = first.Append(input.AsMemory(split));
        var sequence = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
        var reader = new Utf8JsonReader(sequence);

        var result = JsonSerializer.Deserialize<SwiftSortedList<int>>(ref reader);

        Assert.Equal(new[] { 1, 2 }, result.AsReadOnlySpan().ToArray());
        Assert.Same(Comparer<int>.Default, result.Comparer);
    }

    [Fact]
    public void Json_CustomFactoryCannotAliasTheSharedBuiltinComparer()
    {
        var comparer = Comparer<int>.Create(static (left, right) => right.CompareTo(left));
        var registry = new SwiftComparerRegistryBuilder().RegisterOrdering<int>("test.bad-alias", 1, comparer,
            static _ => ReadOnlyMemory<byte>.Empty, static _ => (Comparer<int>.Default, 0)).Build();
        var options = JsonOptions(registry);
        string json = JsonSerializer.Serialize(new SwiftSortedList<int>(comparer), options);

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SwiftSortedList<int>>(json, options));
        var ordinary = new SwiftSortedList<int>(new[] { 1, 2 });
        string defaultJson = JsonSerializer.Serialize(ordinary, options);
        Assert.Contains(DefaultDescriptor, defaultJson);
    }

    public static IEnumerable<object[]> InvalidJson => new[]
    {
        Envelope(DefaultDescriptor).Replace("\"FormatVersion\":1", "\"FormatVersion\":2"),
        Envelope(DefaultDescriptor).Replace("\"FormatVersion\":1", "\"FormatVersion\":0"),
        Envelope(DefaultDescriptor).Replace("\"FormatVersion\":1", "\"FormatVersion\":1.5"),
        Envelope(DefaultDescriptor).Replace("\"FormatVersion\":1", "\"FormatVersion\":65536"),
        Envelope(DefaultDescriptor).Replace("\"FormatVersion\":1", "\"FormatVersion\":\"1\""),
        Envelope(DefaultDescriptor).Replace("\"FormatVersion\":1", "\"formatVersion\":1"),
        Envelope(DefaultDescriptor.Replace("\"Role\":1", "\"Role\":2")),
        Envelope(DefaultDescriptor.Replace("lsf.runtime-ordering-default", "unknown.codec")),
        Envelope(DefaultDescriptor.Replace("lsf.runtime-ordering-default", "lsf.string-ordinal")),
        Envelope(DefaultDescriptor.Replace("\"Version\":1", "\"Version\":2")),
        Envelope(DefaultDescriptor.Replace("lsf.runtime-ordering-default", "")),
        Envelope(DefaultDescriptor.Replace("lsf.runtime-ordering-default", "UPPER")),
        Envelope(DefaultDescriptor.Replace("lsf.runtime-ordering-default", "test{")),
        Envelope(DefaultDescriptor.Replace("lsf.runtime-ordering-default", new string('a', 129))),
        Envelope(DefaultDescriptor.Replace("lsf.runtime-ordering-default", new string('a', 769))),
        Envelope(DefaultDescriptor.Replace("lsf.runtime-ordering-default", "\\uD800")),
        Envelope(DefaultDescriptor.Replace("\"Config\":\"\"", "\"Config\":\"AA==\"")),
        Envelope(DefaultDescriptor.Replace("\"Config\":\"\"", "\"Config\":null")),
        Envelope(DefaultDescriptor.Replace("\"Role\":1", "\"Role\":\"1\"")),
        Envelope(DefaultDescriptor.Replace("\"Role\":1", "\"Role\":256")),
        Envelope(DefaultDescriptor.Replace("\"Role\":1,", "")),
        Envelope(DefaultDescriptor.Replace("\"Config\":\"\"", "\"Config\":\"\",\"Extra\":0")),
        Envelope(DefaultDescriptor.Replace("\"Role\":1,\"Id\":", "\"Id\":1,\"Role\":")),
        Envelope(DefaultDescriptor.Replace("\"Config\":\"\"", "\"Config\":\"not base64\"")),
        Envelope(DefaultDescriptor.Replace("\"Config\":\"\"", "\"Config\":\"" + Convert.ToBase64String(new byte[4097]) + "\"")),
        Envelope(DefaultDescriptor.Replace("\"Config\":\"\"", "\"Config\":\"" + new string('A', 32785) + "\"")),
        Envelope(DefaultDescriptor + "," + DefaultDescriptor),
        Envelope(""),
        Envelope(DefaultDescriptor).Replace("\"Items\":[1,2]", "\"Items\":null"),
        Envelope(DefaultDescriptor).Replace("\"State\":", "\"Unexpected\":0,\"State\":"),
        "{\"State\":{\"Items\":[1,2]},\"FormatVersion\":1,\"Comparers\":[" + DefaultDescriptor + "]}",
        "{\"FormatVersion\":1,\"Comparers\":{},\"State\":{\"Items\":[]}}",
        "{}",
        LegacyDescendingJson
    }.Select(static json => new object[] { json });

    [Theory]
    [MemberData(nameof(InvalidJson))]
    public void Json_StrictReaderRejectsMalformedUnknownAndLegacyOwners(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SwiftSortedList<int>>(json));
    }

    [Fact]
    public void Json_UnknownBuiltinStringRuleFailsWithOtherwiseValidState()
    {
        string json = Envelope(DefaultDescriptor.Replace("lsf.runtime-ordering-default", "lsf.unknown"))
            .Replace("[1,2]", "[\"a\",\"b\"]");

        var exception = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SwiftSortedList<string>>(json));

        Assert.IsType<InvalidOperationException>(exception.GetBaseException());
    }

    [Fact]
    public void Json_InvalidMetadataIsRejectedBeforeTheFactoryOrItemDecoderRuns()
    {
        int factories = 0;
        var comparer = new PersistenceTokenComparer(-1);
        var registry = new SwiftComparerRegistryBuilder().RegisterOrdering<PersistenceToken>("test.token.direction", 1, comparer,
            static _ => new byte[] { 255 }, _ => { factories++; return (new PersistenceTokenComparer(-1), 1); }).Build();
        string descriptor = "{\"Role\":1,\"Id\":\"test.token.direction\",\"Version\":1,\"Config\":\"" + Convert.ToBase64String(new byte[4097]) + "\"}";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SwiftSortedList<PersistenceToken>>(Envelope(descriptor), JsonOptions(registry)));

        Assert.Equal(0, factories);
    }

    [Fact]
    public void Json_DefaultEligibilityUsesDeclaredComparableContractEvenForEmptyOwners()
    {
        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(new SwiftSortedList<object>()));
        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(new SwiftSortedList<PersistenceToken>()));
        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(new SwiftSortedList<string>()));
        var derived = new SwiftSortedList<PersistenceComparableDerived>();
        var derivedResult = JsonSerializer.Deserialize<SwiftSortedList<PersistenceComparableDerived>>(JsonSerializer.Serialize(derived));
        Assert.Empty(derivedResult);
        var nullable = new SwiftSortedList<int?>(new int?[] { null, 2, -1 });
        Assert.Equal(new int?[] { null, -1, 2 }, JsonSerializer.Deserialize<SwiftSortedList<int?>>(JsonSerializer.Serialize(nullable)));
        var enums = new SwiftSortedList<PersistenceRank>(new[] { PersistenceRank.High, PersistenceRank.Low });
        Assert.Equal(new[] { PersistenceRank.Low, PersistenceRank.High }, JsonSerializer.Deserialize<SwiftSortedList<PersistenceRank>>(JsonSerializer.Serialize(enums)));
    }

    [Fact]
    public void Json_NullableDefaultOrderingRequiresTheUnderlyingTypesOwnComparison()
    {
        var comparable = new SwiftSortedList<PersistenceGenericComparable?>(new PersistenceGenericComparable?[]
        {
            new PersistenceGenericComparable { Key = 2 }, null, new PersistenceGenericComparable { Key = -1 }
        });

        var result = JsonSerializer.Deserialize<SwiftSortedList<PersistenceGenericComparable?>>(JsonSerializer.Serialize(comparable));

        Assert.Null(result[0]);
        Assert.Equal(-1, result[1].Value.Key);
        Assert.Equal(2, result[2].Value.Key);
        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(new SwiftSortedList<PersistenceOtherComparable?>()));
        var nullableEnums = new SwiftSortedList<PersistenceRank?>(new PersistenceRank?[] { PersistenceRank.High, null, PersistenceRank.Low });
        Assert.Equal(new PersistenceRank?[] { null, PersistenceRank.Low, PersistenceRank.High },
            JsonSerializer.Deserialize<SwiftSortedList<PersistenceRank?>>(JsonSerializer.Serialize(nullableEnums)));
    }

    [Fact]
    public void Json_ParameterlessClosedConverterUsesSupportedBuiltins()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new SwiftSortedListJsonConverter<int>());
        var source = new SwiftSortedList<int>(new[] { -1, 2 });

        var result = JsonSerializer.Deserialize<SwiftSortedList<int>>(JsonSerializer.Serialize(source, options), options);

        Assert.Equal(new[] { -1, 2 }, result.AsReadOnlySpan().ToArray());
        Assert.Same(Comparer<int>.Default, result.Comparer);
    }

    [Fact]
    public void Json_ClosedConverterDirectCallsHandleNullAndRejectAnUnexpectedToken()
    {
        Assert.Null(ReadClosedJson("null"));
        Assert.Throws<JsonException>(() => ReadClosedJson("[]"));
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            new SwiftSortedListJsonConverter<int>().Write(writer, null, new JsonSerializerOptions());
            writer.Flush();
        }
        Assert.Equal("null", Encoding.UTF8.GetString(buffer.WrittenSpan));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{\"FormatVersion\":")]
    [InlineData("{\"FormatVersion\":1,\"Comparers\":[")]
    public void Json_DirectConverterRejectsIncompleteNonFinalInput(string input)
    {
        Assert.Throws<JsonException>(() => ReadClosedJson(input, false));
    }

    [Fact]
    public void Json_NullOwnersHaveNoComparerAndRawStateKeepsItsOriginalContract()
    {
        Assert.Equal("null", JsonSerializer.Serialize<SwiftSortedList<int>>(null));
        Assert.Null(JsonSerializer.Deserialize<SwiftSortedList<int>>("null"));
        int[] supplied = { 13, 0, -7 };
        var state = new SwiftArrayState<int>(supplied);
        var restored = new SwiftSortedList<int>(state);

        Assert.Same(Comparer<int>.Default, restored.Comparer);
        Assert.Equal(supplied, restored.AsReadOnlySpan().ToArray());
        Assert.NotSame(supplied, restored.InnerArray);
        Assert.Equal("{\"Items\":[13,0,-7]}", JsonSerializer.Serialize(state));
        supplied[0] = 99;
        Assert.Equal(13, restored[0]);
    }

    [Fact]
    public void Json_StateOnlyLegacyPayloadsAreRejectedEvenWithARegisteredComparer()
    {
        int factoryCalls = 0;
        var comparer = Comparer<int>.Create(static (left, right) => right.CompareTo(left));
        var registry = new SwiftComparerRegistryBuilder().RegisterOrdering<int>("test.legacy", 1, comparer,
            static _ => ReadOnlyMemory<byte>.Empty, _ => { factoryCalls++; return (comparer, 0); }).Build();
        var options = JsonOptions(registry);

        foreach (string json in new[] { LegacyDescendingJson, "{\"State\":{\"Items\":[]}}", "{\"Items\":[13,0,-7]}" })
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SwiftSortedList<int>>(json, options));

        Assert.Equal(0, factoryCalls);
    }

#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(128)]
    public void MemoryPack_DefaultAndOrdinalRulesWorkWithoutServiceProvider(int count)
    {
        var integers = new SwiftSortedList<int>(Enumerable.Range(0, count));
        byte[] bytes = MemoryPackSerializer.Serialize(integers);
        Assert.Equal(3, bytes[0]);
        Assert.Equal(Enumerable.Range(0, count), MemoryPackSerializer.Deserialize<SwiftSortedList<int>>(bytes));
        var strings = new SwiftSortedList<string>(new[] { "b", "A" }, StringComparer.OrdinalIgnoreCase);
        var result = MemoryPackSerializer.Deserialize<SwiftSortedList<string>>(MemoryPackSerializer.Serialize(strings));
        Assert.Same(StringComparer.OrdinalIgnoreCase, result.Comparer);
        bool containsLowercaseA = result.Contains("a");
        Assert.True(containsLowercaseA);
        Assert.Null(MemoryPackSerializer.Deserialize<SwiftSortedList<int>>(MemoryPackSerializer.Serialize<SwiftSortedList<int>>(null)));
    }

    [Fact]
    public void MemoryPack_ExplicitBootstrapCanRepeatBeforeRealRoundTrip()
    {
        SwiftSortedList<int>.RegisterFormatter();
        SwiftSortedList<int>.RegisterFormatter();

        Assert.True(MemoryPackFormatterProvider.IsRegistered<SwiftSortedList<int>>());
        var source = new SwiftSortedList<int>(new[] { -1, 2 });
        var result = MemoryPackSerializer.Deserialize<SwiftSortedList<int>>(MemoryPackSerializer.Serialize(source));
        Assert.Equal(new[] { -1, 2 }, result.AsReadOnlySpan().ToArray());
        Assert.Same(Comparer<int>.Default, result.Comparer);
    }

    [Fact]
    public void MemoryPack_ConfiguredOwnerAndGeneratedNestedPathsUseTheSameEnvelope()
    {
        var comparer = new PersistenceTokenComparer(-1);
        var options = new MemoryPackSerializerOptions { ServiceProvider = CreateTokenRegistry(comparer) };
        var source = TokenBox(comparer);
        comparer.Comparisons = 0;
        byte[] ordinary = MemoryPackSerializer.Serialize(source.Single, options);
        var buffer = new ArrayBufferWriter<byte>();
        using (var state = MemoryPackWriterOptionalStatePool.Rent(options))
        {
            var writer = new MemoryPackWriter<ArrayBufferWriter<byte>>(ref buffer, state);
            var value = source.Single;
            SwiftSortedList<PersistenceToken>.Serialize(ref writer, ref value);
            writer.Flush();
        }
        Assert.Equal(ordinary, buffer.WrittenSpan.ToArray());
        using (var state = MemoryPackReaderOptionalStatePool.Rent(options))
        {
            var reader = new MemoryPackReader(ordinary, state);
            SwiftSortedList<PersistenceToken> result = null;
            SwiftSortedList<PersistenceToken>.Deserialize(ref reader, ref result);
            Assert.Equal(0, Assert.IsType<PersistenceTokenComparer>(result.Comparer).Comparisons);
            Assert.Equal(new[] { 9, 4, -3 }, result.Select(static item => item.Key));
            Assert.Equal(0, reader.Remaining);
        }
        var nested = MemoryPackSerializer.Deserialize<SortedListPersistenceBox>(MemoryPackSerializer.Serialize(source, options), options);
        AssertTokenBox(nested);
        var packableBuffer = new ArrayBufferWriter<byte>();
        using (var state = MemoryPackWriterOptionalStatePool.Rent(options))
        {
            var writer = new MemoryPackWriter<ArrayBufferWriter<byte>>(ref packableBuffer, state);
            writer.WritePackableArray(source.Array);
            writer.Flush();
        }
        Assert.Equal(MemoryPackSerializer.Serialize(source.Array, options), packableBuffer.WrittenSpan.ToArray());
        var listBuffer = new ArrayBufferWriter<byte>();
        using (var state = MemoryPackWriterOptionalStatePool.Rent(options))
        {
            var writer = new MemoryPackWriter<ArrayBufferWriter<byte>>(ref listBuffer, state);
            MemoryPack.Formatters.ListFormatter.SerializePackable(ref writer, source.List);
            writer.Flush();
        }
        Assert.Equal(MemoryPackSerializer.Serialize(source.List, options), listBuffer.WrittenSpan.ToArray());
        Assert.Equal(0, comparer.Comparisons);
        Assert.Equal(ordinary, MemoryPackSerializer.Serialize(MemoryPackSerializer.Deserialize<SwiftSortedList<PersistenceToken>>(ordinary, options), options));
    }

    [Fact]
    public void MemoryPack_Utf16StringEncodingPreservesOrderingMetadataAndUnicodeItems()
    {
        var options = new MemoryPackSerializerOptions { StringEncoding = StringEncoding.Utf16 };
        var source = new SwiftSortedList<string>(new[] { "é", "İ", "z" }, StringComparer.Ordinal);

        byte[] bytes = MemoryPackSerializer.Serialize(source, options);
        var result = MemoryPackSerializer.Deserialize<SwiftSortedList<string>>(bytes, options);

        Assert.Same(StringComparer.Ordinal, result.Comparer);
        Assert.Equal(source.AsReadOnlySpan().ToArray(), result.AsReadOnlySpan().ToArray());
        bool containsUnicode = result.Contains("İ");
        Assert.True(containsUnicode);
        Assert.Equal(bytes, MemoryPackSerializer.Serialize(result, options));
    }

    [Fact]
    public void MemoryPack_ExplicitBaseContractProjectsDerivedOwnerItemsAndComparer()
    {
        var comparer = Comparer<int>.Create(static (left, right) => right.CompareTo(left));
        var registry = new SwiftComparerRegistryBuilder().RegisterOrdering<int>("test.base-descending", 1, comparer,
            static _ => ReadOnlyMemory<byte>.Empty, _ => (comparer, 0)).Build();
        var source = new PersistenceDerivedSortedList(comparer) { HostValue = 73 };
        source.AddRange(new[] { 1, 2, 3 });
        var options = MemoryPackSerializerOptions.Default with { ServiceProvider = registry };

        byte[] bytes = MemoryPackSerializer.Serialize<SwiftSortedList<int>>(source, options);
        var result = MemoryPackSerializer.Deserialize<SwiftSortedList<int>>(bytes, options);

        Assert.IsType<SwiftSortedList<int>>(result);
        Assert.Same(comparer, result.Comparer);
        Assert.Equal(new[] { 3, 2, 1 }, result.AsReadOnlySpan().ToArray());
        var baseOwner = new SwiftSortedList<int>(new[] { 1, 2, 3 }, comparer);
        Assert.Equal(MemoryPackSerializer.Serialize(baseOwner, options), bytes);
        bool containsOne = result.Contains(1);
        Assert.True(containsOne);
        result.Add(4);
        Assert.Equal(new[] { 4, 3, 2, 1 }, result.AsReadOnlySpan().ToArray());
        Assert.Equal(73, source.HostValue);
        Assert.Equal(new[] { 3, 2, 1 }, source.AsReadOnlySpan().ToArray());
    }

    [Fact]
    public void MemoryPack_HostServiceCompositionAllowsMissingServiceButRejectsAnIncompatibleOne()
    {
        var source = new SwiftSortedList<int>(new[] { 1, 2 });
        byte[] bytes = MemoryPackSerializer.Serialize(source);
        var missing = MemoryPackSerializerOptions.Default with { ServiceProvider = new ComparerServiceProvider(null) };
        Assert.Equal(bytes, MemoryPackSerializer.Serialize(source, missing));
        Assert.Equal(new[] { 1, 2 }, MemoryPackSerializer.Deserialize<SwiftSortedList<int>>(bytes, missing));
        var wrong = MemoryPackSerializerOptions.Default with { ServiceProvider = new ComparerServiceProvider("not a registry") };

        Assert.Throws<InvalidOperationException>(() => MemoryPackSerializer.Serialize(source, wrong));
        Assert.Throws<InvalidOperationException>(() => MemoryPackSerializer.Deserialize<SwiftSortedList<int>>(bytes, wrong));
    }

    [Fact]
    public void MemoryPack_FailedRestoreLeavesExistingOwnerAndViewsUntouched()
    {
        var original = new SwiftSortedList<int>(new[] { 10, 20 });
        var overwritten = original;
        var view = original.InnerArray;
        byte[] valid = MemoryPackSerializer.Serialize(new SwiftSortedList<int>(new[] { 1, 2, 3 }));
        byte[] truncated = valid[..^1];

        Assert.Throws<MemoryPackSerializationException>(() => MemoryPackSerializer.Deserialize(truncated, ref overwritten));

        Assert.Same(original, overwritten);
        Assert.Same(view, original.InnerArray);
        Assert.Equal(new[] { 10, 20 }, original.AsReadOnlySpan().ToArray());
        byte[] unknownVersion = (byte[])valid.Clone();
        unknownVersion[1] = 2;
        Assert.Throws<MemoryPackSerializationException>(() => MemoryPackSerializer.Deserialize(unknownVersion, ref overwritten));
        Assert.Same(original, overwritten);
    }

    [Fact]
    public void MemoryPack_UnknownCodecAndLegacyInputFailWithoutDefaultRestoration()
    {
        var comparer = new PersistenceTokenComparer(-1);
        var options = new MemoryPackSerializerOptions { ServiceProvider = CreateTokenRegistry(comparer) };
        byte[] custom = MemoryPackSerializer.Serialize(TokenList(comparer), options);

        Assert.Throws<MemoryPackSerializationException>(() => MemoryPackSerializer.Deserialize<SwiftSortedList<PersistenceToken>>(custom));
        Assert.Throws<MemoryPackSerializationException>(() => MemoryPackSerializer.Deserialize<SwiftSortedList<int>>(Convert.FromBase64String(LegacyDescendingMemoryPack)));
    }

    [Theory]
    [InlineData("owner-members")]
    [InlineData("null-descriptors")]
    [InlineData("zero-descriptors")]
    [InlineData("extra-descriptors")]
    [InlineData("descriptor-members")]
    [InlineData("wrong-role")]
    [InlineData("null-id")]
    [InlineData("empty-id")]
    [InlineData("oversized-utf16-id")]
    [InlineData("oversized-utf8-id")]
    [InlineData("negative-character-count")]
    [InlineData("zero-character-count")]
    [InlineData("oversized-character-count")]
    [InlineData("inconsistent-character-count")]
    [InlineData("zero-codec-version")]
    [InlineData("null-config")]
    [InlineData("negative-config")]
    [InlineData("oversized-config")]
    [InlineData("invalid-id-character")]
    [InlineData("null-descriptor")]
    [InlineData("null-state-items")]
    public void MemoryPack_InvalidDescriptorHeadersFailBeforePublishingAnOwner(string mutation)
    {
        var options = new MemoryPackSerializerOptions { StringEncoding = StringEncoding.Utf8 };
        byte[] bytes = MemoryPackSerializer.Serialize(new SwiftSortedList<int>(new[] { 1, 2 }), options);
        int idBytes = ~BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(9, 4));
        Assert.Equal("lsf.runtime-ordering-default".Length, idBytes);
        int codecVersionOffset = 17 + idBytes;
        int configOffset = codecVersionOffset + 2;
        switch (mutation)
        {
            case "owner-members": bytes[0] = 2; break;
            case "null-descriptors": BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(3, 4), -1); break;
            case "zero-descriptors": BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(3, 4), 0); break;
            case "extra-descriptors": BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(3, 4), 2); break;
            case "descriptor-members": bytes[7] = 3; break;
            case "wrong-role": bytes[8] = 2; break;
            case "null-id": BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(9, 4), -1); break;
            case "empty-id": BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(9, 4), 0); break;
            case "oversized-utf16-id": Array.Resize(ref bytes, 512); BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(9, 4), 129); break;
            case "oversized-utf8-id": BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(9, 4), ~129); break;
            case "negative-character-count": BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(13, 4), -1); break;
            case "zero-character-count": BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(13, 4), 0); break;
            case "oversized-character-count": BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(13, 4), 129); break;
            case "inconsistent-character-count": BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(13, 4), idBytes - 1); break;
            case "zero-codec-version": BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(codecVersionOffset, 2), 0); break;
            case "null-config": BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(configOffset, 4), -1); break;
            case "negative-config": BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(configOffset, 4), -2); break;
            case "oversized-config": Array.Resize(ref bytes, 5000); BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(configOffset, 4), 4097); break;
            case "invalid-id-character": bytes[17] = (byte)'A'; break;
            case "null-descriptor": bytes[7] = 255; break;
            case "null-state-items": BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(configOffset + 5, 4), -1); break;
            default: throw new ArgumentException("Unknown descriptor mutation.", nameof(mutation));
        }
        var original = new SwiftSortedList<int>(new[] { 10, 20 });
        var overwritten = original;

        Assert.Throws<MemoryPackSerializationException>(() => MemoryPackSerializer.Deserialize(bytes, ref overwritten, options));

        Assert.Same(original, overwritten);
        Assert.Equal(new[] { 10, 20 }, original.AsReadOnlySpan().ToArray());
    }

    [Fact]
    public void MemoryPack_StateOnlyLegacyPayloadsAreRejectedWithoutReplacingTheOwner()
    {
        int factoryCalls = 0;
        var comparer = Comparer<int>.Create(static (left, right) => right.CompareTo(left));
        var registry = new SwiftComparerRegistryBuilder().RegisterOrdering<int>("test.legacy", 1, comparer,
            static _ => ReadOnlyMemory<byte>.Empty, _ => { factoryCalls++; return (comparer, 0); }).Build();
        var options = MemoryPackSerializerOptions.Default with { ServiceProvider = registry };
        var original = new SwiftSortedList<int>(new[] { 20, 10 }, comparer);

        foreach (byte[] bytes in new[] { Convert.FromBase64String(LegacyDescendingMemoryPack), new byte[] { 1, 1, 0, 0, 0, 0 } })
        {
            var overwritten = original;
            Assert.Throws<MemoryPackSerializationException>(() => MemoryPackSerializer.Deserialize(bytes, ref overwritten, options));
            Assert.Same(original, overwritten);
            Assert.Same(comparer, original.Comparer);
            Assert.Equal(new[] { 20, 10 }, original.AsReadOnlySpan().ToArray());
        }

        Assert.Equal(0, factoryCalls);
    }
#endif

    private static string Envelope(string descriptor) => "{\"FormatVersion\":1,\"Comparers\":[" + descriptor + "],\"State\":{\"Items\":[1,2]}}";

    private static SwiftSortedList<int> ReadClosedJson(string json, bool finalBlock = true)
    {
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json), finalBlock, default);
        reader.Read();
        return new SwiftSortedListJsonConverter<int>().Read(ref reader, typeof(SwiftSortedList<int>), new JsonSerializerOptions());
    }

    private static JsonSerializerOptions JsonOptions(SwiftComparerRegistry registry)
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new SwiftCollectionJsonConverterFactory(registry));
        return options;
    }

    private static SwiftComparerRegistry CreateTokenRegistry(PersistenceTokenComparer comparer)
    {
        return new SwiftComparerRegistryBuilder().RegisterOrdering<PersistenceToken>("test.token.direction", 1, comparer,
            static value => new byte[] { unchecked((byte)((PersistenceTokenComparer)value).Direction) },
            static bytes => (new PersistenceTokenComparer(unchecked((sbyte)bytes.Span[0])), 1)).Build();
    }

    private static SwiftSortedList<PersistenceToken> TokenList(PersistenceTokenComparer comparer)
        => new(new[] { new PersistenceToken { Key = -3 }, new PersistenceToken { Key = 4 }, new PersistenceToken { Key = 9 } }, comparer);

    private static SortedListPersistenceBox TokenBox(PersistenceTokenComparer comparer)
        => new() { Single = TokenList(comparer), Array = new[] { TokenList(comparer) }, List = new List<SwiftSortedList<PersistenceToken>> { TokenList(comparer) } };

    private static void AssertTokenBox(SortedListPersistenceBox value)
    {
        foreach (var list in new[] { value.Single, Assert.Single(value.Array), Assert.Single(value.List) })
        {
            var comparer = Assert.IsType<PersistenceTokenComparer>(list.Comparer);
            Assert.Equal(0, comparer.Comparisons);
            Assert.Equal(-1, comparer.Direction);
            Assert.Equal(new[] { 9, 4, -3 }, list.Select(static item => item.Key));
            bool containsFour = list.Contains(new PersistenceToken { Key = 4 });
            Assert.True(containsFour);
        }
    }

    private sealed class HostileObjectEqualityComparer : IComparer<int>
    {
        internal int Direction { get; }
        internal HostileObjectEqualityComparer(int direction) => Direction = direction;
        public int Compare(int left, int right) => Direction * left.CompareTo(right);
        public override bool Equals(object obj) => throw new InvalidOperationException("Comparer object equality must not be used for registration.");
        public override int GetHashCode() => throw new InvalidOperationException("Comparer object hashing must not be used for registration.");
    }

    private sealed class JsonInputSegment : ReadOnlySequenceSegment<byte>
    {
        internal JsonInputSegment(ReadOnlyMemory<byte> memory) => Memory = memory;
        internal JsonInputSegment Append(ReadOnlyMemory<byte> memory)
        {
            var next = new JsonInputSegment(memory) { RunningIndex = RunningIndex + Memory.Length };
            Next = next;
            return next;
        }
    }

#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
    private sealed class ComparerServiceProvider : IServiceProvider
    {
        private readonly object _service;
        internal ComparerServiceProvider(object service) => _service = service;
        public object GetService(Type serviceType) => serviceType == typeof(SwiftComparerRegistry) ? _service : null;
    }
#endif
}

