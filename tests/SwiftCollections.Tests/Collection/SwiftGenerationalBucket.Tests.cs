using MemoryPack;
using System;
using System.Collections;
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

    [Fact]
    public void Exists_ReturnsTrueIfMatchIsFound()
    {
        var bucket = new SwiftGenerationalBucket<int>();

        bucket.Add(1);
        bucket.Add(2);
        bucket.Add(3);

        Assert.True(bucket.Exists(i => i == 2));
    }

    [Fact]
    public void Find_ReturnsMatchingItem()
    {
        var bucket = new SwiftGenerationalBucket<int>();

        bucket.Add(1);
        bucket.Add(2);
        bucket.Add(3);

        Assert.Equal(2, bucket.Find(i => i == 2));
    }

    [Fact]
    public void Find_ReturnsDefaultIfMatchIsNotFound()
    {
        var bucket = new SwiftGenerationalBucket<int>();

        bucket.Add(1);
        bucket.Add(2);

        Assert.Equal(default, bucket.Find(i => i > 10));
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

    [Fact]
    public void StateConstructor_AllowsNullFreeIndices()
    {
        var state = new SwiftGenerationalBucketState<int>(
            new[] { 10, 20, 30 },
            new[] { true, false, true },
            new uint[] { 1, 2, 3 },
            null,
            3);

        var bucket = new SwiftGenerationalBucket<int>(state);

        Assert.Equal(2, bucket.Count);
        Assert.True(bucket.TryGet(new SwiftGenerationalBucket<int>.Handle(0, 1), out var first));
        Assert.Equal(10, first);
        Assert.True(bucket.TryGet(new SwiftGenerationalBucket<int>.Handle(2, 3), out var third));
        Assert.Equal(30, third);
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

    [Fact]
    public void EnsureCapacity_HandleEqualityAndEnumerationAdapters_Work()
    {
        var bucket = new SwiftGenerationalBucket<int>(2);
        bucket.Add(1);
        bucket.Add(2);

        bucket.EnsureCapacity(64);

        var left = new SwiftGenerationalBucket<int>.Handle(1, 2);
        var same = new SwiftGenerationalBucket<int>.Handle(1, 2);
        var different = new SwiftGenerationalBucket<int>.Handle(1, 3);

        Assert.True(bucket.Capacity >= 64);
        Assert.True(left.Equals(same));
        Assert.True(left.Equals((object)same));
        Assert.True(left == same);
        Assert.True(left != different);
        Assert.Equal("Handle(1:2)", left.ToString());
        Assert.NotEqual(left.GetHashCode(), different.GetHashCode());

        IEnumerator nongeneric = ((IEnumerable)bucket).GetEnumerator();
        IEnumerator<int> generic = ((IEnumerable<int>)bucket).GetEnumerator();

        Assert.True(generic.MoveNext());
        Assert.True(nongeneric.MoveNext());
        Assert.NotNull(nongeneric.Current);

        nongeneric.Reset();

        Assert.True(nongeneric.MoveNext());
    }

    #endregion
}
