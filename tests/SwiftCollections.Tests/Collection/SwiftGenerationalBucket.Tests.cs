using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;

#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
using MemoryPack;
#endif

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

        var handles = new List<SwiftHandle>();

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

        Assert.True(bucket.IsValid(handle));
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
    public void Enumerator_SkipsRemovedSlots()
    {
        var bucket = new SwiftGenerationalBucket<int>();
        SwiftHandle first = bucket.Add(1);
        bucket.Add(2);

        bucket.Remove(first);

        var values = new List<int>();
        foreach (int value in bucket)
            values.Add(value);

        Assert.Equal(new[] { 2 }, values);
    }

    [Fact]
    public void NonGenericEnumerator_CurrentBeforeMoveNext_ThrowsForReferenceTypes()
    {
        IEnumerator enumerator = ((IEnumerable)new SwiftGenerationalBucket<string> { "value" }).GetEnumerator();

        Assert.Throws<InvalidOperationException>(() => _ = enumerator.Current);
    }

    [Fact]
    public void Enumerator_ThrowsIfModified()
    {
        var bucket = new SwiftGenerationalBucket<int>
        {
            1,
            2
        };

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

        var handles = new List<SwiftHandle>();

        for (int i = 0; i < 100; i++)
            handles.Add(bucket.Add(i));

        for (int i = 0; i < 100; i++)
        {
            Assert.True(bucket.TryGet(handles[i], out var value));
            Assert.Equal(i, value);
        }
    }

    [Fact]
    public void Constructor_WithSmallCapacity_UsesDefaultCapacity()
    {
        var bucket = new SwiftGenerationalBucket<int>(1);

        Assert.Equal(SwiftGenerationalBucket<int>.DefaultCapacity, bucket.Capacity);
    }

    [Fact]
    public void Constructor_WithLargeCapacity_UsesNextPowerOfTwo()
    {
        var bucket = new SwiftGenerationalBucket<int>(9);

        Assert.Equal(16, bucket.Capacity);
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
        var bucket = new SwiftGenerationalBucket<int>
        {
            1,
            2,
            3
        };

        Assert.True(bucket.Exists(i => i == 2));
    }

    [Fact]
    public void Exists_SkipsRemovedSlotsBeforeMatchingLaterValue()
    {
        var bucket = new SwiftGenerationalBucket<int>();
        SwiftHandle removed = bucket.Add(1);
        bucket.Add(2);

        Assert.True(bucket.Remove(removed));

        Assert.True(bucket.Exists(static value => value == 2));
    }

    [Fact]
    public void Find_ReturnsMatchingItem()
    {
        var bucket = new SwiftGenerationalBucket<int>
        {
            1,
            2,
            3
        };

        Assert.Equal(2, bucket.Find(i => i == 2));
    }

    [Fact]
    public void Find_ReturnsDefaultIfMatchIsNotFound()
    {
        var bucket = new SwiftGenerationalBucket<int>
        {
            1,
            2
        };

        Assert.Equal(default, bucket.Find(i => i > 10));
    }

    #endregion

    #region JSON Serialization

#if NET8_0_OR_GREATER

    [Fact]
    public void JsonSerialization_RoundTrip()
    {
        var bucket = new SwiftGenerationalBucket<string>
        {
            "A",
            "B",
            "C"
        };

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

#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
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
#endif

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
        Assert.True(bucket.TryGet(new SwiftHandle(0, 1), out var first));
        Assert.Equal(10, first);
        Assert.True(bucket.TryGet(new SwiftHandle(2, 3), out var third));
        Assert.Equal(30, third);
    }

    [Fact]
    public void StateConstructor_AllowsDefaultState()
    {
        var bucket = new SwiftGenerationalBucket<int>(default(SwiftGenerationalBucketState<int>));

        Assert.Equal(0, bucket.Count);
        Assert.Equal(SwiftGenerationalBucket<int>.DefaultCapacity, bucket.Capacity);
    }

    [Fact]
    public void SwiftGenerationalBucketState_Constructor_NormalizesNullArraysToEmptyArrays()
    {
        var state = new SwiftGenerationalBucketState<int>(null, null, null, null, 5);

        Assert.Empty(state.Items);
        Assert.Empty(state.Allocated);
        Assert.Empty(state.Generations);
        Assert.Empty(state.FreeIndices);
        Assert.Equal(5, state.Peak);
    }

    [Fact]
    public void StateConstructor_AllowsShortAllocationAndGenerationArrays()
    {
        var state = new SwiftGenerationalBucketState<int>(
            new[] { 10, 20, 30 },
            new[] { true },
            new uint[] { 7 },
            Array.Empty<int>(),
            3);

        var bucket = new SwiftGenerationalBucket<int>(state);

        Assert.Equal(1, bucket.Count);
        Assert.True(bucket.TryGet(new SwiftHandle(0, 7), out int first));
        Assert.Equal(10, first);
        Assert.False(bucket.TryGet(new SwiftHandle(1, 0), out _));
    }

    [Fact]
    public void StateConstructor_RestoresGenerationsFreeIndicesAndNormalizesPeak()
    {
        var state = new SwiftGenerationalBucketState<int>(
            new[] { 10 },
            new[] { true },
            new uint[] { 7 },
            new[] { 3 },
            -1);

        var bucket = new SwiftGenerationalBucket<int>(state);

        SwiftHandle reused = bucket.Add(40);

        Assert.Equal(3, reused.Index);
        Assert.True(bucket.TryGet(new SwiftHandle(0, 7), out int first));
        Assert.Equal(10, first);
        Assert.True(bucket.TryGet(reused, out int added));
        Assert.Equal(40, added);
    }

    [Fact]
    public void StateConstructor_RestoresMissingItemsAndDescendingFreeIndices()
    {
        var state = new SwiftGenerationalBucketState<int>(
            Array.Empty<int>(),
            new[] { true },
            new uint[] { 7 },
            new[] { 2, 1 },
            0);

        var bucket = new SwiftGenerationalBucket<int>(state);

        Assert.True(bucket.TryGet(new SwiftHandle(0, 7), out int restored));
        Assert.Equal(0, restored);
        Assert.Equal(1, bucket.Add(42).Index);
    }

    [Fact]
    public void StateConstructor_ClampsLargePeakAndRejectsOutOfRangeFreeIndex()
    {
        var largePeakState = new SwiftGenerationalBucketState<int>(
            new[] { 10 },
            new[] { true },
            new uint[] { 7 },
            Array.Empty<int>(),
            999);
        var invalidFreeState = new SwiftGenerationalBucketState<int>(
            new[] { 10 },
            new[] { true },
            new uint[] { 7 },
            new[] { 99 },
            1);

        var bucket = new SwiftGenerationalBucket<int>(largePeakState);

        Assert.Equal(bucket.Capacity, bucket.State.Peak);
        Assert.Throws<ArgumentException>(() => new SwiftGenerationalBucket<int>(invalidFreeState));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Remove_InvalidHandle_ReturnsFalse()
    {
        var bucket = new SwiftGenerationalBucket<int>();

        var handle = new SwiftHandle(999, 0);

        Assert.False(bucket.Remove(handle));
    }

    [Fact]
    public void TryGet_InvalidHandle_ReturnsFalse()
    {
        var bucket = new SwiftGenerationalBucket<int>();

        var handle = new SwiftHandle(999, 0);

        Assert.False(bucket.TryGet(handle, out _));
    }

    [Fact]
    public void EnsureCapacity_HandleEqualityAndEnumerationAdapters_Work()
    {
        var bucket = new SwiftGenerationalBucket<int>(2)
        {
            1,
            2
        };

        bucket.EnsureCapacity(64);
        bucket.EnsureCapacity(64);

        var left = new SwiftHandle(1, 2);
        var same = new SwiftHandle(1, 2);
        var differentIndex = new SwiftHandle(2, 2);
        var different = new SwiftHandle(1, 3);

        Assert.True(bucket.Capacity >= 64);
        Assert.True(left.Equals(same));
        Assert.False(left.Equals(differentIndex));
        Assert.True(left.Equals((object)same));
        Assert.False(left.Equals((object)"not a handle"));
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

    [Fact]
    public void FindAndCloneTo_SkipFreedSlots()
    {
        var bucket = new SwiftGenerationalBucket<string>();
        SwiftHandle removed = bucket.Add("remove");
        bucket.Add("keep");
        bucket.Remove(removed);

        Assert.Equal("keep", bucket.Find(item => item == "keep"));

        var clone = new List<string>();
        bucket.CloneTo(clone);
        Assert.Equal(new[] { "keep" }, clone);
    }

    [Fact]
    public void InvalidHandlePaths_ReturnExpectedResults()
    {
        var bucket = new SwiftGenerationalBucket<int> { 10 };
        var stale = new SwiftHandle(0, 1);
        var outOfRange = new SwiftHandle(bucket.Capacity, 0);
        var removed = new SwiftHandle(0, 0);

        Assert.False(bucket.Remove(stale));
        Assert.True(bucket.Remove(removed));
        Assert.False(bucket.Remove(removed));
        Assert.False(bucket.IsValid(stale));
        Assert.False(bucket.IsValid(outOfRange));
        Assert.False(bucket.IsValid(new SwiftHandle(0, stale.Generation + 1)));
        Assert.Throws<InvalidOperationException>(() => bucket.GetRef(stale));
        Assert.Throws<InvalidOperationException>(() => bucket.GetRef(removed));
        Assert.False(bucket.Exists(static value => value == 99));
    }

    #endregion
}
