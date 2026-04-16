using System.Collections.Generic;
using System.Numerics;
using Xunit;

namespace SwiftCollections.Query.Tests;

public class SwiftSpatialHashNumericsTests
{
    [Fact]
    public void NumericsWrapper_UsesBoundVolumeAdapter()
    {
        var hash = new SwiftSpatialHash<int>(4, 1f);
        hash.Insert(1, new BoundVolume(new Vector3(0, 0, 0), new Vector3(1, 1, 1)));

        var results = new List<int>();
        hash.Query(new BoundVolume(new Vector3(0, 0, 0), new Vector3(1, 1, 1)), results);

        Assert.Single(results);
        Assert.Equal(1, results[0]);
    }

    [Fact]
    public void NumericsWrapper_QueryNeighborhood_FindsAdjacentCells()
    {
        var hash = new SwiftSpatialHash<int>(4, 1f, new SwiftSpatialHashOptions(1));
        hash.Insert(1, new BoundVolume(new Vector3(0f, 0f, 0f), new Vector3(0.25f, 0.25f, 0.25f)));
        hash.Insert(2, new BoundVolume(new Vector3(1.1f, 0f, 0f), new Vector3(1.25f, 0.25f, 0.25f)));

        var results = new List<int>();
        hash.QueryNeighborhood(new BoundVolume(new Vector3(0f, 0f, 0f), new Vector3(0.25f, 0.25f, 0.25f)), results);

        Assert.Equal(2, results.Count);
        Assert.Contains(1, results);
        Assert.Contains(2, results);
    }
}
