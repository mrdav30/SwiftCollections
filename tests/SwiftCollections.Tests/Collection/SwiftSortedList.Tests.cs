using MemoryPack;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace SwiftCollections.Tests;

public class SwiftSortedListTests
{
    #region Test: Constructor

    [Fact]
    public void Constructor_Default_ShouldInitializeEmpty()
    {
        var sorter = new SwiftSortedList<int>();
        Assert.Empty(sorter);
        Assert.True(sorter.Capacity == 0);
    }

    [Fact]
    public void Constructor_Default_ShouldInitializeSorted()
    {
        var sorter = new SwiftSortedList<int>()
        {
            6,
            1,
            15,
            0,
            4
        };
        Assert.True(sorter.Count == 5);
        Assert.True(sorter.PeekMin() == 0);
        Assert.True(sorter.PeekMax() == 15);
    }

    [Fact]
    public void Constructor_WithComparer_ShouldSetCustomComparer()
    {
        static int comparison(int x, int y) => y.CompareTo(x);
        IComparer<int> comparer = Comparer<int>.Create(comparison);
        var sorter = new SwiftSortedList<int>(comparer);
        Assert.Equal(comparer, sorter.Comparer);
    }

    [Fact]
    public void Constructor_WithEnumerable_ShouldInitializeCountAndSortedValues()
    {
        var sorter = new SwiftSortedList<int>(new[] { 7, 3, 9, 1 });

        Assert.Equal(4, sorter.Count);
        Assert.Equal(1, sorter.PeekMin());
        Assert.Equal(9, sorter.PeekMax());

        int index = 0;
        foreach (int item in sorter)
            Assert.Equal(new[] { 1, 3, 7, 9 }[index++], item);
    }

    #endregion

    #region Test: Add and AddRange

    [Fact]
    public void Add_SingleElement_ShouldAddCorrectly()
    {
        var sorter = new SwiftSortedList<int>();
        sorter.Add(5);
        Assert.Single(sorter);
        Assert.Equal(5, sorter.PeekMin());
    }

    [Fact]
    public void Add_MultipleElements_ShouldMaintainOrder()
    {
        var sorter = new SwiftSortedList<int>();
        sorter.Add(10);
        sorter.Add(5);
        sorter.Add(15);
        Assert.Equal(3, sorter.Count);
        Assert.Equal(5, sorter.PeekMin());
        Assert.Equal(15, sorter.PeekMax());
    }

    [Fact]
    public void Enumerator()
    {
        var sorter = new SwiftSortedList<int>
        {
            10,
            5,
            15
        };
        int count = 0;
        foreach (var item in sorter)
            count += item;

        Assert.Equal(30, count);
    }

    [Fact]
    public void AddRange_EmptyCollection_ShouldDoNothing()
    {
        var sorter = new SwiftSortedList<int>();
        sorter.AddRange(new List<int>());
        Assert.Empty(sorter);
    }

    [Fact]
    public void AddRange_SortedCollection_ShouldMergeCorrectly()
    {
        var sorter = new SwiftSortedList<int>();
        sorter.Add(5);
        sorter.AddRange(new List<int> { 1, 3, 7, 9 });
        Assert.Equal(5, sorter.Count);
        Assert.Equal(1, sorter.PeekMin());
        Assert.Equal(9, sorter.PeekMax());
    }

    [Fact]
    public void AddRange_UnsortedCollection_ShouldSortAndMerge()
    {
        var sorter = new SwiftSortedList<int>();
        sorter.Add(10);
        sorter.AddRange(new List<int> { 15, 5, 20 });
        Assert.Equal(4, sorter.Count);
        Assert.Equal(5, sorter.PeekMin());
        Assert.Equal(20, sorter.PeekMax());
    }

    [Fact]
    public void AddRange_UnSortedCollection_ShouldMergeCorrectlyWhenEmpty()
    {
        var sorter = new SwiftSortedList<int>();
        sorter.AddRange(new List<int> { 7, 3, 9, 1 });
        Assert.Equal(4, sorter.Count);
        Assert.Equal(1, sorter.PeekMin());
        Assert.Equal(9, sorter.PeekMax());
    }

    [Fact]
    public void Add_WithUniquenessNotEnforced_AddsDuplicates()
    {
        var sorter = new SwiftSortedList<int>();
        sorter.Add(1);
        sorter.Add(1);

        Assert.Equal(2, sorter.Count);
        Assert.Equal(1, sorter.PeekMin());
    }

    [Fact]
    public void AddRange_WithMixedElements_SortsAndMergesCorrectly()
    {
        var sorter = new SwiftSortedList<int>();
        sorter.AddRange(new[] { 5, 1, 3 });
        sorter.AddRange(new[] { 4, 2, 2 });

        Assert.Equal(6, sorter.Count); // second range contains a duplicate
        Assert.Equal(1, sorter.PeekMin());
        Assert.Equal(5, sorter.PeekMax());
    }

    [Fact]
    public void Insert_WhenIndexExceedsCountButFitsCapacity_ShouldThrow()
    {
        var sorter = new SwiftSortedList<int>(8);
        sorter.Add(1);
        sorter.Add(3);

        Assert.Throws<ArgumentOutOfRangeException>(() => sorter.Insert(2, 5));
        Assert.Equal(2, sorter.Count);
        Assert.Equal(1, sorter[0]);
        Assert.Equal(3, sorter[1]);
    }

    #endregion

    #region Test: PopMin and PopMax 

    [Fact]
    public void PopMin_ShouldRemoveAndReturnSmallestElement()
    {
        var sorter = new SwiftSortedList<int>();
        sorter.Add(10);
        sorter.Add(5);
        sorter.Add(15);
        Assert.Equal(5, sorter.PopMin());
        Assert.Equal(2, sorter.Count);
    }

    [Fact]
    public void PopMax_ShouldRemoveAndReturnLargestElement()
    {
        var sorter = new SwiftSortedList<int>();
        sorter.Add(10);
        sorter.Add(5);
        sorter.Add(15);
        Assert.Equal(15, sorter.PopMax());
        Assert.Equal(2, sorter.Count);
    }

    [Fact]
    public void PopMin_OnEmpty_ShouldThrowException()
    {
        var sorter = new SwiftSortedList<int>();
        Assert.Throws<IndexOutOfRangeException>(() => sorter.PopMin());
    }

    [Fact]
    public void PopMax_OnEmpty_ShouldThrowException()
    {
        var sorter = new SwiftSortedList<int>();
        Assert.Throws<IndexOutOfRangeException>(() => sorter.PopMax());
    }

    #endregion

    #region Test: PeekMin and PeekMax

    [Fact]
    public void PeekMin_ShouldReturnSmallestElement()
    {
        var sorter = new SwiftSortedList<int>();
        sorter.Add(10);
        sorter.Add(5);
        sorter.Add(15);
        Assert.Equal(5, sorter.PeekMin());
    }

    [Fact]
    public void PeekMax_ShouldReturnLargestElement()
    {
        var sorter = new SwiftSortedList<int>();
        sorter.Add(10);
        sorter.Add(5);
        sorter.Add(15);
        Assert.Equal(15, sorter.PeekMax());
    }

    [Fact]
    public void PeekMin_OnEmpty_ShouldThrowException()
    {
        var sorter = new SwiftSortedList<int>();
        Assert.Throws<IndexOutOfRangeException>(() => sorter.PeekMin());
    }

    [Fact]
    public void PeekMax_OnEmpty_ShouldThrowException()
    {
        var sorter = new SwiftSortedList<int>();
        Assert.Throws<IndexOutOfRangeException>(() => sorter.PeekMax());
    }

    #endregion

    #region Test: Contains 

    [Fact]
    public void Contains_ElementExists_ShouldReturnTrue()
    {
        var sorter = new SwiftSortedList<int>();
        sorter.Add(10);
        sorter.Add(5);
        bool contains = sorter.Contains(10);
        Assert.True(contains);
    }

    [Fact]
    public void Contains_ElementDoesNotExist_ShouldReturnFalse()
    {
        var sorter = new SwiftSortedList<int>();
        sorter.Add(10);
        sorter.Add(5);
        bool contains = sorter.Contains(15);
        Assert.False(contains);
    }

    [Fact]
    public void Exists_MatchingElement_ShouldReturnTrue()
    {
        var sorter = new SwiftSortedList<int>();
        sorter.AddRange(new[] { 5, 1, 4, 2 });

        Assert.True(sorter.Exists(i => i == 4));
    }

    [Fact]
    public void Find_MatchingElement_ShouldReturnFirstInSortedOrder()
    {
        var sorter = new SwiftSortedList<int>();
        sorter.AddRange(new[] { 5, 1, 4, 2 });

        Assert.Equal(4, sorter.Find(i => i > 2));
    }

    [Fact]
    public void Find_MissingElement_ShouldReturnDefault()
    {
        var sorter = new SwiftSortedList<int>();
        sorter.AddRange(new[] { 5, 1, 4, 2 });

        Assert.Equal(default, sorter.Find(i => i > 10));
    }

    [Fact]
    public void AsReadOnlySpan_ReturnsSortedWindow()
    {
        var sorter = new SwiftSortedList<int>();
        sorter.AddRange(new[] { 5, 1, 4, 2 });

        Assert.Equal(new[] { 1, 2, 4, 5 }, sorter.AsReadOnlySpan().ToArray());
    }

    [Fact]
    public void IndexOf_FindsCorrectIndex()
    {
        var sorter = new SwiftSortedList<int>();
        sorter.AddRange(new[] { 1, 2, 3, 4, 5 });

        Assert.Equal(2, sorter.IndexOf(3));
        Assert.Equal(-1, sorter.IndexOf(6));
    }

    #endregion

    #region Test: Remove

    [Fact]
    public void Remove_ExistingElement_ShouldRemoveAndReturnTrue()
    {
        var sorter = new SwiftSortedList<int>
        {
            10,
            5,
            6,
            2,
            11,
            7,
            22,
            32,
            0,
            9,
            42,
            50,
            15
        };
        sorter.Remove(5);
        Assert.True(sorter.Remove(11));
        sorter.Remove(50);
        sorter.Remove(7);
        sorter.Remove(2);
        sorter.Add(51);
        sorter.Add(-1);
        sorter.Remove(51);
        sorter.Remove(42);
        Assert.DoesNotContain(11, sorter);
    }

    [Fact]
    public void Remove_NonExistentElement_ShouldReturnFalse()
    {
        var sorter = new SwiftSortedList<int>();
        sorter.Add(10);
        sorter.Add(5);
        Assert.False(sorter.Remove(15));
    }

    #endregion

    #region Test: Clear

    [Fact]
    public void Clear_NonEmptyList_ShouldRemoveAllElements()
    {
        var sorter = new SwiftSortedList<int>();
        sorter.Add(10);
        sorter.Add(5);
        sorter.Clear();
        Assert.Empty(sorter);
    }

    [Fact]
    public void Clear_EmptyList_ShouldDoNothing()
    {
        var sorter = new SwiftSortedList<int>();
        sorter.Clear();
        Assert.Empty(sorter);
    }

    #endregion

    #region Test: CloneTo 

    [Fact]
    public void CloneTo_ShouldCloneElementsToNewCollection()
    {
        var sorter = new SwiftSortedList<int>();
        sorter.AddRange(new List<int> { 5, 10, 15 });
        var targetList = new List<int>();
        sorter.CloneTo(targetList);
        Assert.Equal(sorter, targetList);
    }

    #endregion

    #region Test: Binary Search and Insertion

    [Fact]
    public void BinarySearch_ElementExists_ShouldReturnCorrectIndex()
    {
        var sorter = new SwiftSortedList<int>();
        sorter.AddRange(new List<int> { 1, 3, 5, 7, 9 });
        Assert.Equal(2, sorter.Search(5));
    }

    [Fact]
    public void BinarySearch_ElementNotFound_ShouldReturnInsertionPoint()
    {
        var sorter = new SwiftSortedList<int>();
        sorter.AddRange(new List<int> { 1, 3, 5, 7, 9 });
        Assert.Equal(4, sorter.InsertionPoint(8)); // Insertion point for 8
    }

    [Fact]
    public void CopyTo_ArrayOverloads_CopySortedWindowAtOffset()
    {
        var sorter = new SwiftSortedList<int>();
        sorter.AddRange(new[] { 5, 1, 4, 2 });

        var generic = new int[6];
        Array nonGeneric = new object[6];

        sorter.CopyTo(generic, 1);
        sorter.CopyTo(nonGeneric, 1);

        Assert.Equal(new[] { 0, 1, 2, 4, 5, 0 }, generic);
        Assert.Equal(new object[] { null, 1, 2, 4, 5, null }, (object[])nonGeneric);
    }

    [Fact]
    public void FastClear_ShouldResetCountAndRecenterOffsetWithoutReplacingArray()
    {
        var sorter = new SwiftSortedList<int>();
        sorter.AddRange(new[] { 5, 1, 4, 2 });

        int[] innerArray = sorter.InnerArray;
        int originalOffset = sorter.Offset;

        sorter.FastClear();

        Assert.Empty(sorter);
        Assert.Same(innerArray, sorter.InnerArray);
        Assert.Equal(sorter.InnerArray.Length >> 1, sorter.Offset);
        Assert.Equal(1, innerArray[originalOffset]);
    }

    [Fact]
    public void RemoveAt_WhenCountDropsBelowQuarter_RecenterArrayPreservesOrder()
    {
        var sorter = new SwiftSortedList<int>(32);
        sorter.AddRange(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

        sorter.RemoveAt(0);
        sorter.RemoveAt(0);

        sorter.RemoveAt(0);

        Assert.Equal((sorter.Capacity - sorter.Count) >> 1, sorter.Offset);
        Assert.Equal(new[] { 3, 4, 5, 6, 7, 8, 9 }, sorter.AsReadOnlySpan().ToArray());
    }

    [Fact]
    public void Enumerator_Reset_RestartsEnumeration()
    {
        var sorter = new SwiftSortedList<int>();
        sorter.AddRange(new[] { 5, 1, 4, 2 });

        var enumerator = sorter.GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.Equal(1, enumerator.Current);

        enumerator.Reset();

        Assert.True(enumerator.MoveNext());
        Assert.Equal(1, enumerator.Current);
    }

    [Fact]
    public void EnsureCapacity_PropertiesAndNonGenericEnumeration_ExposeState()
    {
        var sorter = new SwiftSortedList<int>();
        sorter.AddRange(new[] { 5, 1, 4, 2 });
        System.Collections.ICollection collection = sorter;

        sorter.EnsureCapacity(64);

        Assert.True(sorter.Capacity >= 64);
        Assert.False(((ICollection<int>)sorter).IsReadOnly);
        Assert.False(sorter.IsSynchronized);
        Assert.NotNull(collection.SyncRoot);
        Assert.NotEqual(0u, sorter.Version);

        System.Collections.IEnumerator enumerator = ((System.Collections.IEnumerable)sorter).GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.Equal(1, enumerator.Current);
    }

    [Fact]
    public void Constructor_WithEmptyCollection_ShouldUseEmptyBackingArray()
    {
        var sorter = new SwiftSortedList<int>(Array.Empty<int>());

        Assert.Empty(sorter);
        Assert.Equal(0, sorter.Capacity);
        Assert.Equal(0, sorter.Offset);
    }

    [Fact]
    public void Constructor_WithNonCollectionEnumerable_ShouldSortItems()
    {
        var sorter = new SwiftSortedList<int>(GetItems());

        Assert.Equal(new[] { 1, 3, 7, 9 }, sorter.AsReadOnlySpan().ToArray());

        static IEnumerable<int> GetItems()
        {
            yield return 7;
            yield return 3;
            yield return 9;
            yield return 1;
        }
    }

    [Fact]
    public void Constructor_WithLargeCollection_ShouldAllocateCenteredCapacity()
    {
        var sorter = new SwiftSortedList<int>(new List<int> { 9, 8, 7, 6, 5, 4, 3, 2, 1 });

        Assert.Equal(16, sorter.Capacity);
        Assert.Equal((sorter.Capacity - sorter.Count) >> 1, sorter.Offset);
        Assert.Equal(1, sorter.PeekMin());
        Assert.Equal(9, sorter.PeekMax());
    }

    [Fact]
    public void Constructor_WithEmptyState_ShouldInitializeEmptySorter()
    {
        var sorter = new SwiftSortedList<int>(new SwiftArrayState<int>(Array.Empty<int>()));

        Assert.Empty(sorter);
        Assert.Equal(0, sorter.Capacity);
        Assert.Equal(0, sorter.Offset);
    }

    [Fact]
    public void AddRange_EmptyNonCollectionEnumerable_ShouldDoNothing()
    {
        var sorter = new SwiftSortedList<int>();

        sorter.AddRange(GetItems());

        Assert.Empty(sorter);

        static IEnumerable<int> GetItems()
        {
            yield break;
        }
    }

    [Fact]
    public void Remove_MissingElement_ShouldReturnFalse()
    {
        var sorter = new SwiftSortedList<int> { 1, 3, 5 };

        Assert.False(sorter.Remove(2));
    }

    [Fact]
    public void Remove_OnEmptySorter_ShouldReturnFalse()
    {
        Assert.False(new SwiftSortedList<int>().Remove(1));
    }

    [Fact]
    public void Indexer_Get_InvalidIndex_ShouldThrow()
    {
        var sorter = new SwiftSortedList<int> { 1, 2, 3 };

        Assert.Throws<ArgumentOutOfRangeException>(() => _ = sorter[3]);
    }

    [Fact]
    public void SetComparer_WithSameComparer_IsNoOp()
    {
        var comparer = Comparer<int>.Default;
        var sorter = new SwiftSortedList<int>(comparer) { 3, 1, 2 };
        uint versionBefore = sorter.Version;

        sorter.SetComparer(comparer);

        Assert.Equal(versionBefore, sorter.Version);
        Assert.Equal(new[] { 1, 2, 3 }, sorter.AsReadOnlySpan().ToArray());
    }

    [Fact]
    public void PopMinAndPopMax_OnSingleItem_RecenterOffset()
    {
        var minSorter = new SwiftSortedList<int> { 5 };
        int minOffsetBefore = minSorter.InnerArray.Length >> 1;

        Assert.Equal(5, minSorter.PopMin());
        Assert.Equal(minOffsetBefore, minSorter.Offset);

        var maxSorter = new SwiftSortedList<int> { 9 };
        int maxOffsetBefore = maxSorter.InnerArray.Length >> 1;

        Assert.Equal(9, maxSorter.PopMax());
        Assert.Equal(maxOffsetBefore, maxSorter.Offset);
    }

    [Fact]
    public void FastClear_OnEmptySorter_LeavesVersionUnchanged()
    {
        var sorter = new SwiftSortedList<int>();
        uint versionBefore = sorter.Version;

        sorter.FastClear();

        Assert.Equal(versionBefore, sorter.Version);
        Assert.Empty(sorter);
    }

    [Fact]
    public void CopyTo_ArrayOverloads_ThrowWhenDestinationIsTooSmall()
    {
        var sorter = new SwiftSortedList<int> { 1, 2, 3 };

        Assert.Throws<ArgumentException>(() => sorter.CopyTo(new int[2], 0));
        Assert.Throws<ArgumentException>(() => sorter.CopyTo(new object[2], 0));
    }

    [Fact]
    public void Exists_WhenMatchIsMissingOrNull_ShouldReturnFalseOrThrow()
    {
        var sorter = new SwiftSortedList<int> { 1, 2, 3 };

        Assert.False(sorter.Exists(i => i == 9));
        Assert.Throws<ArgumentNullException>(() => sorter.Exists(null));
    }

    [Fact]
    public void CloneTo_NullTarget_ShouldThrow()
    {
        var sorter = new SwiftSortedList<int> { 1, 2, 3 };

        Assert.Throws<ArgumentNullException>(() => sorter.CloneTo(null));
    }

    [Fact]
    public void Enumerator_CurrentAfterEnumerationEnds_ShouldThrow()
    {
        System.Collections.IEnumerator enumerator = ((System.Collections.IEnumerable)new SwiftSortedList<int> { 1, 2, 3 }).GetEnumerator();

        while (enumerator.MoveNext())
        {
        }

        Assert.Throws<InvalidOperationException>(() => _ = enumerator.Current);
    }

    [Fact]
    public void Enumerator_Reset_AfterMutation_ShouldThrow()
    {
        var sorter = new SwiftSortedList<int> { 1, 2, 3 };
        var enumerator = sorter.GetEnumerator();

        sorter.Add(4);

        Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
    }

    #endregion

    #region Test: Serialization

    [Fact]
    public void SerializeAndDeserialize_ShouldMaintainDataIntegrity()
    {
        var sorter = new SwiftSortedList<int>();
        sorter.AddRange(new List<int> { 5, 10, 15 });

        var jsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };
        var json = JsonSerializer.SerializeToUtf8Bytes(sorter, jsonOptions);
        var deserializedSorter = JsonSerializer.Deserialize<SwiftSortedList<int>>(json, jsonOptions);

        Assert.Equal(sorter.Count, deserializedSorter.Count);
        Assert.Equal(sorter.Offset, deserializedSorter.Offset);
        Assert.Equal(sorter.InnerArray.Length, deserializedSorter.InnerArray.Length);
        Assert.Equal(sorter.PeekMin(), deserializedSorter.PeekMin());
        Assert.Equal(sorter.PeekMax(), deserializedSorter.PeekMax());
    }

    [Fact]
    public void SwiftSortedList_MemoryPackSerialization_RoundTripMaintainsData()
    {
        var sorter = new SwiftSortedList<int>();
        sorter.AddRange(new List<int> { 5, 10, 15 });

        byte[] bytes = MemoryPackSerializer.Serialize(sorter);
        SwiftSortedList<int> deserializedSorter = MemoryPackSerializer.Deserialize<SwiftSortedList<int>>(bytes);

        // Check that deserialized values match the original
        Assert.Equal(sorter.Count, deserializedSorter.Count);
        Assert.Equal(sorter.Offset, deserializedSorter.Offset);
        Assert.Equal(sorter.InnerArray.Length, deserializedSorter.InnerArray.Length);
        Assert.Equal(sorter.PeekMin(), deserializedSorter.PeekMin());
        Assert.Equal(sorter.PeekMax(), deserializedSorter.PeekMax());
    }

    [Fact]
    public void SortedList_CustomComparer_RoundTrip()
    {
        var comparer = Comparer<string>.Create(
            (a, b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase));

        var list = new SwiftSortedList<string>(comparer)
        {
            "Bravo",
            "alpha"
        };

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(list);

        var result = JsonSerializer.Deserialize<SwiftSortedList<string>>(json);

        result.SetComparer(comparer);

        Assert.Equal("alpha", result[0]);
    }

    #endregion
}
