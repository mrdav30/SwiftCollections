using System;
using System.Collections.Generic;

#if NET48_OR_GREATER
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
#endif

#if NET8_0_OR_GREATER
using System.Text.Json;
using System.Text.Json.Serialization;
#endif

using Xunit;

namespace SwiftCollections.Tests
{
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
            Assert.Contains("hello", set);
            Assert.DoesNotContain("HELLO", set);
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

            // Serialize the set
#if NET48_OR_GREATER
            var formatter = new BinaryFormatter();
            using var memoryStream = new MemoryStream();
            formatter.Serialize(memoryStream, originalSet);

            // Deserialize the set
            memoryStream.Seek(0, SeekOrigin.Begin);
            var deserializedSet = (SwiftHashSet<int>)formatter.Deserialize(memoryStream);
#endif

#if NET8_0_OR_GREATER
            var jsonOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                ReferenceHandler = ReferenceHandler.IgnoreCycles,
                IncludeFields = true,
                IgnoreReadOnlyProperties = true
            };
            var json = JsonSerializer.SerializeToUtf8Bytes(originalSet, jsonOptions);
            var deserializedSet = JsonSerializer.Deserialize<SwiftHashSet<int>>(json, jsonOptions);
#endif

            // Validate the deserialized set
            Assert.Equal(originalSet.Count, deserializedSet.Count);
            foreach (var item in originalSet)
                Assert.Contains(item, deserializedSet);
        }
    }
}
