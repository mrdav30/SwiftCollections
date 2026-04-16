using FixedMathSharp;
using System.Collections.Generic;
using Xunit;

namespace SwiftCollections.Query.Tests;

public class SwiftSpatialHashFixedMathSharpTypedTests
{
    [Fact]
    public void SwiftFixedSpatialHash_Wrapper_UsesFixedBoundVolumeAdapter()
    {
        var hash = new SwiftFixedSpatialHash<int>(4, (Fixed64)1);
        hash.Insert(1, new FixedBoundVolume(new Vector3d(0, 0, 0), new Vector3d(1, 1, 1)));

        var results = new List<int>();
        hash.Query(new FixedBoundVolume(new Vector3d(0, 0, 0), new Vector3d(1, 1, 1)), results);

        Assert.Single(results);
        Assert.Equal(1, results[0]);
    }

    [Fact]
    public void FixedSpatialHash_DeduplicatesEntriesAcrossMultipleCells()
    {
        var hash = new SwiftFixedSpatialHash<int>(4, (Fixed64)1);
        hash.Insert(1, new FixedBoundVolume(new Vector3d(0, 0, 0), new Vector3d(3, 3, 3)));

        var results = new List<int>();
        hash.Query(new FixedBoundVolume(new Vector3d(1, 1, 1), new Vector3d(2, 2, 2)), results);

        Assert.Single(results);
        Assert.Equal(1, results[0]);
    }
}
