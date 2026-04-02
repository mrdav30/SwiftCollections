using MemoryPack;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace SwiftCollections.Tests;

public class SwiftHashSetTests
{
    [Fact]
    public void Add_NewItem_ReturnsTrue()
    {
        var set = new SwiftHashSet<int>();
        bool added = set.Add(1);

        Assert.True(added);
        Assert.Single(set);
        Assert.Contains(1, set);
    }

    [Fact]
    public void Constructor_DefaultStringComparer_UsesDeterministicHashesAcrossInstances()
    {
        var first = new SwiftHashSet<string>();
        var second = new SwiftHashSet<string>();

        Assert.NotSame(EqualityComparer<string>.Default, first.Comparer);
        Assert.Equal(first.Comparer.GetHashCode("Hello"), second.Comparer.GetHashCode("Hello"));
    }

    [Fact]
    public void Constructor_DefaultObjectComparer_UsesDeterministicStringHashesAcrossInstances()
    {
        var first = new SwiftHashSet<object>();
        var second = new SwiftHashSet<object>();

        Assert.NotSame(EqualityComparer<object>.Default, first.Comparer);
        Assert.Equal(first.Comparer.GetHashCode("Hello"), second.Comparer.GetHashCode("Hello"));
    }

    [Fact]
    public void Add_DuplicateItem_ReturnsFalse()
    {
        var set = new SwiftHashSet<int>
        {
            1
        };
        bool added = set.Add(1);

        Assert.False(added);
        Assert.Single(set);
    }

    [Fact]
    public void Remove_ExistingItem_ReturnsTrue()
    {
        var set = new SwiftHashSet<int> { 1, 2, 3 };
        bool removed = set.Remove(2);

        Assert.True(removed);
        Assert.Equal(2, set.Count);
        Assert.DoesNotContain(2, set);
    }

    [Fact]
    public void Remove_NonExistingItem_ReturnsFalse()
    {
        var set = new SwiftHashSet<int> { 1, 2, 3 };
        bool removed = set.Remove(4);

        Assert.False(removed);
        Assert.Equal(3, set.Count);
    }

    [Fact]
    public void Contains_ExistingItem_ReturnsTrue()
    {
        var set = new SwiftHashSet<int> { 1, 2, 3 };
        bool contains = set.Contains(2);

        Assert.True(contains);
    }

    [Fact]
    public void Contains_NonExistingItem_ReturnsFalse()
    {
        var set = new SwiftHashSet<int> { 1, 2, 3 };
        bool contains = set.Contains(4);

        Assert.False(contains);
    }

    [Fact]
    public void Exists_MatchingItem_ReturnsTrue()
    {
        var set = new SwiftHashSet<int> { 1, 2, 3 };

        Assert.True(set.Exists(i => i == 2));
    }

    [Fact]
    public void Find_MatchingItem_ReturnsValue()
    {
        var set = new SwiftHashSet<int> { 1, 2, 3 };

        Assert.Equal(2, set.Find(i => i == 2));
    }

    [Fact]
    public void Find_MissingItem_ReturnsDefault()
    {
        var set = new SwiftHashSet<int> { 1, 2, 3 };

        Assert.Equal(default, set.Find(i => i == 4));
    }

    [Fact]
    public void Clear_RemovesAllItems()
    {
        var set = new SwiftHashSet<int> { 1, 2, 3 };
        set.Clear();

        Assert.Empty(set);
        Assert.DoesNotContain(1, set);
    }

    [Fact]
    public void Enumerate_ItemsAreIteratedCorrectly()
    {
        var set = new SwiftHashSet<int> { 1, 2, 3 };
        var items = new List<int>();

        foreach (var item in set)
        {
            items.Add(item);
        }

        Assert.Equal(3, items.Count);
        Assert.Contains(1, items);
        Assert.Contains(2, items);
        Assert.Contains(3, items);
    }

    [Fact]
    public void AddRange_ItemsAreAddedCorrectly()
    {
        var initialItems = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 10, 11, 12 };
        var set = new SwiftHashSet<int>(initialItems);

        Assert.Equal(11, set.Count);
        Assert.Contains(1, set);
        Assert.Contains(2, set);
        Assert.Contains(3, set);
    }

    [Fact(Timeout = 2000)]
    public void AddRange_FromPopulatedHashSet_AddsAllItems()
    {
        var destination = new SwiftHashSet<int> { 0, 1, 2, 3, 4, 5 };
        var source = new SwiftHashSet<int> { 6, 7, 8 };

        destination.AddRange(source);

        Assert.Equal(9, destination.Count);

        for (int i = 0; i < 9; i++)
            Assert.Contains(i, destination);
    }

    [Fact]
    public void AddRange_NonCollection_EnumeratesSourceOnce()
    {
        var set = new SwiftHashSet<int>();
        var source = new SingleUseEnumerable<int>(new[] { 1, 2, 3 });

        set.AddRange(source);

        Assert.Equal(3, set.Count);
        Assert.Contains(1, set);
        Assert.Contains(2, set);
        Assert.Contains(3, set);
    }

    [Fact]
    public void EnsureCapacity_CapacityIsIncreased()
    {
        var set = new SwiftHashSet<int>();
        set.EnsureCapacity(1000);

        for (int i = 0; i < 1000; i++)
        {
            set.Add(i);
        }

        Assert.Equal(1000, set.Count);
    }

    [Fact]
    public void RemoveAfterResizing_ItemIsRemovedCorrectly()
    {
        var set = new SwiftHashSet<int>();

        // Add items to trigger resize
        for (int i = 0; i < 100; i++)
        {
            set.Add(i);
        }

        bool removed = set.Remove(50);

        Assert.True(removed);
        Assert.Equal(99, set.Count);
        Assert.DoesNotContain(50, set);
    }

    [Fact]
    public void Add_AfterRemoving_ItemIsAddedCorrectly()
    {
        var set = new SwiftHashSet<int> { 1, 2, 3 };
        set.Remove(2);
        bool added = set.Add(2);

        Assert.True(added);
        Assert.Equal(3, set.Count);
        Assert.Contains(2, set);
    }

    [Fact]
    public void CopyTo_ArrayIsFilledCorrectly()
    {
        var set = new SwiftHashSet<int> { 1, 2, 3 };
        var array = new int[3];
        set.CopyTo(array, 0);

        Assert.Contains(1, array);
        Assert.Contains(2, array);
        Assert.Contains(3, array);
    }

    [Fact]
    public void CopyTo_WithIndex_ArrayIsFilledCorrectly()
    {
        var set = new SwiftHashSet<int> { 1, 2, 3 };
        var array = new int[5];
        set.CopyTo(array, 2);

        Assert.Equal(0, array[0]);
        Assert.Equal(0, array[1]);
        Assert.Contains(1, array);
        Assert.Contains(2, array);
        Assert.Contains(3, array);
    }

    [Fact]
    public void Add_NullValue_NotAllowed()
    {
        var set = new SwiftHashSet<string>();
        Assert.Throws<ArgumentNullException>(() => set.Add(null));
    }

    [Fact]
    public void Remove_NullValue_RemovesSuccessfully()
    {
        var set = new SwiftHashSet<string>();
        Assert.Throws<ArgumentNullException>(() => set.Remove(null));
    }

    [Fact]
    public void Contains_NullValue_ReturnsFalse()
    {
        var set = new SwiftHashSet<string>();
        Assert.DoesNotContain(null, set);
    }

    [Fact]
    public void Enumerator_ModifyDuringEnumeration_ThrowsException()
    {
        var set = new SwiftHashSet<int> { 1, 2, 3 };
        var enumerator = set.GetEnumerator();

        enumerator.MoveNext();
        set.Add(4);

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void Add_MultipleItems_NoCollisions()
    {
        var set = new SwiftHashSet<int>();

        for (int i = 0; i < 10000; i++)
        {
            set.Add(i);
        }

        Assert.Equal(10000, set.Count);
        Assert.Contains(5000, set);
    }

    [Fact]
    public void Remove_MultipleItems_AfterResizing()
    {
        var set = new SwiftHashSet<int>();

        for (int i = 0; i < 1000; i++)
        {
            set.Add(i);
        }

        for (int i = 0; i < 1000; i += 2)
        {
            set.Remove(i);
        }

        Assert.Equal(500, set.Count);

        for (int i = 0; i < 1000; i++)
        {
            if (i % 2 == 0)
            {
                Assert.DoesNotContain(i, set);
            }
            else
            {
                Assert.Contains(i, set);
            }
        }
    }

    [Fact]
    public void EnsureCapacity_SmallerThanCurrentCapacity_NoEffect()
    {
        var set = new SwiftHashSet<int>();

        for (int i = 0; i < 50; i++)
        {
            set.Add(i);
        }

        int capacityBefore = set.Count;
        set.EnsureCapacity(10);
        int capacityAfter = set.Count;

        Assert.Equal(capacityBefore, capacityAfter);
    }

    [Fact]
    public void Add_DifferentTypesWithCustomComparer()
    {
        var comparer = StringComparer.OrdinalIgnoreCase;
        var set = new SwiftHashSet<string>(comparer);

        set.Add("hello");
        bool added = set.Add("HELLO");

        Assert.False(added);
        Assert.Single(set);

        // Set should only contain one item due to case-insensitive comparer, but still should recognize both as present
        Assert.Contains("hello", set);
        Assert.Contains("HELLO", set);
    }

    [Fact]
    public void Indexer_ReturnsStoredValueMatchingComparer()
    {
        var set = new SwiftHashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Hello"
        };

        Assert.Equal("Hello", set["hello"]);
    }

    [Fact]
    public void Indexer_MissingKey_Throws()
    {
        var set = new SwiftHashSet<int>();

        Assert.Throws<KeyNotFoundException>(() => _ = set[42]);
    }

    [Fact]
    public void Remove_ItemNotPresentAfterResizing()
    {
        var set = new SwiftHashSet<int>();

        for (int i = 0; i < 1000; i++)
        {
            set.Add(i);
        }

        set.Remove(5000); // Item not present

        Assert.Equal(1000, set.Count);
        Assert.DoesNotContain(5000, set);
    }

    [Fact]
    public void Clear_AfterAddingAndRemovingItems()
    {
        var set = new SwiftHashSet<int>();

        for (int i = 0; i < 100; i++)
        {
            set.Add(i);
        }

        for (int i = 0; i < 50; i++)
        {
            set.Remove(i);
        }

        set.Clear();

        Assert.Empty(set);
        Assert.DoesNotContain(0, set);
        Assert.DoesNotContain(99, set);
    }

    [Fact]
    public void Add_AfterClearingSet()
    {
        var set = new SwiftHashSet<int> { 1, 2, 3 };
        set.Clear();
        bool added = set.Add(1);

        Assert.True(added);
        Assert.Single(set);
        Assert.Contains(1, set);
    }

    [Fact]
    public void Enumerator_Reset_WorksCorrectly()
    {
        var set = new SwiftHashSet<int> { 1, 2, 3 };
        var enumerator = set.GetEnumerator();

        while (enumerator.MoveNext())
        {
            // Do nothing
        }

        enumerator.Reset();
        bool moved = enumerator.MoveNext();

        Assert.True(moved);
        Assert.Contains(enumerator.Current, set);
    }

    [Fact]
    public void Constructor_WithNullComparer_UsesDefault()
    {
        var set = new SwiftHashSet<int>(null)
        {
            1
        };

        Assert.Contains(1, set);
    }

    [Fact]
    public void CopyTo_NullArray_ThrowsException()
    {
        var set = new SwiftHashSet<int> { 1, 2, 3 };

        Assert.Throws<ArgumentNullException>(() => set.CopyTo(null, 0));
    }

    [Fact]
    public void CopyTo_NegativeIndex_ThrowsException()
    {
        var set = new SwiftHashSet<int> { 1, 2, 3 };
        var array = new int[3];

        Assert.Throws<ArgumentOutOfRangeException>(() => set.CopyTo(array, -1));
    }

    [Fact]
    public void CopyTo_IndexGreaterThanArrayLength_ThrowsException()
    {
        var set = new SwiftHashSet<int> { 1, 2, 3 };
        var array = new int[3];

        Assert.Throws<ArgumentOutOfRangeException>(() => set.CopyTo(array, 4));
    }

    [Fact]
    public void CopyTo_ArrayNotLargeEnough_ThrowsException()
    {
        var set = new SwiftHashSet<int> { 1, 2, 3 };
        var array = new int[2];

        Assert.Throws<InvalidOperationException>(() => set.CopyTo(array, 0));
    }

    [Fact]
    public void Add_Remove_LargeNumberOfItems()
    {
        var set = new SwiftHashSet<int>();

        // Add items
        for (int i = 0; i < 100000; i++)
        {
            set.Add(i);
        }

        Assert.Equal(100000, set.Count);

        // Remove items
        for (int i = 0; i < 100000; i += 2)
        {
            set.Remove(i);
        }

        Assert.Equal(50000, set.Count);
    }

    [Fact]
    public void Add_Remove_LargeNumberOfItems_String()
    {
        string[] randomStringArray = new string[100000];
        for (int i = 0; i < 100000; i++)
            randomStringArray[i] = Guid.NewGuid().ToString();

        var set = new SwiftHashSet<string>();

        // Add items
        for (int i = 0; i < randomStringArray.Length; i++)
            set.Add(randomStringArray[i]);

        Assert.Equal(100000, set.Count);

        // Remove items
        for (int i = 0; i < randomStringArray.Length; i += 2)
            set.Remove(randomStringArray[i]);

        Assert.Equal(50000, set.Count);

        for (int i = 0; i < 10; i++)
            set.Add(Guid.NewGuid().ToString());
    }

    [Fact]
    public void SerializationAndDeserialization_MaintainsState()
    {
        var originalSet = new SwiftHashSet<int> { 1, 2, 3, 4, 5 };

        var jsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };
        var json = JsonSerializer.SerializeToUtf8Bytes(originalSet, jsonOptions);
        var deserializedSet = JsonSerializer.Deserialize<SwiftHashSet<int>>(json, jsonOptions);

        // Validate the deserialized set
        Assert.Equal(originalSet.Count, deserializedSet.Count);
        foreach (var item in originalSet)
            Assert.Contains(item, deserializedSet);
    }

    [Fact]
    public void SwiftHashSet_MemoryPackSerialization_RoundTripMaintainsData()
    {
        var originalValue = new SwiftHashSet<int> { 1, 2, 3, 4, 5 };

        byte[] bytes = MemoryPackSerializer.Serialize(originalValue);
        SwiftHashSet<int> deserializedValue = MemoryPackSerializer.Deserialize<SwiftHashSet<int>>(bytes);

        // Check that deserialized values match the original
        Assert.Equal(originalValue.Count, deserializedValue.Count);
        Assert.Equal(originalValue, deserializedValue);
    }

    [Fact]
    public void HashSet_CustomComparer_RoundTrip()
    {
        var comparer = StringComparer.OrdinalIgnoreCase;

        var set = new SwiftHashSet<string>(comparer)
        {
            "Hello",
            "World"
        };

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(set);

        var result = JsonSerializer.Deserialize<SwiftHashSet<string>>(json);

        // default comparer now
        Assert.DoesNotContain("hello", result);
        Assert.Equal(new SwiftHashSet<string>().Comparer.GetHashCode("Hello"), result.Comparer.GetHashCode("Hello"));

        result.SetComparer(comparer);

        Assert.Contains("hello", result);
    }

    [Fact]
    public void HashSet_CustomComparer_MemoryPackRoundTrip()
    {
        var comparer = StringComparer.OrdinalIgnoreCase;

        var set = new SwiftHashSet<string>(comparer)
        {
            "Hello"
        };

        byte[] bytes = MemoryPackSerializer.Serialize(set);

        var result = MemoryPackSerializer.Deserialize<SwiftHashSet<string>>(bytes);

        Assert.DoesNotContain("hello", result);
        Assert.Equal(new SwiftHashSet<string>().Comparer.GetHashCode("Hello"), result.Comparer.GetHashCode("Hello"));

        result.SetComparer(comparer);

        Assert.Contains("hello", result);
    }

    [Fact]
    public void HashSet_SetComparer_RehashesEntriesOutsideInitialProbeSample()
    {
        var comparer = new SelectiveIntHashComparer((15, 14));
        var set = new SwiftHashSet<int>(16, comparer);

        for (int i = 0; i < 8; i++)
            set.Add(i);

        set.Add(15);

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(set);
        var result = JsonSerializer.Deserialize<SwiftHashSet<int>>(json);

        Assert.Contains(15, result);

        result.SetComparer(comparer);

        Assert.Contains(15, result);
    }

    [Fact]
    public void HashSet_IsSubsetOf_IgnoresDuplicatesInOther()
    {
        var set = new SwiftHashSet<int> { 1, 2 };

        Assert.True(set.IsSubsetOf(new[] { 1, 1, 2 }));
        Assert.True(set.IsProperSubsetOf(new[] { 1, 1, 2, 2, 3 }));
    }

    [Fact]
    public void HashSet_IsProperSupersetOf_IgnoresDuplicatesInOther()
    {
        var set = new SwiftHashSet<int> { 1, 2 };

        Assert.True(set.IsProperSupersetOf(new[] { 1, 1 }));
    }

    [Fact]
    public void HashSet_SymmetricExceptWith_Self_ClearsSet()
    {
        var set = new SwiftHashSet<int> { 1, 2, 3 };

        set.SymmetricExceptWith(set);

        Assert.Empty(set);
    }

    private sealed class SingleUseEnumerable<T> : IEnumerable<T>
    {
        private readonly IEnumerable<T> _items;
        private bool _enumerated;

        public SingleUseEnumerable(IEnumerable<T> items)
        {
            _items = items;
        }

        public IEnumerator<T> GetEnumerator()
        {
            if (_enumerated)
                throw new InvalidOperationException("The source was enumerated more than once.");

            _enumerated = true;
            return _items.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
