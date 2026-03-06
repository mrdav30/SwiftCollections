using MemoryPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace SwiftCollections.Tests;

public class SwiftPackedSetTests
{
    [Fact]
    public void HandlesReferenceTypes()
    {
        var set = new SwiftPackedSet<string>();

        set.Add("Alpha");
        set.Add("Beta");

        Assert.True(set.Contains("Alpha"));
        Assert.True(set.Remove("Alpha"));
    }

    [Fact]
    public void Add_InsertsItem()
    {
        var set = new SwiftPackedSet<int>();

        bool added = set.Add(42);

        Assert.True(added);
        Assert.True(set.Contains(42));
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void Add_Duplicate_ReturnsFalse()
    {
        var set = new SwiftPackedSet<int>();

        Assert.True(set.Add(10));
        Assert.False(set.Add(10));

        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void Contains_ReturnsCorrectResult()
    {
        var set = new SwiftPackedSet<int>();

        set.Add(5);

        Assert.True(set.Contains(5));
        Assert.False(set.Contains(10));
    }

    [Fact]
    public void Remove_RemovesItem()
    {
        var set = new SwiftPackedSet<int>();

        set.Add(1);
        set.Add(2);

        bool removed = set.Remove(1);

        Assert.True(removed);
        Assert.False(set.Contains(1));
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void Remove_Nonexistent_ReturnsFalse()
    {
        var set = new SwiftPackedSet<int>();

        bool removed = set.Remove(99);

        Assert.False(removed);
    }

    [Fact]
    public void Remove_SwapBackMaintainsIntegrity()
    {
        var set = new SwiftPackedSet<int>();

        set.Add(1);
        set.Add(2);
        set.Add(3);

        set.Remove(2);

        Assert.Equal(2, set.Count);
        Assert.True(set.Contains(1));
        Assert.True(set.Contains(3));
        Assert.False(set.Contains(2));
    }

    [Fact]
    public void Clear_RemovesAllItems()
    {
        var set = new SwiftPackedSet<int>();

        set.Add(1);
        set.Add(2);

        set.Clear();

        Assert.Empty(set);
        Assert.False(set.Contains(1));
    }

    [Fact]
    public void DenseArray_ContainsAllValues()
    {
        var set = new SwiftPackedSet<int>();

        set.Add(10);
        set.Add(20);
        set.Add(30);

        var dense = set.Dense;

        Assert.Contains(10, dense.Take(set.Count));
        Assert.Contains(20, dense.Take(set.Count));
        Assert.Contains(30, dense.Take(set.Count));
    }

    [Fact]
    public void Enumerator_ReturnsAllValues()
    {
        var set = new SwiftPackedSet<int>();

        set.Add(1);
        set.Add(2);

        var values = set.ToList();

        Assert.Contains(1, values);
        Assert.Contains(2, values);
    }

    [Fact]
    public void Enumerator_ModificationThrows()
    {
        var set = new SwiftPackedSet<int>();

        set.Add(1);
        set.Add(2);

        var enumerator = set.GetEnumerator();

        set.Add(3);

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void LargeCollection_AddRemove()
    {
        var set = new SwiftPackedSet<int>();

        const int size = 10000;

        for (int i = 0; i < size; i++)
            set.Add(i);

        Assert.Equal(size, set.Count);

        for (int i = 0; i < size; i += 2)
            set.Remove(i);

        Assert.Equal(size / 2, set.Count);
    }

    [Fact]
    public void CloneTo_CopiesValues()
    {
        var set = new SwiftPackedSet<int>();

        set.Add(1);
        set.Add(2);

        var list = new List<int>();

        set.CloneTo(list);

        Assert.Equal(2, list.Count);
        Assert.Contains(1, list);
        Assert.Contains(2, list);
    }

    #region Serialization

    [Fact]
    public void Json_RoundTrip_PreservesValues()
    {
        var set = new SwiftPackedSet<int>();

        set.Add(10);
        set.Add(20);

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(set);

        var result = JsonSerializer.Deserialize<SwiftPackedSet<int>>(json);

        Assert.Equal(2, result.Count);
        Assert.True(result.Contains(10));
        Assert.True(result.Contains(20));
    }

    [Fact]
    public void Json_RoundTrip_PreservesDenseIntegrity()
    {
        var set = new SwiftPackedSet<int>();

        set.Add(5);
        set.Add(10);
        set.Add(15);

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(set);

        var result = JsonSerializer.Deserialize<SwiftPackedSet<int>>(json);

        var dense = result.Dense;

        for (int i = 0; i < result.Count; i++)
            Assert.True(result.Contains(dense[i]));
    }

    [Fact]
    public void Json_EmptySet_RoundTrip()
    {
        var set = new SwiftPackedSet<int>();

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(set);

        var result = JsonSerializer.Deserialize<SwiftPackedSet<int>>(json);

        Assert.Empty(result);
    }

    [Fact]
    public void MemoryPack_RoundTrip_PreservesValues()
    {
        var set = new SwiftPackedSet<int>();

        set.Add(1);
        set.Add(2);

        byte[] bytes = MemoryPackSerializer.Serialize(set);

        var result = MemoryPackSerializer.Deserialize<SwiftPackedSet<int>>(bytes);

        Assert.Equal(2, result.Count);
        Assert.True(result.Contains(1));
        Assert.True(result.Contains(2));
    }

    [Fact]
    public void MemoryPack_RoundTrip_PreservesLargeCollections()
    {
        var set = new SwiftPackedSet<int>();

        for (int i = 0; i < 1000; i++)
            set.Add(i);

        byte[] bytes = MemoryPackSerializer.Serialize(set);

        var result = MemoryPackSerializer.Deserialize<SwiftPackedSet<int>>(bytes);

        Assert.Equal(1000, result.Count);

        for (int i = 0; i < 1000; i++)
            Assert.True(result.Contains(i));
    }

    [Fact]
    public void MemoryPack_EmptySet_RoundTrip()
    {
        var set = new SwiftPackedSet<int>();

        byte[] bytes = MemoryPackSerializer.Serialize(set);

        var result = MemoryPackSerializer.Deserialize<SwiftPackedSet<int>>(bytes);

        Assert.Empty(result);
    }

    #endregion
}
