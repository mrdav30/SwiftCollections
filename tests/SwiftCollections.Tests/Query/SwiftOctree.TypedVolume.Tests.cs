using System.Collections.Generic;
using Xunit;

namespace SwiftCollections.Query.Tests;

public class SwiftOctreeTypedVolumeTests
{
    [Fact]
    public void InsertAndQuery_WithTypedVolume_ReturnsIntersectingEntries()
    {
        var octree = CreateTypedOctree(new SwiftOctreeOptions(4, 2), 1f);
        octree.Insert(1, new TestBoundVolume(1, 1, 1, 2, 2, 2));
        octree.Insert(2, new TestBoundVolume(20, 20, 20, 22, 22, 22));

        var results = new List<int>();
        octree.Query(new TestBoundVolume(0, 0, 0, 4, 4, 4), results);

        Assert.Single(results);
        Assert.Equal(1, results[0]);
    }

    [Fact]
    public void Insert_WithExistingKey_ReplacesBoundsWithoutGrowingCount()
    {
        var octree = CreateTypedOctree(new SwiftOctreeOptions(4, 2), 1f);

        Assert.True(octree.Insert(1, new TestBoundVolume(1, 1, 1, 2, 2, 2)));
        Assert.False(octree.Insert(1, new TestBoundVolume(10, 10, 10, 12, 12, 12)));

        var results = new List<int>();
        octree.Query(new TestBoundVolume(9, 9, 9, 13, 13, 13), results);

        Assert.Single(results);
        Assert.Equal(1, octree.Count);
        Assert.Equal(1, results[0]);
    }

    [Fact]
    public void SplitThreshold_SubdividesNodesWhenCapacityIsExceeded()
    {
        var octree = CreateTypedOctree(new SwiftOctreeOptions(4, 1), 1f);
        octree.Insert(1, new TestBoundVolume(1, 1, 1, 2, 2, 2));
        octree.Insert(2, new TestBoundVolume(12, 12, 12, 13, 13, 13));

        Assert.True(octree.DebugRootHasChildren);
        Assert.True(octree.DebugNodeCount > 1);
        Assert.True(octree.DebugMaxDepth >= 1);
    }

    [Fact]
    public void MinimumNodeSize_PreventsUnboundedSubdivisionInDenseRegions()
    {
        var worldBounds = new TestBoundVolume(0, 0, 0, 1, 1, 1);
        var octree = new SwiftOctree<int, TestBoundVolume>(worldBounds, new SwiftOctreeOptions(8, 1), new TestBoundVolumeOctreePartitioner(0.5f));

        octree.Insert(1, new TestBoundVolume(0.05f, 0.05f, 0.05f, 0.10f, 0.10f, 0.10f));
        octree.Insert(2, new TestBoundVolume(0.12f, 0.12f, 0.12f, 0.18f, 0.18f, 0.18f));
        octree.Insert(3, new TestBoundVolume(0.19f, 0.19f, 0.19f, 0.24f, 0.24f, 0.24f));

        Assert.Equal(1, octree.DebugMaxDepth);
    }

    [Fact]
    public void UpdateEntryBounds_ReinsertsEntriesAcrossOctants()
    {
        var octree = CreateTypedOctree(new SwiftOctreeOptions(5, 1), 1f);
        octree.Insert(1, new TestBoundVolume(1, 1, 1, 2, 2, 2));

        Assert.True(octree.UpdateEntryBounds(1, new TestBoundVolume(24, 24, 24, 26, 26, 26)));

        var oldResults = new List<int>();
        octree.Query(new TestBoundVolume(0, 0, 0, 4, 4, 4), oldResults);

        var newResults = new List<int>();
        octree.Query(new TestBoundVolume(23, 23, 23, 27, 27, 27), newResults);

        Assert.Empty(oldResults);
        Assert.Single(newResults);
        Assert.Equal(1, newResults[0]);
    }

    [Fact]
    public void QueryAcrossOctantBoundaries_ReturnsSpanningEntriesOnlyOnce()
    {
        var octree = CreateTypedOctree(new SwiftOctreeOptions(5, 1), 1f);
        octree.Insert(1, new TestBoundVolume(15, 15, 15, 17, 17, 17));

        var results = new List<int>();
        octree.Query(new TestBoundVolume(14, 14, 14, 18, 18, 18), results);

        Assert.Single(results);
        Assert.Equal(1, results[0]);
    }

    [Fact]
    public void Remove_WithMergeEnabled_CollapsesSparseChildren()
    {
        var octree = CreateTypedOctree(new SwiftOctreeOptions(4, 1, true), 1f);
        octree.Insert(1, new TestBoundVolume(1, 1, 1, 2, 2, 2));
        octree.Insert(2, new TestBoundVolume(12, 12, 12, 13, 13, 13));

        Assert.True(octree.DebugRootHasChildren);

        Assert.True(octree.Remove(2));

        Assert.False(octree.DebugRootHasChildren);
        Assert.Equal(1, octree.DebugNodeCount);
    }

    [Fact]
    public void NonUniformDensityStress_PreservesClusterAndSparseLookups()
    {
        var octree = CreateTypedOctree(new SwiftOctreeOptions(6, 2), 0.5f);

        for (int i = 0; i < 12; i++)
        {
            float min = 1f + (i * 0.2f);
            octree.Insert(i, new TestBoundVolume(min, min, min, min + 0.1f, min + 0.1f, min + 0.1f));
        }

        octree.Insert(100, new TestBoundVolume(20, 20, 20, 22, 22, 22));
        octree.Insert(101, new TestBoundVolume(28, 2, 28, 30, 4, 30));

        var clusterResults = new List<int>();
        octree.Query(new TestBoundVolume(0, 0, 0, 5, 5, 5), clusterResults);

        var sparseResults = new List<int>();
        octree.Query(new TestBoundVolume(19, 19, 19, 23, 23, 23), sparseResults);

        Assert.Equal(12, clusterResults.Count);
        Assert.Single(sparseResults);
        Assert.Equal(100, sparseResults[0]);
    }

    private static SwiftOctree<int, TestBoundVolume> CreateTypedOctree(SwiftOctreeOptions options, float minNodeSize)
    {
        return new SwiftOctree<int, TestBoundVolume>(
            new TestBoundVolume(0, 0, 0, 32, 32, 32),
            options,
            new TestBoundVolumeOctreePartitioner(minNodeSize));
    }

    private sealed class TestBoundVolumeOctreePartitioner : IOctreeBoundsPartitioner<TestBoundVolume>
    {
        private readonly float _minNodeSize;

        public TestBoundVolumeOctreePartitioner(float minNodeSize)
        {
            _minNodeSize = minNodeSize;
        }

        public bool ContainsBounds(TestBoundVolume outer, TestBoundVolume inner)
        {
            return inner.MinX >= outer.MinX &&
                   inner.MinY >= outer.MinY &&
                   inner.MinZ >= outer.MinZ &&
                   inner.MaxX <= outer.MaxX &&
                   inner.MaxY <= outer.MaxY &&
                   inner.MaxZ <= outer.MaxZ;
        }

        public bool CanSubdivide(TestBoundVolume bounds)
        {
            float childSizeX = (bounds.MaxX - bounds.MinX) * 0.5f;
            float childSizeY = (bounds.MaxY - bounds.MinY) * 0.5f;
            float childSizeZ = (bounds.MaxZ - bounds.MinZ) * 0.5f;

            return childSizeX >= _minNodeSize &&
                   childSizeY >= _minNodeSize &&
                   childSizeZ >= _minNodeSize;
        }

        public bool TryGetContainingChildIndex(TestBoundVolume nodeBounds, TestBoundVolume entryBounds, out int childIndex)
        {
            float midX = (nodeBounds.MinX + nodeBounds.MaxX) * 0.5f;
            float midY = (nodeBounds.MinY + nodeBounds.MaxY) * 0.5f;
            float midZ = (nodeBounds.MinZ + nodeBounds.MaxZ) * 0.5f;

            int xBit;
            if (entryBounds.MinX >= midX)
                xBit = 1;
            else if (entryBounds.MaxX <= midX)
                xBit = 0;
            else
            {
                childIndex = -1;
                return false;
            }

            int yBit;
            if (entryBounds.MinY >= midY)
                yBit = 1;
            else if (entryBounds.MaxY <= midY)
                yBit = 0;
            else
            {
                childIndex = -1;
                return false;
            }

            int zBit;
            if (entryBounds.MinZ >= midZ)
                zBit = 1;
            else if (entryBounds.MaxZ <= midZ)
                zBit = 0;
            else
            {
                childIndex = -1;
                return false;
            }

            childIndex = xBit | (yBit << 1) | (zBit << 2);
            return true;
        }

        public TestBoundVolume CreateChildBounds(TestBoundVolume parentBounds, int childIndex)
        {
            float midX = (parentBounds.MinX + parentBounds.MaxX) * 0.5f;
            float midY = (parentBounds.MinY + parentBounds.MaxY) * 0.5f;
            float midZ = (parentBounds.MinZ + parentBounds.MaxZ) * 0.5f;

            bool upperX = (childIndex & 1) != 0;
            bool upperY = (childIndex & 2) != 0;
            bool upperZ = (childIndex & 4) != 0;

            return new TestBoundVolume(
                upperX ? midX : parentBounds.MinX,
                upperY ? midY : parentBounds.MinY,
                upperZ ? midZ : parentBounds.MinZ,
                upperX ? parentBounds.MaxX : midX,
                upperY ? parentBounds.MaxY : midY,
                upperZ ? parentBounds.MaxZ : midZ);
        }
    }
}
