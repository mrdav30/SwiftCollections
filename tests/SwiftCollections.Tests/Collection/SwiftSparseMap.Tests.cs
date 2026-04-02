using MemoryPack;
using System;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace SwiftCollections.Tests;

public class SwiftSparseMapTests
{
    [Fact]
    public void Add_InsertsItem()
    {
        var set = new SwiftSparseMap<int>();

        set.Add(5, 42);

        Assert.True(set.ContainsKey(5));
        Assert.Equal(42, set[5]);
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void TryAdd_ReturnsFalseForExistingKey()
    {
        var set = new SwiftSparseMap<int>();

        Assert.True(set.TryAdd(1, 10));
        Assert.False(set.TryAdd(1, 20));

        Assert.Equal(10, set[1]);
    }

    [Fact]
    public void Add_ZeroKey_InsertsItem()
    {
        var set = new SwiftSparseMap<int>();

        set.Add(0, 7);

        Assert.True(set.ContainsKey(0));
        Assert.Equal(7, set[0]);
    }

    [Fact]
    public void Indexer_OverwritesExistingValue()
    {
        var set = new SwiftSparseMap<int>();

        set.Add(2, 5);
        set[2] = 10;

        Assert.Equal(10, set[2]);
    }

    [Fact]
    public void ContainsKey_ReturnsFalseForMissingKey()
    {
        var set = new SwiftSparseMap<int>();

        Assert.False(set.ContainsKey(100));
    }

    [Fact]
    public void Add_NegativeKey_Throws()
    {
        var set = new SwiftSparseMap<int>();

        Assert.Throws<ArgumentOutOfRangeException>(() => set.Add(-1, 5));
    }

    [Fact]
    public void Add_IntMaxKey_Throws()
    {
        var set = new SwiftSparseMap<int>();

        Assert.Throws<ArgumentOutOfRangeException>(() => set.Add(int.MaxValue, 5));
    }

    [Fact]
    public void TryGetValue_ReturnsTrueWhenPresent()
    {
        var set = new SwiftSparseMap<int>();

        set.Add(3, 99);

        bool result = set.TryGetValue(3, out int value);

        Assert.True(result);
        Assert.Equal(99, value);
    }

    [Fact]
    public void TryGetValue_ReturnsFalseWhenMissing()
    {
        var set = new SwiftSparseMap<int>();

        bool result = set.TryGetValue(10, out int value);

        Assert.False(result);
        Assert.Equal(default, value);
    }

    [Fact]
    public void Remove_RemovesExistingKey()
    {
        var set = new SwiftSparseMap<int>();

        set.Add(1, 10);

        bool removed = set.Remove(1);

        Assert.True(removed);
        Assert.False(set.ContainsKey(1));
        Assert.Empty(set);
    }

    [Fact]
    public void Remove_ReturnsFalseForMissingKey()
    {
        var set = new SwiftSparseMap<int>();

        Assert.False(set.Remove(50));
    }

    [Fact]
    public void Remove_PerformsSwapBackCorrectly()
    {
        var set = new SwiftSparseMap<int>();

        set.Add(1, 100);
        set.Add(2, 200);
        set.Add(3, 300);

        set.Remove(2);

        Assert.False(set.ContainsKey(2));
        Assert.Equal(2, set.Count);

        Assert.True(set.ContainsKey(1));
        Assert.True(set.ContainsKey(3));
    }

    [Fact]
    public void Clear_RemovesAllItems()
    {
        var set = new SwiftSparseMap<int>();

        set.Add(1, 10);
        set.Add(2, 20);

        set.Clear();

        Assert.Empty(set);
    }

    [Fact]
    public void DenseArrays_MatchStoredValues()
    {
        var set = new SwiftSparseMap<int>();

        set.Add(1, 10);
        set.Add(2, 20);
        set.Add(3, 30);

        var keys = set.DenseKeys;
        var values = set.DenseValues;

        Assert.Equal(3, set.Count);

        for (int i = 0; i < set.Count; i++)
        {
            Assert.Equal(values[i], set[keys[i]]);
        }
    }

    [Fact]
    public void SupportsNonSequentialKeys()
    {
        var set = new SwiftSparseMap<int>();

        const int sparseKey = 64;

        set.Add(sparseKey, 99);

        Assert.True(set.ContainsKey(sparseKey));
        Assert.Equal(99, set[sparseKey]);
    }

    [Fact]
    public void Enumerator_ReturnsAllItems()
    {
        var set = new SwiftSparseMap<int>();

        set.Add(1, 10);
        set.Add(2, 20);

        var list = set.ToList();

        Assert.Equal(2, list.Count);
        Assert.Contains(list, x => x.Key == 1 && x.Value == 10);
        Assert.Contains(list, x => x.Key == 2 && x.Value == 20);
    }

    [Fact]
    public void Enumerator_ModificationThrows()
    {
        var set = new SwiftSparseMap<int>();

        set.Add(1, 10);
        set.Add(2, 20);

        var enumerator = set.GetEnumerator();

        set.Add(3, 30);

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    #region Serialization

    [Fact]
    public void Json_RoundTrip_PreservesData()
    {
        var set = new SwiftSparseMap<int>();

        set.Add(1, 10);
        set.Add(2, 20);

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(set);

        var result = JsonSerializer.Deserialize<SwiftSparseMap<int>>(json);

        Assert.Equal(2, result.Count);
        Assert.Equal(10, result[1]);
        Assert.Equal(20, result[2]);
    }

    [Fact]
    public void Json_RoundTrip_PreservesDenseIteration()
    {
        var set = new SwiftSparseMap<int>();

        set.Add(5, 50);
        set.Add(10, 100);

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(set);

        var result = JsonSerializer.Deserialize<SwiftSparseMap<int>>(json);

        var keys = result.DenseKeys;

        for (int i = 0; i < result.Count; i++)
        {
            Assert.Equal(result[keys[i]], result.DenseValues[i]);
        }
    }

    [Fact]
    public void MemoryPack_RoundTrip_PreservesData()
    {
        var set = new SwiftSparseMap<int>();

        set.Add(1, 10);
        set.Add(2, 20);

        byte[] bytes = MemoryPackSerializer.Serialize(set);

        var result = MemoryPackSerializer.Deserialize<SwiftSparseMap<int>>(bytes);

        Assert.Equal(2, result.Count);
        Assert.Equal(10, result[1]);
        Assert.Equal(20, result[2]);
    }

    [Fact]
    public void MemoryPack_RoundTrip_PreservesSparseKeys()
    {
        var set = new SwiftSparseMap<int>();

        set.Add(64, 99);

        byte[] bytes = MemoryPackSerializer.Serialize(set);

        var result = MemoryPackSerializer.Deserialize<SwiftSparseMap<int>>(bytes);

        Assert.True(result.ContainsKey(64));
        Assert.Equal(99, result[64]);
    }

    #endregion
}
