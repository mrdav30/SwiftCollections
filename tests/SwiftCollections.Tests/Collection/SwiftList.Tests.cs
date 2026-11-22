using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
using MemoryPack;
#endif

namespace SwiftCollections.Tests;

public class SwiftListTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ObjectAdapter_RemoveEqualityInvalidCastPreservesIdentityAndContents(bool typed)
    {
        var failure = new InvalidCastException("Equality failed.");
        var stored = new ThrowingEqualityItem(failure);
        var list = new SwiftList<ThrowingEqualityItem> { stored };
        var enumerator = list.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.Same(failure, Assert.Throws<InvalidCastException>(() =>
        {
            if (typed)
                list.Remove(new ThrowingEqualityItem(failure));
            else
                ((IList)list).Remove(new ThrowingEqualityItem(failure));
        }));

        Assert.Same(stored, Assert.Single(list));
        Assert.False(enumerator.MoveNext());
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void ObjectAdapter_LookupEqualityFailuresRetainIdentityAndContents(
        bool contains, bool typed, bool invalidCast)
    {
        Exception failure = invalidCast
            ? new InvalidCastException("Equality failed.")
            : new InvalidOperationException("Equality failed.");
        var stored = new ThrowingEqualityItem(failure);
        var sought = new ThrowingEqualityItem(failure);
        var list = new SwiftList<ThrowingEqualityItem> { stored };
        var enumerator = list.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Exception actual = Record.Exception(() =>
        {
            if (typed)
            {
                if (contains) list.Contains(sought); else list.IndexOf(sought);
            }
            else
            {
                if (contains) ((IList)list).Contains(sought); else ((IList)list).IndexOf(sought);
            }
        });

        Assert.Same(failure, actual);
        Assert.Same(stored, Assert.Single(list));
        Assert.False(enumerator.MoveNext());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ObjectAdapter_LookupRetainsWrongTypeAndNullUnboxingErrors(bool contains)
    {
        var list = new SwiftList<int> { 10 };
        IList objects = list;
        void Query(object value)
        {
            if (contains) objects.Contains(value); else objects.IndexOf(value);
        }

        NotSupportedException wrongType = Assert.Throws<NotSupportedException>(() => Query("bad"));
        Assert.Equal("Unsupported value type for bad", wrongType.Message);
        NotSupportedException nullValue = Assert.Throws<NotSupportedException>(() => Query(null));
        Assert.Equal("Unsupported value type for ", nullValue.Message);
        Assert.Equal(new[] { 10 }, list);
    }

    [Theory]
    [InlineData("add")]
    [InlineData("insert")]
    [InlineData("remove")]
    public void ObjectAdapter_InvalidTypeAndNullUnboxingKeepContents(string operation)
    {
        var list = new SwiftList<int> { 10, 20 };
        IList objects = list;
        void Mutate(object value)
        {
            switch (operation)
            {
                case "add": objects.Add(value); break;
                case "insert": objects.Insert(1, value); break;
                case "remove": objects.Remove(value); break;
                default: throw new ArgumentOutOfRangeException(nameof(operation));
            }
        }

        NotSupportedException wrongType = Assert.Throws<NotSupportedException>(() => Mutate("bad"));
        Assert.Equal("Wrong value type for bad", wrongType.Message);
        Assert.Null(wrongType.InnerException);
        Assert.Throws<NullReferenceException>(() => Mutate(null));
        Assert.Equal(new[] { 10, 20 }, list);
    }

    [Theory]
    [InlineData("add")]
    [InlineData("insert")]
    [InlineData("remove")]
    [InlineData("contains")]
    [InlineData("indexof")]
    public void ObjectAdapter_NullRemainsValidForReferenceAndNullableElements(string operation)
    {
        var references = new SwiftList<string> { "keep" };
        var nullable = new SwiftList<int?> { 7 };
        IList referenceObjects = references;
        IList nullableObjects = nullable;
        switch (operation)
        {
            case "add":
                Assert.Equal(1, referenceObjects.Add(null));
                Assert.Equal(1, nullableObjects.Add(null));
                Assert.Equal(new[] { "keep", null }, references);
                Assert.Equal(new int?[] { 7, null }, nullable);
                break;
            case "insert":
                referenceObjects.Insert(0, null);
                nullableObjects.Insert(0, null);
                Assert.Equal(new[] { null, "keep" }, references);
                Assert.Equal(new int?[] { null, 7 }, nullable);
                break;
            case "remove":
                references.Add(null);
                nullable.Add(null);
                referenceObjects.Remove(null);
                nullableObjects.Remove(null);
                Assert.Equal(new[] { "keep" }, references);
                Assert.Equal(new int?[] { 7 }, nullable);
                break;
            case "contains":
                Assert.False(referenceObjects.Contains(null));
                Assert.False(nullableObjects.Contains(null));
                references.Add(null);
                nullable.Add(null);
                Assert.True(referenceObjects.Contains(null));
                Assert.True(nullableObjects.Contains(null));
                Assert.Equal(new[] { "keep", null }, references);
                Assert.Equal(new int?[] { 7, null }, nullable);
                break;
            case "indexof":
                Assert.Equal(-1, referenceObjects.IndexOf(null));
                Assert.Equal(-1, nullableObjects.IndexOf(null));
                references.Add(null);
                nullable.Add(null);
                Assert.Equal(1, referenceObjects.IndexOf(null));
                Assert.Equal(1, nullableObjects.IndexOf(null));
                Assert.Equal(new[] { "keep", null }, references);
                Assert.Equal(new int?[] { 7, null }, nullable);
                break;
            default: throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }

    [Fact]
    public void ObjectAdapter_InsertConvertsValueBeforeCheckingIndex()
    {
        var list = new SwiftList<int> { 10 };
        IList objects = list;

        Assert.Throws<NotSupportedException>(() => objects.Insert(-1, "bad"));
        Assert.Throws<NullReferenceException>(() => objects.Insert(-1, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => objects.Insert(-1, 20));
        Assert.Equal(new[] { 10 }, list);
    }

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
        var list = new SwiftList<int>(SwiftList<int>.DefaultCapacity);

        for (int i = 1; i <= SwiftList<int>.DefaultCapacity; i++)
            list.Add(i);

        list.AddRange(new List<int> { 9, 10 });

        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, list.ToArray());
        Assert.True(list.Capacity > SwiftList<int>.DefaultCapacity);
    }

    [Fact]
    public void AddRange_IReadOnlyCollection_EnsuresCapacityBeforeEnumeration()
    {
        var list = new SwiftList<int>();
        var source = new CapacityObservingReadOnlyCollection<int>(
            new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 },
            () => list.Capacity);

        list.AddRange(source);

        Assert.True(source.ObservedCapacity >= source.Count);
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }, list.ToArray());
    }

    [Fact]
    public void AddRange_EmptyKnownCountSources_AreNoOp()
    {
        var list = new SwiftList<int> { 1 };
        var enumerator = list.GetEnumerator();
        IEnumerable<int> collection = Array.Empty<int>();
        var readOnlyCollection = new CapacityObservingReadOnlyCollection<int>(Array.Empty<int>(), () => list.Capacity);

        list.AddRange(collection);
        list.AddRange(readOnlyCollection);

        Assert.Equal(new[] { 1 }, list.ToArray());
        Assert.True(enumerator.MoveNext());
        Assert.Equal(1, enumerator.Current);
        Assert.False(enumerator.MoveNext());
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
        var list = new SwiftList<int>(SwiftList<int>.DefaultCapacity);
        list.AddRange(new[] { 1, 2, 4, 5, 6, 7, 8, 9 });

        list.Insert(2, 3);

        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }, list.ToArray());
        Assert.True(list.Capacity > SwiftList<int>.DefaultCapacity);
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
        Assert.Throws<IndexOutOfRangeException>(() => list.RemoveAt(0));
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
    public void SortInPlace_DefaultComparerArgument_ShouldSortElementsInAscendingOrder()
    {
        var list = new SwiftList<int> { 3, 1, 2 };
        list.SortInPlace(Comparer<int>.Default);
        Assert.Equal(new[] { 1, 2, 3 }, list.ToArray());
    }

    [Fact]
    public void SortInPlace_DefaultComparer_ShouldSortWithoutChangingCapacity()
    {
        var list = new SwiftList<int>(16) { 5, 1, 3, 2, 4 };
        int capacityBefore = list.Capacity;

        list.SortInPlace();

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, list.ToArray());
        Assert.Equal(capacityBefore, list.Capacity);
    }

    [Fact]
    public void SortInPlace_CustomComparer_ShouldSortWithoutAllocatingComparerState()
    {
        var list = new SwiftList<int> { 1, 3, 2 };

        list.SortInPlace(DescendingIntComparer.Instance);

        Assert.Equal(new[] { 3, 2, 1 }, list.ToArray());
    }

    [Fact]
    public void SortInPlace_CustomComparer_ShouldHandleDuplicateHeavyInput()
    {
        var list = new SwiftList<int> { 4, 1, 4, 3, 2, 3, 1, 0, 4, 2, 0, 3, 1, 2, 4 };

        list.SortInPlace(DescendingIntComparer.Instance);

        for (int i = 1; i < list.Count; i++)
            Assert.True(list[i - 1] >= list[i]);
    }

    [Fact]
    public void SortInPlace_OrganPipeInput_ShouldSortWithClassAndStructComparers()
    {
        int[] source = CreateOrganPipe(256);
        int[] expected = (int[])source.Clone();
        Array.Sort(expected);
        var classComparerList = new SwiftList<int>(source);
        var structComparerList = new SwiftList<int>(source);
        IComparer<int> classComparer = Comparer<int>.Create(static (x, y) => x.CompareTo(y));

        classComparerList.SortInPlace(classComparer);
        structComparerList.SortInPlace(default(SwiftIntAscendingComparer));

        Assert.Equal(expected, classComparerList.ToArray());
        Assert.Equal(expected, structComparerList.ToArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void SortInPlace_SmallClassComparer_DoesNotCompare(int count)
    {
        var list = count == 0 ? new SwiftList<int>() : new SwiftList<int> { 42 };

        list.SortInPlace(ThrowingIntComparer.Instance);

        Assert.Equal(count, list.Count);
        if (count == 1)
            Assert.Equal(42, list[0]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void SortInPlace_SmallStructComparer_DoesNotCompare(int count)
    {
        var list = count == 0 ? new SwiftList<int>() : new SwiftList<int> { 42 };

        list.SortInPlace(default(ThrowingIntStructComparer));

        Assert.Equal(count, list.Count);
        if (count == 1)
            Assert.Equal(42, list[0]);
    }

    [Fact]
    public void SortInPlace_DefaultComparer_ShouldNotAllocateAfterWarmup()
    {
        const int Count = 1024;
        int[] source = CreateDescendingRange(Count);
        var list = new SwiftList<int>(Count);

        RefillAndSort(list, source);

        long before = GC.GetAllocatedBytesForCurrentThread();
        RefillAndSort(list, source);
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(before, after);
        AssertSortedAscending(list);
    }

    [Fact]
    public void SortInPlace_CustomComparer_ShouldNotAllocateAfterWarmup()
    {
        const int Count = 1024;
        int[] source = CreateAscendingRange(Count);
        var list = new SwiftList<int>(Count);

        RefillAndSort(list, source, DescendingIntComparer.Instance);

        long before = GC.GetAllocatedBytesForCurrentThread();
        RefillAndSort(list, source, DescendingIntComparer.Instance);
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(before, after);
        AssertSortedDescending(list);
    }

    [Fact]
    public void SortInPlace_StructCustomComparer_ShouldNotAllocateAfterWarmup()
    {
        const int Count = 1024;
        int[] source = CreateAscendingRange(Count);
        var list = new SwiftList<int>(Count);
        var comparer = new DescendingIntStructComparer();

        RefillAndSort(list, source, comparer);

        long before = GC.GetAllocatedBytesForCurrentThread();
        RefillAndSort(list, source, comparer);
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(before, after);
        AssertSortedDescending(list);
    }

    [Fact]
    public void SortInPlace_ShouldInvalidateExistingEnumerator()
    {
        var list = new SwiftList<int> { 3, 1, 2 };
        var enumerator = list.GetEnumerator();

        list.SortInPlace();

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
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

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(0, 16, 5, 16)]
    [InlineData(1, 0, 0, 8)]
    [InlineData(7, 0, 0, 8)]
    [InlineData(8, 0, 0, 8)]
    [InlineData(8, 8, 3, 8)]
    [InlineData(9, 0, 0, 16)]
    [InlineData(9, 8, 2, 16)]
    [InlineData(17, 8, 3, 32)]
    [InlineData(100, 0, 0, 128)]
    [InlineData(100, 8, 3, 128)]
    [InlineData(100, 16, 3, 128)]
    [InlineData(100, 128, 3, 128)]
    [InlineData(3, 16, 10, 16)]
    [InlineData(17, 20, 20, 20)]
    [InlineData(20, 20, 20, 20)]
    public void CopyTo_ListTarget_GrowsForTheWholeCopyAndReusesSufficientStorage(
        int count, int targetCapacity, int targetCount, int expectedCapacity)
    {
        int[] items = CreateAscendingRange(count);
        var source = new SwiftList<int>(items);
        var target = new SwiftList<int>(new int[targetCapacity]);
        target.Clear();
        for (int i = 0; i < targetCount; i++)
            target.Add(-100 - i);
        int[] sourceStorage = source.InnerArray;
        int[] targetStorage = target.InnerArray;
        var sourceEnumerator = source.GetEnumerator();
        var targetEnumerator = target.GetEnumerator();

        source.CopyTo(target);

        Assert.Equal(items, target.ToArray());
        Assert.Equal(count, target.Count);
        Assert.Equal(expectedCapacity, target.Capacity);
        if (count <= targetCapacity)
            Assert.Same(targetStorage, target.InnerArray);
        else
            Assert.NotSame(targetStorage, target.InnerArray);
        for (int i = count; i < targetCount; i++)
            Assert.Equal(-100 - i, target.InnerArray[i]);
        Assert.Equal(items, source.ToArray());
        Assert.Same(sourceStorage, source.InnerArray);
        Assert.Equal(count != 0, sourceEnumerator.MoveNext());
        Assert.Throws<InvalidOperationException>(() => targetEnumerator.MoveNext());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(100)]
    public void CopyTo_ListTarget_SelfCopyPreservesStorageAndInvalidatesEnumerator(int count)
    {
        int[] items = CreateAscendingRange(count);
        var list = new SwiftList<int>(items);
        int[] storage = list.InnerArray;
        var enumerator = list.GetEnumerator();

        list.CopyTo(list);

        Assert.Equal(items, list.ToArray());
        Assert.Equal(count, list.Count);
        Assert.Equal(count, list.Capacity);
        Assert.Same(storage, list.InnerArray);
        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void CopyTo_ListTarget_ShallowCopiesIntoIndependentStorage()
    {
        var first = new object();
        var second = new object();
        var source = new SwiftList<object>(new[] { first, second });
        var target = new SwiftList<object>();

        source.CopyTo(target);

        Assert.NotSame(source.InnerArray, target.InnerArray);
        Assert.Same(first, target[0]);
        Assert.Same(second, target[1]);
        target[0] = second;
        Assert.Same(first, source[0]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void CopyTo_ListTarget_NullPreservesSourceAndExistingException(int count)
    {
        int[] items = CreateAscendingRange(count);
        var source = new SwiftList<int>(items);
        int[] storage = source.InnerArray;
        var enumerator = source.GetEnumerator();

        Assert.Throws<NullReferenceException>(() => source.CopyTo((SwiftList<int>)null));

        Assert.Equal(items, source.ToArray());
        Assert.Same(storage, source.InnerArray);
        Assert.Equal(count, source.Capacity);
        Assert.Equal(count != 0, enumerator.MoveNext());
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
    public void SortInPlace_ShouldWorkWithCustomComparer()
    {
        var list = new SwiftList<int> { 1, 3, 2 };
        list.SortInPlace(Comparer<int>.Create((x, y) => y - x));  // Sort in descending order
        Assert.Equal(new[] { 3, 2, 1 }, list.ToArray());
    }

    [Fact]
    public void SortInPlace_DefaultComparer_ShouldSortAscending()
    {
        var list = new SwiftList<int> { 3, 1, 2 };

        list.SortInPlace();

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
        Assert.Same(collection.SyncRoot, collection.SyncRoot);
        Assert.Contains("1, 2, 3", list.ToString());
    }

    [Fact]
    public void IListIndexer_WithNullReferenceValue_ReturnsNull()
    {
        IList list = new SwiftList<string>
        {
            null
        };

        Assert.Null(list[0]);
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

        var copied = new SwiftList<int>();
        deserializedList.CopyTo(copied);
        Assert.Equal(CreateAscendingRange(100), copied.ToArray());
        int[] restoredStorage = deserializedList.InnerArray;
        new SwiftList<int>(new[] { 9, 8, 7 }).CopyTo(deserializedList);
        Assert.Equal(new[] { 9, 8, 7 }, deserializedList.ToArray());
        Assert.Same(restoredStorage, deserializedList.InnerArray);
    }

#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
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

        var copied = new SwiftList<int>();
        deserializedValue.CopyTo(copied);
        Assert.Equal(CreateAscendingRange(100), copied.ToArray());
        int[] restoredStorage = deserializedValue.InnerArray;
        new SwiftList<int>(new[] { 9, 8, 7 }).CopyTo(deserializedValue);
        Assert.Equal(new[] { 9, 8, 7 }, deserializedValue.ToArray());
        Assert.Same(restoredStorage, deserializedValue.InnerArray);
    }
#endif

    private sealed class DescendingIntComparer : IComparer<int>
    {
        public static readonly DescendingIntComparer Instance = new();

        private DescendingIntComparer()
        {
        }

        public int Compare(int x, int y) => y.CompareTo(x);
    }

    private sealed class ThrowingIntComparer : IComparer<int>
    {
        public static readonly ThrowingIntComparer Instance = new();

        private ThrowingIntComparer()
        {
        }

        public int Compare(int x, int y) => throw new InvalidOperationException();
    }

    private static int[] CreateAscendingRange(int count)
    {
        var values = new int[count];
        for (int i = 0; i < values.Length; i++)
            values[i] = i;

        return values;
    }

    private static int[] CreateDescendingRange(int count)
    {
        var values = new int[count];
        for (int i = 0; i < values.Length; i++)
            values[i] = values.Length - i - 1;

        return values;
    }

    private static int[] CreateOrganPipe(int count)
    {
        var values = new int[count];
        int midpoint = (count + 1) >> 1;

        for (int i = 0; i < midpoint; i++)
            values[i] = i;

        for (int i = midpoint; i < values.Length; i++)
            values[i] = values.Length - i - 1;

        return values;
    }

    private static void RefillAndSort(SwiftList<int> list, int[] source)
    {
        list.FastClear();
        list.AddRange(source.AsSpan());
        list.SortInPlace();
    }

    private static void RefillAndSort(SwiftList<int> list, int[] source, IComparer<int> comparer)
    {
        list.FastClear();
        list.AddRange(source.AsSpan());
        list.SortInPlace(comparer);
    }

    private static void RefillAndSort<TComparer>(SwiftList<int> list, int[] source, TComparer comparer)
        where TComparer : struct, IComparer<int>
    {
        list.FastClear();
        list.AddRange(source.AsSpan());
        list.SortInPlace(comparer);
    }

    private static void AssertSortedAscending(SwiftList<int> list)
    {
        for (int i = 0; i < list.Count; i++)
            Assert.Equal(i, list[i]);
    }

    private static void AssertSortedDescending(SwiftList<int> list)
    {
        for (int i = 0; i < list.Count; i++)
            Assert.Equal(list.Count - i - 1, list[i]);
    }

    private readonly struct DescendingIntStructComparer : IComparer<int>
    {
        public int Compare(int x, int y) => y.CompareTo(x);
    }

    private readonly struct ThrowingIntStructComparer : IComparer<int>
    {
        public int Compare(int x, int y) => throw new InvalidOperationException();
    }
    private sealed class ThrowingEqualityItem(Exception failure) : IEquatable<ThrowingEqualityItem>
    {
        public bool Equals(ThrowingEqualityItem other) => throw failure;

        public override bool Equals(object obj) => obj is ThrowingEqualityItem other && Equals(other);

        public override int GetHashCode() => 0;
    }

}
