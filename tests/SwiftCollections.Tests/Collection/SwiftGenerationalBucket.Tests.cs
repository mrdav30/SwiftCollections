using MemoryPack;
using System;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;

namespace SwiftCollections.Tests;

public class SwiftGenerationalBucketTests
{
    #region Basic Add / Get

    [Fact]
    public void Add_ReturnsValidHandle_AndValueCanBeRetrieved()
    {
        var bucket = new SwiftGenerationalBucket<string>();

        var handle = bucket.Add("hello");

        Assert.True(bucket.TryGet(handle, out var value));
        Assert.Equal("hello", value);
    }

    [Fact]
    public void Add_MultipleItems_AllAccessible()
    {
        var bucket = new SwiftGenerationalBucket<int>();

        var handles = new List<SwiftGenerationalBucket<int>.Handle>();

        for (int i = 0; i < 100; i++)
            handles.Add(bucket.Add(i));

        for (int i = 0; i < 100; i++)
        {
            Assert.True(bucket.TryGet(handles[i], out var value));
            Assert.Equal(i, value);
        }
    }

    #endregion

    #region Remove / Generation Safety

    [Fact]
    public void Remove_InvalidatesHandle()
    {
        var bucket = new SwiftGenerationalBucket<string>();

        var handle = bucket.Add("test");

        Assert.True(bucket.Remove(handle));

        Assert.False(bucket.TryGet(handle, out _));
        Assert.False(bucket.IsValid(handle));
    }

    [Fact]
    public void ReusedSlot_ChangesGeneration()
    {
        var bucket = new SwiftGenerationalBucket<int>();

        var h1 = bucket.Add(1);

        bucket.Remove(h1);

        var h2 = bucket.Add(2);

        Assert.NotEqual(h1.Generation, h2.Generation);
        Assert.False(bucket.TryGet(h1, out _));

        Assert.True(bucket.TryGet(h2, out var value));
        Assert.Equal(2, value);
    }

    #endregion

    #region GetRef

    [Fact]
    public void GetRef_AllowsDirectMutation()
    {
        var bucket = new SwiftGenerationalBucket<int>();

        var handle = bucket.Add(10);

        ref var value = ref bucket.GetRef(handle);

        value = 42;

        Assert.True(bucket.TryGet(handle, out var result));
        Assert.Equal(42, result);
    }

    #endregion

    #region Enumeration

    [Fact]
    public void Enumerator_IteratesAllValues()
    {
        var bucket = new SwiftGenerationalBucket<int>();

        for (int i = 0; i < 50; i++)
            bucket.Add(i);

        int count = 0;

        foreach (var value in bucket)
            count++;

        Assert.Equal(bucket.Count, count);
    }

    [Fact]
    public void Enumerator_ThrowsIfModified()
    {
        var bucket = new SwiftGenerationalBucket<int>();

        bucket.Add(1);
        bucket.Add(2);

        var enumerator = bucket.GetEnumerator();

        bucket.Add(3);

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    #endregion

    #region Capacity / Resize

    [Fact]
    public void Resize_PreservesItems()
    {
        var bucket = new SwiftGenerationalBucket<int>(2);

        var handles = new List<SwiftGenerationalBucket<int>.Handle>();

        for (int i = 0; i < 100; i++)
            handles.Add(bucket.Add(i));

        for (int i = 0; i < 100; i++)
        {
            Assert.True(bucket.TryGet(handles[i], out var value));
            Assert.Equal(i, value);
        }
    }

    #endregion

    #region Clone

    [Fact]
    public void CloneTo_CopiesValues()
    {
        var bucket = new SwiftGenerationalBucket<int>();

        for (int i = 0; i < 20; i++)
            bucket.Add(i);

        var clone = new List<int>();

        bucket.CloneTo(clone);

        Assert.Equal(bucket.Count, clone.Count);

        foreach (var value in clone)
            Assert.Contains(value, clone);
    }

    #endregion

    #region JSON Serialization

#if NET8_0_OR_GREATER

    [Fact]
    public void JsonSerialization_RoundTrip()
    {
        var bucket = new SwiftGenerationalBucket<string>();

        bucket.Add("A");
        bucket.Add("B");
        bucket.Add("C");

        string json = JsonSerializer.Serialize(bucket);

        var restored = JsonSerializer.Deserialize<SwiftGenerationalBucket<string>>(json);

        Assert.Equal(bucket.Count, restored.Count);

        int count = 0;

        foreach (var item in restored)
            count++;

        Assert.Equal(bucket.Count, count);
    }

#endif

    #endregion

    #region MemoryPack Serialization

    [Fact]
    public void MemoryPack_RoundTrip()
    {
        var bucket = new SwiftGenerationalBucket<int>();

        for (int i = 0; i < 50; i++)
            bucket.Add(i);

        byte[] data = MemoryPackSerializer.Serialize(bucket);

        var restored = MemoryPackSerializer.Deserialize<SwiftGenerationalBucket<int>>(data);

        Assert.Equal(bucket.Count, restored.Count);

        int count = 0;

        foreach (var value in restored)
            count++;

        Assert.Equal(bucket.Count, count);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Remove_InvalidHandle_ReturnsFalse()
    {
        var bucket = new SwiftGenerationalBucket<int>();

        var handle = new SwiftGenerationalBucket<int>.Handle(999, 0);

        Assert.False(bucket.Remove(handle));
    }

    [Fact]
    public void TryGet_InvalidHandle_ReturnsFalse()
    {
        var bucket = new SwiftGenerationalBucket<int>();

        var handle = new SwiftGenerationalBucket<int>.Handle(999, 0);

        Assert.False(bucket.TryGet(handle, out _));
    }

    #endregion
}