#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
using System;
using System.Buffers;
using System.Collections.Generic;
using MemoryPack;
using SwiftCollections.Observable;
using SwiftCollections.Serialization;
using Xunit;

namespace SwiftCollections.Tests;

public partial class HashComparerPersistenceTests
{
    [Fact]
    public void MemoryPack_ExplicitBootstrapAndDirectPackableCallsUseTheSameOwnerEnvelope()
    {
        SwiftDictionary<PersistenceHashKey, int>.RegisterFormatter();
        SwiftHashSet<PersistenceHashKey>.RegisterFormatter();
        SwiftBiDictionary<PersistenceHashKey, int>.RegisterFormatter();
        SwiftObservableDictionary<PersistenceHashKey, int>.RegisterFormatter();
        var comparer = new PersistenceHashKeyComparer(9);
        var registry = KeyRegistry(comparer);
        var dictionary = new SwiftDictionary<PersistenceHashKey, int>(8, comparer) { { Key(1), 17 } };
        var set = new SwiftHashSet<PersistenceHashKey>(new[] { Key(1), Key(2) }, comparer);
        var bidirectional = new SwiftBiDictionary<PersistenceHashKey, int>(comparer, null) { { Key(1), 17 } };
        var observable = new SwiftObservableDictionary<PersistenceHashKey, int>();
        observable.SetComparer(comparer);
        observable.Add(Key(1), 17);

        Assert.Equal(17, DirectPackableRoundTrip(dictionary, registry)[Key(1)]);
        Assert.Equal(2, DirectPackableRoundTrip(set, registry).Count);
        Assert.Equal(1, DirectPackableRoundTrip(bidirectional, registry).GetKey(17).Identity);
        Assert.Equal(17, DirectPackableRoundTrip(observable, registry)[Key(1)]);
        Assert.Null(DirectPackableRoundTrip<SwiftDictionary<PersistenceHashKey, int>>(null, registry));
        Assert.Null(DirectPackableRoundTrip<SwiftHashSet<PersistenceHashKey>>(null, registry));
        Assert.Null(DirectPackableRoundTrip<SwiftBiDictionary<PersistenceHashKey, int>>(null, registry));
        Assert.Null(DirectPackableRoundTrip<SwiftObservableDictionary<PersistenceHashKey, int>>(null, registry));
    }

    [Fact]
    public void MemoryPack_SuccessfulRefRestoreReplacesObservableOwnerAndDetachesOldSubscribers()
    {
        var original = new SwiftObservableDictionary<int, int> { { 10, 20 } };
        var overwritten = original;
        int oldEvents = 0;
        original.CollectionChanged += (_, _) => oldEvents++;
        byte[] bytes = MemoryPackSerializer.Serialize(new SwiftObservableDictionary<int, int> { { 1, 2 } });

        MemoryPackSerializer.Deserialize(bytes, ref overwritten);

        Assert.NotSame(original, overwritten);
        Assert.Equal(2, overwritten[1]);
        Assert.Equal(20, original[10]);
        Assert.Equal(0, oldEvents);
        overwritten.Add(3, 4);
        Assert.Equal(0, oldEvents);
        original.Add(30, 40);
        Assert.Equal(1, oldEvents);
    }

    [Fact]
    public void MemoryPack_FrozenStateOnlyHashOwnerRecordsAreRejected()
    {
        // Produced at a5371dc1caa9e1b81b631ac9ce88d5f03933ebc2, Release/net10.0, MemoryPack 1.21.4.
        const string dictionary = "AQECAAAA+////wQAAABzYW1lCwAAAPv///8EAAAAc2FtZRYAAAA=";
        const string set = "AQECAAAA+////wQAAABzYW1l+////wQAAABzYW1l";
        const string bidirectional = "AQECAAAA+////wQAAABzYW1l+////wQAAABsZWZ0+////wQAAABzYW1l+v///wUAAAByaWdodA==";
        const string reverseDistinct = "AQECAAAA+////wQAAABsZWZ0+////wQAAABzYW1l+v///wUAAAByaWdodPv///8EAAAAc2FtZQ==";

        AssertInvalid<SwiftDictionary<string, int>>(Convert.FromBase64String(dictionary), true);
        AssertInvalid<SwiftHashSet<string>>(Convert.FromBase64String(set), true);
        AssertInvalid<SwiftBiDictionary<string, string>>(Convert.FromBase64String(bidirectional), true);
        AssertInvalid<SwiftBiDictionary<string, string>>(Convert.FromBase64String(reverseDistinct), true);
        AssertInvalid<SwiftObservableDictionary<string, int>>(Convert.FromBase64String(dictionary), true);
    }

    private static T DirectPackableRoundTrip<T>(T source, SwiftComparerRegistry registry) where T : class, IMemoryPackable<T>
    {
        var options = new MemoryPackSerializerOptions { ServiceProvider = registry };
        byte[] ordinary = MemoryPackSerializer.Serialize(source, options);
        var buffer = new ArrayBufferWriter<byte>();
        using (var state = MemoryPackWriterOptionalStatePool.Rent(options))
        {
            var writer = new MemoryPackWriter<ArrayBufferWriter<byte>>(ref buffer, state);
            T.Serialize(ref writer, ref source);
            writer.Flush();
        }
        Assert.Equal(ordinary, buffer.WrittenSpan.ToArray());
        T result = null;
        using (var state = MemoryPackReaderOptionalStatePool.Rent(options))
        {
            var reader = new MemoryPackReader(ordinary, state);
            T.Deserialize(ref reader, ref result);
            Assert.Equal(0, reader.Remaining);
        }
        var arrayBuffer = new ArrayBufferWriter<byte>();
        T[] array = new[] { source, null };
        using (var state = MemoryPackWriterOptionalStatePool.Rent(options))
        {
            var writer = new MemoryPackWriter<ArrayBufferWriter<byte>>(ref arrayBuffer, state);
            writer.WritePackableArray(array);
            writer.Flush();
        }
        Assert.Equal(MemoryPackSerializer.Serialize(array, options), arrayBuffer.WrittenSpan.ToArray());
        var listBuffer = new ArrayBufferWriter<byte>();
        var list = new List<T> { source, null };
        using (var state = MemoryPackWriterOptionalStatePool.Rent(options))
        {
            var writer = new MemoryPackWriter<ArrayBufferWriter<byte>>(ref listBuffer, state);
            MemoryPack.Formatters.ListFormatter.SerializePackable(ref writer, list);
            writer.Flush();
        }
        Assert.Equal(MemoryPackSerializer.Serialize(list, options), listBuffer.WrittenSpan.ToArray());
        return result;
    }
}
#endif
