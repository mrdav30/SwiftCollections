using System;
using System.Collections.Generic;
using FixedMathSharp;
using Xunit;

namespace SwiftCollections.Query.Tests;

public class SwiftFixedBVH2DTests
{
    [Fact]
    public void PublicLifecycle_UpsertsMovesRemovesAndReuses()
    {
        var bvh = new SwiftFixedBVH2D<int>(1);
        var first = Box(0, 0, 1, 1);
        var second = Box(10, 10, 11, 11);
        Assert.Equal(-1, bvh.RootNodeIndex);
        Assert.False(bvh.RootNode.IsAllocated);
        Assert.True(bvh.Insert(0, first));
        Assert.True(bvh.Insert(1, second));
        Assert.False(bvh.Insert(1, first));
        Assert.Equal(2, bvh.Count);
        var results = new List<int> { -1 };
        bvh.Query(first, results);
        Assert.Equal(-1, results[0]);
        Assert.Equal(3, results.Count);
        Assert.Contains(0, results);
        Assert.Contains(1, results);

        bvh.UpdateEntryBounds(1, second);
        bvh.UpdateEntryBounds(999, first);
        results.Clear();
        bvh.Query(first, results);
        Assert.Equal(new[] { 0 }, results);
        results.Clear();
        bvh.Query(second, results);
        Assert.Equal(new[] { 1 }, results);
        int oldIndex = bvh.FindEntry(1);
        Assert.True(bvh.Remove(1));
        Assert.False(bvh.Remove(1));
        Assert.Equal(-1, bvh.FindEntry(1));
        Assert.True(bvh.Insert(2, second));
        Assert.Equal(oldIndex, bvh.FindEntry(2));
        bvh.Clear();
        Assert.Equal(0, bvh.Count);
        Assert.Equal(-1, bvh.RootNodeIndex);
        Assert.True(bvh.Insert(3, default));
        results.Clear();
        bvh.Query(default, results);
        Assert.Equal(new[] { 3 }, results);
    }

    [Fact]
    public void CanonicalKeyAndBorrowedNodeStorage_KeepGenericOwnerContracts()
    {
        var bvh = new SwiftFixedBVH2D<string>(0);
        string canonical = new(new[] { 'k', 'e', 'y' });
        string equalKey = new(new[] { 'k', 'e', 'y' });
        Assert.True(bvh.Insert(canonical, default));
        var borrowed = bvh.NodePool;
        var rootCopy = bvh.RootNode;
        bvh.EnsureCapacity(32);
        Assert.NotSame(borrowed, bvh.NodePool);
        Assert.True(rootCopy.IsAllocated);
        Assert.False(bvh.Insert(equalKey, Box(2, 2, 3, 3)));
        Assert.Same(canonical, bvh.NodePool[bvh.FindEntry(equalKey)].Value);
        Assert.Equal(default, rootCopy.Bounds);
        Assert.Equal(Box(2, 2, 3, 3), bvh.RootNode.Bounds);

        Assert.Throws<ArgumentNullException>("value", () => bvh.Insert(null, default));
        Assert.Throws<ArgumentNullException>("value", () => bvh.UpdateEntryBounds(null, default));
        Assert.Throws<ArgumentNullException>("value", () => bvh.Remove(null));
        Assert.Throws<ArgumentNullException>("value", () => bvh.FindEntry(null));
        Assert.Throws<ArgumentNullException>("results", () => bvh.Query(default, null));
        Assert.Equal(1, bvh.Count);
    }

    [Fact]
    public void TryQuery_AppendsInQueryOrderAndPreflightsWithoutWrites()
    {
        var bvh = new SwiftFixedBVH2D<int>(4);
        bvh.Insert(0, Box(0, 0, 1, 1));
        bvh.Insert(1, Box(2, 2, 3, 3));
        var all = Box(-1, -1, 4, 4);
        var expected = new List<int> { -1 };
        bvh.Query(all, expected);
        var results = new SwiftList<int>(4) { -1 };
        Assert.True(bvh.TryQuery(all, results, new int[2]));
        Assert.Equal(expected, results);

        int[] shortScratch = { 37 };
        Assert.False(bvh.TryQuery(all, results, shortScratch));
        Assert.Equal(expected, results);
        Assert.Equal(37, shortScratch[0]);
        var noCapacity = new SwiftList<int>(0);
        int[] scratch = { 37, 41 };
        Assert.False(bvh.TryQuery(all, noCapacity, scratch));
        Assert.Empty(noCapacity);
        Assert.Equal(new[] { 37, 41 }, scratch);
        Assert.Throws<ArgumentNullException>("results", () => bvh.TryQuery(all, null, scratch));
        Assert.Throws<ArgumentException>("traversal", () => bvh.TryQuery(all, results, results.InnerArray));
        Assert.Throws<ArgumentException>("results", () => bvh.TryQuery(all, new DerivedList(), scratch));
        bvh.Clear();
        Assert.True(bvh.TryQuery(all, noCapacity, Array.Empty<int>()));
    }

    [Fact]
    public void FullDomainBounds_DoNotNeedRepresentableSizesOrAreas()
    {
        var bvh = new SwiftFixedBVH2D<int>(4);
        Fixed64 low = Fixed64.MinValue;
        Fixed64 high = Fixed64.MaxValue;
        var lower = new FixedBoundVolume2D(new Vector2d(low, low), new Vector2d(low + Fixed64.One, low + Fixed64.One));
        var upper = new FixedBoundVolume2D(new Vector2d(high - Fixed64.One, high - Fixed64.One), new Vector2d(high, high));
        bvh.Insert(0, lower);
        bvh.Insert(1, upper);
        bvh.Insert(2, default);
        bvh.Insert(3, Box(-1, -1, 1, 1));
        var results = new List<int>();
        bvh.Query(new FixedBoundVolume2D(new Vector2d(low, low), new Vector2d(high, high)), results);
        results.Sort();
        Assert.Equal(new[] { 0, 1, 2, 3 }, results);
        results.Clear();
        bvh.Query(lower, results);
        Assert.Equal(new[] { 0 }, results);
        bvh.UpdateEntryBounds(1, lower);
        results.Clear();
        bvh.Query(lower, results);
        results.Sort();
        Assert.Equal(new[] { 0, 1 }, results);
    }

    [Fact]
    public void Query_AgreesWithBruteForceThroughGrowthAndMovingEntries()
    {
        const int count = 128;
        var bounds = new FixedBoundVolume2D[count];
        var wrapper = new SwiftFixedBVH2D<int>(1);
        var generic = new SwiftBVH<int, FixedBoundVolume2D>(1);
        for (int i = 0; i < count; i++)
        {
            bounds[i] = Box(i % 16 * 3 - 24, i / 16 * 3 - 12, i % 16 * 3 - 23, i / 16 * 3 - 11);
            wrapper.Insert(i, bounds[i]);
            generic.Insert(i, bounds[i]);
        }
        for (int i = 0; i < count / 4; i++)
        {
            bounds[i] = Box(i % 8 - 4, i % 5 - 2, i % 8 + 2, i % 5 + 4);
            wrapper.UpdateEntryBounds(i, bounds[i]);
            generic.UpdateEntryBounds(i, bounds[i]);
        }
        for (int q = 0; q < 32; q++)
        {
            FixedBoundVolume2D query = Box(q - 20, q % 11 - 8, q - 16, q % 11 - 3);
            var actual = new List<int>();
            var repeated = new List<int>();
            wrapper.Query(query, actual);
            generic.Query(query, repeated);
            Assert.Equal(actual, repeated);
            var expected = new List<int>();
            for (int i = 0; i < count; i++)
            {
                if (query.Min.X <= bounds[i].Max.X && query.Max.X >= bounds[i].Min.X
                    && query.Min.Y <= bounds[i].Max.Y && query.Max.Y >= bounds[i].Min.Y)
                    expected.Add(i);
            }
            actual.Sort();
            Assert.Equal(expected, actual);
        }
    }

    private static FixedBoundVolume2D Box(int minX, int minY, int maxX, int maxY) =>
        new(new Vector2d(minX, minY), new Vector2d(maxX, maxY));

    private sealed class DerivedList : SwiftList<int>
    {
    }
}
