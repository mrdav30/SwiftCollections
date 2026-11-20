using System;
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
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
    [InlineData(true, false)]
    [InlineData(true, true)]
#endif
    public void EqualityCallbackFailuresHaveTransportExceptionsAndNeverPublishPartialOwners(bool binary, bool hashThrows)
    {
        var comparer = new ThrowingHashComparer(hashThrows, false);
        var registry = new SwiftComparerRegistryBuilder().RegisterEquality<int>("test.throwing", 1, comparer,
            static _ => ReadOnlyMemory<byte>.Empty, _ => (new ThrowingHashComparer(hashThrows, true), 0)).Build();
        var dictionary = new SwiftDictionary<int, int>(8, comparer) { { 1, 11 }, { 2, 22 } };
        var set = new SwiftHashSet<int>(new[] { 1, 2 }, comparer);
        var bidirectional = new SwiftBiDictionary<int, int>(comparer, null) { { 1, 11 }, { 2, 22 } };
        var observable = new SwiftObservableDictionary<int, int>();
        observable.SetComparer(comparer);
        observable.Add(1, 11);
        observable.Add(2, 22);

        AssertCallbackFailure<SwiftDictionary<int, int>>(Encode(dictionary, binary, registry), binary, registry);
        AssertCallbackFailure<SwiftHashSet<int>>(Encode(set, binary, registry), binary, registry);
        AssertCallbackFailure<SwiftBiDictionary<int, int>>(Encode(bidirectional, binary, registry), binary, registry);
        AssertCallbackFailure<SwiftObservableDictionary<int, int>>(Encode(observable, binary, registry), binary, registry);
#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
        if (binary)
        {
            var original = new SwiftDictionary<int, int> { { 7, 77 } };
            var overwritten = original;
            var options = new MemoryPackSerializerOptions { ServiceProvider = registry };
            byte[] bytes = Encode(dictionary, true, registry);
            var exception = Assert.Throws<MemoryPackSerializationException>(() => MemoryPackSerializer.Deserialize(bytes, ref overwritten, options));
            Assert.IsType<ArithmeticException>(exception.InnerException);
            Assert.Same(original, overwritten);
            Assert.Equal(77, overwritten[7]);
            Assert.Single(overwritten);
        }
#endif
        Assert.Equal(2, dictionary.Count);
        Assert.Equal(2, set.Count);
        Assert.Equal(2, bidirectional.Count);
        Assert.Equal(2, observable.Count);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void HashSetStateNullSkippingRemainsDistinctFromItsNullRejectingAddContract(bool binary)
    {
        var state = new SwiftArrayState<string>(new[] { null, "value", null });
        var restoredRaw = new SwiftHashSet<string>(state);

        var restored = Decode<SwiftHashSet<string>>(Envelope(binary, state,
            ((byte)3, "lsf.string-ordinal", Array.Empty<byte>())), binary);

        Assert.Equal(new[] { "value" }, restored);
        Assert.Equal(new[] { "value" }, restoredRaw);
        Assert.Same(StringComparer.Ordinal, restored.Comparer);
        Assert.Throws<ArgumentNullException>(() => restored.Add(null));
        Assert.Equal(new[] { null, "value", null }, state.Items);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void NullDictionaryKeysAndReverseValuesAreRejectedBeforePublication(bool binary)
    {
        var state = new SwiftDictionaryState<string, string>(new[] { new KeyValuePair<string, string>(null, "value") });
        var forward = ((byte)2, "lsf.string-ordinal", Array.Empty<byte>());
        var reverse = ((byte)4, "lsf.string-ordinal", Array.Empty<byte>());
        var effective = ((byte)5, "lsf.string-ordinal", Array.Empty<byte>());

        AssertInvalid<SwiftDictionary<string, string>>(Envelope(binary, state, forward), binary);
        AssertInvalid<SwiftBiDictionary<string, string>>(Envelope(binary, state, forward, reverse, effective), binary);
        state = new SwiftDictionaryState<string, string>(new[] { new KeyValuePair<string, string>("key", null) });
        AssertInvalid<SwiftBiDictionary<string, string>>(Envelope(binary, state, forward, reverse, effective), binary);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public void CultureDependentEqualityRequiresAnExplicitApplicationCodec(bool binary)
    {
        var comparer = StringComparer.InvariantCultureIgnoreCase;
        var source = new SwiftDictionary<string, int>(8, comparer) { { "Alpha", 17 } };
        Assert.Throws<NotSupportedException>(() => Encode(source, binary));
        var registry = new SwiftComparerRegistryBuilder().RegisterEquality<string>("test.host-collation", 1, comparer,
            static _ => ReadOnlyMemory<byte>.Empty, _ => (comparer, 0)).Build();

        var result = RoundTrip(source, binary, registry);

        Assert.Same(comparer, result.Comparer);
        Assert.Equal(17, result["ALPHA"]);
    }

    private static void AssertCallbackFailure<T>(byte[] bytes, bool binary, SwiftComparerRegistry registry)
    {
#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
        if (binary)
        {
            var exception = Assert.Throws<MemoryPackSerializationException>(() => Decode<T>(bytes, true, registry));
            Assert.IsType<ArithmeticException>(exception.InnerException);
            return;
        }
#endif
        var jsonException = Assert.Throws<JsonException>(() => Decode<T>(bytes, false, registry));
        Assert.IsType<ArithmeticException>(jsonException.InnerException);
    }

    private sealed class ThrowingHashComparer : IEqualityComparer<int>
    {
        private readonly bool _hashThrows, _fail;
        internal ThrowingHashComparer(bool hashThrows, bool fail) { _hashThrows = hashThrows; _fail = fail; }
        public bool Equals(int left, int right)
        {
            if (_fail && !_hashThrows) throw new ArithmeticException("Equality callback failed.");
            return left == right;
        }
        public int GetHashCode(int value)
        {
            if (_fail && _hashThrows) throw new ArithmeticException("Hash callback failed.");
            return 0;
        }
    }
}
