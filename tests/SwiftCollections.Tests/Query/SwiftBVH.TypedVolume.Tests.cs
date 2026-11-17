using System;
using System.Collections.Generic;
using SwiftCollections.Observable;
using Xunit;

namespace SwiftCollections.Query.Tests
{
    public class SwiftBVHTypedVolumeTests
    {
        [Fact]
        public void InsertAndQuery_WithCustomTypedVolume_ReturnsExpectedMatches()
        {
            var bvh = new SwiftBVH<int, TestBoundVolume>(8);
            var first = new TestBoundVolume(0, 0, 0, 1, 1, 1);
            var second = new TestBoundVolume(10, 10, 10, 11, 11, 11);
            var query = new TestBoundVolume(-1, -1, -1, 2, 2, 2);

            bvh.Insert(1, first);
            bvh.Insert(2, second);

            var results = new List<int>();
            bvh.Query(query, results);

            Assert.Single(results);
            Assert.Contains(1, results);
        }

        [Fact]
        public void TryQueryPreservesOrderAndLeavesResultsUnchangedOnShortStorage()
        {
            var bvh = new SwiftBVH<int, TestBoundVolume>(8);
            var bounds = new TestBoundVolume(0, 0, 0, 1, 1, 1);
            bvh.Insert(1, bounds);
            bvh.Insert(2, bounds);
            bvh.Insert(3, bounds);
            bvh.Remove(2);

            var expected = new List<int> { 7 };
            bvh.Query(bounds, expected);
            var results = new SwiftList<int>(3) { 7 };
            Assert.False(bvh.TryQuery(bounds, results, new int[1]));
            Assert.Equal(new[] { 7 }, results);
            var shortResults = new SwiftList<int>(2);
            for (int i = 0; i < shortResults.Capacity - 1; i++)
                shortResults.Add(7);
            int previousCount = shortResults.Count;
            Assert.False(bvh.TryQuery(bounds, shortResults, new int[2]));
            Assert.Equal(previousCount, shortResults.Count);
            Assert.All(shortResults, item => Assert.Equal(7, item));
            Assert.True(bvh.TryQuery(bounds, results, new int[2]));
            Assert.Equal(expected, results);
        }

        [Fact]
        public void TryQueryWithEmptyTreeCompletesWithNoStorage()
        {
            var bvh = new SwiftBVH<string, TestBoundVolume>(8);
            var bounds = new TestBoundVolume(0, 0, 0, 1, 1, 1);
            var results = new SwiftList<string>();
            Assert.True(bvh.TryQuery(bounds, results, Span<int>.Empty));
            Assert.Empty(results);
        }

        [Fact]
        public void TryQueryRejectsOverlappingAndCallbackBackedStorage()
        {
            var bvh = new SwiftBVH<int, TestBoundVolume>(8);
            var bounds = new TestBoundVolume(0, 0, 0, 1, 1, 1);
            var results = new SwiftList<int>(2);
            Assert.Throws<ArgumentException>("traversal", () => bvh.TryQuery(bounds, results, results.InnerArray.AsSpan(1)));
            var observable = new SwiftObservableList<int>(2);
            Assert.Throws<ArgumentException>("results", () => bvh.TryQuery(bounds, observable, new int[2]));
        }

        [Fact]
        public void UpdateEntryBounds_WithCustomTypedVolume_UpdatesTraversalResults()
        {
            var bvh = new SwiftBVH<int, TestBoundVolume>(8);
            var first = new TestBoundVolume(0, 0, 0, 1, 1, 1);
            var second = new TestBoundVolume(10, 10, 10, 11, 11, 11);
            var movedSecond = new TestBoundVolume(0.5f, 0.5f, 0.5f, 1.5f, 1.5f, 1.5f);

            bvh.Insert(1, first);
            bvh.Insert(2, second);
            bvh.UpdateEntryBounds(2, movedSecond);

            var results = new List<int>();
            bvh.Query(new TestBoundVolume(0, 0, 0, 2, 2, 2), results);

            Assert.Equal(2, results.Count);
            Assert.Contains(1, results);
            Assert.Contains(2, results);
        }
    }
}
