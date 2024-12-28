using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using Xunit;

namespace SwiftCollections.Query.Tests
{
    public class SwiftBVHNumericsTests
    {
        [Fact]
        public void Insert_SingleVolume_StoresCorrectly()
        {
            var bvh = new SwiftBVH<int>(10);
            var volume = new BoundVolume(new Vector3(0, 0, 0), new Vector3(1, 1, 1));

            bvh.Insert(0, volume);

            var results = new List<int>();
            bvh.Query(volume, results);

            Assert.Single(results);
            Assert.Equal(0, results[0]);
        }

        [Fact]
        public void Insert_Remove_SingleVolume_StoresCorrectly()
        {
            var bvh = new SwiftBVH<int>(10);
            var volume = new BoundVolume(new Vector3(0, 0, 0), new Vector3(1, 1, 1));

            bvh.Insert(0, volume);

            var results = new List<int>();
            bvh.Query(volume, results);

            Assert.Single(results);
            Assert.Equal(0, results[0]);

            bvh.Remove(0);

            Assert.Equal(0, bvh.Count);
        }

        [Fact]
        public void Query_NoIntersection_ReturnsEmpty()
        {
            var bvh = new SwiftBVH<int>(10);
            var volume1 = new BoundVolume(new Vector3(0, 0, 0), new Vector3(1, 1, 1));
            var volume2 = new BoundVolume(new Vector3(10, 10, 10), new Vector3(11, 11, 11));

            bvh.Insert(0, volume1);

            var results = new List<int>();
            bvh.Query(volume2, results);

            Assert.Empty(results);
        }

        [Fact]
        public void Query_OverlappingVolumes_ReturnsAllIntersectingKeys()
        {
            var bvh = new SwiftBVH<int>(10);
            var volume1 = new BoundVolume(new Vector3(0, 0, 0), new Vector3(1, 1, 1));
            var volume2 = new BoundVolume(new Vector3(0.5f, 0.5f, 0.5f), new Vector3(1.5f, 1.5f, 1.5f));

            bvh.Insert(0, volume1);
            bvh.Insert(1, volume2);

            var queryVolume = new BoundVolume(new Vector3(0.25f, 0.25f, 0.25f), new Vector3(1.25f, 1.25f, 1.25f));
            var results = new List<int>();
            bvh.Query(queryVolume, results);

            Assert.Equal(2, results.Count);
            Assert.Contains(0, results);
            Assert.Contains(1, results);
        }

        [Fact]
        public void Clear_RemovesAllNodes()
        {
            var bvh = new SwiftBVH<int>(10);
            var volume = new BoundVolume(new Vector3(0, 0, 0), new Vector3(1, 1, 1));

            bvh.Insert(0, volume);
            bvh.Clear();

            var results = new List<int>();
            bvh.Query(volume, results);

            Assert.Empty(results);
        }

        [Fact]
        public void Insert_LargeNumberOfVolumes_PerformsEfficiently()
        {
            var bvh = new SwiftBVH<int>(10000);
            int numVolumes = 1000;
            var random = new Random();

            for (int i = 0; i < numVolumes; i++)
            {
                var min = new Vector3(
                    (float)(random.NextDouble() * 100),
                    (float)(random.NextDouble() * 100),
                    (float)(random.NextDouble() * 100));
                var max = new Vector3(
                    (float)(min.X + random.NextDouble() * 10),
                    (float)(min.Y + random.NextDouble() * 10),
                    (float)(min.Z + random.NextDouble() * 10));

                bvh.Insert(i, new BoundVolume(min, max));
            }

            var queryVolume = new BoundVolume(new Vector3(50, 50, 50), new Vector3(60, 60, 60));
            var results = new List<int>();
            bvh.Query(queryVolume, results);

            Assert.True(results.Count > 0); // Validate some intersection occurs
        }

        [Fact]
        public void BoundingVolume_Union_CombinesVolumesCorrectly()
        {
            var volume1 = new BoundVolume(new Vector3(0, 0, 0), new Vector3(1, 1, 1));
            var volume2 = new BoundVolume(new Vector3(1, 1, 1), new Vector3(2, 2, 2));
            var union = volume1.Union(volume2);

            Assert.Equal(new Vector3(0, 0, 0), union.Min);
            Assert.Equal(new Vector3(2, 2, 2), union.Max);
        }

        [Fact]
        public void BoundingVolume_Intersects_ReturnsCorrectly()
        {
            var volume1 = new BoundVolume(new Vector3(0, 0, 0), new Vector3(1, 1, 1));
            var volume2 = new BoundVolume(new Vector3(0.5f, 0.5f, 0.5f), new Vector3(1.5f, 1.5f, 1.5f));
            var volume3 = new BoundVolume(new Vector3(2, 2, 2), new Vector3(3, 3, 3));

            Assert.True(volume1.Intersects(volume2));
            Assert.False(volume1.Intersects(volume3));
        }

        [Fact]
        public void Insert_IdenticalVolumes_PreservesAll()
        {
            var bvh = new SwiftBVH<int>(10);
            var volume = new BoundVolume(new Vector3(0, 0, 0), new Vector3(1, 1, 1));

            bvh.Insert(0, volume);
            bvh.Insert(1, volume);

            var results = new List<int>();
            bvh.Query(volume, results);

            Assert.Equal(2, results.Count);
            Assert.Contains(0, results);
            Assert.Contains(1, results);
        }

        [Fact]
        public void UpdateEntry_PropagatesChanges()
        {
            var bvh = new SwiftBVH<int>(10);
            var volume1 = new BoundVolume(new Vector3(0, 0, 0), new Vector3(1, 1, 1));
            var volume2 = new BoundVolume(new Vector3(2, 2, 2), new Vector3(3, 3, 3));

            bvh.Insert(0, volume1);
            bvh.Insert(1, volume2);

            // Update volume1 to overlap with volume2
            var updatedVolume1 = new BoundVolume(new Vector3(1.5f, 1.5f, 1.5f), new Vector3(2.5f, 2.5f, 2.5f));
            bvh.UpdateEntryBounds(0, updatedVolume1);

            var results = new List<int>();
            var queryVolume = new BoundVolume(new Vector3(2, 2, 2), new Vector3(3, 3, 3));
            bvh.Query(queryVolume, results);

            Assert.Equal(2, results.Count);
            Assert.Contains(0, results);
            Assert.Contains(1, results);
        }

        [Fact]
        public void ValidateParentConsistency()
        {
            var bvh = new SwiftBVH<int>(10);
            var volume1 = new BoundVolume(new Vector3(0, 0, 0), new Vector3(1, 1, 1));
            var volume2 = new BoundVolume(new Vector3(2, 2, 2), new Vector3(3, 3, 3));

            bvh.Insert(0, volume1);
            bvh.Insert(1, volume2);

            // Traverse the tree to validate parent pointers
            void ValidateParents(int nodeIndex)
            {
                SwiftBVHNode<int> node = bvh.NodePool[nodeIndex];
                if (node.LeftChildIndex != -1)
                {
                    Assert.Equal(nodeIndex, bvh.NodePool[node.LeftChildIndex].ParentIndex);
                    ValidateParents(node.LeftChildIndex);
                }

                if (node.RightChildIndex != -1)
                {
                    Assert.Equal(nodeIndex, bvh.NodePool[node.RightChildIndex].ParentIndex);
                    ValidateParents(node.RightChildIndex);
                }
            }

            ValidateParents(bvh.RootIndex);
        }

        [Fact]
        public void TreeDepth_DoesNotGrowExponentially_Linear()
        {
            var bvh = new SwiftBVH<string>(1000);
            int numVolumes = 1000;

            for (int i = 0; i < numVolumes; i++)
            {
                var volume = new BoundVolume(new Vector3(i, i, i), new Vector3(i + 1, i + 1, i + 1));
                bvh.Insert(Guid.NewGuid().ToString(), volume);
            }

            int maxDepth = 0;
            void Traverse(int nodeIndex, int depth)
            {
                if (nodeIndex == -1) return;
                maxDepth = Math.Max(maxDepth, depth);
                Traverse(bvh.NodePool[nodeIndex].LeftChildIndex, depth + 1);
                Traverse(bvh.NodePool[nodeIndex].RightChildIndex, depth + 1);
            }

            Traverse(bvh.RootIndex, 1);

            double theoreticalMaxDepth = 2.5 * (Math.Log(numVolumes) / Math.Log(2));
            Assert.True(maxDepth < theoreticalMaxDepth, "Tree depth is excessively high.");
        }

        [Fact]
        public void TreeDepth_DoesNotGrowExponentially_Random()
        {
            var bvh = new SwiftBVH<string>(1000);
            int numVolumes = 1000;

            var random = new Random();
            for (int i = 0; i < numVolumes; i++)
            {
                var volume = new BoundVolume(
                    new Vector3(random.Next(0, 100), random.Next(0, 100), random.Next(0, 100)),
                    new Vector3(random.Next(1, 10), random.Next(1, 10), random.Next(1, 10))
                );
                bvh.Insert(Guid.NewGuid().ToString(), volume);
            }

            int maxDepth = 0;
            void Traverse(int nodeIndex, int depth)
            {
                if (nodeIndex == -1) return;
                maxDepth = Math.Max(maxDepth, depth);
                Traverse(bvh.NodePool[nodeIndex].LeftChildIndex, depth + 1);
                Traverse(bvh.NodePool[nodeIndex].RightChildIndex, depth + 1);
            }

            Traverse(bvh.RootIndex, 1);

            double theoreticalMaxDepth = 2 * (Math.Log(numVolumes) / Math.Log(2));
            Assert.True(maxDepth < theoreticalMaxDepth, "Tree depth is excessively high.");
        }

        [Fact]
        public void OverlappingBoundaries_QueryReturnsAllMatchingVolumes()
        {
            var bvh = new SwiftBVH<int>(10);
            var volume = new BoundVolume(new Vector3(0, 0, 0), new Vector3(1, 1, 1));

            bvh.Insert(0, volume);
            bvh.Insert(1, volume);

            var results = new List<int>();
            bvh.Query(volume, results);

            Assert.Equal(2, results.Count);
            Assert.Contains(0, results);
            Assert.Contains(1, results);
        }

        [Fact]
        public void StressTest_ResizingIntegrity()
        {
            var bvh = new SwiftBVH<int>(10);
            int numVolumes = 10000;

            for (int i = 0; i < numVolumes; i++)
            {
                var volume = new BoundVolume(new Vector3(i, i, i), new Vector3(i + 1, i + 1, i + 1));
                bvh.Insert(i, volume);
            }

            var queryVolume = new BoundVolume(new Vector3(0, 0, 0), new Vector3(10000, 10000, 10000));
            var results = new List<int>();
            bvh.Query(queryVolume, results);

            Assert.Equal(numVolumes, results.Count);
        }


        [Fact]
        public void MultiThreaded_ConcurrentOperations()
        {
            var bvh = new SwiftBVH<int>(200);
            int numThreads = 4;
            int volumesPerThread = 50;

            var threads = new List<Thread>();
            for (int t = 0; t < numThreads; t++)
            {
                int threadIndex = t;
                threads.Add(new Thread(() =>
                {
                    for (int i = 0; i < volumesPerThread; i++)
                    {
                        int id = threadIndex * volumesPerThread + i;
                        var volume = new BoundVolume(new Vector3(id, id, id), new Vector3(id + 1, id + 1, id + 1));
                        bvh.Insert(id, volume);
                    }
                }));
            }

            foreach (var thread in threads) thread.Start();
            foreach (var thread in threads) thread.Join();

            var results = new List<int>();
            var queryVolume = new BoundVolume(new Vector3(0, 0, 0), new Vector3(200, 200, 200));
            bvh.Query(queryVolume, results);

            Assert.Equal(numThreads * volumesPerThread, results.Count);
        }

        [Fact]
        public void MultiThreaded_ConcurrentUpdatesAndQueries()
        {
            var bvh = new SwiftBVH<int>(200);
            int numThreads = 4;
            int volumesPerThread = 50;

            var threads = new List<Thread>();
            for (int t = 0; t < numThreads; t++)
            {
                int threadIndex = t;
                threads.Add(new Thread(() =>
                {
                    for (int i = 0; i < volumesPerThread; i++)
                    {
                        int id = threadIndex * volumesPerThread + i;
                        var volume = new BoundVolume(new Vector3(id, id, id), new Vector3(id + 1, id + 1, id + 1));
                        bvh.Insert(id, volume);
                        bvh.UpdateEntryBounds(id, new BoundVolume(new Vector3(id - 1, id - 1, id - 1), new Vector3(id + 2, id + 2, id + 2)));
                    }
                }));
            }

            foreach (var thread in threads) thread.Start();
            foreach (var thread in threads) thread.Join();

            var results = new List<int>();
            var queryVolume = new BoundVolume(new Vector3(0, 0, 0), new Vector3(200, 200, 200));
            bvh.Query(queryVolume, results);

            Assert.Equal(numThreads * volumesPerThread, results.Count);
        }

        [Fact]
        public void MultiThreaded_HighConcurrency()
        {
            var bvh = new SwiftBVH<int>(500);
            int numThreads = 8;
            int volumesPerThread = 100;

            var threads = new List<Thread>();
            for (int t = 0; t < numThreads; t++)
            {
                int threadIndex = t;
                threads.Add(new Thread(() =>
                {
                    for (int i = 0; i < volumesPerThread; i++)
                    {
                        int id = threadIndex * volumesPerThread + i;
                        var volume = new BoundVolume(new Vector3(id, id, id), new Vector3(id + 1, id + 1, id + 1));
                        bvh.Insert(id, volume);
                        bvh.UpdateEntryBounds(id, new BoundVolume(new Vector3(id - 1, id - 1, id - 1), new Vector3(id + 2, id + 2, id + 2)));
                        bvh.Remove(id);
                    }
                }));
            }

            foreach (var thread in threads) thread.Start();
            foreach (var thread in threads) thread.Join();

            var results = new List<int>();
            var queryVolume = new BoundVolume(new Vector3(0, 0, 0), new Vector3(500, 500, 500));
            bvh.Query(queryVolume, results);

            Assert.Empty(results);
        }

        [Fact]
        public void Query_EmptyBVH_ReturnsNoResults()
        {
            var bvh = new SwiftBVH<int>(10);
            var queryVolume = new BoundVolume(new Vector3(0, 0, 0), new Vector3(1, 1, 1));

            var results = new List<int>();
            bvh.Query(queryVolume, results);

            Assert.Empty(results);
        }

        [Fact]
        public void RapidInsertionsAndDeletions_MaintainTreeIntegrity()
        {
            var bvh = new SwiftBVH<int>(10);
            for (int i = 0; i < 50; i++)
            {
                var volume = new BoundVolume(new Vector3(i, i, i), new Vector3(i + 1, i + 1, i + 1));
                bvh.Insert(i, volume);
            }

            for (int i = 0; i < 25; i++)
                bvh.Remove(i);

            var results = new List<int>();
            var queryVolume = new BoundVolume(new Vector3(0, 0, 0), new Vector3(50, 50, 50));
            bvh.Query(queryVolume, results);

            Assert.Equal(25, results.Count);
        }

        [Fact]
        public void RapidInsertionsAndDeletions_ClearsBVH()
        {
            var bvh = new SwiftBVH<int>(10);
            for (int i = 0; i < 50; i++)
            {
                var volume = new BoundVolume(new Vector3(i, i, i), new Vector3(i + 1, i + 1, i + 1));
                bvh.Insert(i, volume);
            }

            for (int i = 50; i >= 0; i--)
                bvh.Remove(i);

            var results = new List<int>();
            var queryVolume = new BoundVolume(new Vector3(0, 0, 0), new Vector3(50, 50, 50));
            bvh.Query(queryVolume, results);

            Assert.Empty(results);
        }

        [Fact]
        public void RapidInsertionsAndDeletions_RootRemains()
        {
            var bvh = new SwiftBVH<int>(10);
            for (int i = 0; i < 50; i++)
            {
                var volume = new BoundVolume(new Vector3(i, i, i), new Vector3(i + 1, i + 1, i + 1));
                bvh.Insert(i, volume);
            }

            for (int i = 50; i >= 1; i--)
                bvh.Remove(i);

            var results = new List<int>();
            var queryVolume = new BoundVolume(new Vector3(0, 0, 0), new Vector3(50, 50, 50));
            bvh.Query(queryVolume, results);

            Assert.Equal(1, bvh.Count);
            Assert.Single(results);
        }

        [Fact]
        public void SubtreeSize_ConsistentAfterInsertions()
        {
            var bvh = new SwiftBVH<int>(10);
            for (int i = 0; i < 10; i++)
            {
                var volume = new BoundVolume(new Vector3(i, i, i), new Vector3(i + 1, i + 1, i + 1));
                bvh.Insert(i, volume);
            }

            void ValidateSubtreeSize(int nodeIndex)
            {
                var node = bvh.NodePool[nodeIndex];
                if (node.IsLeaf)
                    Assert.Equal(0, node.SubtreeSize);
                else
                {
                    int leftSize = bvh.NodePool[node.LeftChildIndex].SubtreeSize;
                    int rightSize = bvh.NodePool[node.RightChildIndex].SubtreeSize;
                    // parent should always be 1 more than children
                    Assert.Equal(1 + leftSize + rightSize, node.SubtreeSize);
                    ValidateSubtreeSize(node.LeftChildIndex);
                    ValidateSubtreeSize(node.RightChildIndex);
                }
            }

            ValidateSubtreeSize(bvh.RootIndex);
        }

        [Fact]
        public void Insert_IdenticalBoundingVolumes_ReturnsAllValues()
        {
            var bvh = new SwiftBVH<int>(10);
            var volume = new BoundVolume(new Vector3(0, 0, 0), new Vector3(1, 1, 1));

            bvh.Insert(1, volume);
            bvh.Insert(2, volume);

            var results = new List<int>();
            bvh.Query(volume, results);

            Assert.Contains(1, results);
            Assert.Contains(2, results);
        }

        [Fact]
        public void SingleNodeTree_BasicFunctionality()
        {
            var bvh = new SwiftBVH<int>(1);
            var volume = new BoundVolume(new Vector3(0, 0, 0), new Vector3(1, 1, 1));

            bvh.Insert(1, volume);

            var results = new List<int>();
            bvh.Query(volume, results);

            Assert.Single(results);
            Assert.Equal(1, results[0]);
        }

        [Fact]
        public void DuplicateKeyHandling_ReplacesOldEntry()
        {
            var bvh = new SwiftBVH<int>(10);
            var volume1 = new BoundVolume(new Vector3(0, 0, 0), new Vector3(1, 1, 1));
            var volume2 = new BoundVolume(new Vector3(2, 2, 2), new Vector3(3, 3, 3));

            bvh.Insert(1, volume1);
            bvh.Insert(1, volume2);

            var results = new List<int>();
            bvh.Query(volume2, results);

            Assert.Single(results);
            Assert.Equal(1, results[0]);
        }
    }
}
