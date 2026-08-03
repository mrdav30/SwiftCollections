using FixedMathSharp;
using System;
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FixedSpatialHash_AtSignedCellBoundary_ShouldCompleteInsertQueryAndRemove(bool maximum)
    {
        var hash = new SwiftFixedSpatialHash<int>(4, Fixed64.One);
        Fixed64 coordinate = maximum ? Fixed64.MaxValue : Fixed64.MinValue;
        var bounds = new FixedBoundVolume(
            new Vector3d(coordinate, coordinate, coordinate),
            new Vector3d(coordinate, coordinate, coordinate));
        var results = new List<int>();

        Assert.True(hash.Insert(1, bounds));
        hash.Query(bounds, results);
        Assert.Equal(new[] { 1 }, results);

        results.Clear();
        hash.QueryNeighborhood(bounds, results);
        Assert.Equal(new[] { 1 }, results);

        Assert.True(hash.Remove(1));
        results.Clear();
        hash.Query(bounds, results);
        Assert.Empty(results);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SwiftFixedSpatialHash_RejectsInvalidCellSizes(int cellSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SwiftFixedSpatialHash<int>(4, (Fixed64)cellSize));
    }
}
