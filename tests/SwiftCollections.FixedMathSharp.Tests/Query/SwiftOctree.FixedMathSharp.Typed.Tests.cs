using FixedMathSharp;
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
}
