using FixedMathSharp;
using System.Collections.Generic;
using Xunit;

namespace SwiftCollections.Query.Tests
{
    public class SwiftBVHFixedMathSharpTypedTests
    {
        [Fact]
        public void SwiftFixedBVH_Wrapper_UsesFixedBoundVolumeEngine()
        {
            var bvh = new SwiftFixedBVH<int>(8);
            bvh.Insert(1, new FixedBoundVolume(new Vector3d(0, 0, 0), new Vector3d(1, 1, 1)));

            var results = new List<int>();
            bvh.Query(new FixedBoundVolume(new Vector3d(-1, -1, -1), new Vector3d(2, 2, 2)), results);

            Assert.Single(results);
            Assert.Contains(1, results);
        }

        [Fact]
        public void InsertAndQuery_WithFixedBoundVolume_ReturnsExpectedMatches()
        {
            var bvh = new SwiftBVH<int, FixedBoundVolume>(8);
            var first = new FixedBoundVolume(new Vector3d(0, 0, 0), new Vector3d(1, 1, 1));
            var second = new FixedBoundVolume(new Vector3d(10, 10, 10), new Vector3d(11, 11, 11));

            bvh.Insert(1, first);
            bvh.Insert(2, second);

            var results = new List<int>();
            bvh.Query(new FixedBoundVolume(new Vector3d(-1, -1, -1), new Vector3d(2, 2, 2)), results);

            Assert.Single(results);
            Assert.Contains(1, results);
        }

        [Fact]
        public void UpdateEntryBounds_WithFixedBoundVolume_UpdatesTraversalResults()
        {
            var bvh = new SwiftBVH<int, FixedBoundVolume>(8);
            var left = new FixedBoundVolume(new Vector3d(0, 0, 0), new Vector3d(1, 1, 1));
            var right = new FixedBoundVolume(new Vector3d(10, 10, 10), new Vector3d(11, 11, 11));
            var moved = new FixedBoundVolume(new Vector3d(0.5f, 0.5f, 0.5f), new Vector3d(1.5f, 1.5f, 1.5f));

            bvh.Insert(1, left);
            bvh.Insert(2, right);
            bvh.UpdateEntryBounds(2, moved);

            var results = new List<int>();
            bvh.Query(new FixedBoundVolume(new Vector3d(0, 0, 0), new Vector3d(2, 2, 2)), results);

            Assert.Equal(2, results.Count);
            Assert.Contains(1, results);
            Assert.Contains(2, results);
        }
    }
}
