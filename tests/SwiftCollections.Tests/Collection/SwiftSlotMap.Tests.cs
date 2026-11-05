using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Xunit;

#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
using MemoryPack;
#endif

namespace SwiftCollections.Tests;

public class SwiftSlotMapTests
{
    [Fact]
    public void Add_ShouldAddItemAndReturnIndex()
    {
        // Arrange
        var map = new SwiftSlotMap<string>();

        // Act
        int index1 = map.Add("Item1");
        int index2 = map.Add("Item2");

        // Assert
        index1.Should().Be(0);
        index2.Should().Be(1);
        map.Count.Should().Be(2);
        map[index1].Should().Be("Item1");
        map[index2].Should().Be("Item2");
    }

    [Fact]
    public void RemoveAt_ShouldRemoveItemAtIndex()
    {
        // Arrange
        var map = new SwiftSlotMap<int>();
        int index = map.Add(42);

        // Act
        map.TryRemoveAt(index);

        // Assert
        map.Count.Should().Be(0);
        Action act = () => { var item = map[index]; };
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Remove_ShouldRemoveItemByValue()
    {
        // Arrange
        var map = new SwiftSlotMap<string>
        {
            "Item1",
            "Item2"
        };

        // Act
        bool removed = map.TryRemove("Item1");

        // Assert
        removed.Should().BeTrue();
        map.Count.Should().Be(1);
        map.Contains("Item1").Should().BeFalse();
        map.Contains("Item2").Should().BeTrue();
    }

    [Fact]
    public void Indexer_GetAndSet_ShouldAccessAndModifyItem()
    {
        // Arrange
        var map = new SwiftSlotMap<int>();
        int index = map.Add(10);

        // Act
        int value = map[index];
        map[index] = 20;

        // Assert
        value.Should().Be(10);
        map[index].Should().Be(20);
    }

    [Fact]
    public void GetEnumerator_ShouldEnumerateAllItems()
    {
        // Arrange
        var map = new SwiftSlotMap<string>
        {
            "Item1",
            "Item2",
            "Item3"
        };

        // Act
        var items = new List<string>();
        foreach (var item in map)
        {
            items.Add(item);
        }

        // Assert
        items.Should().Contain(new[] { "Item1", "Item2", "Item3" });
        items.Count.Should().Be(3);
    }

    [Fact]
    public void Remove_NonExistentItem_ShouldReturnFalse()
    {
        // Arrange
        var map = new SwiftSlotMap<int>
        {
            1,
            2
        };

        // Act
        bool removed = map.TryRemove(3);

        // Assert
        removed.Should().BeFalse();
        map.Count.Should().Be(2);
    }

    [Fact]
    public void Indexer_InvalidIndex_ShouldThrowException()
    {
        // Arrange
        var map = new SwiftSlotMap<string>
        {
            "Item1"
        };

        // Act
        Action act = () => { var item = map[5]; };

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void InsertAt_ShouldInsertItemAtSpecifiedIndex()
    {
        // Arrange
        var map = new SwiftSlotMap<string>
        {
            "Item0", // Index 0
            "Item1" // Index 1
        };

        // Act
        map.InsertAt(5, "Item5");

        // Assert
        map[5].Should().Be("Item5");
        map.Count.Should().Be(3);
        map.Contains("Item5").Should().BeTrue();
    }

    [Fact]
    public void InsertAt_ExistingIndex_ShouldReplaceItem()
    {
        // Arrange
        var map = new SwiftSlotMap<int>
        {
            10 // Index 0
        };

        // Act
        map.InsertAt(0, 20);

        // Assert
        map[0].Should().Be(20);
        map.Count.Should().Be(1);
    }

    [Fact]
    public void Clear_ShouldRemoveAllItems()
    {
        // Arrange
        var map = new SwiftSlotMap<int>
        {
            1,
            2,
            3
        };

        // Act
        map.Clear();

        // Assert
        map.Count.Should().Be(0);
        Action act = () => { var item = map[0]; };
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Clear_WhenEmpty_IsNoOpAndSlotMapRemainsReusable()
    {
        var map = new SwiftSlotMap<int>();

        map.Clear();
        int index = map.Add(42);

        map.Count.Should().Be(1);
        index.Should().Be(0);
        map[index].Should().Be(42);
    }

    [Fact]
    public void Add_ShouldExpandCapacityWhenNeeded()
    {
        // Arrange
        var map = new SwiftSlotMap<int>(8)
        {
            3,
            4,
            5,
            6,
            7,
            8,
            // Act
            1,
            2
        };
        int capacityBefore = map.Capacity;
        map.Add(3); // Should trigger capacity expansion
        int capacityAfter = map.Capacity;

        // Assert
        capacityAfter.Should().BeGreaterThan(capacityBefore);
        map.Count.Should().Be(9);
    }

    [Fact]
    public void Add_ShouldReuseFreeIndices()
    {
        // Arrange
        var map = new SwiftSlotMap<string>();
        int index1 = map.Add("Item1");
        int index2 = map.Add("Item2");

        // Act
        map.TryRemoveAt(index1);
        int index3 = map.Add("Item3");

        // Assert
        index3.Should().Be(index1); // Should reuse the free index
        map.Count.Should().Be(2);
        map[index3].Should().Be("Item3");
    }

    [Fact]
    public void Contains_ShouldReturnTrueIfItemExists()
    {
        // Arrange
        var map = new SwiftSlotMap<string>
        {
            "Item1",
            "Item2"
        };

        // Act & Assert
        map.Contains("Item1").Should().BeTrue();
        map.Contains("Item3").Should().BeFalse();
    }

    [Fact]
    public void Exists_ShouldReturnTrueIfMatchIsFound()
    {
        var map = new SwiftSlotMap<int>
        {
            1,
            2,
            3
        };

        map.Exists(i => i == 2).Should().BeTrue();
    }

    [Fact]
    public void Find_ShouldReturnMatchingItem()
    {
        var map = new SwiftSlotMap<int>
        {
            1,
            2,
            3
        };

        map.Find(i => i == 2).Should().Be(2);
    }

    [Fact]
    public void Find_ShouldReturnDefaultIfMatchIsNotFound()
    {
        var map = new SwiftSlotMap<int>
        {
            1,
            2
        };

        map.Find(i => i > 10).Should().Be(default);
    }

    [Fact]
    public void CopyTo_ShouldCopyItemsToArray()
    {
        // Arrange
        var map = new SwiftSlotMap<int>
        {
            1,
            2,
            3
        };
        int[] array = new int[3];

        // Act
        map.CopyTo(array, 0);

        // Assert
        array.Should().Contain(new[] { 1, 2, 3 });
    }

    [Fact]
    public void NonGenericCopyTo_WithInvalidReferenceArray_ThrowsArgumentException()
    {
        ICollection map = new SwiftSlotMap<string>
        {
            "one",
            "two"
        };

        Assert.Throws<ArgumentException>(() => map.CopyTo(new Uri[2], 0));
    }

    [Fact]
    public void IndexOf_NullWhenOnlyNonNullValuesExist_ReturnsMinusOne()
    {
        var map = new SwiftSlotMap<string>
        {
            "one",
            "two"
        };

        Assert.Equal(-1, map.IndexOf(null));
    }

    [Fact]
    public void TrimExcessCapacity_WhenSlotMapIsEmpty_ShrinksToDefaultCapacity()
    {
        var map = new SwiftSlotMap<int>(64);

        map.TrimExcessCapacity();

        Assert.Empty(map);
        Assert.True(map.Capacity >= SwiftSlotMap<int>.DefaultCapacity);
        Assert.True(map.Capacity < 64);
    }

    [Fact]
    public void Enumerator_Reset_ShouldRestartEnumeration()
    {
        // Arrange
        var map = new SwiftSlotMap<string>
        {
            "Item1",
            "Item2"
        };
        var enumerator = map.GetEnumerator();

        // Act
        var itemsFirstPass = new List<string>();
        while (enumerator.MoveNext())
        {
            itemsFirstPass.Add(enumerator.Current);
        }

        enumerator.Reset();

        var itemsSecondPass = new List<string>();
        while (enumerator.MoveNext())
        {
            itemsSecondPass.Add(enumerator.Current);
        }

        // Assert
        itemsFirstPass.Should().Equal(itemsSecondPass);
    }

    [Fact]
    public void RemoveAt_InvalidIndex_ShouldReturnFalse()
    {
        // Arrange
        var map = new SwiftSlotMap<int>();

        // Act
        bool removed = map.TryRemoveAt(5);

        // Assert
        removed.Should().BeFalse();
    }

    [Fact]
    public void Enumeration_ShouldThrowWhenSlotMapModified()
    {
        // Arrange
        var map = new SwiftSlotMap<int>();
        for (int i = 0; i < 5; i++)
            map.Add(i);

        // Act
        Action act = () =>
        {
            var items = new List<int>();
            foreach (var item in map)
            {
                items.Add(item);
                if (item == 2)
                    map.Add(5); // Modify the map during enumeration
            }
        };
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Capacity_ShouldReflectInternalArraySize()
    {
        // Arrange
        var map = new SwiftSlotMap<int>(5);

        // Act
        int initialCapacity = map.Capacity;
        map.Add(1);
        map.Add(2);
        map.Add(3);
        map.Add(4);
        map.Add(5);
        map.Add(6);
        map.Add(7);
        map.Add(8);
        map.Add(9);// Should trigger capacity increase
        int updatedCapacity = map.Capacity;

        // Assert
        initialCapacity.Should().Be(8);
        updatedCapacity.Should().BeGreaterThan(initialCapacity);
    }

    [Fact]
    public void IsAllocated_ShouldCheckIfItemIsPresent()
    {
        // Adding items and storing their indices
        var map = new SwiftSlotMap<string>();
        int indexA = map.Add("Item A");
        int indexB = map.Add("Item B");

        string itemA = string.Empty;
        if (map.IsAllocated(indexA))
            itemA = map[indexA];

        itemA.Should().BeSameAs(map[0]);

        // Removing an item
        map.TryRemoveAt(indexA);

        // Checking allocation after removal
        map.IsAllocated(indexA).Should().BeFalse();
    }

    [Fact]
    public void Add_Remove_LargeNumberOfItems()
    {
        var set = new SwiftSlotMap<int>();

        // Add items
        for (int i = 0; i < 100000; i++)
        {
            set.Add(i);
        }

        Assert.Equal(100000, set.Count);

        // Remove items
        for (int i = 0; i < 100000; i += 2)
        {
            set.TryRemoveAt(i);
        }

        Assert.Equal(50000, set.Count);
    }

    [Fact]
    public void SwiftSlotMap_Serialization_RoundTripMaintainsData()
    {
        var originalSlotMap = new SwiftSlotMap<int>();
        for (int i = 0; i < 100; i++)
            originalSlotMap.Add(i);

        var jsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            IncludeFields = true,
            IgnoreReadOnlyProperties = true
        };
        var json = JsonSerializer.SerializeToUtf8Bytes(originalSlotMap, jsonOptions);
        var deserializedSlotMap = JsonSerializer.Deserialize<SwiftSlotMap<int>>(json, jsonOptions);

        // Verify that the deserialized list matches the original
        Assert.Equal(originalSlotMap.Count, deserializedSlotMap.Count);
        Assert.Equal(originalSlotMap.PeakCount, deserializedSlotMap.PeakCount);
        for (int i = 0; i < originalSlotMap.Count; i++)
            Assert.Equal(originalSlotMap[i], deserializedSlotMap[i]);
    }

#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
    [Fact]
    public void SwiftSlotMap_MemoryPackSerialization_RoundTripMaintainsData()
    {
        var originalValue = new SwiftSlotMap<int>();
        for (int i = 0; i < 100; i++)
            originalValue.Add(i);

        byte[] bytes = MemoryPackSerializer.Serialize(originalValue);
        SwiftSlotMap<int> deserializedValue = MemoryPackSerializer.Deserialize<SwiftSlotMap<int>>(bytes);

        // Check that deserialized values match the original
        Assert.Equal(originalValue.Count, deserializedValue.Count);
        Assert.Equal(originalValue.PeakCount, deserializedValue.PeakCount);
        Assert.Equal(originalValue, deserializedValue);
    }
#endif

    [Fact]
    public void SwiftSlotMap_StateConstructor_AllowsNullFreeIndices()
    {
        var state = new SwiftSlotMapState<int>(
            new[] { 10, 20, 30 },
            new[] { true, false, true },
            null,
            3);

        var map = new SwiftSlotMap<int>(state);

        map.Count.Should().Be(2);
        map.PeakCount.Should().Be(3);
        map[0].Should().Be(10);
        map[2].Should().Be(30);
    }

    [Fact]
    public void SwiftSlotMap_StateConstructor_AllowsDefaultState()
    {
        var map = new SwiftSlotMap<int>(default(SwiftSlotMapState<int>));

        map.Count.Should().Be(0);
        map.PeakCount.Should().Be(0);
        map.Capacity.Should().Be(SwiftSlotMap<int>.DefaultCapacity);
    }

    [Fact]
    public void SwiftSlotMap_StateConstructor_TreatsMissingAllocationFlagsAsUnallocated()
    {
        var state = new SwiftSlotMapState<int>(
            new[] { 10, 20 },
            new[] { true },
            Array.Empty<int>(),
            2);

        var map = new SwiftSlotMap<int>(state);

        map.Count.Should().Be(1);
        map.PeakCount.Should().Be(2);
        map[0].Should().Be(10);
        map.TryGetValue(1, out _).Should().BeFalse();
    }

    [Fact]
    public void SwiftSlotMapState_Constructor_NormalizesNullArraysToEmptyArrays()
    {
        var state = new SwiftSlotMapState<int>(null, null, null, 5);

        state.Items.Should().BeEmpty();
        state.Allocated.Should().BeEmpty();
        state.FreeIndices.Should().BeEmpty();
        state.PeakCount.Should().Be(5);
    }

    [Fact]
    public void SwiftSlotMap_StateConstructor_RestoresFreeIndicesAndNormalizesPeak()
    {
        var state = new SwiftSlotMapState<int>(
            new[] { 10 },
            new[] { true },
            new[] { 3 },
            -1);

        var map = new SwiftSlotMap<int>(state);

        map.PeakCount.Should().Be(4);
        int reused = map.Add(40);
        reused.Should().Be(3);
        map[reused].Should().Be(40);
    }

    [Fact]
    public void StateConstructor_RestoresMissingItemsAndDescendingFreeIndices()
    {
        var state = new SwiftSlotMapState<int>(
            Array.Empty<int>(),
            new[] { true },
            new[] { 2, 1 },
            0);

        var map = new SwiftSlotMap<int>(state);

        map.Count.Should().Be(1);
        map[0].Should().Be(0);
        map.Add(42).Should().Be(1);
    }

    [Fact]
    public void SwiftSlotMap_StateConstructor_ClampsLargePeakAndRejectsOutOfRangeFreeIndex()
    {
        var largePeakState = new SwiftSlotMapState<int>(
            new[] { 10 },
            new[] { true },
            Array.Empty<int>(),
            999);
        var invalidFreeState = new SwiftSlotMapState<int>(
            new[] { 10 },
            new[] { true },
            new[] { 99 },
            1);

        var map = new SwiftSlotMap<int>(largePeakState);

        map.PeakCount.Should().Be(map.Capacity);
        Assert.Throws<InvalidOperationException>(() => new SwiftSlotMap<int>(invalidFreeState));
    }

    [Fact]
    public void SwiftSlotMap_NullValueSearchAndMissPaths_ReturnExpectedResults()
    {
        var map = new SwiftSlotMap<string>();
        map.Add("first");
        int nullIndex = map.Add(null);
        map.Add("last");

        map.IndexOf(null).Should().Be(nullIndex);
        map.Contains(null).Should().BeTrue();
        map.TryGetValue(99, out string missing).Should().BeFalse();
        missing.Should().BeNull();
        map.Exists(static value => value == "missing").Should().BeFalse();
    }

    [Fact]
    public void SwiftSlotMap_ExplicitCollectionAddAndHighInsertUseExpectedSlots()
    {
        var map = new SwiftSlotMap<int>(8);
        ((ICollection<int>)map).Add(1);

        map.InsertAt(20, 20);

        map[0].Should().Be(1);
        map[20].Should().Be(20);
        map.Capacity.Should().BeGreaterThan(8);
    }

    [Fact]
    public void SwiftSlotMap_NonGenericCopyTo_ValidatesShapeCapacityAndType()
    {
        var map = new SwiftSlotMap<int>
        {
            1,
            2
        };
        Array nonZeroLowerBound = Array.CreateInstance(typeof(int), new[] { 4 }, new[] { 1 });

        Assert.Throws<ArgumentException>(() => ((ICollection)map).CopyTo(new int[1, 2], 0));
        Assert.Throws<ArgumentException>(() => ((ICollection)map).CopyTo(nonZeroLowerBound, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ((ICollection)map).CopyTo(new int[2], 3));
        Assert.Throws<ArgumentException>(() => ((ICollection)map).CopyTo(new int[1], 0));
        Assert.Throws<ArgumentException>(() => ((ICollection)map).CopyTo(new string[2], 0));
    }

    [Fact]
    public void EnsureCapacity_ShouldGrowWithoutChangingDenseEntries()
    {
        var map = new SwiftSlotMap<int>
        {
            10,
            20
        };

        map.EnsureCapacity(64);

        map.Capacity.Should().BeGreaterThanOrEqualTo(64);
        map[0].Should().Be(10);
        map[1].Should().Be(20);
    }

    [Fact]
    public void EnsureCapacity_WhenCapacityAlreadyExists_DoesNotGrow()
    {
        var map = new SwiftSlotMap<int>();
        int capacityBefore = map.Capacity;

        map.EnsureCapacity(capacityBefore);

        map.Capacity.Should().Be(capacityBefore);
    }

    [Fact]
    public void EnsureCapacity_ShouldPreserveSparseEntriesAtHighIndices()
    {
        var map = new SwiftSlotMap<string>(8);
        map.InsertAt(0, "near");
        map.InsertAt(7, "far");

        map.EnsureCapacity(16);

        map[0].Should().Be("near");
        map[7].Should().Be("far");
    }

    [Fact]
    public void RemoveAt_WithinConstructorCapacity_ShouldNotAllocate()
    {
        const int Capacity = 64;
        var map = new SwiftSlotMap<int>(Capacity);
        var indices = new int[Capacity];

        for (int i = 0; i < Capacity; i++)
            indices[i] = map.Add(i);

        for (int i = 0; i < Capacity; i++)
            map.TryRemoveAt(indices[i]).Should().BeTrue();

        for (int i = 0; i < Capacity; i++)
            indices[i] = map.Add(i);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < Capacity; i++)
            map.TryRemoveAt(indices[i]);

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        allocated.Should().Be(0);
    }

    [Fact]
    public void RemoveAt_WithinEnsuredCapacity_ShouldNotAllocate()
    {
        const int Capacity = 64;
        var map = new SwiftSlotMap<int>();
        var indices = new int[Capacity];

        map.EnsureCapacity(Capacity);
        for (int i = 0; i < Capacity; i++)
            indices[i] = map.Add(i);

        for (int i = 0; i < Capacity; i++)
            map.TryRemoveAt(indices[i]).Should().BeTrue();

        for (int i = 0; i < Capacity; i++)
            indices[i] = map.Add(i);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < Capacity; i++)
            map.TryRemoveAt(indices[i]);

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        allocated.Should().Be(0);
    }

    [Fact]
    public void TrimExcessCapacity_ShouldShrinkDenseSlotMapAndPreserveEntries()
    {
        var map = new SwiftSlotMap<int>(64)
        {
            10,
            20,
            30
        };

        map.TrimExcessCapacity();

        map.Capacity.Should().BeLessThan(64);
        map[0].Should().Be(10);
        map[1].Should().Be(20);
        map[2].Should().Be(30);
    }

    [Fact]
    public void TrimExcessCapacity_AfterRemovingAllItems_DropsUnusedPeak()
    {
        var map = new SwiftSlotMap<int>(64);
        int index = map.Add(1);

        map.RemoveAt(index);
        map.TrimExcessCapacity();

        map.Should().BeEmpty();
        map.PeakCount.Should().Be(0);
        map.Capacity.Should().Be(SwiftSlotMap<int>.DefaultCapacity);
    }

    [Fact]
    public void TrimExcessCapacity_ShouldShrinkSparseSlotMapWithoutBreakingStableTickets()
    {
        var map = new SwiftSlotMap<string>(64);
        map.InsertAt(0, "near");
        map.InsertAt(31, "far");

        Action trim = map.TrimExcessCapacity;

        trim.Should().NotThrow();
        map[0].Should().Be("near");
        map[31].Should().Be("far");
        map.Capacity.Should().BeGreaterThan(31);
        map.Capacity.Should().BeLessThan(64);
    }

    [Fact]
    public void TryGetValue_ShouldReportPresenceAndAbsence()
    {
        var map = new SwiftSlotMap<string>();
        int key = map.Add("value");

        map.TryGetValue(key, out string existing).Should().BeTrue();
        existing.Should().Be("value");

        map.TryGetValue(key + 10, out string missing).Should().BeFalse();
        missing.Should().BeNull();
    }

    [Fact]
    public void ICollectionCopyTo_ShouldCopySlotMapValues()
    {
        ICollection map = new SwiftSlotMap<string>();
        ((SwiftSlotMap<string>)map).Add("alpha");
        ((SwiftSlotMap<string>)map).Add("beta");

        var array = new object[2];

        map.CopyTo(array, 0);

        array.Should().Equal("alpha", "beta");
    }

    [Fact]
    public void CloneTo_ShouldReplaceTargetContents()
    {
        var map = new SwiftSlotMap<int>
        {
            10,
            20
        };

        var list = new List<int> { 1, 2, 3 };

        map.CloneTo(list);

        list.Should().Equal(10, 20);
    }

    [Fact]
    public void SearchCopyAndClone_SkipFreedSlots()
    {
        var map = new SwiftSlotMap<string> { "remove", "keep" };
        map.RemoveAt(0);

        Assert.False(map.Exists(item => item == "missing"));
        Assert.Equal("keep", map.Find(item => item == "keep"));
        Assert.Equal(-1, map.IndexOf(null));

        var copied = new string[1];
        map.CopyTo(copied, 0);
        var cloned = new List<string>();
        map.CloneTo(cloned);

        Assert.Equal(new[] { "keep" }, copied);
        Assert.Equal(new[] { "keep" }, cloned);

        map.InsertAt(0, "replacement");
        Assert.Equal(2, map.Count);
    }

    [Fact]
    public void CollectionMembersAndNonGenericEnumerator_ExposeCurrentState()
    {
        ICollection<int> map = new SwiftSlotMap<int>
        {
            10,
            20
        };

        Assert.False(map.IsReadOnly);
        Assert.False(((SwiftSlotMap<int>)map).IsSynchronized);
        Assert.NotNull(((SwiftSlotMap<int>)map).SyncRoot);
        Assert.Same(((SwiftSlotMap<int>)map).SyncRoot, ((SwiftSlotMap<int>)map).SyncRoot);
        Assert.True(map.Remove(10));

        IEnumerator enumerator = ((IEnumerable)map).GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.NotNull(enumerator.Current);
    }
}
