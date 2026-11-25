using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;
using System.Threading;
using SwiftCollections.Observable;
using Xunit;

namespace SwiftCollections.Query.Tests;

public class SwiftBVH2DTests
{
    [Fact]
    public void Query_AfterResultCallbackThrows_DiscardsPendingTraversalAcrossReuse()
    {
        var bvh = new SwiftBVH2D<int>(8);
        for (int i = 1; i <= 4; i++) bvh.Insert(i, default);
        var throwing = new ThrowOnSecondAdd();

        Assert.Throws<InvalidOperationException>(() => bvh.Query(default, throwing));
        Assert.Single(throwing);
        Assert.InRange(throwing[0], 1, 4);

        var results = new SwiftList<int>(4);
        bvh.Query(default, results);
        results.SortInPlace();
        Assert.Equal(new[] { 1, 2, 3, 4 }, results);

        bvh.Clear();
        var distant = new BoundVolume2D(new Vector2(10), new Vector2(11));
        bvh.Insert(99, distant);
        results.FastClear();
        bvh.Query(default, results);
        Assert.Empty(results);
        bvh.Query(distant, results);
        Assert.Equal(new[] { 99 }, results);

        Assert.True(bvh.Remove(99));
        bvh.Insert(100, default);
        results.FastClear();
        bvh.Query(default, results);
        Assert.Equal(new[] { 100 }, results);
    }

    private sealed class ThrowOnSecondAdd : Collection<int>
    {
        protected override void InsertItem(int index, int item)
        {
            if (Count == 1) throw new InvalidOperationException("Stop with pending BVH nodes.");
            base.InsertItem(index, item);
        }
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(129, false)]
    [InlineData(129, true)]
    public void Query_FreshInstanceUsesReservedScratch(int count, bool allHits)
    {
        WarmQueryOnAnotherInstance();
        var bvh = new SwiftBVH2D<int>(2 * count);
        for (int i = 0; i < count; i++)
            bvh.Insert(i, new BoundVolume2D(new Vector2(2 * i), new Vector2(2 * i + 1)));
        var results = new SwiftList<int>(allHits ? count : 1);
        var query = allHits
            ? new BoundVolume2D(Vector2.Zero, new Vector2(2 * count))
            : new BoundVolume2D(Vector2.Zero, Vector2.One);

        long allocated = MeasureQueryAllocation(bvh, query, results);

        Assert.Equal(0, allocated);
        Assert.Equal(allHits ? count : 1, results.Count);
        if (allHits)
            for (int i = 0; i < count; i++) Assert.Contains(i, results);
        else
            Assert.Equal(0, results[0]);
    }

    [Fact]
    public void Query_GrowthRemovalAndClearReuseKeepScratchReserved()
    {
        WarmQueryOnAnotherInstance();
        var bvh = new SwiftBVH2D<int>(1);
        var results = new SwiftList<int>(257);
        for (int i = 0; i < 129; i++) bvh.Insert(i, default);

        long afterInsertionGrowth = MeasureQueryAllocation(bvh, default, results);
        Assert.Equal(0, afterInsertionGrowth);
        Assert.Equal(129, results.Count);

        bvh.EnsureCapacity(2048);
        for (int i = 129; i < 257; i++) bvh.Insert(i, default);
        results.FastClear();
        long afterReservationGrowth = MeasureQueryAllocation(bvh, default, results);
        Assert.Equal(0, afterReservationGrowth);
        Assert.Equal(257, results.Count);

        for (int i = 1; i < 257; i++) Assert.True(bvh.Remove(i));
        results.FastClear();
        long afterRemoval = MeasureQueryAllocation(bvh, default, results);
        Assert.Equal(0, afterRemoval);
        Assert.Equal(new[] { 0 }, results);

        bvh.Clear();
        bvh.Insert(999, default);
        results.FastClear();
        long afterClear = MeasureQueryAllocation(bvh, default, results);
        Assert.Equal(0, afterClear);
        Assert.Equal(new[] { 999 }, results);
    }

    [Fact]
    public void Query_SerialThreadHandoffUsesTheOwnersReservedScratch()
    {
        var bvh = new SwiftBVH2D<int>(512);
        for (int i = 0; i < 129; i++) bvh.Insert(i, default);
        var results = new SwiftList<int>(129);
        long allocated = -1;
        Exception failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                WarmQueryOnAnotherInstance();
                allocated = MeasureQueryAllocation(bvh, default, results);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.Start();
        thread.Join();

        Assert.Null(failure);
        Assert.Equal(0, allocated);
        Assert.Equal(129, results.Count);
        for (int i = 0; i < 129; i++) Assert.Contains(i, results);
    }

    private static void WarmQueryOnAnotherInstance()
    {
        var warm = new SwiftBVH2D<int>(8);
        warm.Insert(0, default);
        warm.Insert(1, default);
        var results = new SwiftList<int>(2);
        for (int i = 0; i < 1000; i++)
        {
            results.FastClear();
            MeasureQueryAllocation(warm, default, results);
        }
    }

    private static long MeasureQueryAllocation(SwiftBVH2D<int> bvh, BoundVolume2D query, SwiftList<int> results)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        bvh.Query(query, results);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [Fact]
    public void Lifecycle_UpsertsCanonicalKeysMovesRemovesAndReusesStorage()
    {
        var bvh = new SwiftBVH2D<string>(0);
        Assert.Equal(0, bvh.Count);
        Assert.Equal(-1, bvh.RootNodeIndex);
        Assert.False(bvh.RootNode.IsAllocated);
        string original = new(new[] { 'a' });
        string equal = new(new[] { 'a' });
        var moved = new BoundVolume2D(new Vector2(10), new Vector2(11));
        Assert.NotSame(original, equal);
        Assert.True(bvh.Insert(original, default));
        Assert.False(bvh.Insert(equal, moved));
        Assert.Equal(1, bvh.Count);
        Assert.Same(original, bvh.NodePool[bvh.FindEntry(equal)].Value);
        var results = new List<string> { "prefix" };
        bvh.Query(default, results);
        Assert.Equal(new[] { "prefix" }, results);
        bvh.Query(moved, results);
        Assert.Equal(new[] { "prefix", "a" }, results);
        bvh.UpdateEntryBounds("missing", default);
        bvh.UpdateEntryBounds(equal, default);
        results.Clear();
        bvh.Query(default, results);
        Assert.Equal(new[] { "a" }, results);
        Assert.True(bvh.Remove(equal));
        Assert.False(bvh.Remove(equal));
        Assert.Equal(-1, bvh.FindEntry(equal));
        Assert.Equal(0, bvh.Count);
        Assert.True(bvh.Insert("b", moved));
        bvh.Clear();
        Assert.Equal(0, bvh.Count);
        Assert.True(bvh.Insert("c", default));
        results.Clear();
        bvh.Query(default, results);
        Assert.Equal(new[] { "c" }, results);
    }

    [Fact]
    public void NodePoolGrowth_PreservesLeavesButDetachesBorrowedArray()
    {
        var bvh = new SwiftBVH2D<int>(1);
        bvh.Insert(0, default);
        var borrowed = bvh.NodePool;
        int index = bvh.FindEntry(0);
        bvh.EnsureCapacity(64);
        Assert.NotSame(borrowed, bvh.NodePool);
        Assert.Equal(64, bvh.NodePool.Length);
        Assert.Equal(index, bvh.FindEntry(0));
        Assert.True(borrowed[index].IsAllocated);
        bvh.UpdateEntryBounds(0, new BoundVolume2D(Vector2.One, new Vector2(2)));
        Assert.Equal(default, borrowed[index].Bounds);
        Assert.Equal(Vector2.One, bvh.NodePool[index].Bounds.Min);
        var current = bvh.NodePool;
        bvh.EnsureCapacity(8);
        Assert.Same(current, bvh.NodePool);
    }

    [Fact]
    public void TryQuery_PreservesOrderAppendAndFailureWithoutWrites()
    {
        var bvh = new SwiftBVH2D<int>(4);
        bvh.Insert(0, default);
        bvh.Insert(1, default);
        var expected = new List<int> { 99 };
        bvh.Query(default, expected);
        var results = new SwiftList<int>(4) { 99 };
        Assert.False(bvh.TryQuery(default, results, new int[1]));
        Assert.Equal(new[] { 99 }, results);
        var shortResults = new SwiftList<int>(4);
        while (shortResults.Count < shortResults.Capacity - 1)
            shortResults.Add(99);
        var before = shortResults.ToArray();
        Assert.False(bvh.TryQuery(default, shortResults, new int[2]));
        Assert.Equal(before, shortResults);
        Assert.True(bvh.TryQuery(default, results, new int[2]));
        Assert.Equal(expected, results);
        bvh.Clear();
        Assert.True(bvh.TryQuery(default, results, Span<int>.Empty));
        Assert.Equal(expected, results);
    }

    [Fact]
    public void InvalidInputs_UseSharedBvhGuardsWithoutMutation()
    {
        var bvh = new SwiftBVH2D<string>(4);
        Assert.Throws<ArgumentNullException>("value", () => bvh.Insert(null, default));
        Assert.Throws<ArgumentNullException>("value", () => bvh.UpdateEntryBounds(null, default));
        Assert.Throws<ArgumentNullException>("value", () => bvh.Remove(null));
        Assert.Throws<ArgumentNullException>("value", () => bvh.FindEntry(null));
        Assert.Throws<ArgumentNullException>("results", () => bvh.Query(default, null));
        Assert.Throws<ArgumentNullException>("results", () => bvh.TryQuery(default, null, Span<int>.Empty));
        Assert.Equal(0, bvh.Count);

        var ints = new SwiftBVH2D<int>(4);
        var results = new SwiftList<int>(4);
        Assert.Throws<ArgumentException>("traversal", () => ints.TryQuery(default, results, results.InnerArray.AsSpan()));
        Assert.Throws<ArgumentException>("results", () => ints.TryQuery(default, new SwiftObservableList<int>(4), new int[4]));
        Assert.Empty(results);
    }

    [Fact]
    public void Queries_MatchIndependentInclusiveOracleThroughGrowthAndMovement()
    {
        const int count = 129;
        var bounds = new BoundVolume2D[count];
        var bvh = new SwiftBVH2D<int>(1);
        var twin = new SwiftBVH2D<int>(1);
        for (int i = 0; i < count; i++)
        {
            var min = new Vector2((i * 17) % 31 - 15, (i * 11) % 29 - 14);
            bounds[i] = new BoundVolume2D(min, min + new Vector2(i % 3));
            Assert.True(bvh.Insert(i, bounds[i]));
            Assert.True(twin.Insert(i, bounds[i]));
        }
        Verify();
        for (int i = 0; i < count; i += 3)
        {
            bounds[i] = new BoundVolume2D(bounds[i].Min + Vector2.One, bounds[i].Max + Vector2.One);
            bvh.UpdateEntryBounds(i, bounds[i]);
            twin.UpdateEntryBounds(i, bounds[i]);
        }
        Verify();

        void Verify()
        {
            for (int q = 0; q < 9; q++)
            {
                var query = new BoundVolume2D(new Vector2(q - 6, q - 8), new Vector2(q + 2, q + 1));
                var expected = new List<int>();
                for (int i = 0; i < count; i++)
                    if (bounds[i].Min.X <= query.Max.X && bounds[i].Max.X >= query.Min.X &&
                        bounds[i].Min.Y <= query.Max.Y && bounds[i].Max.Y >= query.Min.Y)
                        expected.Add(i);
                var hits = new List<int>();
                var other = new List<int>();
                bvh.Query(query, hits);
                twin.Query(query, other);
                Assert.Equal(hits, other);
                hits.Sort();
                Assert.Equal(expected, hits);
            }
        }
    }

    [Fact]
    public void FullDomainAndZeroArea_BvhDoesNotReadUnrepresentableSize()
    {
        var bvh = new SwiftBVH2D<int>(1);
        var full = new BoundVolume2D(new Vector2(-float.MaxValue), new Vector2(float.MaxValue));
        var horizontal = new BoundVolume2D(new Vector2(-float.MaxValue, 0), new Vector2(float.MaxValue, 0));
        var vertical = new BoundVolume2D(new Vector2(0, -float.MaxValue), new Vector2(0, float.MaxValue));
        bvh.Insert(0, full);
        bvh.Insert(1, horizontal);
        bvh.Insert(2, vertical);
        bvh.Insert(3, default);
        var result = new List<int>();
        bvh.Query(default, result);
        result.Sort();
        Assert.Equal(new[] { 0, 1, 2, 3 }, result);
        bvh.UpdateEntryBounds(0, default);
        Assert.True(bvh.Remove(2));
        result.Clear();
        bvh.Query(new BoundVolume2D(new Vector2(1), new Vector2(2)), result);
        Assert.Empty(result);
    }
}
