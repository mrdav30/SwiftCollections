using System.Collections.Generic;
using Xunit;

namespace SwiftCollections.Query.Tests;

public class SwiftSpatialHashTypedVolumeTests
{
    private static readonly ISpatialHashCellMapper<TestBoundVolume> s_unitCellMapper = new TestBoundVolumeCellMapper(1f);

    [Fact]
    public void InsertAndQuery_WithTypedVolume_ReturnsIntersectingEntries()
    {
        var hash = new SwiftSpatialHash<int, TestBoundVolume>(4, s_unitCellMapper);

        hash.Insert(1, new TestBoundVolume(0, 0, 0, 1, 1, 1));
        hash.Insert(2, new TestBoundVolume(10, 10, 10, 11, 11, 11));

        var results = new List<int>();
        hash.Query(new TestBoundVolume(0, 0, 0, 2, 2, 2), results);

        Assert.Single(results);
        Assert.Contains(1, results);
    }

    [Fact]
    public void UpdateEntryBounds_RelocatesExistingEntriesAcrossCells()
    {
        var hash = new SwiftSpatialHash<int, TestBoundVolume>(4, s_unitCellMapper);
        hash.Insert(1, new TestBoundVolume(0, 0, 0, 1, 1, 1));

        Assert.True(hash.UpdateEntryBounds(1, new TestBoundVolume(5, 5, 5, 6, 6, 6)));

        var results = new List<int>();
        hash.Query(new TestBoundVolume(4, 4, 4, 7, 7, 7), results);

        Assert.Single(results);
        Assert.Equal(1, results[0]);
    }

    [Fact]
    public void Remove_DeletesEntriesFromAllOccupiedCells()
    {
        var hash = new SwiftSpatialHash<int, TestBoundVolume>(4, s_unitCellMapper);
        hash.Insert(1, new TestBoundVolume(-1, -1, -1, 1, 1, 1));

        Assert.True(hash.Remove(1));

        var results = new List<int>();
        hash.Query(new TestBoundVolume(-2, -2, -2, 2, 2, 2), results);

        Assert.Empty(results);
        Assert.Equal(0, hash.Count);
    }

    [Fact]
    public void Query_DeduplicatesLargeEntriesSpanningMultipleCells()
    {
        var hash = new SwiftSpatialHash<int, TestBoundVolume>(4, s_unitCellMapper);
        hash.Insert(1, new TestBoundVolume(0, 0, 0, 3, 3, 3));

        var results = new List<int>();
        hash.Query(new TestBoundVolume(1, 1, 1, 2, 2, 2), results);

        Assert.Single(results);
        Assert.Equal(1, results[0]);
    }

    [Fact]
    public void QueryNeighborhood_IncludesNeighboringCellsWithoutExactIntersectionFiltering()
    {
        var hash = new SwiftSpatialHash<int, TestBoundVolume>(4, s_unitCellMapper, new SwiftSpatialHashOptions(1));
        hash.Insert(1, new TestBoundVolume(0, 0, 0, 0.25f, 0.25f, 0.25f));
        hash.Insert(2, new TestBoundVolume(1.2f, 0, 0, 1.4f, 0.25f, 0.25f));

        var results = new List<int>();
        hash.QueryNeighborhood(new TestBoundVolume(0, 0, 0, 0.25f, 0.25f, 0.25f), results);

        Assert.Equal(2, results.Count);
        Assert.Contains(1, results);
        Assert.Contains(2, results);
    }

    [Fact]
    public void Query_FiltersFalsePositivesWhenEntriesShareQueriedCells()
    {
        var hash = new SwiftSpatialHash<int, TestBoundVolume>(4, s_unitCellMapper);
        hash.Insert(1, new TestBoundVolume(0f, 0f, 0f, 0.2f, 0.2f, 0.2f));
        hash.Insert(2, new TestBoundVolume(0.8f, 0.8f, 0.8f, 0.95f, 0.95f, 0.95f));

        var results = new List<int>();
        hash.Query(new TestBoundVolume(0f, 0f, 0f, 0.3f, 0.3f, 0.3f), results);

        Assert.Single(results);
        Assert.Equal(1, results[0]);
    }

    [Fact]
    public void Query_HandlesNegativeCellCoordinatesDeterministically()
    {
        var hash = new SwiftSpatialHash<int, TestBoundVolume>(4, s_unitCellMapper);
        hash.Insert(1, new TestBoundVolume(-2.75f, -0.5f, -1.25f, -1.1f, 0.25f, -0.1f));

        var results = new List<int>();
        hash.Query(new TestBoundVolume(-3f, -1f, -2f, -1f, 1f, 0f), results);

        Assert.Single(results);
        Assert.Equal(1, results[0]);
    }

    [Fact]
    public void Insert_WithExistingKey_ReplacesBoundsWithoutGrowingCount()
    {
        var hash = new SwiftSpatialHash<int, TestBoundVolume>(4, s_unitCellMapper);
        Assert.True(hash.Insert(1, new TestBoundVolume(0, 0, 0, 1, 1, 1)));
        Assert.False(hash.Insert(1, new TestBoundVolume(8, 8, 8, 9, 9, 9)));

        var results = new List<int>();
        hash.Query(new TestBoundVolume(7, 7, 7, 10, 10, 10), results);

        Assert.Single(results);
        Assert.Equal(1, hash.Count);
        Assert.Equal(1, results[0]);
    }

    [Fact]
    public void CollisionHeavyScenarios_PreserveAllLookups()
    {
        var hash = new SwiftSpatialHash<int, TestBoundVolume>(2, s_unitCellMapper);

        for (int i = 0; i < 32; i++)
            hash.Insert(i, new TestBoundVolume(0, 0, 0, 0.25f, 0.25f, 0.25f));

        var results = new List<int>();
        hash.Query(new TestBoundVolume(0, 0, 0, 1, 1, 1), results);

        Assert.Equal(32, results.Count);
    }

    private sealed class TestBoundVolumeCellMapper : ISpatialHashCellMapper<TestBoundVolume>
    {
        private readonly float _cellSize;

        public TestBoundVolumeCellMapper(float cellSize)
        {
            _cellSize = cellSize;
        }

        public void GetCellRange(TestBoundVolume bounds, out SwiftSpatialHashCellIndex minCell, out SwiftSpatialHashCellIndex maxCell)
        {
            minCell = new SwiftSpatialHashCellIndex(ToCell(bounds.MinX), ToCell(bounds.MinY), ToCell(bounds.MinZ));
            maxCell = new SwiftSpatialHashCellIndex(ToCell(bounds.MaxX), ToCell(bounds.MaxY), ToCell(bounds.MaxZ));
        }

        private int ToCell(float value) => (int)System.MathF.Floor(value / _cellSize);
    }
}
