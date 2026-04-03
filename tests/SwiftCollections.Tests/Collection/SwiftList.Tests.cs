using MemoryPack;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace SwiftCollections.Tests;

public class SwiftListTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithZeroCapacity()
    {
        var list = new SwiftList<int>();
        Assert.Equal(0, list.Capacity);
        Assert.Empty(list);
    }

    [Fact]
    public void Constructor_ShouldInitializeWithDefaultCapacity()
    {
        var list = new SwiftList<int>(1);
        Assert.Equal(SwiftList<int>.DefaultCapacity, list.Capacity);
        Assert.Empty(list);
    }

    [Fact]
    public void Constructor_ShouldInitializeWithItems()
    {
        var list = new SwiftList<int>(new List<int> { 1, 2, 3 });
        Assert.Equal(3, list.Count);
        Assert.Equal(1, list[0]);
        Assert.Equal(2, list[1]);
        Assert.Equal(3, list[2]);
    }

    [Fact]
    public void Constructor_WithEmptyCollection_ShouldUseEmptyBackingArray()
    {
        var list = new SwiftList<int>(Array.Empty<int>());

        Assert.Empty(list);
        Assert.Equal(0, list.Capacity);
    }

    [Fact]
    public void Constructor_WithEmptyState_ShouldInitializeEmptyList()
    {
        var list = new SwiftList<int>(new SwiftArrayState<int>(Array.Empty<int>()));

        Assert.Empty(list);
        Assert.Equal(0, list.Capacity);
    }

    [Fact]
    public void Add_ShouldIncreaseCount()
    {
#pragma warning disable IDE0028 // Simplify collection initialization
        var list = new SwiftList<int>();
        list.Add(1);
#pragma warning restore IDE0028
        Assert.Single(list);
        Assert.Equal(1, list[0]);
    }

    [Fact]
    public void RemoveAll_ShouldRemoveMatchingItems()
    {
        var list = new SwiftList<int> { 1, 2, 3, 4, 5 };
        list.RemoveAll(i => i % 2 == 0);  // Remove even numbers
        Assert.Equal(3, list.Count);
        Assert.DoesNotContain(2, list);
        Assert.DoesNotContain(4, list);
    }

    [Fact]
    public void AddRange_ShouldAppendElements()
    {
        var list = new SwiftList<int>();
        list.AddRange(new[] { 1, 2, 3 });
        Assert.Equal(3, list.Count);
        Assert.Equal(1, list[0]);
        Assert.Equal(3, list[2]);
    }

    [Fact]
    public void AddRange_NonCollectionEnumerable_ShouldAppendElements()
    {
        var list = new SwiftList<int> { 1 };

        list.AddRange(GetItems());

        Assert.Equal(new[] { 1, 2, 3, 4 }, list.ToArray());

        static IEnumerable<int> GetItems()
        {
            yield return 2;
            yield return 3;
            yield return 4;
        }
    }

    [Fact]
    public void AddRange_ReadOnlySpan_ShouldAppendElements()
    {
        var list = new SwiftList<int> { 1, 2 };

        list.AddRange(new[] { 3, 4, 5 }.AsSpan());

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, list.ToArray());
    }

    [Fact]
    public void AddRange_ICollection_WithAvailableCapacity_AppendsWithoutResizing()
    {
        var list = new SwiftList<int>(8) { 1, 2 };
        int capacityBefore = list.Capacity;

        list.AddRange(new List<int> { 3, 4 });

        Assert.Equal(new[] { 1, 2, 3, 4 }, list.ToArray());
        Assert.Equal(capacityBefore, list.Capacity);
    }

    [Fact]
    public void AddRange_ICollection_WithInsufficientCapacity_ResizesAndAppends()
    {
        var list = new SwiftList<int>(1) { 1, 2, 3, 4 };

        list.AddRange(new List<int> { 5, 6, 7 });

        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7 }, list.ToArray());
        Assert.True(list.Capacity >= list.Count);
    }

    [Fact]
    public void AddRange_EmptySpan_ShouldDoNothing()
    {
        var list = new SwiftList<int> { 1, 2, 3 };

        list.AddRange(ReadOnlySpan<int>.Empty);

        Assert.Equal(new[] { 1, 2, 3 }, list.ToArray());
    }

    [Fact]
    public void Remove_ShouldRemoveElement()
    {
        var list = new SwiftList<int> { 1, 2, 3 };
        bool removed = list.Remove(2);
        Assert.True(removed);
        Assert.Equal(2, list.Count);
        Assert.Equal(3, list[1]);
    }

    [Fact]
    public void RemoveAt_ShouldRemoveElementAtIndex()
    {
        var list = new SwiftList<int> { 1, 2, 3 };
        list.RemoveAt(1);
        Assert.Equal(2, list.Count);
        Assert.Equal(3, list[1]);
    }

    [Fact]
    public void RemoveAt_ShouldClearReleasedReferenceSlot()
    {
        var list = new SwiftList<string> { "a", "b", "c" };

        list.RemoveAt(1);

        Assert.Equal(2, list.Count);
        Assert.Null(list.InnerArray[list.Count]);
    }

    [Fact]
    public void Remove_ShouldReturnFalseWhenItemIsMissing()
    {
        var list = new SwiftList<int> { 1, 2, 3 };

        Assert.False(list.Remove(9));
        Assert.Equal(new[] { 1, 2, 3 }, list.ToArray());
    }

    [Fact]
    public void RemoveAll_ShouldHandleAllElementsMatching()
    {
        var list = new SwiftList<int> { 2, 4, 6 };
        list.RemoveAll(i => i % 2 == 0);  // All elements match
        Assert.Empty(list);
    }

    [Fact]
    public void RemoveAll_ShouldHandleNoElementsMatching()
    {
        var list = new SwiftList<int> { 1, 3, 5 };
        list.RemoveAll(i => i % 2 == 0);  // No elements match
        Assert.Equal(3, list.Count);
    }

    [Fact]
    public void RemoveAll_ShouldClearReleasedReferenceSlots()
    {
        var list = new SwiftList<string> { "keep", "drop-1", "keep-2", "drop-2" };

        int removed = list.RemoveAll(value => value.StartsWith("drop", StringComparison.Ordinal));

        Assert.Equal(2, removed);
        Assert.Equal(2, list.Count);
        Assert.Null(list.InnerArray[2]);
        Assert.Null(list.InnerArray[3]);
    }

    [Fact]
    public void Swap_ShouldSwapElements()
    {
        var list = new SwiftList<int> { 1, 2 };
        list.Swap(0, 1);
        Assert.Equal(2, list[0]);
        Assert.Equal(1, list[1]);
    }

    [Fact]
    public void Clear_ShouldResetList()
    {
        var list = new SwiftList<int> { 1, 2, 3 };
        list.Clear();
        Assert.Empty(list);
    }

    [Fact]
    public void Clear_ShouldReleaseStoredReferences()
    {
        var list = new SwiftList<string> { "a", "b", "c" };

        list.Clear();

        Assert.Empty(list);
        Assert.Null(list.InnerArray[0]);
        Assert.Null(list.InnerArray[1]);
        Assert.Null(list.InnerArray[2]);
    }

    [Fact]
    public void FastClear_ShouldResetCountWithoutReleasingElements()
    {
        var list = new SwiftList<int> { 1, 2, 3 };
        list.FastClear();
        Assert.Empty(list);
        Assert.Equal(SwiftList<int>.DefaultCapacity, list.Capacity);  // Capacity remains the same
    }

    [Fact]
    public void Insert_ShouldAddElementAtSpecifiedIndex()
    {
        var list = new SwiftList<int> { 1, 2, 4 };
        list.Insert(2, 3);  // Insert 3 at index 2
        Assert.Equal(4, list.Count);
        Assert.Equal(3, list[2]);
    }

    [Fact]
    public void Insert_ShouldInsertAtBeginning()
    {
        var list = new SwiftList<int> { 1, 2, 3 };
        list.Insert(0, 0);  // Insert at the start
        Assert.Equal(4, list.Count);
        Assert.Equal(0, list[0]);
    }

    [Fact]
    public void Insert_ShouldInsertAtEnd()
    {
        var list = new SwiftList<int> { 1, 2, 3 };
        list.Insert(list.Count, 4);  // Insert at the end
        Assert.Equal(4, list.Count);
        Assert.Equal(4, list[3]);
    }

    [Fact]
    public void Insert_WhenBackingArrayIsFull_ResizesAndShiftsItems()
    {
        var list = new SwiftList<int>(1) { 1, 2, 4, 5 };

        list.Insert(2, 3);

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, list.ToArray());
        Assert.True(list.Capacity > 4);
    }

    [Fact]
    public void Insert_WhenIndexExceedsCountButFitsCapacity_ShouldThrow()
    {
        var list = new SwiftList<int>(8) { 1, 2, 3 };

        Assert.Throws<ArgumentOutOfRangeException>(() => list.Insert(5, 99));
        Assert.Equal(new[] { 1, 2, 3 }, list.ToArray());
    }

    [Fact]
    public void IndexOf_ShouldReturnCorrectIndex()
    {
        var list = new SwiftList<int> { 1, 2, 3 };
        Assert.Equal(1, list.IndexOf(2));  // Item 2 is at index 1
    }

    [Fact]
    public void IndexOf_ShouldReturnNegativeOneIfNotFound()
    {
        var list = new SwiftList<int> { 1, 2, 3 };
        Assert.Equal(-1, list.IndexOf(4));  // Item 4 does not exist
    }

    [Fact]
    public void IndexOf_ShouldReturnFirstOccurrence()
    {
        var list = new SwiftList<int> { 1, 2, 3, 2 };
        Assert.Equal(1, list.IndexOf(2));  // Should return the index of the first 2
    }

    [Fact]
    public void ToArray_ShouldReturnArrayWithElements()
    {
        var list = new SwiftList<int> { 1, 2, 3 };
        int[] array = list.ToArray();
        Assert.Equal(new[] { 1, 2, 3 }, array);
    }

    [Fact]
    public void AsSpan_ShouldExposeLiveViewOverPopulatedItems()
    {
        var list = new SwiftList<int> { 1, 2, 3 };

        Span<int> span = list.AsSpan();
        span[1] = 42;

        Assert.Equal(3, span.Length);
        Assert.Equal(42, list[1]);
    }

    [Fact]
    public void AsReadOnlySpan_ShouldOnlyIncludeActiveItems()
    {
        var list = new SwiftList<int>(16) { 1, 2, 3 };

        ReadOnlySpan<int> span = list.AsReadOnlySpan();

        Assert.Equal(new[] { 1, 2, 3 }, span.ToArray());
    }

    [Fact]
    public void CopyTo_Span_ShouldCopyItems()
    {
        var list = new SwiftList<int> { 1, 2, 3 };
        var destination = new int[5];

        list.CopyTo(destination.AsSpan(1, list.Count));

        Assert.Equal(new[] { 0, 1, 2, 3, 0 }, destination);
    }

    [Fact]
    public void RemoveAt_ShouldThrowOnEmptyList()
    {
        var list = new SwiftList<int>();
        Assert.Throws<ArgumentOutOfRangeException>(() => list.RemoveAt(0));
    }

    [Fact]
    public void ToArray_ShouldReturnEmptyArrayForEmptyList()
    {
        var list = new SwiftList<int>();
        Assert.Empty(list.ToArray());
    }

    [Fact]
    public void Contains_ShouldReturnTrueIfItemExists()
    {
        var list = new SwiftList<int> { 1, 2, 3 };
        bool contains = list.Contains(2);
        Assert.True(contains);
    }

    [Fact]
    public void Contains_ShouldHandleNullForReferenceTypes()
    {
        var list = new SwiftList<string> { "a", null, "b" };
        bool contains = list.Contains(null);
        Assert.True(contains);
    }

    [Fact]
    public void Contains_ShouldReturnFalseIfItemDoesNotExist()
    {
        var list = new SwiftList<int> { 1, 2, 3 };
        bool contains = list.Contains(4);
        Assert.False(contains);
    }

    [Fact]
    public void Exists_ShouldReturnTrueIfMatchIsFound()
    {
        var list = new SwiftList<int> { 1, 2, 3 };

        Assert.True(list.Exists(i => i > 2));
    }

    [Fact]
    public void Exists_ShouldReturnFalseIfMatchIsNotFound()
    {
        var list = new SwiftList<int> { 1, 2, 3 };

        Assert.False(list.Exists(i => i > 10));
    }

    [Fact]
    public void Exists_ShouldThrowIfMatchIsNull()
    {
        var list = new SwiftList<int> { 1, 2, 3 };

        Assert.Throws<ArgumentNullException>(() => list.Exists(null));
    }

    [Fact]
    public void Find_ShouldReturnFirstMatchingItem()
    {
        var list = new SwiftList<int> { 1, 2, 3, 4 };

        Assert.Equal(3, list.Find(i => i > 2));
    }

    [Fact]
    public void Find_ShouldReturnDefaultIfMatchIsNotFound()
    {
        var list = new SwiftList<int> { 1, 2, 3 };

        Assert.Equal(default, list.Find(i => i > 10));
    }

    [Fact]
    public void Find_ShouldReturnNullForReferenceTypesWhenMatchIsNotFound()
    {
        var list = new SwiftList<string> { "a", "b" };

        Assert.Null(list.Find(s => s == "missing"));
    }

    [Fact]
    public void Find_ShouldThrowIfMatchIsNull()
    {
        var list = new SwiftList<int> { 1, 2, 3 };

        Assert.Throws<ArgumentNullException>(() => list.Find(null));
    }

    [Fact]
    public void Reverse_ShouldReverseListOrder()
    {
        var list = new SwiftList<int> { 1, 2, 3 };
        list.Reverse();
        Assert.Equal(3, list[0]);
        Assert.Equal(2, list[1]);
        Assert.Equal(1, list[2]);
    }

    [Fact]
    public void Sort_ShouldSortElementsInAscendingOrder()
    {
        var list = new SwiftList<int> { 3, 1, 2 };
        list.Sort(Comparer<int>.Default);
        Assert.Equal(new[] { 1, 2, 3 }, list.ToArray());
    }

    [Fact]
    public void EnsureCapacity_ShouldIncreaseCapacityIfNeeded()
    {
        var list = new SwiftList<int>(2);  // Start with capacity 2
        list.EnsureCapacity(10);  // Ensure capacity is at least 10
        Assert.True(list.Capacity >= 10);
    }

    [Fact]
    public void EnsureCapacity_ShouldNotDecreaseCapacity()
    {
        var list = new SwiftList<int>(10);
        list.EnsureCapacity(9);  // Capacity should remain the same
        Assert.Equal(16, list.Capacity);
    }

    [Fact]
    public void CopyTo_ShouldCopyElementsToAnotherSwiftList()
    {
        var list = new SwiftList<int> { 1, 2, 3 };
        var target = new SwiftList<int>(3);  // Pre-allocate space

        list.CopyTo(target);
        Assert.Equal(3, target.Count);
        Assert.Equal(new[] { 1, 2, 3 }, target.ToArray());
    }

    [Fact]
    public void CopyTo_ShouldResizeTargetSwiftListWhenNeeded()
    {
        var list = new SwiftList<int> { 1, 2, 3 };
        var target = new SwiftList<int>();

        list.CopyTo(target);

        Assert.Equal(new[] { 1, 2, 3 }, target.ToArray());
        Assert.True(target.Capacity >= target.Count);
    }

    [Fact]
    public void CopyTo_ShouldCopyElementsToArrayAtSpecifiedIndex()
    {
        var list = new SwiftList<int> { 1, 2, 3 };
        var array = new int[5];  // Pre-allocate array with extra space

        list.CopyTo(array, 2);  // Copy starting at index 2
        Assert.Equal(new[] { 0, 0, 1, 2, 3 }, array);
    }

    [Fact]
    public void CopyTo_ShouldThrowExceptionIfArrayIndexIsOutOfRange()
    {
        var list = new SwiftList<int> { 1, 2, 3 };
        var array = new int[3];

        Assert.Throws<ArgumentException>(() => list.CopyTo(array, 2));
    }

    [Fact]
    public void CopyTo_ShouldThrowIfTargetArrayTooSmall()
    {
        var list = new SwiftList<int> { 1, 2, 3 };
        var array = new int[2];  // Smaller than the list count

        Assert.Throws<ArgumentException>(() => list.CopyTo(array, 0));
    }

    [Fact]
    public void IList_AdapterOperations_WorkForValidValues()
    {
        IList list = new SwiftList<int> { 1, 3 };

        int addedIndex = list.Add(4);
        list.Insert(1, 2);
        list[0] = 0;

        Assert.Equal(1, list.IndexOf(2));
        Assert.True(list.Contains(4));

        list.Remove(3);

        Assert.Equal(2, addedIndex);
        Assert.Equal(new[] { 0, 2, 4 }, ((SwiftList<int>)list).ToArray());
    }

    [Fact]
    public void IList_AdapterOperations_ThrowForUnsupportedValueTypes()
    {
        IList list = new SwiftList<int> { 1, 2, 3 };

        Assert.Throws<NotSupportedException>(() => list.Add("bad"));
        Assert.Throws<NotSupportedException>(() => list.Insert(0, "bad"));
        Assert.Throws<NotSupportedException>(() => list[0] = "bad");
        Assert.Throws<NotSupportedException>(() => list.IndexOf("bad"));
        Assert.Throws<NotSupportedException>(() => list.Contains("bad"));
        Assert.Throws<NotSupportedException>(() => list.Remove("bad"));
    }

    [Fact]
    public void CopyTo_NonGenericArray_ShouldCopyElementsAtOffset()
    {
        var list = new SwiftList<int> { 1, 2, 3 };
        Array destination = new int[5];

        list.CopyTo(destination, 1);

        Assert.Equal(new[] { 0, 1, 2, 3, 0 }, (int[])destination);
    }

    [Fact]
    public void CopyTo_SpanAndNonGenericArray_ThrowForShortOrInvalidDestinations()
    {
        var list = new SwiftList<int> { 1, 2, 3 };
        Array nonZeroLowerBound = Array.CreateInstance(typeof(int), new[] { 4 }, new[] { 1 });

        Assert.Throws<ArgumentException>(() => list.CopyTo(new int[2].AsSpan()));
        Assert.Throws<ArgumentException>(() => list.CopyTo(new int[1, 3], 0));
        Assert.Throws<ArgumentException>(() => list.CopyTo(nonZeroLowerBound, 0));
    }

    [Fact]
    public void Enumerator_Reset_ShouldRestartAndIEnumeratorCurrentShouldExposeCurrentItem()
    {
        IEnumerable list = new SwiftList<int> { 1, 2, 3 };
        IEnumerator enumerator = list.GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.Equal(1, enumerator.Current);

        enumerator.Reset();

        Assert.True(enumerator.MoveNext());
        Assert.Equal(1, enumerator.Current);
    }

    [Fact]
    public void Enumerator_CurrentAfterEnumerationEnds_ShouldThrow()
    {
        IEnumerable list = new SwiftList<int> { 1, 2, 3 };
        IEnumerator enumerator = list.GetEnumerator();

        while (enumerator.MoveNext())
        {
        }

        Assert.Throws<InvalidOperationException>(() => _ = enumerator.Current);
    }

    [Fact]
    public void Sort_ShouldWorkWithCustomComparer()
    {
        var list = new SwiftList<int> { 1, 3, 2 };
        list.Sort(Comparer<int>.Create((x, y) => y - x));  // Sort in descending order
        Assert.Equal(new[] { 3, 2, 1 }, list.ToArray());
    }

    [Fact]
    public void Sort_DefaultComparer_ShouldSortAscending()
    {
        var list = new SwiftList<int> { 3, 1, 2 };

        list.Sort();

        Assert.Equal(new[] { 1, 2, 3 }, list.ToArray());
    }

    [Fact]
    public void TrimExcessCapacity_ShouldShrinkBackingArrayAndPreserveItems()
    {
        var list = new SwiftList<int>(64) { 1, 2, 3 };

        list.TrimExcessCapacity();

        Assert.True(list.Capacity < 64);
        Assert.Equal(new[] { 1, 2, 3 }, list.ToArray());
    }

    [Fact]
    public void TrimExcessCapacity_WhenCountExceedsDefault_ShrinksToNextPowerOfTwo()
    {
        var list = new SwiftList<int>(64);
        list.AddRange(new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9 });

        list.TrimExcessCapacity();

        Assert.Equal(16, list.Capacity);
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }, list.ToArray());
    }

    [Fact]
    public void CloneTo_ShouldReplaceTargetContents()
    {
        var list = new SwiftList<int> { 1, 2, 3 };
        var target = new List<int> { 99 };

        list.CloneTo(target);

        Assert.Equal(new[] { 1, 2, 3 }, target);
    }

    [Fact]
    public void IListAndICollectionProperties_ExposeCurrentState()
    {
        var list = new SwiftList<int> { 1, 2, 3 };
        IList nongeneric = list;
        ICollection collection = list;

        Assert.False(list.IsReadOnly);
        Assert.False(list.IsSynchronized);
        Assert.False(nongeneric.IsFixedSize);
        Assert.Equal(2, nongeneric[1]);
        Assert.NotNull(collection.SyncRoot);
        Assert.Contains("1, 2, 3", list.ToString());
    }

    [Fact]
    public void ToString_OnEmptyList_FallsBackToBaseRepresentation()
    {
        string value = new SwiftList<int>().ToString();

        Assert.Contains(nameof(SwiftList<int>), value);
    }

    [Fact]
    public void Enumerator_Reset_AfterMutation_ShouldThrow()
    {
        var list = new SwiftList<int> { 1, 2, 3 };
        var enumerator = list.GetEnumerator();

        list.Add(4);

        Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
    }

    [Fact]
    public void LargeList_ShouldHandleMultipleAdditionsAndRemovals()
    {
        var list = new SwiftList<int>();
        for (int i = 0; i < 100000; i++) list.Add(i);
        Assert.Equal(100000, list.Count);

        list.RemoveAll(i => i % 2 == 0);  // Remove even numbers
        Assert.Equal(50000, list.Count);
    }

    [Fact]
    public void SwiftList_Serialization_RoundTripMaintainsData()
    {
        // Create and populate the original SwiftList
        var originalList = new SwiftList<int>();
        for (int i = 0; i < 100; i++)
            originalList.Add(i);

        var jsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };
        var json = JsonSerializer.SerializeToUtf8Bytes(originalList, jsonOptions);
        var deserializedList = JsonSerializer.Deserialize<SwiftList<int>>(json, jsonOptions);

        // Verify that the deserialized list matches the original
        Assert.Equal(originalList.Count, deserializedList.Count);
        for (int i = 0; i < originalList.Count; i++)
        {
            Assert.Equal(originalList[i], deserializedList[i]);
        }
    }

    [Fact]
    public void SwiftList_MemoryPackSerialization_RoundTripMaintainsData()
    {
        var originalValue = new SwiftList<int>();
        for (int i = 0; i < 100; i++)
            originalValue.Add(i);

        byte[] bytes = MemoryPackSerializer.Serialize(originalValue);
        SwiftList<int> deserializedValue = MemoryPackSerializer.Deserialize<SwiftList<int>>(bytes);

        // Check that deserialized values match the original
        Assert.Equal(originalValue.Count, deserializedValue.Count);
        Assert.Equal(originalValue, deserializedValue);
    }
}
