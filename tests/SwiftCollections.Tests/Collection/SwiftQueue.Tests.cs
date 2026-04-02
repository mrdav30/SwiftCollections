using MemoryPack;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace SwiftCollections.Tests;

public class SwiftQueueTests
{
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
