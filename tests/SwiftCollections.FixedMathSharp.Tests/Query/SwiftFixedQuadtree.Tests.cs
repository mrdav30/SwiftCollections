using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using FixedMathSharp;
using Xunit;

namespace SwiftCollections.Query.Tests;

public class SwiftFixedQuadtreeTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_RejectsNonpositiveMinimumNodeSize(int size)
    {
        Assert.Throws<ArgumentOutOfRangeException>("minNodeSize", () =>
            new SwiftFixedQuadtree<int>(Box(0, 0, 16, 16), new SwiftQuadtreeOptions(4, 1), (Fixed64)size));
    }

    [Fact]
    public void FourChildren_PreserveParentFirstOrderAndUpperHalfPointTies()
    {
        var world = Box(0, 0, 16, 16);
        var tree = new SwiftFixedQuadtree<int>(world, new SwiftQuadtreeOptions(1, 1), Fixed64.One);
        for (int child = 3; child >= 0; child--)
        {
            int x = (child & 1) == 0 ? 1 : 10;
            int y = (child & 2) == 0 ? 1 : 10;
            Assert.True(tree.Insert(child, Box(x, y, x + 1, y + 1)));
        }
        tree.Insert(10, Box(7, 1, 9, 2));
        tree.Insert(11, Box(1, 7, 2, 9));
        tree.Insert(12, Box(8, 8, 8, 8));
        tree.Insert(13, Box(6, 1, 8, 2));

        var results = new List<int> { -1 };
        tree.Query(world, results);
        Assert.Equal(new[] { -1, 10, 11, 0, 13, 1, 2, 3, 12 }, results);
        Assert.Equal(5, DebugValue<int>(tree, "DebugNodeCount"));
        Assert.Equal(1, DebugValue<int>(tree, "DebugMaxDepth"));
        Assert.True(DebugValue<bool>(tree, "DebugRootHasChildren"));
        results.Clear();
        tree.Query(Box(8, 1, 8, 1), results);
        Assert.Equal(new[] { 10, 13 }, results);
        results.Clear();
        tree.Query(Box(8, 8, 8, 8), results);
        Assert.Equal(new[] { 12 }, results);
    }

    [Fact]
    public void Partitioner_UsesMatchingMidpointsForSelectionAndEveryChild()
    {
        var partitioner = Partitioner(Fixed64.FromRaw(1));
        var world = RawBox(0, -5, 5, 0);
        long midX = MidpointOracle(0, 5);
        long midY = MidpointOracle(-5, 0);
        for (int child = 0; child < 4; child++)
        {
            var bounds = partitioner.CreateChildBounds(world, child);
            Assert.Equal((child & 1) == 0 ? 0 : midX, bounds.Min.X.m_rawValue);
            Assert.Equal((child & 1) == 0 ? midX : 5, bounds.Max.X.m_rawValue);
            Assert.Equal((child & 2) == 0 ? -5 : midY, bounds.Min.Y.m_rawValue);
            Assert.Equal((child & 2) == 0 ? midY : 0, bounds.Max.Y.m_rawValue);
            Assert.True(partitioner.ContainsBounds(world, bounds));
            Assert.True(partitioner.TryGetContainingChildIndex(world, bounds, out int actual));
            Assert.Equal(child, actual);
        }
        Assert.True(partitioner.TryGetContainingChildIndex(world, RawBox(midX, midY, midX, midY), out int upper));
        Assert.Equal(3, upper);
        Assert.False(partitioner.TryGetContainingChildIndex(world, RawBox(1, -5, 3, -4), out int xStraddler));
        Assert.Equal(-1, xStraddler);
        Assert.False(partitioner.TryGetContainingChildIndex(world, RawBox(0, -3, 1, -1), out int yStraddler));
        Assert.Equal(-1, yStraddler);
    }

    [Theory]
    [InlineData(0L, 1L, 1L, false)]
    [InlineData(1L, 2L, 1L, false)]
    [InlineData(0L, 3L, 1L, true)]
    [InlineData(0L, 5L, 2L, true)]
    [InlineData(0L, 5L, 3L, false)]
    [InlineData(-5L, 0L, 2L, true)]
    [InlineData(-5L, 0L, 3L, false)]
    public void MinimumChildSpan_ChecksBothNearestEvenHalves(long min, long max, long required, bool expected)
    {
        var partitioner = Partitioner(Fixed64.FromRaw(required));
        Assert.Equal(expected, partitioner.CanSubdivide(RawBox(min, min, max, max)));
        Assert.False(partitioner.CanSubdivide(RawBox(0, min, 0, max)));
        Assert.False(partitioner.CanSubdivide(RawBox(min, 0, max, 0)));
    }

    [Theory]
    [InlineData(0L, 1L, 1L)]
    [InlineData(1L, 2L, 1L)]
    [InlineData(0L, 5L, 3L)]
    public void TinyRawWorld_StopsBeforeEndpointEqualOrUndersizedSplits(long min, long max, long required)
    {
        var world = RawBox(min, min, max, max);
        var tree = new SwiftFixedQuadtree<int>(world, new SwiftQuadtreeOptions(16, 1), Fixed64.FromRaw(required));
        tree.Insert(1, RawBox(min, min, min, min));
        tree.Insert(2, RawBox(max, max, max, max));
        Assert.False(DebugValue<bool>(tree, "DebugRootHasChildren"));
        Assert.Equal(1, DebugValue<int>(tree, "DebugNodeCount"));
        var results = new List<int>();
        tree.Query(world, results);
        Assert.Equal(new[] { 1, 2 }, results);
    }

    [Fact]
    public void FullDomain_SubdividesWithoutReadingOverflowingSizeOrArea()
    {
        var world = RawBox(long.MinValue, long.MinValue, long.MaxValue, long.MaxValue);
        var tree = new SwiftFixedQuadtree<int>(world, new SwiftQuadtreeOptions(2, 1), Fixed64.MaxValue);
        var low = RawBox(long.MinValue, long.MinValue, long.MinValue, long.MinValue);
        var high = RawBox(long.MaxValue, long.MaxValue, long.MaxValue, long.MaxValue);
        Assert.True(tree.Insert(1, low));
        Assert.True(tree.Insert(2, high));
        Assert.True(DebugValue<bool>(tree, "DebugRootHasChildren"));
        Assert.Equal(5, DebugValue<int>(tree, "DebugNodeCount"));
        Assert.Throws<OverflowException>(() => _ = world.Size);
        Assert.Throws<OverflowException>(() => _ = world.Area);
        var results = new List<int>();
        tree.Query(world, results);
        Assert.Equal(new[] { 1, 2 }, results);
        results.Clear();
        tree.Query(high, results);
        Assert.Equal(new[] { 2 }, results);
    }

    [Fact]
    public void PartitionAdmission_MatchesBigIntegerOracleAcrossSeededRawDomains()
    {
        uint seed = 0x51F03D;
        for (int i = 0; i < 512; i++)
        {
            long a = unchecked((long)Next64(ref seed));
            long b = unchecked((long)Next64(ref seed));
            long c = unchecked((long)Next64(ref seed));
            long d = unchecked((long)Next64(ref seed));
            long required = (long)(Next64(ref seed) & 0x7FFF_FFFF_FFFF_FFFFUL);
            if (required == 0) required = 1;
            long minX = Math.Min(a, b), maxX = Math.Max(a, b);
            long minY = Math.Min(c, d), maxY = Math.Max(c, d);
            var bounds = RawBox(minX, minY, maxX, maxY);
            Assert.Equal(MidpointOracle(minX, maxX), bounds.Center.X.m_rawValue);
            Assert.Equal(MidpointOracle(minY, maxY), bounds.Center.Y.m_rawValue);
            bool expected = HasHalvesOracle(minX, maxX, required) && HasHalvesOracle(minY, maxY, required);
            Assert.Equal(expected, Partitioner(Fixed64.FromRaw(required)).CanSubdivide(bounds));
        }
    }

    [Theory]
    [InlineData(-1, 1, 2, 2)]
    [InlineData(1, -1, 2, 2)]
    [InlineData(1, 1, 17, 2)]
    [InlineData(1, 1, 2, 17)]
    public void WorldRejection_PrecedesUpsertAndPreservesStoredBounds(int minX, int minY, int maxX, int maxY)
    {
        var world = Box(0, 0, 16, 16);
        var original = Box(1, 1, 2, 2);
        var tree = new SwiftFixedQuadtree<int>(world, new SwiftQuadtreeOptions(4, 1), Fixed64.One);
        tree.Insert(1, original);
        var outside = Box(minX, minY, maxX, maxY);
        Assert.Throws<ArgumentOutOfRangeException>("bounds", () => tree.Insert(1, outside));
        Assert.Throws<ArgumentOutOfRangeException>("newBounds", () => tree.UpdateEntryBounds(1, outside));
        Assert.False(tree.UpdateEntryBounds(2, outside));
        Assert.True(tree.TryGetBounds(1, out var current));
        Assert.Equal(original, current);
        Assert.Equal(1, tree.Count);
    }

    [Fact]
    public void CanonicalKeysAndDefaultOptions_PreserveIdentityAndAnUnsplitTree()
    {
        var world = Box(0, 0, 16, 16);
        var tree = new SwiftFixedQuadtree<string>(world, default, Fixed64.One);
        string canonical = new(new[] { 'k' });
        string probe = new(new[] { 'k' });
        Assert.True(tree.Insert(canonical, default));
        var replacement = Box(1, 1, 2, 2);
        Assert.False(tree.Insert(probe, replacement));
        Assert.True(tree.UpdateEntryBounds(probe, replacement));
        Assert.False(tree.TryGetBounds("absent", out var missing));
        Assert.Equal(default, missing);
        var results = new List<string>();
        tree.Query(world, results);
        Assert.Same(canonical, Assert.Single(results));
        Assert.Equal(world, tree.WorldBounds);
        Assert.Equal(default, tree.Options);
        Assert.False(DebugValue<bool>(tree, "DebugRootHasChildren"));
        Assert.Throws<ArgumentNullException>("key", () => tree.Insert(null, default));
        Assert.Throws<ArgumentNullException>("key", () => tree.Remove(null));
        Assert.Throws<ArgumentNullException>("key", () => tree.Contains(null));
        Assert.Throws<ArgumentNullException>("key", () => tree.TryGetBounds(null, out _));
        Assert.Throws<ArgumentNullException>("key", () => tree.UpdateEntryBounds(null, default));
        Assert.Throws<ArgumentNullException>("results", () => tree.Query(default, null));
        tree.Clear();
        tree.Clear();
        Assert.Equal(0, tree.Count);
        Assert.False(tree.Remove(probe));
        Assert.True(tree.Insert(probe, replacement));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GrowthMovementRemovalAndReuse_AgreeWithIndependentHitSets(bool merge)
    {
        const int count = 129;
        var world = Box(0, 0, 64, 64);
        var tree = new SwiftFixedQuadtree<int>(world, new SwiftQuadtreeOptions(8, 1, merge), Fixed64.One);
        var bounds = new FixedBoundVolume2D[count];
        var alive = new bool[count];
        for (int i = 0; i < count; i++)
        {
            bounds[i] = Box(i % 16 * 2, i / 16 * 2, i % 16 * 2 + 1, i / 16 * 2 + 1);
            Assert.True(tree.Insert(i * 1024, bounds[i]));
            alive[i] = true;
        }
        Verify();
        for (int i = 0; i < count; i += 4)
        {
            bounds[i] = Box(i % 8 + 40, i % 5 + 40, i % 8 + 41, i % 5 + 41);
            Assert.True(tree.UpdateEntryBounds(i * 1024, bounds[i]));
        }
        Verify();
        for (int i = 0; i < count; i += 3) { Assert.True(tree.Remove(i * 1024)); alive[i] = false; }
        Verify();
        for (int i = 0; i < count; i += 3) { Assert.True(tree.Insert(i * 1024, bounds[i])); alive[i] = true; }
        Verify();
        tree.Clear();
        Assert.False(DebugValue<bool>(tree, "DebugRootHasChildren"));
        Assert.Equal(1, DebugValue<int>(tree, "DebugNodeCount"));
        for (int i = count - 1; i >= 0; i--) Assert.True(tree.Insert(i * 1024, bounds[i]));
        Verify();

        void Verify()
        {
            int expectedCount = 0;
            foreach (bool value in alive) if (value) expectedCount++;
            Assert.Equal(expectedCount, tree.Count);
            for (int q = 0; q < 24; q++)
            {
                var query = Box(q * 2, q % 11, q * 2 + 5, q % 11 + 5);
                var actual = new List<int>();
                tree.Query(query, actual);
                var expected = new List<int>();
                for (int i = 0; i < count; i++)
                    if (alive[i] && query.Min.X <= bounds[i].Max.X && query.Max.X >= bounds[i].Min.X
                        && query.Min.Y <= bounds[i].Max.Y && query.Max.Y >= bounds[i].Min.Y)
                        expected.Add(i * 1024);
                actual.Sort();
                Assert.Equal(expected, actual);
            }
        }
    }

    [Fact]
    public void WarmTypedQueriesAndUnchangedLookups_AllocateNothing()
    {
        var tree = new SwiftFixedQuadtree<int>(Box(0, 0, 16, 16), new SwiftQuadtreeOptions(4, 1), Fixed64.One);
        var bounds = new[] { Box(1, 1, 2, 2), Box(10, 1, 11, 2), Box(1, 10, 2, 11), Box(10, 10, 11, 11) };
        for (int i = 0; i < bounds.Length; i++) tree.Insert(i, bounds[i]);
        var results = new SwiftList<int>(16);
        for (int i = 0; i < 16; i++) WarmOperations(tree, bounds, results);
        long before = GC.GetAllocatedBytesForCurrentThread();
        int hits = WarmOperations(tree, bounds, results);
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(hits > 0);
        Assert.Equal(0, bytes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int WarmOperations(SwiftFixedQuadtree<int> tree, FixedBoundVolume2D[] bounds, SwiftList<int> results)
    {
        int hits = 0;
        for (int pass = 0; pass < 64; pass++)
            for (int i = 0; i < bounds.Length; i++)
            {
                results.Clear();
                tree.Query(bounds[i], results);
                hits += results.Count;
                if (tree.Contains(i)) hits++;
                if (tree.TryGetBounds(i, out var found) && found.Equals(bounds[i])) hits++;
                if (tree.UpdateEntryBounds(i, bounds[i])) hits++;
            }
        return hits;
    }

    private static IQuadtreeBoundsPartitioner<FixedBoundVolume2D> Partitioner(Fixed64 minimum)
    {
        Type type = typeof(SwiftFixedQuadtree<int>).GetNestedType("FixedBoundVolumeQuadtreePartitioner", BindingFlags.NonPublic);
        Assert.NotNull(type);
        if (type.ContainsGenericParameters) type = type.MakeGenericType(typeof(int));
        return (IQuadtreeBoundsPartitioner<FixedBoundVolume2D>)Activator.CreateInstance(type, minimum);
    }

    private static T DebugValue<T>(object tree, string name)
    {
        PropertyInfo property = typeof(SwiftQuadtree<int, FixedBoundVolume2D>).GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic);
        // Reference-key owners share the same inherited debug contract.
        if (property == null || !property.DeclaringType.IsInstanceOfType(tree))
            property = tree.GetType().BaseType.GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return (T)property.GetValue(tree);
    }

    private static long MidpointOracle(long min, long max)
    {
        BigInteger sum = (BigInteger)min + max;
        BigInteger half = BigInteger.DivRem(sum, 2, out var remainder);
        if (remainder != 0 && !half.IsEven) half += sum.Sign;
        return (long)half;
    }

    private static bool HasHalvesOracle(long min, long max, long required)
    {
        long midpoint = MidpointOracle(min, max);
        return (BigInteger)midpoint - min >= required && (BigInteger)max - midpoint >= required;
    }

    private static ulong Next64(ref uint seed) => ((ulong)Next32(ref seed) << 32) | Next32(ref seed);
    private static uint Next32(ref uint seed)
    {
        seed ^= seed << 13;
        seed ^= seed >> 17;
        seed ^= seed << 5;
        return seed;
    }

    private static FixedBoundVolume2D Box(int minX, int minY, int maxX, int maxY) =>
        new(new Vector2d(minX, minY), new Vector2d(maxX, maxY));

    private static FixedBoundVolume2D RawBox(long minX, long minY, long maxX, long maxY) =>
        new(new Vector2d(Fixed64.FromRaw(minX), Fixed64.FromRaw(minY)),
            new Vector2d(Fixed64.FromRaw(maxX), Fixed64.FromRaw(maxY)));
}
