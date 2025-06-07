using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Xunit;

namespace SwiftCollections.Tests
{
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
            Assert.Equal(4, list.Capacity);
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
        public void FastClear_ShouldResetCountWithoutReleasingElements()
        {
            var list = new SwiftList<int> { 1, 2, 3 };
            list.FastClear();
            Assert.Empty(list);
            Assert.Equal(4, list.Capacity);  // Capacity remains the same
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
#pragma warning disable xUnit2017 // Do not use Contains() to check if a value exists in a collection
            Assert.True(list.Contains(2));  // List contains 2
#pragma warning restore xUnit2017
        }

        [Fact]
        public void Contains_ShouldHandleNullForReferenceTypes()
        {
            var list = new SwiftList<string> { "a", null, "b" };
            Assert.Contains(null, list);
        }

        [Fact]
        public void Contains_ShouldReturnFalseIfItemDoesNotExist()
        {
            var list = new SwiftList<int> { 1, 2, 3 };
#pragma warning disable xUnit2017 // Do not use Contains() to check if a value exists in a collection
            Assert.False(list.Contains(4));  // List does not contain 4
#pragma warning restore xUnit2017
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
        public void Sort_ShouldWorkWithCustomComparer()
        {
            var list = new SwiftList<int> { 1, 3, 2 };
            list.Sort(Comparer<int>.Create((x, y) => y - x));  // Sort in descending order
            Assert.Equal(new[] { 3, 2, 1 }, list.ToArray());
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
            {
                originalList.Add(i);
            }

            // Serialize the SwiftList
            var formatter = new BinaryFormatter();
            using var stream = new MemoryStream();
            formatter.Serialize(stream, originalList);

            // Reset stream position and deserialize
            stream.Seek(0, SeekOrigin.Begin);
            var deserializedList = (SwiftList<int>)formatter.Deserialize(stream);

            // Verify that the deserialized list matches the original
            Assert.Equal(originalList.Count, deserializedList.Count);
            for (int i = 0; i < originalList.Count; i++)
            {
                Assert.Equal(originalList[i], deserializedList[i]);
            }
        }
    }
}
