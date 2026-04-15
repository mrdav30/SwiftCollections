using System.Collections.Generic;
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
