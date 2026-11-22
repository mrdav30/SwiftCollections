using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using FixedMathSharp;
using Xunit;

namespace SwiftCollections.Query.Tests;

public class SwiftFixedSpatialHash2DTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructors_RejectNonpositiveCellSizes(int size)
    {
        Assert.Throws<ArgumentOutOfRangeException>("cellSize", () => new SwiftFixedSpatialHash2D<int>(4, (Fixed64)size));
        Assert.Throws<ArgumentOutOfRangeException>("cellSize", () => new SwiftFixedSpatialHash2D<int>(4, (Fixed64)size, default));
    }

    [Fact]
    public void Mapping_MatchesExactRawFloorOracleAndExistingXYZMapping()
    {
        long unit = 1L << 32;
        long[] sizes = { 1, 2, unit / 2, unit - 1, unit, unit + 1, 2 * unit, 50 * unit, long.MaxValue };
        long[] coordinates = { long.MinValue, long.MinValue + 1, -50 * unit - 1, -50 * unit,
            -50 * unit + 1, -unit - 1, -unit, -unit + 1, -1, 0, 1, unit - 1, unit,
            unit + 1, 50 * unit - 1, 50 * unit, 50 * unit + 1, long.MaxValue - 1, long.MaxValue };

        foreach (long size in sizes)
        {
            var xy = new SwiftFixedSpatialHash2D<int>(1, Fixed64.FromRaw(size));
            var xyz = new SwiftFixedSpatialHash<int>(1, Fixed64.FromRaw(size));
            for (int i = 0; i < coordinates.Length; i++)
            {
                long x = coordinates[i];
                long y = coordinates[coordinates.Length - 1 - i];
                long z = coordinates[(i + 7) % coordinates.Length];
                var expected = new SwiftSpatialHashCellIndex2D(CellOracle(x, size), CellOracle(y, size));
                Assert.Equal(expected, xy.GetCellIndex(new Vector2d(Fixed64.FromRaw(x), Fixed64.FromRaw(y))));
                Assert.Equal(new SwiftSpatialHashCellIndex(expected.X, expected.Y, CellOracle(z, size)),
                    xyz.GetCellIndex(new Vector3d(Fixed64.FromRaw(x), Fixed64.FromRaw(y), Fixed64.FromRaw(z))));
            }
        }
    }

    [Fact]
    public void Mapping_MatchesSeededFullDomainOracle()
    {
        uint state = 0x51FA00D;
        for (int i = 0; i < 512; i++)
        {
            long sizeRaw = (long)(Next64(ref state) & 0x7FFF_FFFF_FFFF_FFFFUL);
            if (sizeRaw == 0) sizeRaw = 1;
            long x = unchecked((long)Next64(ref state));
            long y = unchecked((long)Next64(ref state));
            var hash = new SwiftFixedSpatialHash2D<int>(1, Fixed64.FromRaw(sizeRaw));
            Assert.Equal(new SwiftSpatialHashCellIndex2D(CellOracle(x, sizeRaw), CellOracle(y, sizeRaw)),
                hash.GetCellIndex(new Vector2d(Fixed64.FromRaw(x), Fixed64.FromRaw(y))));
        }
    }

    [Fact]
    public void DefaultOptionsAndExplicitDefault_HaveDifferentNeighborhoodPadding()
    {
        var omitted = new SwiftFixedSpatialHash2D<int>(1, Fixed64.One);
        var explicitDefault = new SwiftFixedSpatialHash2D<int>(1, Fixed64.One, default);
        Assert.Equal(1, omitted.Options.NeighborhoodPadding);
        Assert.Equal(0, explicitDefault.Options.NeighborhoodPadding);
        var adjacent = Box(1, 0, 1, 0);
        omitted.Insert(1, adjacent);
        explicitDefault.Insert(1, adjacent);
        var results = new List<int>();
        omitted.Query(default, results);
        Assert.Empty(results);
        omitted.QueryNeighborhood(default, results);
        Assert.Equal(new[] { 1 }, results);
        results.Clear();
        explicitDefault.QueryNeighborhood(default, results);
        Assert.Empty(results);
    }

    [Fact]
    public void PointCandidates_AppendCellOccupantsWithoutExactFiltering()
    {
        var hash = new SwiftFixedSpatialHash2D<int>(1, (Fixed64)10);
        hash.Insert(-1, Box(0, 0, 5, 5));
        hash.Insert(2, Box(8, 8, 9, 9));
        var results = new List<int> { -1 };
        hash.CollectPointCandidates(new Vector2d(1, 1), results);
        Assert.Equal(new[] { -1, -1, 2 }, results);
        results.Clear();
        hash.Query(Box(1, 1, 1, 1), results);
        Assert.Equal(new[] { -1 }, results);
        hash.CollectPointCandidates(new Vector2d(20, 20), results);
        Assert.Equal(new[] { -1 }, results);
        Assert.Throws<ArgumentNullException>("results", () => hash.CollectPointCandidates(Vector2d.Zero, null));
    }

    [Fact]
    public void ClampedCells_DoNotTurnBroadCandidatesIntoExactIntersections()
    {
        var hash = new SwiftFixedSpatialHash2D<int>(1, Fixed64.Epsilon);
        var maximum = new Vector2d(Fixed64.MaxValue, Fixed64.MaxValue);
        var other = new Vector2d(Fixed64.FromRaw(long.MaxValue - 1), Fixed64.FromRaw(long.MaxValue - 1));
        hash.Insert(1, new FixedBoundVolume2D(maximum, maximum));
        hash.Insert(2, new FixedBoundVolume2D(other, other));
        var results = new List<int>();
        hash.CollectPointCandidates(maximum, results);
        Assert.Equal(new[] { 1, 2 }, results);
        results.Clear();
        hash.Query(new FixedBoundVolume2D(maximum, maximum), results);
        Assert.Equal(new[] { 1 }, results);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void SignedCellEdges_CompleteMutationAndClippedNeighborhoodQueries(bool maxX, bool maxY)
    {
        var hash = new SwiftFixedSpatialHash2D<int>(1, Fixed64.One);
        var point = new Vector2d(maxX ? Fixed64.MaxValue : Fixed64.MinValue,
            maxY ? Fixed64.MaxValue : Fixed64.MinValue);
        var bounds = new FixedBoundVolume2D(point, point);
        Assert.True(hash.Insert(0, bounds));
        var results = new List<int>();
        hash.Query(bounds, results);
        Assert.Equal(new[] { 0 }, results);
        results.Clear();
        hash.QueryNeighborhood(bounds, results);
        Assert.Equal(new[] { 0 }, results);
        Assert.True(hash.UpdateEntryBounds(0, default));
        Assert.True(hash.Contains(0));
        Assert.True(hash.TryGetBounds(0, out var found));
        Assert.Equal(default, found);
        results.Clear();
        hash.Query(bounds, results);
        Assert.Empty(results);
        Assert.True(hash.Remove(0));
        Assert.False(hash.Remove(0));
        hash.QueryNeighborhood(default, results);
        Assert.Empty(results);
    }

    [Fact]
    public void FractionalRangeMapping_IncludesNegativeEndpointBoundaryTouches()
    {
        var hash = new SwiftFixedSpatialHash2D<int>(1, Fixed64.Half);
        var first = new FixedBoundVolume2D(new Vector2d(-Fixed64.Quarter, -Fixed64.Half),
            new Vector2d(Fixed64.Quarter, Fixed64.Half));
        hash.Insert(0, first);
        var edge = new FixedBoundVolume2D(new Vector2d(Fixed64.Quarter, Fixed64.Half),
            new Vector2d(Fixed64.Quarter, Fixed64.Half));
        var results = new List<int>();
        hash.Query(edge, results);
        Assert.Equal(new[] { 0 }, results);
        results.Clear();
        hash.Query(first, results);
        Assert.Equal(new[] { 0 }, results);
        Assert.True(hash.UpdateEntryBounds(0, Box(8, 8, 9, 9)));
        results.Clear();
        hash.Query(first, results);
        Assert.Empty(results);
        Assert.True(hash.Remove(0));
    }

    [Fact]
    public void PublicLifecycle_RetainsCanonicalKeysAndRejectsNullBeforeMutation()
    {
        var hash = new SwiftFixedSpatialHash2D<string>(0, Fixed64.One);
        string canonical = new(new[] { 'k', 'e', 'y' });
        string equalKey = new(new[] { 'k', 'e', 'y' });
        Assert.True(hash.Insert(canonical, default));
        Assert.False(hash.Insert(equalKey, Box(2, 2, 3, 3)));
        Assert.Equal(1, hash.Count);
        var results = new List<string>();
        hash.Query(Box(2, 2, 3, 3), results);
        Assert.Same(canonical, Assert.Single(results));
        Assert.False(hash.UpdateEntryBounds("absent", default));
        Assert.False(hash.Contains("absent"));
        Assert.False(hash.TryGetBounds("absent", out var absent));
        Assert.Equal(default, absent);
        Assert.Throws<ArgumentNullException>("key", () => hash.Insert(null, default));
        Assert.Throws<ArgumentNullException>("key", () => hash.UpdateEntryBounds(null, default));
        Assert.Throws<ArgumentNullException>("key", () => hash.Remove(null));
        Assert.Throws<ArgumentNullException>("key", () => hash.Contains(null));
        Assert.Throws<ArgumentNullException>("key", () => hash.TryGetBounds(null, out _));
        Assert.Throws<ArgumentNullException>("results", () => hash.Query(default, null));
        Assert.Throws<ArgumentNullException>("results", () => hash.QueryNeighborhood(default, null));
        Assert.Equal(1, hash.Count);
        hash.EnsureCapacity(128);
        hash.Clear();
        Assert.Equal(0, hash.Count);
        Assert.True(hash.Insert(canonical, default));
        Assert.True(hash.Remove(equalKey));
    }

    [Fact]
    public void GrowthMovesRemovalAndReuse_AgreeWithIndependentBruteForceHits()
    {
        const int count = 129;
        var hash = new SwiftFixedSpatialHash2D<int>(1, Fixed64.Two);
        var bounds = new FixedBoundVolume2D[count];
        var alive = new bool[count];
        for (int i = 0; i < count; i++)
        {
            bounds[i] = Box(i % 16 * 3 - 24, i / 16 * 3 - 12, i % 16 * 3 - 23, i / 16 * 3 - 11);
            Assert.True(hash.Insert(i * 1024, bounds[i]));
            alive[i] = true;
        }
        Verify();
        for (int i = 0; i < count; i += 4)
        {
            bounds[i] = Box(i % 8 - 4, i % 5 - 2, i % 8 + 2, i % 5 + 4);
            Assert.True(hash.UpdateEntryBounds(i * 1024, bounds[i]));
        }
        Verify();
        for (int i = 0; i < count; i += 3)
        {
            Assert.True(hash.Remove(i * 1024));
            alive[i] = false;
        }
        Verify();
        for (int i = 0; i < count; i += 3)
        {
            Assert.True(hash.Insert(i * 1024, bounds[i]));
            alive[i] = true;
        }
        Verify();
        hash.Clear();
        Assert.Equal(0, hash.Count);
        for (int i = count - 1; i >= 0; i--) Assert.True(hash.Insert(i * 1024, bounds[i]));
        Verify();

        void Verify()
        {
            int expectedCount = 0;
            foreach (bool value in alive) if (value) expectedCount++;
            Assert.Equal(expectedCount, hash.Count);
            for (int q = 0; q < 24; q++)
            {
                var query = Box(q - 20, q % 11 - 8, q - 16, q % 11 - 3);
                var actual = new List<int>();
                hash.Query(query, actual);
                var expected = new List<int>();
                for (int i = 0; i < count; i++)
                {
                    if (alive[i] && query.Min.X <= bounds[i].Max.X && query.Max.X >= bounds[i].Min.X
                        && query.Min.Y <= bounds[i].Max.Y && query.Max.Y >= bounds[i].Min.Y)
                        expected.Add(i * 1024);
                }
                actual.Sort();
                Assert.Equal(expected, actual);
            }
        }
    }

    [Fact]
    public void WarmTyped2DUpserts_AllocateNothing()
    {
        var index = new SwiftFixedSpatialHash2D<int>(16, Fixed64.One);
        var bounds = Box(0, 0, 2, 2);
        Assert.True(index.Insert(1, bounds));
        int upserts = 0;
        long allocated = MeasureWarmedAllocations(() =>
        {
            upserts = 0;
            for (int i = 0; i < 1000; i++)
                if (!index.Insert(1, bounds)) upserts++;
        });
        Assert.Equal(1000, upserts);
        Assert.Equal(1, index.Count);
        Assert.True(index.TryGetBounds(1, out var stored));
        Assert.Equal(bounds, stored);
        Assert.Equal(0L, allocated);
    }

    [Fact]
    public void WarmTyped3DUpserts_AllocateNothing()
    {
        var index = new SwiftFixedSpatialHash<int>(16, Fixed64.One);
        var bounds = new FixedBoundVolume(new Vector3d(0, 0, 0), new Vector3d(2, 2, 2));
        Assert.True(index.Insert(1, bounds));
        int upserts = 0;
        long allocated = MeasureWarmedAllocations(() =>
        {
            upserts = 0;
            for (int i = 0; i < 1000; i++)
                if (!index.Insert(1, bounds)) upserts++;
        });
        Assert.Equal(1000, upserts);
        Assert.Equal(1, index.Count);
        Assert.True(index.TryGetBounds(1, out var stored));
        Assert.Equal(bounds, stored);
        Assert.Equal(0L, allocated);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long MeasureWarmedAllocations(Action action)
    {
        action();
        long before = GC.GetAllocatedBytesForCurrentThread();
        action();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private static int CellOracle(long raw, long size)
    {
        BigInteger quotient = BigInteger.DivRem(new BigInteger(raw), new BigInteger(size), out var remainder);
        if (remainder.Sign < 0) quotient--;
        return quotient < int.MinValue ? int.MinValue : quotient > int.MaxValue ? int.MaxValue : (int)quotient;
    }

    private static ulong Next64(ref uint state) => ((ulong)Next32(ref state) << 32) | Next32(ref state);
    private static uint Next32(ref uint state)
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return state;
    }

    private static FixedBoundVolume2D Box(int minX, int minY, int maxX, int maxY) =>
        new(new Vector2d(minX, minY), new Vector2d(maxX, maxY));
}
