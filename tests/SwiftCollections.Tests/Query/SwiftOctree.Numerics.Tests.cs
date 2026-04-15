using System.Collections.Generic;
using System.Numerics;
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
}
