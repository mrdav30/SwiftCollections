using FluentAssertions;
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
    public class SwiftBucketTests
    {
        [Fact]
        public void Add_ShouldAddItemAndReturnIndex()
        {
            // Arrange
            var bucket = new SwiftBucket<string>();

            // Act
            int index1 = bucket.Add("Item1");
            int index2 = bucket.Add("Item2");

            // Assert
            index1.Should().Be(0);
            index2.Should().Be(1);
            bucket.Count.Should().Be(2);
            bucket[index1].Should().Be("Item1");
            bucket[index2].Should().Be("Item2");
        }

        [Fact]
        public void RemoveAt_ShouldRemoveItemAtIndex()
        {
            // Arrange
            var bucket = new SwiftBucket<int>();
            int index = bucket.Add(42);

            // Act
            bucket.TryRemoveAt(index);

            // Assert
            bucket.Count.Should().Be(0);
            Action act = () => { var item = bucket[index]; };
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void Remove_ShouldRemoveItemByValue()
        {
            // Arrange
            var bucket = new SwiftBucket<string>();
            bucket.Add("Item1");
            bucket.Add("Item2");

            // Act
            bool removed = bucket.Remove("Item1");

            // Assert
            removed.Should().BeTrue();
            bucket.Count.Should().Be(1);
            bucket.Contains("Item1").Should().BeFalse();
            bucket.Contains("Item2").Should().BeTrue();
        }

        [Fact]
        public void Indexer_GetAndSet_ShouldAccessAndModifyItem()
        {
            // Arrange
            var bucket = new SwiftBucket<int>();
            int index = bucket.Add(10);

            // Act
            int value = bucket[index];
            bucket[index] = 20;

            // Assert
            value.Should().Be(10);
            bucket[index].Should().Be(20);
        }

        [Fact]
        public void GetEnumerator_ShouldEnumerateAllItems()
        {
            // Arrange
            var bucket = new SwiftBucket<string>();
            bucket.Add("Item1");
            bucket.Add("Item2");
            bucket.Add("Item3");

            // Act
            var items = new List<string>();
            foreach (var item in bucket)
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
            var bucket = new SwiftBucket<int>();
            bucket.Add(1);
            bucket.Add(2);

            // Act
            bool removed = bucket.Remove(3);

            // Assert
            removed.Should().BeFalse();
            bucket.Count.Should().Be(2);
        }

        [Fact]
        public void Indexer_InvalidIndex_ShouldThrowException()
        {
            // Arrange
            var bucket = new SwiftBucket<string>();
            bucket.Add("Item1");

            // Act
            Action act = () => { var item = bucket[5]; };

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void InsertAt_ShouldInsertItemAtSpecifiedIndex()
        {
            // Arrange
            var bucket = new SwiftBucket<string>();
            bucket.Add("Item0"); // Index 0
            bucket.Add("Item1"); // Index 1

            // Act
            bucket.InsertAt(5, "Item5");

            // Assert
            bucket[5].Should().Be("Item5");
            bucket.Count.Should().Be(3);
            bucket.Contains("Item5").Should().BeTrue();
        }

        [Fact]
        public void InsertAt_ExistingIndex_ShouldReplaceItem()
        {
            // Arrange
            var bucket = new SwiftBucket<int>();
            bucket.Add(10); // Index 0

            // Act
            bucket.InsertAt(0, 20);

            // Assert
            bucket[0].Should().Be(20);
            bucket.Count.Should().Be(1);
        }

        [Fact]
        public void Clear_ShouldRemoveAllItems()
        {
            // Arrange
            var bucket = new SwiftBucket<int>();
            bucket.Add(1);
            bucket.Add(2);
            bucket.Add(3);

            // Act
            bucket.Clear();

            // Assert
            bucket.Count.Should().Be(0);
            Action act = () => { var item = bucket[0]; };
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void Add_ShouldExpandCapacityWhenNeeded()
        {
            // Arrange
            var bucket = new SwiftBucket<int>(8)
            {
                3,4,5,6,7,8
            };

            // Act
            bucket.Add(1);
            bucket.Add(2);
            int capacityBefore = bucket.Capacity;
            bucket.Add(3); // Should trigger capacity expansion
            int capacityAfter = bucket.Capacity;

            // Assert
            capacityAfter.Should().BeGreaterThan(capacityBefore);
            bucket.Count.Should().Be(9);
        }

        [Fact]
        public void Add_ShouldReuseFreeIndices()
        {
            // Arrange
            var bucket = new SwiftBucket<string>();
            int index1 = bucket.Add("Item1");
            int index2 = bucket.Add("Item2");

            // Act
            bucket.TryRemoveAt(index1);
            int index3 = bucket.Add("Item3");

            // Assert
            index3.Should().Be(index1); // Should reuse the free index
            bucket.Count.Should().Be(2);
            bucket[index3].Should().Be("Item3");
        }

        [Fact]
        public void Contains_ShouldReturnTrueIfItemExists()
        {
            // Arrange
            var bucket = new SwiftBucket<string>();
            bucket.Add("Item1");
            bucket.Add("Item2");

            // Act & Assert
            bucket.Contains("Item1").Should().BeTrue();
            bucket.Contains("Item3").Should().BeFalse();
        }

        [Fact]
        public void CopyTo_ShouldCopyItemsToArray()
        {
            // Arrange
            var bucket = new SwiftBucket<int>();
            bucket.Add(1);
            bucket.Add(2);
            bucket.Add(3);
            int[] array = new int[3];

            // Act
            bucket.CopyTo(array, 0);

            // Assert
            array.Should().Contain(new[] { 1, 2, 3 });
        }

        [Fact]
        public void Enumerator_Reset_ShouldRestartEnumeration()
        {
            // Arrange
            var bucket = new SwiftBucket<string>();
            bucket.Add("Item1");
            bucket.Add("Item2");
            var enumerator = bucket.GetEnumerator();

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
            var bucket = new SwiftBucket<int>();

            // Act
            bool removed = bucket.TryRemoveAt(5);

            // Assert
            removed.Should().BeFalse();
        }

        [Fact]
        public void Enumeration_ShouldThrowWhenBucketModified()
        {
            // Arrange
            var bucket = new SwiftBucket<int>();
            for (int i = 0; i < 5; i++)
                bucket.Add(i);

            // Act
            Action act = () =>
            {
                var items = new List<int>();
                foreach (var item in bucket)
                {
                    items.Add(item);
                    if (item == 2)
                        bucket.Add(5); // Modify the bucket during enumeration
                }
            };
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Capacity_ShouldReflectInternalArraySize()
        {
            // Arrange
            var bucket = new SwiftBucket<int>(5);

            // Act
            int initialCapacity = bucket.Capacity;
            bucket.Add(1);
            bucket.Add(2);
            bucket.Add(3);
            bucket.Add(4);
            bucket.Add(5);
            bucket.Add(6);
            bucket.Add(7);
            bucket.Add(8);
            bucket.Add(9);// Should trigger capacity increase
            int updatedCapacity = bucket.Capacity;

            // Assert
            initialCapacity.Should().Be(8);
            updatedCapacity.Should().BeGreaterThan(initialCapacity);
        }

        [Fact]
        public void IsAllocated_ShouldCheckIfItemIsPresent()
        {
            // Adding items and storing their indices
            var bucket = new SwiftBucket<string>();
            int indexA = bucket.Add("Item A");
            int indexB = bucket.Add("Item B");

            string itemA = string.Empty;
            if (bucket.IsAllocated(indexA))
                itemA = bucket[indexA];

            itemA.Should().BeSameAs(bucket[0]);

            // Removing an item
            bucket.TryRemoveAt(indexA);

            // Checking allocation after removal
            bucket.IsAllocated(indexA).Should().BeFalse();
        }

        [Fact]
        public void Add_Remove_LargeNumberOfItems()
        {
            var set = new SwiftBucket<int>();

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
        public void SwiftBucket_Serialization_RoundTripMaintainsData()
        {
            var originalBucket = new SwiftBucket<int>();
            for (int i = 0; i < 100; i++)
            {
                originalBucket.Add(i);
            }

            // Serialize the SwiftBucket
#if NET48_OR_GREATER
            var formatter = new BinaryFormatter();
            using var stream = new MemoryStream();
            formatter.Serialize(stream, originalBucket);

            // Reset stream position and deserialize
            stream.Seek(0, SeekOrigin.Begin);
            var deserializedBucket = (SwiftBucket<int>)formatter.Deserialize(stream);
#endif

#if NET8_0_OR_GREATER
            var jsonOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                ReferenceHandler = ReferenceHandler.IgnoreCycles,
                IncludeFields = true,
                IgnoreReadOnlyProperties = true
            };
            var json = JsonSerializer.SerializeToUtf8Bytes(originalBucket, jsonOptions);
            var deserializedBucket = JsonSerializer.Deserialize<SwiftBucket<int>>(json, jsonOptions);
#endif

            // Verify that the deserialized list matches the original
            Assert.Equal(originalBucket.Count, deserializedBucket.Count);
            for (int i = 0; i < originalBucket.Count; i++)
            {
                Assert.Equal(originalBucket[i], deserializedBucket[i]);
            }
        }
    }
}