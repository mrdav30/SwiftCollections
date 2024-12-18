using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Xunit;

namespace SwiftCollections.Tests
{
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
            Comparison<int> comparison = (x, y) => y.CompareTo(x);
            IComparer<int> comparer = Comparer<int>.Create(comparison);
            var sorter = new SwiftSortedList<int>(comparer);
            Assert.Equal(comparer, sorter.Comparer);
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

        [Fact]        public void PeekMax_OnEmpty_ShouldThrowException()
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
            Assert.Contains(10, sorter);
        }

        [Fact]
        public void Contains_ElementDoesNotExist_ShouldReturnFalse()
        {
            var sorter = new SwiftSortedList<int>();
            sorter.Add(10);
            sorter.Add(5);
            Assert.DoesNotContain(15, sorter);
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

        #endregion

        #region Test: Serialization

        [Fact]
        public void SerializeAndDeserialize_ShouldMaintainDataIntegrity()
        {
            var sorter = new SwiftSortedList<int>();
            sorter.AddRange(new List<int> { 5, 10, 15 });

            var formatter = new BinaryFormatter();
            using var stream = new MemoryStream();
            formatter.Serialize(stream, sorter);

            stream.Seek(0, SeekOrigin.Begin);
            var deserializedSorter = (SwiftSortedList<int>)formatter.Deserialize(stream);

            Assert.Equal(sorter.Count, deserializedSorter.Count);
            Assert.Equal(sorter.PeekMin(), deserializedSorter.PeekMin());
            Assert.Equal(sorter.PeekMax(), deserializedSorter.PeekMax());
        }

        #endregion
    }
}
