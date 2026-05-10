using FixedMathSharp;
using System;
using System.Collections.Generic;
using Xunit;

namespace SwiftCollections.Query.Tests;

public class SwiftOctreeFixedMathSharpTypedTests
{
    [Fact]
    public void SwiftFixedOctree_Wrapper_UsesFixedBoundVolumeAdapter()
    {
        var worldBounds = new FixedBoundVolume(new Vector3d(0, 0, 0), new Vector3d(32, 32, 32));
        var octree = new SwiftFixedOctree<int>(worldBounds, new SwiftOctreeOptions(5, 1), (Fixed64)1);
        octree.Insert(1, new FixedBoundVolume(new Vector3d(1, 1, 1), new Vector3d(2, 2, 2)));

        var results = new List<int>();
        octree.Query(new FixedBoundVolume(new Vector3d(0, 0, 0), new Vector3d(4, 4, 4)), results);

        Assert.Single(results);
        Assert.Equal(1, results[0]);
    }

    [Fact]
    public void FixedOctree_UpdateEntryBounds_ReinsertsAcrossOctants()
    {
        var worldBounds = new FixedBoundVolume(new Vector3d(0, 0, 0), new Vector3d(32, 32, 32));
        var octree = new SwiftFixedOctree<int>(worldBounds, new SwiftOctreeOptions(6, 1), (Fixed64)1);
        octree.Insert(1, new FixedBoundVolume(new Vector3d(1, 1, 1), new Vector3d(2, 2, 2)));

        Assert.True(octree.UpdateEntryBounds(1, new FixedBoundVolume(new Vector3d(20, 20, 20), new Vector3d(22, 22, 22))));

        var results = new List<int>();
        octree.Query(new FixedBoundVolume(new Vector3d(19, 19, 19), new Vector3d(23, 23, 23)), results);

        Assert.Single(results);
        Assert.Equal(1, results[0]);
    }

    [Fact]
    public void FixedOctree_SubdividesIntoAllChildOctantsAndQueriesEachRegion()
    {
        var worldBounds = new FixedBoundVolume(new Vector3d(0, 0, 0), new Vector3d(16, 16, 16));
        var octree = new SwiftFixedOctree<int>(worldBounds, new SwiftOctreeOptions(4, 1), (Fixed64)1);

        for (int childIndex = 0; childIndex < 8; childIndex++)
            Assert.True(octree.Insert(childIndex, CreateOctantVolume(childIndex)));

        for (int childIndex = 0; childIndex < 8; childIndex++)
        {
            var results = new List<int>();
            octree.Query(CreateOctantVolume(childIndex), results);

            Assert.Contains(childIndex, results);
        }
    }

    [Fact]
    public void FixedOctree_KeepsMidpointSpanningBoundsQueryableAtRoot()
    {
        var worldBounds = new FixedBoundVolume(new Vector3d(0, 0, 0), new Vector3d(16, 16, 16));
        var octree = new SwiftFixedOctree<int>(worldBounds, new SwiftOctreeOptions(4, 1), (Fixed64)1);
        octree.Insert(1, new FixedBoundVolume(new Vector3d(1, 1, 1), new Vector3d(2, 2, 2)));
        octree.Insert(2, new FixedBoundVolume(new Vector3d(12, 12, 12), new Vector3d(13, 13, 13)));

        octree.Insert(10, new FixedBoundVolume(new Vector3d(7, 1, 1), new Vector3d(9, 2, 2)));
        octree.Insert(11, new FixedBoundVolume(new Vector3d(1, 7, 1), new Vector3d(2, 9, 2)));
        octree.Insert(12, new FixedBoundVolume(new Vector3d(1, 1, 7), new Vector3d(2, 2, 9)));

        var results = new List<int>();
        octree.Query(new FixedBoundVolume(new Vector3d(0, 0, 0), new Vector3d(10, 10, 10)), results);

        Assert.Contains(10, results);
        Assert.Contains(11, results);
        Assert.Contains(12, results);
    }

    [Fact]
    public void FixedOctree_MinNodeSizePreventsSubdividingSmallChildren()
    {
        var worldBounds = new FixedBoundVolume(new Vector3d(0, 0, 0), new Vector3d(2, 2, 2));
        var octree = new SwiftFixedOctree<int>(worldBounds, new SwiftOctreeOptions(4, 1), (Fixed64)2);

        octree.Insert(1, new FixedBoundVolume(new Vector3d(0, 0, 0), new Vector3d(0.25, 0.25, 0.25)));
        octree.Insert(2, new FixedBoundVolume(new Vector3d(1.5, 1.5, 1.5), new Vector3d(1.75, 1.75, 1.75)));

        var results = new List<int>();
        octree.Query(worldBounds, results);

        Assert.Equal(2, octree.Count);
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void FixedOctree_ClearRemovesEntriesAndResetsTreeShape()
    {
        var worldBounds = new FixedBoundVolume(new Vector3d(0, 0, 0), new Vector3d(16, 16, 16));
        var octree = new SwiftFixedOctree<int>(worldBounds, new SwiftOctreeOptions(4, 1), (Fixed64)1);
        octree.Insert(1, new FixedBoundVolume(new Vector3d(1, 1, 1), new Vector3d(2, 2, 2)));
        octree.Insert(2, new FixedBoundVolume(new Vector3d(12, 12, 12), new Vector3d(13, 13, 13)));

        octree.Clear();

        var results = new List<int>();
        octree.Query(worldBounds, results);

        Assert.Empty(results);
        Assert.Equal(0, octree.Count);
        Assert.True(octree.Insert(3, new FixedBoundVolume(new Vector3d(3, 3, 3), new Vector3d(4, 4, 4))));
        Assert.True(octree.Contains(3));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void FixedOctree_RejectsInvalidMinimumNodeSize(int minNodeSize)
    {
        var worldBounds = new FixedBoundVolume(new Vector3d(0, 0, 0), new Vector3d(16, 16, 16));

        Assert.Throws<ArgumentOutOfRangeException>(() => new SwiftFixedOctree<int>(worldBounds, new SwiftOctreeOptions(4, 1), (Fixed64)minNodeSize));
    }

    private static FixedBoundVolume CreateOctantVolume(int childIndex)
    {
        int minX = (childIndex & 1) == 0 ? 1 : 10;
        int minY = (childIndex & 2) == 0 ? 1 : 10;
        int minZ = (childIndex & 4) == 0 ? 1 : 10;

        return new FixedBoundVolume(
            new Vector3d(minX, minY, minZ),
            new Vector3d(minX + 1, minY + 1, minZ + 1));
    }
}
