using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SwiftCollections.Utility;
using Xunit;

namespace SwiftCollections.Query.Tests;

public class SwiftSpatialHash2DTests
{
    [Fact]
    public void CellIndex_StoresOnlyTwoCoordinatesAndUsesDeterministicValueSemantics()
    {
        var cell = new SwiftSpatialHashCellIndex2D(-3, 7);
        var same = new SwiftSpatialHashCellIndex2D(-3, 7);
        var differentX = new SwiftSpatialHashCellIndex2D(0, 7);
        var differentY = new SwiftSpatialHashCellIndex2D(-3, 0);
        Assert.Equal(8, Marshal.SizeOf<SwiftSpatialHashCellIndex2D>());
        Assert.Equal(-3, cell.X);
        Assert.Equal(7, cell.Y);
        Assert.Equal(0, default(SwiftSpatialHashCellIndex2D).X);
        Assert.Equal(0, default(SwiftSpatialHashCellIndex2D).Y);
        Assert.True(cell.Equals(same));
        Assert.True(cell.Equals((object)same));
        Assert.True(cell == same);
        Assert.False(cell != same);
        Assert.False(cell.Equals(differentX));
        Assert.False(cell.Equals(differentY));
        Assert.False(cell.Equals((object)differentY));
        Assert.False(cell.Equals(null));
        Assert.False(cell.Equals("cell"));
        Assert.False(cell == differentX);
        Assert.True(cell != differentY);
        Assert.Equal(SwiftHashTools.CombineHashCodes(-3, 7), cell.GetHashCode());
        Assert.Equal(cell.GetHashCode(), same.GetHashCode());
        Assert.Equal("(-3, 7)", cell.ToString());
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void NumericsConstructor_RejectsNonpositiveOrNonfiniteCellSize(float size)
    {
        Assert.Throws<ArgumentOutOfRangeException>("cellSize", () => new SwiftSpatialHash2D<int>(1, size));
        Assert.Throws<ArgumentOutOfRangeException>("cellSize", () => new SwiftSpatialHash2D<int>(1, size, default));
    }

    [Fact]
    public void DefaultsAndExactQueries_DistinguishNeighborhoodAndCellCandidates()
    {
        var normal = new ExposedHash(0, 1);
        var unpadded = new SwiftSpatialHash2D<int>(-1, 1, default);
        Assert.Equal(1, normal.Options.NeighborhoodPadding);
        Assert.Equal(0, unpadded.Options.NeighborhoodPadding);
        Assert.Equal(0, normal.Count);
        var near = Box(0, 0, 0.2f, 0.2f);
        var sharedCell = Box(0.8f, 0.8f, 0.9f, 0.9f);
        var neighbor = Box(1.1f, 0, 1.2f, 0.2f);
        foreach (var index in new SwiftSpatialHash2D<int, BoundVolume2D>[] { normal, unpadded })
        {
            index.Clear();
            index.Insert(1, near);
            index.Insert(2, sharedCell);
            index.Insert(3, neighbor);
        }
        var results = new List<int> { 1 };
        normal.Query(near, results);
        Assert.Equal(new[] { 1, 1 }, results);
        results.Clear();
        normal.QueryNeighborhood(near, results);
        Assert.Equal(new[] { 1, 2, 3 }, results);
        results.Clear();
        unpadded.QueryNeighborhood(near, results);
        Assert.Equal(new[] { 1, 2 }, results);
        results.Clear();
        normal.Candidates(new(0, 0), results);
        Assert.Equal(new[] { 1, 2 }, results);
        normal.Candidates(new(12, 12), results);
        Assert.Equal(new[] { 1, 2 }, results);
        Assert.Throws<ArgumentNullException>("results", () => normal.Candidates(default, null));
        normal.Clear();
        normal.Query(default, results);
        normal.QueryNeighborhood(default, results);
        Assert.Equal(new[] { 1, 2 }, results);
    }

    [Fact]
    public void InclusiveEndpointsAndCellOrder_PreserveDuplicateSuppressionAndAppendPrefix()
    {
        var index = new SwiftSpatialHash2D<int>(1, 1);
        index.Insert(4, Point(-1, -1));
        index.Insert(2, Point(-1, 0));
        index.Insert(3, Point(0, -1));
        index.Insert(1, default);
        index.Insert(5, Box(-1, -1, 0, 0));
        var results = new List<int> { 99 };
        index.Query(Box(-1, -1, 0, 0), results);
        Assert.Equal(new[] { 99, 4, 5, 2, 3, 1 }, results);
        results.Clear();
        index.Query(default, results);
        Assert.Equal(new[] { 1, 5 }, results);
        Assert.True(index.Remove(5));
        Assert.False(index.Remove(5));
        results.Clear();
        index.Query(Box(-1, -1, 0, 0), results);
        Assert.Equal(new[] { 4, 2, 3, 1 }, results);
    }

    [Fact]
    public void LookupUpsertAndClear_KeepCanonicalKeysAndCopyBounds()
    {
        var index = new SwiftSpatialHash2D<string>(1, 1);
        string canonical = new(new[] { 'k', 'e', 'y' });
        string equal = new(new[] { 'k', 'e', 'y' });
        var oldBounds = Point(0, 0);
        Assert.False(index.Contains("absent"));
        Assert.False(index.TryGetBounds("absent", out var absent));
        Assert.Equal(default, absent);
        Assert.False(index.UpdateEntryBounds("absent", oldBounds));
        Assert.False(index.Remove("absent"));
        Assert.True(index.Insert(canonical, oldBounds));
        Assert.True(index.UpdateEntryBounds(equal, oldBounds));
        Assert.False(index.Insert(equal, Point(3, 3)));
        Assert.Equal(1, index.Count);
        Assert.True(index.Contains(equal));
        Assert.True(index.TryGetBounds(equal, out var stored));
        Assert.Equal(Point(3, 3), stored);
        Assert.Equal(Point(0, 0), oldBounds);
        var results = new List<string>();
        index.Query(Point(3, 3), results);
        Assert.Single(results);
        Assert.Same(canonical, results[0]);
        index.Query(oldBounds, results);
        Assert.Single(results);
        Assert.True(index.Remove(equal));
        Assert.True(index.Insert("replacement", default));
        index.Clear();
        index.Clear();
        Assert.Equal(0, index.Count);
        Assert.False(index.Contains("replacement"));
        Assert.True(index.Insert("reused", default));
    }

    [Fact]
    public void NullInputs_UseArgumentContractsEvenOnAnEmptyOwner()
    {
        Assert.Throws<ArgumentNullException>("cellMapper", () => new SwiftSpatialHash2D<string, BoundVolume2D>(0, null));
        Assert.Throws<ArgumentNullException>("cellMapper", () => new SwiftSpatialHash2D<string, BoundVolume2D>(0, null, default));
        var index = new SwiftSpatialHash2D<string>(1, 1);
        Assert.Throws<ArgumentNullException>("key", () => index.Insert(null, default));
        Assert.Throws<ArgumentNullException>("key", () => index.Remove(null));
        Assert.Throws<ArgumentNullException>("key", () => index.UpdateEntryBounds(null, default));
        Assert.Throws<ArgumentNullException>("key", () => index.Contains(null));
        Assert.Throws<ArgumentNullException>("key", () => index.TryGetBounds(null, out _));
        Assert.Throws<ArgumentNullException>("results", () => index.Query(default, null));
        Assert.Throws<ArgumentNullException>("results", () => index.QueryNeighborhood(default, null));
        Assert.Equal(0, index.Count);
    }

    [Fact]
    public void NumericsMapping_WidensDivisionFloorsNegativesAndClampsBothAxes()
    {
        var tiny = new ExposedHash(1, float.MaxValue);
        tiny.Insert(1, Point(-float.Epsilon, float.Epsilon));
        var results = new List<int>();
        tiny.Candidates(new(-1, 0), results);
        Assert.Equal(new[] { 1 }, results);
        results.Clear();
        tiny.Candidates(default, results);
        Assert.Empty(results);

        var index = new ExposedHash(1, float.Epsilon);
        index.Insert(2, Point(float.MaxValue, -float.MaxValue));
        results.Clear();
        index.Candidates(new(int.MaxValue, int.MinValue), results);
        Assert.Equal(new[] { 2 }, results);
        results.Clear();
        index.QueryNeighborhood(Point(float.MaxValue, -float.MaxValue), results);
        Assert.Equal(new[] { 2 }, results);
        Assert.True(index.Remove(2));

        index.Insert(3, Point(-float.MaxValue, float.MaxValue));
        results.Clear();
        index.Candidates(new(int.MinValue, int.MaxValue), results);
        Assert.Equal(new[] { 3 }, results);
        results.Clear();
        index.QueryNeighborhood(Point(-float.MaxValue, float.MaxValue), results);
        Assert.Equal(new[] { 3 }, results);
    }

    [Fact]
    public void CustomMapper_IsRetainedAndExceptionsLeaveObservablePreMutationState()
    {
        var mapper = new UnitMapper();
        var index = new SwiftSpatialHash2D<int, BoundVolume2D>(1, mapper);
        var bounds = Point(0, 0);
        index.Insert(1, bounds);
        mapper.Throw = true;
        var results = new List<int> { 77 };
        Assert.Throws<InvalidOperationException>(() => index.Query(bounds, results));
        Assert.Equal(new[] { 77 }, results);
        Assert.Throws<InvalidOperationException>(() => index.Remove(1));
        Assert.True(index.Contains(1));
        Assert.True(index.TryGetBounds(1, out var unchanged));
        Assert.Equal(bounds, unchanged);
        Assert.Equal(1, index.Count);
        mapper.Throw = false;
        index.Query(bounds, results);
        Assert.Equal(new[] { 77, 1 }, results);
        Assert.True(index.Remove(1));
        mapper.Throw = true;
        index.Query(bounds, results);
        Assert.False(index.UpdateEntryBounds(5, bounds));
    }

    [Fact]
    public void CustomBackend_RequiresOnlyTheBoundVolumeInterface()
    {
        var index = new SwiftSpatialHash2D<int, InterfaceOnlyVolume>(1, new InterfaceOnlyMapper());
        var value = new InterfaceOnlyVolume(Point(0, 0));
        Assert.True(index.Insert(1, value));
        Assert.True(index.UpdateEntryBounds(1, value));
        var results = new List<int>();
        index.Query(value, results);
        Assert.Equal(new[] { 1 }, results);
        Assert.True(index.Remove(1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void InvertedCustomRanges_KeepEmptyCellLoopsAndApplyPaddingBeforeAdmission(int axis)
    {
        var mapper = new ControlledRangeMapper { InvalidAxis = axis };
        var index = new SwiftSpatialHash2D<int, BoundVolume2D>(1, mapper);
        Assert.True(index.Insert(1, default));
        Assert.Equal(1, index.Count);
        Assert.True(index.Contains(1));
        var results = new List<int> { 77 };
        index.Query(default, results);
        Assert.Equal(new[] { 77 }, results);
        Assert.True(index.Remove(1));
        Assert.Equal(0, index.Count);

        mapper.InvalidAxis = -1;
        Assert.True(index.Insert(2, default));
        mapper.InvalidAxis = axis;
        index.Query(default, results);
        Assert.Equal(new[] { 77 }, results);
        index.QueryNeighborhood(default, results);
        Assert.Equal(new[] { 77, 2 }, results);
        mapper.InvalidAxis = -1;
        Assert.True(index.Remove(2));
    }

    [Fact]
    public void DestinationFailure_PreservesPrefixAndNextQueryCanVisitEveryEntryAgain()
    {
        var index = new SwiftSpatialHash2D<int>(1, 1);
        index.Insert(1, default);
        index.Insert(2, default);
        var results = new ThrowingResults(1);
        Assert.Throws<InvalidOperationException>(() => index.Query(default, results));
        Assert.Equal(new[] { 1 }, results.Items);
        var retried = new List<int>();
        index.Query(default, retried);
        Assert.Equal(new[] { 1, 2 }, retried);
        Assert.Equal(2, index.Count);
    }

    [Fact]
    public void QueryStampRollover_ResetsLiveAndFreedSlotsWithoutRepeatingHits()
    {
        var index = new SwiftSpatialHash2D<int>(1, 1);
        index.Insert(1, Box(0, 0, 2, 2));
        index.Insert(2, Point(5, 5));
        Assert.True(index.Remove(2));
        SetQueryStamp(index, int.MaxValue);
        var results = new List<int> { 88 };
        index.Query(Box(0, 0, 1, 1), results);
        Assert.Equal(new[] { 88, 1 }, results);
        results.Clear();
        index.Query(Box(0, 0, 1, 1), results);
        Assert.Equal(new[] { 1 }, results);
        Assert.Equal(1, index.Count);
    }

    [Fact]
    public void WarmTypedQueriesAndLookups_AllocateNothing()
    {
        var index = new SwiftSpatialHash2D<int>(16, 1);
        var bounds = Box(0, 0, 2, 2);
        index.Insert(1, bounds);
        var results = new SwiftList<int>(16);
        int hits = 0;
        long allocated = MeasureWarmedAllocations(() =>
        {
            hits = 0;
            for (int i = 0; i < 1000; i++)
            {
                results.Clear();
                index.Query(bounds, results);
                hits += results.Count;
                results.Clear();
                index.QueryNeighborhood(bounds, results);
                hits += results.Count;
                if (index.Contains(1)) hits++;
                if (index.TryGetBounds(1, out var stored) && stored == bounds) hits++;
                if (index.UpdateEntryBounds(1, bounds)) hits++;
            }
        });
        Assert.Equal(0L, allocated);
        Assert.Equal(5000, hits);
    }

    [Fact]
    public void GrowthCollisionsRelocationAndReuse_AgreeWithIndependentHitSets()
    {
        const int count = 257;
        var mapper = new UnitMapper();
        var index = new SwiftSpatialHash2D<int, BoundVolume2D>(1, mapper);
        var repeated = new SwiftSpatialHash2D<int, BoundVolume2D>(1, mapper);
        var current = new BoundVolume2D[count];
        var active = new bool[count];
        for (int i = 0; i < count; i++)
        {
            current[i] = Box(i % 16 - 8, i / 16 - 8, i % 16 - 7.25f, i / 16 - 7.25f);
            active[i] = true;
            Assert.True(index.Insert(i * 1024, current[i]));
            Assert.True(repeated.Insert(i * 1024, current[i]));
        }
        index.EnsureCapacity(count * 4);
        index.EnsureCapacity(0);
        Verify();
        for (int i = 0; i < count; i += 3)
        {
            Assert.True(index.Remove(i * 1024));
            Assert.True(repeated.Remove(i * 1024));
            active[i] = false;
        }
        Verify();
        for (int i = 0; i < count; i++)
        {
            current[i] = Box(i % 8 - 4, i % 5 - 2, i % 8 - 3.25f, i % 5 - 1.25f);
            if (active[i])
            {
                Assert.True(index.UpdateEntryBounds(i * 1024, current[i]));
                Assert.True(repeated.UpdateEntryBounds(i * 1024, current[i]));
            }
            else
            {
                Assert.True(index.Insert(i * 1024, current[i]));
                Assert.True(repeated.Insert(i * 1024, current[i]));
                active[i] = true;
            }
        }
        Verify();
        index.Clear();
        repeated.Clear();
        Array.Clear(active);
        Verify();

        void Verify()
        {
            int live = 0;
            for (int i = 0; i < count; i++)
            {
                if (active[i]) live++;
                Assert.Equal(active[i], index.Contains(i * 1024));
                Assert.Equal(active[i], index.TryGetBounds(i * 1024, out var actual));
                Assert.Equal(active[i] ? current[i] : default, actual);
            }
            Assert.Equal(live, index.Count);
            for (int q = 0; q < 16; q++)
            {
                var query = Box(q - 9, q % 5 - 4, q - 5, q % 5);
                var actual = new List<int>();
                var ordered = new List<int>();
                index.Query(query, actual);
                repeated.Query(query, ordered);
                Assert.Equal(ordered, actual);
                var expected = new List<int>();
                for (int i = 0; i < count; i++)
                    if (active[i] && query.Min.X <= current[i].Max.X && query.Max.X >= current[i].Min.X
                        && query.Min.Y <= current[i].Max.Y && query.Max.Y >= current[i].Min.Y)
                        expected.Add(i * 1024);
                actual.Sort();
                Assert.Equal(expected, actual);
            }
        }
    }

    private static BoundVolume2D Point(float x, float y) => Box(x, y, x, y);
    private static BoundVolume2D Box(float minX, float minY, float maxX, float maxY) => new(new(minX, minY), new(maxX, maxY));

    private static void SetQueryStamp(object owner, int stamp)
    {
        Type ownerType = owner.GetType();
        while (ownerType.GetField("_core", BindingFlags.Instance | BindingFlags.NonPublic) == null)
            ownerType = ownerType.BaseType!;
        FieldInfo field = ownerType.GetField("_core", BindingFlags.Instance | BindingFlags.NonPublic)!;
        object core = field.GetValue(owner)!;
        core.GetType().GetField("_queryStamp", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(core, stamp);
        field.SetValue(owner, core);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long MeasureWarmedAllocations(Action action)
    {
        action();
        long before = GC.GetAllocatedBytesForCurrentThread();
        action();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private sealed class ExposedHash : SwiftSpatialHash2D<int>
    {
        public ExposedHash(int capacity, float size) : base(capacity, size) { }
        public void Candidates(SwiftSpatialHashCellIndex2D cell, ICollection<int> results) => CollectCellCandidates(cell, results);
    }

    private sealed class UnitMapper : ISpatialHashCellMapper2D<BoundVolume2D>
    {
        public bool Throw;
        public void GetCellRange(BoundVolume2D bounds, out SwiftSpatialHashCellIndex2D minCell, out SwiftSpatialHashCellIndex2D maxCell)
        {
            if (Throw) throw new InvalidOperationException("Mapper control failure.");
            minCell = new((int)Math.Floor(bounds.Min.X), (int)Math.Floor(bounds.Min.Y));
            maxCell = new((int)Math.Floor(bounds.Max.X), (int)Math.Floor(bounds.Max.Y));
        }
    }

    private readonly struct InterfaceOnlyVolume : IBoundVolume<InterfaceOnlyVolume>
    {
        private readonly BoundVolume2D _bounds;
        public InterfaceOnlyVolume(BoundVolume2D bounds) => _bounds = bounds;
        public InterfaceOnlyVolume Union(InterfaceOnlyVolume other) => new(_bounds.Union(other._bounds));
        public bool Intersects(InterfaceOnlyVolume other) => _bounds.Intersects(other._bounds);
        public long GetCost(InterfaceOnlyVolume other) => _bounds.GetCost(other._bounds);
        public bool BoundsEquals(InterfaceOnlyVolume other) => _bounds.BoundsEquals(other._bounds);
    }

    private sealed class InterfaceOnlyMapper : ISpatialHashCellMapper2D<InterfaceOnlyVolume>
    {
        public void GetCellRange(InterfaceOnlyVolume bounds, out SwiftSpatialHashCellIndex2D minCell, out SwiftSpatialHashCellIndex2D maxCell)
        {
            minCell = default;
            maxCell = default;
        }
    }

    private sealed class ControlledRangeMapper : ISpatialHashCellMapper2D<BoundVolume2D>
    {
        public int InvalidAxis = -1;
        public void GetCellRange(BoundVolume2D bounds, out SwiftSpatialHashCellIndex2D minCell, out SwiftSpatialHashCellIndex2D maxCell)
        {
            minCell = new(InvalidAxis == 0 ? 1 : 0, InvalidAxis == 1 ? 1 : 0);
            maxCell = default;
        }
    }

    private sealed class ThrowingResults : ICollection<int>
    {
        private readonly int _limit;
        public ThrowingResults(int limit) => _limit = limit;
        public readonly List<int> Items = new();
        public int Count => Items.Count;
        public bool IsReadOnly => false;
        public void Add(int item)
        {
            if (Items.Count == _limit) throw new InvalidOperationException("Destination control failure.");
            Items.Add(item);
        }
        public void Clear() => Items.Clear();
        public bool Contains(int item) => Items.Contains(item);
        public void CopyTo(int[] array, int arrayIndex) => Items.CopyTo(array, arrayIndex);
        public bool Remove(int item) => Items.Remove(item);
        public IEnumerator<int> GetEnumerator() => Items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
