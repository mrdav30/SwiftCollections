using MemoryPack;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace SwiftCollections.Tests;

public class SwiftQueueTests
{
    [Fact]
    public void Constructor_WithNonCollectionEnumerable_CopiesItemsInQueueOrder()
    {
        var queue = new SwiftQueue<int>(GetItems());

        Assert.Equal(new[] { 1, 2, 3 }, queue.ToArray());

        static IEnumerable<int> GetItems()
        {
            yield return 1;
            yield return 2;
            yield return 3;
        }
    }

    [Fact]
    public void Constructor_WithCollectionEnumerable_CopiesItemsInQueueOrder()
    {
        var queue = new SwiftQueue<int>(new List<int> { 1, 2, 3 });

        Assert.Equal(new[] { 1, 2, 3 }, queue.ToArray());
    }

    [Fact]
    public void Constructor_WithEmptyState_InitializesEmptyQueue()
    {
        var queue = new SwiftQueue<int>(new SwiftArrayState<int>(Array.Empty<int>()));

        Assert.Empty(queue);
        Assert.Equal(0, queue.Capacity);
    }

    [Fact]
    public void Enqueue_AddsElementsToQueue()
    {
        var queue = new SwiftQueue<int>();
        queue.Enqueue(1);
        queue.Enqueue(2);

        Assert.Equal(2, queue.Count);
        Assert.Equal(1, queue.Peek());
        Assert.Equal(2, queue.PeekTail());
    }

    [Fact]
    public void EnqueueRange_ReadOnlySpan_AppendsItemsInQueueOrder()
    {
        var queue = new SwiftQueue<int>();
        queue.Enqueue(1);

        queue.EnqueueRange(new[] { 2, 3, 4 }.AsSpan());

        Assert.Equal(new[] { 1, 2, 3, 4 }, queue.ToArray());
    }

    [Fact]
    public void Dequeue_RemovesAndReturnsElements()
    {
        var queue = new SwiftQueue<int>();
        queue.Enqueue(1);
        queue.Enqueue(2);

        int item1 = queue.Dequeue();
        int item2 = queue.Dequeue();

        Assert.Equal(1, item1);
        Assert.Equal(2, item2);
        Assert.Empty(queue);
    }

    [Fact]
    public void Dequeue_ShouldClearReleasedReferenceSlot()
    {
        var queue = new SwiftQueue<string>();
        queue.Enqueue("first");
        queue.Enqueue("second");

        queue.Dequeue();

        Assert.Null(queue.InnerArray[0]);
    }

    [Fact]
    public void Dequeue_ThrowsExceptionWhenEmpty()
    {
        var queue = new SwiftQueue<int>();

        Assert.Throws<InvalidOperationException>(() => queue.Dequeue());
    }

    [Fact]
    public void Peek_ReturnsFrontElementWithoutRemoving()
    {
        var queue = new SwiftQueue<int>();
        queue.Enqueue(1);
        queue.Enqueue(2);

        Assert.Equal(1, queue.Peek());
        Assert.Equal(2, queue.Count); // Ensure the count is unchanged
    }

    [Fact]
    public void Peek_ThrowsExceptionWhenEmpty()
    {
        var queue = new SwiftQueue<int>();

        Assert.Throws<InvalidOperationException>(() => queue.Peek());
    }

    [Fact]
    public void PeekTail_ReturnsLastElementWithoutRemoving()
    {
        var queue = new SwiftQueue<int>();
        queue.Enqueue(1);
        queue.Enqueue(2);

        Assert.Equal(2, queue.PeekTail());
        Assert.Equal(2, queue.Count); // Ensure the count is unchanged
    }

    [Fact]
    public void PeekTail_ThrowsExceptionWhenEmpty()
    {
        var queue = new SwiftQueue<int>();

        Assert.Throws<InvalidOperationException>(() => queue.PeekTail());
    }

    [Fact]
    public void Clear_RemovesAllElements()
    {
        var queue = new SwiftQueue<int>();
        queue.Enqueue(1);
        queue.Enqueue(2);
        queue.Clear();

        Assert.Empty(queue);
        Assert.Throws<InvalidOperationException>(() => queue.Peek());
    }

    [Fact]
    public void Clear_ShouldReleaseStoredReferences()
    {
        var queue = new SwiftQueue<string>();
        queue.Enqueue("first");
        queue.Enqueue("second");

        queue.Clear();

        Assert.Null(queue.InnerArray[0]);
        Assert.Null(queue.InnerArray[1]);
    }

    [Fact]
    public void FastClear_ResetsQueueWithoutResizing()
    {
        var queue = new SwiftQueue<int>(10);
        queue.Enqueue(1);
        queue.Enqueue(2);
        queue.FastClear();

        Assert.Empty(queue);
        Assert.Equal(16, queue.Capacity); // Capacity will be 16 since it's the next power of 2 after 10
    }

    [Fact]
    public void EnsureCapacity_IncreasesCapacity()
    {
        var queue = new SwiftQueue<int>(2);
        queue.Enqueue(1);
        queue.Enqueue(2);

        queue.EnsureCapacity(5);
        Assert.True(queue.Capacity >= 5);
    }

    [Fact]
    public void EnqueueRange_NonCollectionEnumerable_AppendsItems()
    {
        var queue = new SwiftQueue<int>();
        queue.Enqueue(1);

        queue.EnqueueRange(GetItems());

        Assert.Equal(new[] { 1, 2, 3, 4 }, queue.ToArray());

        static IEnumerable<int> GetItems()
        {
            yield return 2;
            yield return 3;
            yield return 4;
        }
    }

    [Fact]
    public void ToArray_CreatesArrayWithAllElements()
    {
        var queue = new SwiftQueue<int>();
        for (int i = 0; i < 5; i++) queue.Enqueue(i);

        var array = queue.ToArray();

        Assert.Equal(5, array.Length);
        Assert.Equal(0, array[0]);
        Assert.Equal(4, array[4]);
    }

    [Fact]
    public void GetSegments_ReturnsSingleSegmentForContiguousQueue()
    {
        var queue = new SwiftQueue<int>();
        queue.EnqueueRange(new[] { 1, 2, 3 }.AsSpan());

        queue.GetSegments(out ReadOnlySpan<int> first, out ReadOnlySpan<int> second);

        Assert.Equal(new[] { 1, 2, 3 }, first.ToArray());
        Assert.True(second.IsEmpty);
    }

    [Fact]
    public void GetSegments_ReturnsWrappedSegmentsForWrappedQueue()
    {
        var queue = new SwiftQueue<int>(8);
        queue.EnqueueRange(new[] { 0, 1, 2, 3, 4, 5 }.AsSpan());

        for (int i = 0; i < 4; i++)
            queue.Dequeue();

        queue.EnqueueRange(new[] { 6, 7, 8 }.AsSpan());

        queue.GetSegments(out ReadOnlySpan<int> first, out ReadOnlySpan<int> second);

        Assert.Equal(new[] { 4, 5, 6, 7 }, first.ToArray());
        Assert.Equal(new[] { 8 }, second.ToArray());
    }

    [Fact]
    public void CopyTo_Span_CopiesItemsInQueueOrder()
    {
        var queue = new SwiftQueue<int>(8);
        queue.EnqueueRange(new[] { 0, 1, 2, 3, 4, 5 }.AsSpan());

        for (int i = 0; i < 4; i++)
            queue.Dequeue();

        queue.EnqueueRange(new[] { 6, 7, 8 }.AsSpan());

        var destination = new int[queue.Count];
        queue.CopyTo(destination);

        Assert.Equal(new[] { 4, 5, 6, 7, 8 }, destination);
    }

    [Fact]
    public void CopyTo_GenericAndNonGenericArrays_CopyWrappedQueueInOrder()
    {
        var queue = new SwiftQueue<int>(8);
        queue.EnqueueRange(new[] { 0, 1, 2, 3, 4, 5 }.AsSpan());

        for (int i = 0; i < 4; i++)
            queue.Dequeue();

        queue.EnqueueRange(new[] { 6, 7, 8 }.AsSpan());

        var genericDestination = new int[7];
        Array objectDestination = new object[7];

        queue.CopyTo(genericDestination, 1);
        queue.CopyTo(objectDestination, 1);

        Assert.Equal(new[] { 0, 4, 5, 6, 7, 8, 0 }, genericDestination);
        Assert.Equal(new object[] { null, 4, 5, 6, 7, 8, null }, (object[])objectDestination);
    }

    [Fact]
    public void TryPeekAndTryDequeue_HandleEmptyAndPopulatedQueues()
    {
        var queue = new SwiftQueue<int>();

        Assert.False(queue.TryPeek(out int emptyPeek));
        Assert.Equal(default, emptyPeek);
        Assert.False(queue.TryDequeue(out int emptyDequeue));
        Assert.Equal(default, emptyDequeue);

        queue.Enqueue(1);
        queue.Enqueue(2);

        Assert.True(queue.TryPeek(out int peeked));
        Assert.Equal(1, peeked);
        Assert.True(queue.TryDequeue(out int dequeued));
        Assert.Equal(1, dequeued);
        Assert.Equal(new[] { 2 }, queue.ToArray());
    }

    [Fact]
    public void TryDequeue_ShouldClearReleasedReferenceSlot()
    {
        var queue = new SwiftQueue<string>();
        queue.Enqueue("first");
        queue.Enqueue("second");

        Assert.True(queue.TryDequeue(out string value));

        Assert.Equal("first", value);
        Assert.Null(queue.InnerArray[0]);
    }

    [Fact]
    public void Contains_SearchesAcrossWrappedQueue()
    {
        var queue = new SwiftQueue<int>(8);
        queue.EnqueueRange(new[] { 0, 1, 2, 3, 4, 5 }.AsSpan());

        for (int i = 0; i < 4; i++)
            queue.Dequeue();

        queue.EnqueueRange(new[] { 6, 7, 8 }.AsSpan());

        bool containsSeven = queue.Contains(7);
        bool containsMissing = queue.Contains(42);

        Assert.True(containsSeven);
        Assert.False(containsMissing);
    }

    [Fact]
    public void EnqueueRange_ArrayAndIndexerSet_UpdateQueueInPlace()
    {
        var queue = new SwiftQueue<int>();

        queue.EnqueueRange(new[] { 1, 2, 3 });
        queue[1] = 42;

        Assert.Equal(new[] { 1, 42, 3 }, queue.ToArray());
    }

    [Fact]
    public void GetSegments_EmptyQueue_ReturnsEmptySpans()
    {
        var queue = new SwiftQueue<int>();

        queue.GetSegments(out ReadOnlySpan<int> first, out ReadOnlySpan<int> second);

        Assert.True(first.IsEmpty);
        Assert.True(second.IsEmpty);
    }

    [Fact]
    public void ICollectionAdapterMembers_ExposeExpectedState()
    {
        ICollection<int> generic = new SwiftQueue<int>();
        ICollection nongeneric = (ICollection)generic;

        generic.Add(1);

        Assert.False(generic.IsReadOnly);
        Assert.False(((SwiftQueue<int>)generic).IsSynchronized);
        Assert.NotNull(nongeneric.SyncRoot);
        Assert.Throws<NotSupportedException>(() => generic.Remove(1));
    }

    [Fact]
    public void Enumerator_IEnumeratorCurrentAndReset_WorkForWrappedQueue()
    {
        var queue = new SwiftQueue<int>(8);
        queue.EnqueueRange(new[] { 0, 1, 2, 3, 4, 5 }.AsSpan());

        for (int i = 0; i < 4; i++)
            queue.Dequeue();

        queue.EnqueueRange(new[] { 6, 7, 8 }.AsSpan());

        IEnumerator enumerator = ((IEnumerable)queue).GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.Equal(4, enumerator.Current);

        enumerator.Reset();

        Assert.True(enumerator.MoveNext());
        Assert.Equal(4, enumerator.Current);
    }

    [Fact]
    public void CloneTo_CopiesElementsToAnotherCollection()
    {
        var queue = new SwiftQueue<int>();
        for (int i = 0; i < 5; i++) queue.Enqueue(i);

        var list = new System.Collections.Generic.List<int>();
        queue.CloneTo(list);

        Assert.Equal(queue.Count, list.Count);
        for (int i = 0; i < queue.Count; i++)
        {
            Assert.Equal(queue.Dequeue(), list[i]);
        }
    }

    [Fact]
    public void Enumeration_IteratesOverElements()
    {
        var queue = new SwiftQueue<int>();
        for (int i = 0; i < 5; i++) queue.Enqueue(i);

        int index = 0;
        foreach (var item in queue)
        {
            Assert.Equal(index, item);
            index++;
        }
    }

    [Fact]
    public void Enumerator_Reset_RestartsWrappedQueueFromHead()
    {
        var queue = new SwiftQueue<int>(8);
        for (int i = 0; i < 6; i++) queue.Enqueue(i);

        for (int i = 0; i < 4; i++) queue.Dequeue();

        queue.Enqueue(6);
        queue.Enqueue(7);
        queue.Enqueue(8);
        queue.Enqueue(9);

        var enumerator = queue.GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.Equal(4, enumerator.Current);

        enumerator.Reset();

        Assert.True(enumerator.MoveNext());
        Assert.Equal(4, enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(5, enumerator.Current);
    }

    [Fact]
    public void Exists_ReturnsTrueIfMatchIsFound()
    {
        var queue = new SwiftQueue<int>();
        for (int i = 0; i < 5; i++) queue.Enqueue(i);

        Assert.True(queue.Exists(i => i == 3));
    }

    [Fact]
    public void Find_ReturnsFirstMatchInQueueOrder_WhenWrapped()
    {
        var queue = new SwiftQueue<int>(8);
        for (int i = 0; i < 6; i++) queue.Enqueue(i);

        for (int i = 0; i < 4; i++) queue.Dequeue();

        queue.Enqueue(6);
        queue.Enqueue(7);
        queue.Enqueue(8);
        queue.Enqueue(9);

        Assert.Equal(8, queue.Find(i => i > 7));
    }

    [Fact]
    public void Find_ReturnsDefaultIfMatchIsNotFound()
    {
        var queue = new SwiftQueue<int>();
        for (int i = 0; i < 5; i++) queue.Enqueue(i);

        Assert.Equal(default, queue.Find(i => i > 10));
    }

    [Fact]
    public void Exists_ReturnsFalseForMissingPredicateOnWrappedQueue()
    {
        var queue = new SwiftQueue<int>(8);
        queue.EnqueueRange(new[] { 0, 1, 2, 3, 4, 5 }.AsSpan());

        for (int i = 0; i < 4; i++)
            queue.Dequeue();

        queue.EnqueueRange(new[] { 6, 7, 8 }.AsSpan());

        Assert.False(queue.Exists(i => i == 42));
        Assert.Throws<ArgumentNullException>(() => queue.Exists(null));
    }

    [Fact]
    public void EnqueueDequeue_WrapsAroundArrayCorrectly()
    {
        var queue = new SwiftQueue<int>(4);
        queue.Enqueue(1);
        queue.Enqueue(2);
        queue.Dequeue(); // Wrap head
        queue.Dequeue();
        queue.Enqueue(3);
        queue.Enqueue(4);
        queue.Enqueue(5);

        Assert.Equal(3, queue.Peek());      // Head after wrap-around
        Assert.Equal(5, queue.PeekTail());  // Tail after wrap-around
    }

    [Fact]
    public void Enqueue_WhenFullAndWrapped_ResizesAndPreservesQueueOrder()
    {
        var queue = new SwiftQueue<int>(8);
        queue.EnqueueRange(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }.AsSpan());

        for (int i = 0; i < 4; i++)
            queue.Dequeue();

        queue.EnqueueRange(new[] { 8, 9, 10, 11 }.AsSpan());
        queue.Enqueue(12);

        Assert.Equal(new[] { 4, 5, 6, 7, 8, 9, 10, 11, 12 }, queue.ToArray());
        Assert.True(queue.Capacity > 8);
    }

    [Fact]
    public void Clear_WrappedReferenceQueue_ClearsAllStoredSlots()
    {
        var queue = new SwiftQueue<string>(8);
        queue.EnqueueRange(new[] { "a", "b", "c", "d", "e", "f" });

        for (int i = 0; i < 4; i++)
            queue.Dequeue();

        queue.EnqueueRange(new[] { "g", "h", "i" });

        queue.Clear();

        Assert.All(queue.InnerArray, item => Assert.Null(item));
        Assert.Empty(queue);
    }

    [Fact]
    public void TrimExcessCapacity_CompactsWrappedQueue()
    {
        var queue = new SwiftQueue<int>(10);

        // Enqueue enough elements to fill most of the capacity and then wrap around
        for (int i = 0; i < 8; i++) queue.Enqueue(i);
        for (int i = 0; i < 6; i++) queue.Dequeue(); // Wraps around, leaves 2 elements

        // Add more items to increase the count again
        for (int i = 8; i < 10; i++) queue.Enqueue(i); // Count is now 4, Capacity is 10

        queue.TrimExcessCapacity();

        // Assert the queue has been resized to match the count
        Assert.True(queue.Capacity <= SwiftQueue<int>.DefaultCapacity && queue.Count == 4);

        int[] expected = { 6, 7, 8, 9 };
        Assert.Equal(expected, queue.ToArray());
    }

    [Fact]
    public void TrimExcessCapacity_ShrinksQueueCapacityWhenBelowThreshold()
    {
        var queue = new SwiftQueue<int>(10);

        // Enqueue and then dequeue to reduce Count to a point well below half of the capacity
        for (int i = 0; i < 8; i++) queue.Enqueue(i);
        for (int i = 0; i < 8; i++) queue.Dequeue(); // Leaves the queue empty

        queue.TrimExcessCapacity();

        // Expect the capacity to be at or close to the default, as it should have shrunk
        Assert.True(queue.Capacity <= queue.Count || queue.Capacity <= SwiftQueue<int>.DefaultCapacity);
    }

    [Fact]
    public void CopyTo_ArrayValidation_ThrowsForInvalidShapeOrType()
    {
        var queue = new SwiftQueue<int>();
        queue.EnqueueRange(new[] { 1, 2 });

        Array nonZeroLowerBound = Array.CreateInstance(typeof(int), new[] { 4 }, new[] { 1 });

        Assert.Throws<ArgumentException>(() => queue.CopyTo(new int[1, 2], 0));
        Assert.Throws<ArgumentException>(() => queue.CopyTo(nonZeroLowerBound, 0));
        Assert.Throws<ArgumentException>(() => queue.CopyTo(new string[2], 0));
    }

    [Fact]
    public void CopyTo_GenericArrayAndSpan_ThrowWhenDestinationIsTooSmall()
    {
        var queue = new SwiftQueue<int>();
        queue.EnqueueRange(new[] { 1, 2 });

        Assert.Throws<ArgumentException>(() => queue.CopyTo(new int[1], 0));
        Assert.Throws<ArgumentException>(() => queue.CopyTo(new int[1].AsSpan()));
    }

    [Fact]
    public void Enumerator_CurrentAfterEnumerationEnds_ThrowsInvalidOperationException()
    {
        var typedQueue = new SwiftQueue<int>();
        typedQueue.EnqueueRange(new[] { 1, 2, 3 });

        IEnumerable queue = typedQueue;
        IEnumerator enumerator = queue.GetEnumerator();

        while (enumerator.MoveNext())
        {
        }

        Assert.Throws<InvalidOperationException>(() => _ = enumerator.Current);
    }

    [Fact]
    public void SwiftQueue_Serialization_RoundTripMaintainsData()
    {
        var originalQueue = new SwiftQueue<int>();
        for (int i = 0; i < 100; i++)
            originalQueue.Enqueue(i);

        var jsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };
        var json = JsonSerializer.SerializeToUtf8Bytes(originalQueue, jsonOptions);
        var deserializedQueue = JsonSerializer.Deserialize<SwiftQueue<int>>(json, jsonOptions);

        // Verify that the deserialized list matches the original
        Assert.Equal(originalQueue.Count, deserializedQueue.Count);
        for (int i = 0; i < originalQueue.Count; i++)
        {
            Assert.Equal(originalQueue[i], deserializedQueue[i]);
        }
    }

    [Fact]
    public void SwiftQueue_MemoryPackSerialization_RoundTripMaintainsData()
    {
        var originalValue = new SwiftQueue<int>();
        for (int i = 0; i < 100; i++)
            originalValue.Enqueue(i);

        byte[] bytes = MemoryPackSerializer.Serialize(originalValue);
        SwiftQueue<int> deserializedValue = MemoryPackSerializer.Deserialize<SwiftQueue<int>>(bytes);

        // Check that deserialized values match the original
        Assert.Equal(originalValue.Count, deserializedValue.Count);
        Assert.Equal(originalValue, deserializedValue);
    }
}
