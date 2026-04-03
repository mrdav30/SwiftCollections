using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using Xunit;

namespace SwiftCollections.Tests;

public class SwiftHashToolsTests
{
    [Fact]
    public void PowerHelpers_HandleBoundaryValues()
    {
        Assert.False(SwiftHashTools.IsPowerOfTwo(0));
        Assert.False(SwiftHashTools.IsPowerOfTwo(3));
        Assert.True(SwiftHashTools.IsPowerOfTwo(1));
        Assert.True(SwiftHashTools.IsPowerOfTwo(16));

        Assert.Equal(1, SwiftHashTools.NextPowerOfTwo(-5));
        Assert.Equal(1, SwiftHashTools.NextPowerOfTwo(0));
        Assert.Equal(1, SwiftHashTools.NextPowerOfTwo(1));
        Assert.Equal(8, SwiftHashTools.NextPowerOfTwo(5));
        Assert.Equal(16, SwiftHashTools.NextPowerOfTwo(16));
        Assert.Equal(int.MaxValue, SwiftHashTools.NextPowerOfTwo(1 << 30));
    }

    [Fact]
    public void CombineHashCodes_MatchesTheImplementedMixingAlgorithms()
    {
        (int, string, object, int) tuple = (1, "two", null, 4);
        object[] values = { 1, "two", null, 4 };

        int tupleHash = ((ITuple)tuple).CombineHashCodes(seed: 7, shift1: 3, shift2: 5, shift3: 2, factor3: 11);
        int objectArrayHash = values.CombineHashCodes(seed: 7, shift1: 3, shift2: 5, shift3: 2, factor3: 11);

        Assert.Equal(ComputeTupleHash((ITuple)tuple, 7, 3, 5, 2, 11), tupleHash);
        Assert.Equal(ComputeObjectArrayHash(values, 7, 3, 5, 2, 11), objectArrayHash);
        Assert.Equal(SwiftHashTools.CombineHashCodes(values), SwiftHashTools.CombineHashCodes(1, "two", null, 4));
    }

    [Fact]
    public void DeterministicComparerFactories_ReturnExpectedComparers()
    {
        var defaultStringComparer = SwiftHashTools.GetDeterministicStringEqualityComparer();
        var explicitDefaultStringComparer = SwiftHashTools.GetDeterministicStringEqualityComparer(SwiftHashTools.DefaultDeterministicStringHashSeed);
        var seededStringComparer = SwiftHashTools.GetDeterministicStringEqualityComparer(13);
        var defaultObjectComparer = SwiftHashTools.GetDeterministicObjectEqualityComparer();
        var explicitDefaultObjectComparer = SwiftHashTools.GetDeterministicObjectEqualityComparer(SwiftHashTools.DefaultDeterministicObjectHashSeed);
        var seededObjectComparer = SwiftHashTools.GetDeterministicObjectEqualityComparer(23);

        Assert.IsType<SwiftDeterministicStringEqualityComparer>(SwiftHashTools.GetDeterministicEqualityComparer<string>());
        Assert.IsType<SwiftDeterministicObjectEqualityComparer>(SwiftHashTools.GetDeterministicEqualityComparer<object>());
        Assert.Same(EqualityComparer<int>.Default, SwiftHashTools.GetDeterministicEqualityComparer<int>());
        Assert.Same(defaultStringComparer, explicitDefaultStringComparer);
        Assert.Same(defaultObjectComparer, explicitDefaultObjectComparer);
        Assert.NotSame(defaultStringComparer, seededStringComparer);
        Assert.NotSame(defaultObjectComparer, seededObjectComparer);
        Assert.Same(defaultStringComparer, SwiftHashTools.GetDefaultEqualityComparer<string>());
        Assert.Same(defaultObjectComparer, SwiftHashTools.GetDefaultEqualityComparer<object>());
        Assert.Same(EqualityComparer<int>.Default, SwiftHashTools.GetDefaultEqualityComparer<int>());
        Assert.Same(StringComparer.Ordinal, SwiftHashTools.GetDefaultEqualityComparer(StringComparer.Ordinal));
    }

    [Fact]
    public void WellKnownComparerDetection_RecognizesBuiltInDeterministicComparers()
    {
        Assert.True(SwiftHashTools.IsWellKnownEqualityComparer(null));
        Assert.True(SwiftHashTools.IsWellKnownEqualityComparer(EqualityComparer<string>.Default));
        Assert.True(SwiftHashTools.IsWellKnownEqualityComparer(EqualityComparer<object>.Default));
        Assert.True(SwiftHashTools.IsWellKnownEqualityComparer(new SwiftDeterministicStringEqualityComparer()));
        Assert.True(SwiftHashTools.IsWellKnownEqualityComparer(new SwiftDeterministicObjectEqualityComparer()));
        Assert.False(SwiftHashTools.IsWellKnownEqualityComparer(StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void RandomizedComparerFactory_ReturnsExpectedComparerTypes()
    {
        Assert.IsType<SwiftStringEqualityComparer>(SwiftHashTools.GetSwiftEqualityComparer(EqualityComparer<string>.Default));
        Assert.IsType<SwiftStringEqualityComparer>(SwiftHashTools.GetSwiftEqualityComparer(new SwiftDeterministicStringEqualityComparer()));
        Assert.IsType<SwiftObjectEqualityComparer>(SwiftHashTools.GetSwiftEqualityComparer(EqualityComparer<object>.Default));
        Assert.IsType<SwiftObjectEqualityComparer>(SwiftHashTools.GetSwiftEqualityComparer(new SwiftDeterministicObjectEqualityComparer()));
        Assert.IsType<SwiftObjectEqualityComparer>(SwiftHashTools.GetSwiftEqualityComparer(StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void EntropyAndSerializationInfoTable_AreReusableAcrossCalls()
    {
        var values = new HashSet<long>();

        for (int i = 0; i < 130; i++)
            values.Add(SwiftHashTools.GetEntropy());

        ConditionalWeakTable<object, SerializationInfo> first = SwiftHashTools.SerializationInfoTable;
        ConditionalWeakTable<object, SerializationInfo> second = SwiftHashTools.SerializationInfoTable;
        object key = new object();
        var info = CreateSerializationInfo(typeof(object));

        first.Add(key, info);

        Assert.True(values.Count > 1);
        Assert.Same(first, second);
        Assert.True(second.TryGetValue(key, out SerializationInfo stored));
        Assert.Same(info, stored);
    }

    [Fact]
    public void MurmurHash3_IsDeterministicForEvenAndOddLengthStrings()
    {
        int oddHash = SwiftHashTools.MurmurHash3("abc", 17);
        int evenHash = SwiftHashTools.MurmurHash3("abcd", 17);

        Assert.Equal(oddHash, SwiftHashTools.MurmurHash3("abc", 17));
        Assert.Equal(evenHash, SwiftHashTools.MurmurHash3("abcd", 17));
        Assert.NotEqual(oddHash, SwiftHashTools.MurmurHash3("abc", 18));
        Assert.True(oddHash >= 0);
        Assert.True(evenHash >= 0);
    }

    private static int ComputeTupleHash(ITuple tuple, int seed, int shift1, int shift2, int shift3, int factor3)
    {
        int hash1 = (seed << shift1) + seed;
        int hash2 = hash1;

        for (int i = 0; i < tuple.Length; i++)
        {
            int itemHash = tuple[i]?.GetHashCode() ?? 0;
            unchecked
            {
                if (i % 2 == 0)
                    hash1 = ((hash1 << shift2) + hash1 + (hash1 >> shift3)) ^ itemHash;
                else
                    hash2 = ((hash2 << shift2) + hash2 + (hash2 >> shift3)) ^ itemHash;
            }
        }

        return hash1 ^ (hash2 * factor3);
    }

    private static int ComputeObjectArrayHash(object[] values, int seed, int shift1, int shift2, int shift3, int factor3)
    {
        int hash1 = (seed << shift1) + seed;
        int hash2 = hash1;

        for (int i = 0; i < values.Length; i++)
        {
            int itemHash = values[i]?.GetHashCode() ?? 0;
            unchecked
            {
                if ((i & 1) == 0)
                    hash1 = ((hash1 << shift2) + hash1 + (hash1 >> shift3)) ^ itemHash;
                else
                    hash2 = ((hash2 << shift2) + hash2 + (hash2 >> shift3)) ^ itemHash;
            }
        }

        return hash1 + (hash2 * factor3);
    }

    private static SerializationInfo CreateSerializationInfo(Type type)
    {
#pragma warning disable SYSLIB0050
        return new SerializationInfo(type, new FormatterConverter());
#pragma warning restore SYSLIB0050
    }
}
