using System.Collections.Generic;
using System.Numerics;
using System;
using Xunit;

namespace SwiftCollections.Query.Tests;

public class SwiftOctreeNumericsTests
{
    [Fact]
    public void NumericsWrapper_UsesBoundVolumeAdapter()
    {
        var worldBounds = new BoundVolume(new Vector3(0, 0, 0), new Vector3(16, 16, 16));
        var octree = new SwiftOctree<int>(worldBounds, new SwiftOctreeOptions(4, 2), 1f);
        octree.Insert(1, new BoundVolume(new Vector3(1, 1, 1), new Vector3(2, 2, 2)));

        var results = new List<int>();
        octree.Query(new BoundVolume(new Vector3(0, 0, 0), new Vector3(4, 4, 4)), results);

        Assert.Single(results);
        Assert.Equal(1, results[0]);
    }

    [Fact]
    public void NumericsWrapper_UpdateEntryBounds_MovesEntriesAcrossRegions()
    {
        var worldBounds = new BoundVolume(new Vector3(0, 0, 0), new Vector3(16, 16, 16));
        var octree = new SwiftOctree<int>(worldBounds, new SwiftOctreeOptions(5, 1), 1f);
        octree.Insert(1, new BoundVolume(new Vector3(1, 1, 1), new Vector3(2, 2, 2)));

        Assert.True(octree.UpdateEntryBounds(1, new BoundVolume(new Vector3(10, 10, 10), new Vector3(12, 12, 12))));

        var results = new List<int>();
        octree.Query(new BoundVolume(new Vector3(9, 9, 9), new Vector3(13, 13, 13)), results);

        Assert.Single(results);
        Assert.Equal(1, results[0]);
    }

    [Fact]
    public void NumericsWrapper_SubdividesIntoAllChildOctantsAndQueriesEachRegion()
    {
        var worldBounds = new BoundVolume(new Vector3(0, 0, 0), new Vector3(16, 16, 16));
        var octree = new SwiftOctree<int>(worldBounds, new SwiftOctreeOptions(4, 1), 1f);

        for (int childIndex = 0; childIndex < 8; childIndex++)
            Assert.True(octree.Insert(childIndex, CreateOctantVolume(childIndex)));

        Assert.True(octree.DebugRootHasChildren);
        Assert.True(octree.DebugNodeCount >= 9);

        for (int childIndex = 0; childIndex < 8; childIndex++)
        {
            var results = new List<int>();
            octree.Query(CreateOctantVolume(childIndex), results);

            Assert.Contains(childIndex, results);
        }
    }

    [Fact]
    public void NumericsWrapper_KeepsMidpointSpanningBoundsQueryableAtRoot()
    {
        var worldBounds = new BoundVolume(new Vector3(0, 0, 0), new Vector3(16, 16, 16));
        var octree = new SwiftOctree<int>(worldBounds, new SwiftOctreeOptions(4, 1), 1f);
        octree.Insert(1, new BoundVolume(new Vector3(1, 1, 1), new Vector3(2, 2, 2)));
        octree.Insert(2, new BoundVolume(new Vector3(12, 12, 12), new Vector3(13, 13, 13)));

        octree.Insert(10, new BoundVolume(new Vector3(7, 1, 1), new Vector3(9, 2, 2)));
        octree.Insert(11, new BoundVolume(new Vector3(1, 7, 1), new Vector3(2, 9, 2)));
        octree.Insert(12, new BoundVolume(new Vector3(1, 1, 7), new Vector3(2, 2, 9)));

        var results = new List<int>();
        octree.Query(new BoundVolume(new Vector3(0, 0, 0), new Vector3(10, 10, 10)), results);

        Assert.Contains(10, results);
        Assert.Contains(11, results);
        Assert.Contains(12, results);
    }

    [Fact]
    public void NumericsWrapper_MinNodeSizePreventsSubdividingSmallChildren()
    {
        var worldBounds = new BoundVolume(new Vector3(0, 0, 0), new Vector3(2, 2, 2));
        var octree = new SwiftOctree<int>(worldBounds, new SwiftOctreeOptions(4, 1), 2f);

        octree.Insert(1, new BoundVolume(new Vector3(0.1f, 0.1f, 0.1f), new Vector3(0.2f, 0.2f, 0.2f)));
        octree.Insert(2, new BoundVolume(new Vector3(1.6f, 1.6f, 1.6f), new Vector3(1.8f, 1.8f, 1.8f)));

        Assert.False(octree.DebugRootHasChildren);
        Assert.Equal(1, octree.DebugNodeCount);
    }

    [Fact]
    public void NumericsWrapper_ClearRemovesEntriesAndResetsTreeShape()
    {
        var worldBounds = new BoundVolume(new Vector3(0, 0, 0), new Vector3(16, 16, 16));
        var octree = new SwiftOctree<int>(worldBounds, new SwiftOctreeOptions(4, 1), 1f);
        octree.Insert(1, new BoundVolume(new Vector3(1, 1, 1), new Vector3(2, 2, 2)));
        octree.Insert(2, new BoundVolume(new Vector3(12, 12, 12), new Vector3(13, 13, 13)));

        octree.Clear();

        var results = new List<int>();
        octree.Query(worldBounds, results);

        Assert.Empty(results);
        Assert.Equal(0, octree.Count);
        Assert.False(octree.DebugRootHasChildren);
        Assert.True(octree.Insert(3, new BoundVolume(new Vector3(3, 3, 3), new Vector3(4, 4, 4))));
        Assert.True(octree.Contains(3));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void NumericsWrapper_RejectsInvalidMinimumNodeSize(float minNodeSize)
    {
        var worldBounds = new BoundVolume(new Vector3(0, 0, 0), new Vector3(16, 16, 16));

        Assert.Throws<ArgumentOutOfRangeException>(() => new SwiftOctree<int>(worldBounds, new SwiftOctreeOptions(4, 1), minNodeSize));
    }

    private static BoundVolume CreateOctantVolume(int childIndex)
    {
        float minX = (childIndex & 1) == 0 ? 1f : 10f;
        float minY = (childIndex & 2) == 0 ? 1f : 10f;
        float minZ = (childIndex & 4) == 0 ? 1f : 10f;

        return new BoundVolume(
            new Vector3(minX, minY, minZ),
            new Vector3(minX + 1f, minY + 1f, minZ + 1f));
    }
}
