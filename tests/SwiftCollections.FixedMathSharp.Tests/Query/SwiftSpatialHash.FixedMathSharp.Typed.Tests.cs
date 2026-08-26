using System;
using System.Collections.Generic;
using FixedMathSharp;
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

    [Fact]
    public void FixedSpatialHash_CollectPointCandidates_ReturnsEveryEntryFromTheMappedCell()
    {
        var hash = new SwiftFixedSpatialHash<int>(4, (Fixed64)10);
        hash.Insert(1, new FixedBoundVolume(new Vector3d(0, 0, 0), new Vector3d(5, 5, 5)));
        hash.Insert(2, new FixedBoundVolume(new Vector3d(8, 8, 8), new Vector3d(9, 9, 9)));

        var results = new List<int>();
        hash.CollectPointCandidates(new Vector3d(1, 1, 1), results);
        Assert.Equal(new[] { 1, 2 }, results);

        results.Clear();
        hash.CollectPointCandidates(new Vector3d(20, 20, 20), results);
        Assert.Empty(results);
    }

    [Fact]
    public void FixedSpatialHash_CollectPointCandidates_RejectsNullResults()
    {
        var hash = new SwiftFixedSpatialHash<int>(4, Fixed64.One);

        Assert.Throws<ArgumentNullException>(() => hash.CollectPointCandidates(Vector3d.Zero, null!));
    }

    [Fact]
    public void FixedSpatialHash_CollectPointCandidates_FloorsNegativeFractionalCells()
    {
        var hash = new SwiftFixedSpatialHash<int>(4, Fixed64.Two);
        Fixed64 coordinate = Fixed64.FromRaw(-1L);
        var point = new Vector3d(coordinate, coordinate, coordinate);
        hash.Insert(1, new FixedBoundVolume(point, point));
        var results = new List<int>();

        hash.CollectPointCandidates(point, results);

        Assert.Equal(new[] { 1 }, results);
    }

    [Fact]
    public void FixedSpatialHash_GetCellIndex_UsesExactRawFloorAtBoundary()
    {
        var hash = new SwiftFixedSpatialHash<int>(4, (Fixed64)50);
        Fixed64 coordinate = Fixed64.FromRaw(((long)50 << 32) - 1L);

        SwiftSpatialHashCellIndex cell = hash.GetCellIndex(
            new Vector3d(coordinate, coordinate, coordinate));

        Assert.Equal(new SwiftSpatialHashCellIndex(0, 0, 0), cell);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FixedSpatialHash_CollectPointCandidates_SupportsFractionalCellSizes(bool negative)
    {
        var hash = new SwiftFixedSpatialHash<int>(4, Fixed64.Half);
        Fixed64 coordinate = negative ? -Fixed64.Quarter : Fixed64.Quarter;
        var point = new Vector3d(coordinate, coordinate, coordinate);
        hash.Insert(1, new FixedBoundVolume(point, point));
        var results = new List<int>();

        hash.CollectPointCandidates(point, results);

        Assert.Equal(new[] { 1 }, results);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FixedSpatialHash_CollectPointCandidates_ClampsCellCoordinatesToTheSignedDomain(bool maximum)
    {
        var hash = new SwiftFixedSpatialHash<int>(4, Fixed64.Epsilon);
        Fixed64 coordinate = maximum ? Fixed64.MaxValue : Fixed64.MinValue;
        var point = new Vector3d(coordinate, coordinate, coordinate);
        hash.Insert(1, new FixedBoundVolume(point, point));
        var results = new List<int>();

        hash.CollectPointCandidates(point, results);

        Assert.Equal(new[] { 1 }, results);
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
