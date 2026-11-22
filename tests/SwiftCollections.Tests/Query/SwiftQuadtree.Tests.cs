using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using SwiftCollections.Utility;
using Xunit;

namespace SwiftCollections.Query.Tests;

public class SwiftQuadtreeTests
{
    [Fact]
    public void Options_ValidateAndCompareEveryComponentWithDeterministicHashing()
    {
        var value = new SwiftQuadtreeOptions(4, 2);
        var same = new SwiftQuadtreeOptions(4, 2, true);
        Assert.Equal(4, value.MaxDepth);
        Assert.Equal(2, value.NodeCapacity);
        Assert.True(value.EnableMergeOnRemove);
        Assert.True(value.Equals(same));
        Assert.True(value.Equals((object)same));
        Assert.True(value == same);
        Assert.False(value != same);
        foreach (var different in new[] { new SwiftQuadtreeOptions(3, 2), new SwiftQuadtreeOptions(4, 3), new SwiftQuadtreeOptions(4, 2, false) })
        {
            Assert.False(value.Equals(different));
            Assert.False(value.Equals((object)different));
            Assert.False(value == different);
            Assert.True(value != different);
        }
        Assert.False(value.Equals(null));
        Assert.False(value.Equals("options"));
        Assert.Equal(SwiftHashTools.CombineHashCodes(4, 2, 1), value.GetHashCode());
        Assert.Equal(value.GetHashCode(), same.GetHashCode());
        Assert.Equal(SwiftHashTools.CombineHashCodes(4, 2, 0), new SwiftQuadtreeOptions(4, 2, false).GetHashCode());
        Assert.Equal(SwiftHashTools.CombineHashCodes(0, 0, 0), default(SwiftQuadtreeOptions).GetHashCode());
        Assert.Equal(0, default(SwiftQuadtreeOptions).MaxDepth);
        Assert.Equal(0, default(SwiftQuadtreeOptions).NodeCapacity);
        Assert.False(default(SwiftQuadtreeOptions).EnableMergeOnRemove);
        Assert.Throws<ArgumentOutOfRangeException>("maxDepth", () => new SwiftQuadtreeOptions(-1, 1));
        Assert.Throws<ArgumentOutOfRangeException>("maxDepth", () => new SwiftQuadtreeOptions(-1, 1, false));
        Assert.Throws<ArgumentOutOfRangeException>("nodeCapacity", () => new SwiftQuadtreeOptions(0, 0));
        Assert.Throws<ArgumentOutOfRangeException>("nodeCapacity", () => new SwiftQuadtreeOptions(0, -1, false));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Constructor_RejectsNonpositiveOrNonfiniteMinimumSize(float size)
    {
        Assert.Throws<ArgumentOutOfRangeException>("minNodeSize", () => new SwiftQuadtree<int>(Box(0, 0, 16, 16), default, size));
    }

    [Fact]
    public void DefaultWorldAndOptions_AcceptOriginEntriesWithoutSubdivision()
    {
        var index = new SwiftQuadtree<int>(default, default, float.Epsilon);
        Assert.Equal(default, index.WorldBounds);
        Assert.Equal(default, index.Options);
        Assert.Equal(0, index.Count);
        Assert.False(index.DebugRootHasChildren);
        Assert.Equal(1, index.DebugNodeCount);
        Assert.Equal(0, index.DebugMaxDepth);
        index.Clear();
        Assert.True(index.Insert(1, default));
        Assert.True(index.Insert(2, default));
        Assert.False(index.Insert(1, default));
        Assert.True(index.UpdateEntryBounds(1, default));
        Assert.False(index.DebugRootHasChildren);
        var results = new List<int> { 1 };
        index.Query(default, results);
        Assert.Equal(new[] { 1, 1, 2 }, results);
        Assert.Equal(2, index.Count);
        Assert.True(index.TryGetBounds(1, out var bounds));
        Assert.Equal(default, bounds);
    }

    [Fact]
    public void NullInputs_AreRejectedBeforeBackendCallsOrMutation()
    {
        var partitioner = new ControlledPartitioner();
        Assert.Throws<ArgumentNullException>("boundsPartitioner", () => new SwiftQuadtree<string, BoundVolume2D>(default, default, null));
        var index = new SwiftQuadtree<string, BoundVolume2D>(Box(0, 0, 16, 16), default, partitioner);
        Assert.Throws<ArgumentNullException>("key", () => index.Insert(null, default));
        Assert.Throws<ArgumentNullException>("key", () => index.Remove(null));
        Assert.Throws<ArgumentNullException>("key", () => index.Contains(null));
        Assert.Throws<ArgumentNullException>("key", () => index.TryGetBounds(null, out _));
        Assert.Throws<ArgumentNullException>("key", () => index.UpdateEntryBounds(null, default));
        Assert.Throws<ArgumentNullException>("results", () => index.Query(default, null));
        Assert.Equal(0, partitioner.ContainsCalls);
        Assert.Equal(0, index.Count);
    }

    [Theory]
    [InlineData(-1, 1, 2, 2)]
    [InlineData(1, -1, 2, 2)]
    [InlineData(1, 1, 17, 2)]
    [InlineData(1, 1, 2, 17)]
    public void WorldRejection_PrecedesDuplicateLookupAndPreservesExistingBounds(float x0, float y0, float x1, float y1)
    {
        var index = new SwiftQuadtree<int>(Box(0, 0, 16, 16), new SwiftQuadtreeOptions(4, 1), 1);
        var inside = Box(1, 1, 2, 2);
        var outside = Box(x0, y0, x1, y1);
        Assert.True(index.Insert(1, inside));
        Assert.Throws<ArgumentOutOfRangeException>("bounds", () => index.Insert(2, outside));
        Assert.Throws<ArgumentOutOfRangeException>("bounds", () => index.Insert(1, outside));
        Assert.Throws<ArgumentOutOfRangeException>("newBounds", () => index.UpdateEntryBounds(1, outside));
        Assert.False(index.UpdateEntryBounds(99, outside));
        Assert.False(index.Contains(99));
        Assert.False(index.TryGetBounds(99, out var missing));
        Assert.Equal(default, missing);
        Assert.False(index.Remove(99));
        Assert.Equal(1, index.Count);
        Assert.True(index.TryGetBounds(1, out var stored));
        Assert.Equal(inside, stored);
        var hits = new List<int>();
        index.Query(Box(-2, -2, 18, 18), hits);
        Assert.Equal(new[] { 1 }, hits);
    }

    [Fact]
    public void LookupAndUpsert_RetainCanonicalKeysWithoutRequiringEquatable()
    {
        var index = new SwiftQuadtree<CollisionKey>(Box(0, 0, 16, 16), new SwiftQuadtreeOptions(2, 1), 1);
        var original = new CollisionKey(7);
        var probe = new CollisionKey(7);
        var bounds = Box(1, 1, 2, 2);
        Assert.True(index.Insert(original, bounds));
        bounds = Box(12, 12, 13, 13);
        Assert.False(index.Insert(probe, bounds));
        Assert.True(index.Contains(probe));
        Assert.True(index.UpdateEntryBounds(probe, bounds));
        Assert.True(index.TryGetBounds(probe, out var stored));
        Assert.Equal(bounds, stored);
        var hits = new List<CollisionKey>();
        index.Query(bounds, hits);
        Assert.Single(hits);
        Assert.Same(original, hits[0]);
        Assert.Equal(1, index.Count);
        Assert.True(index.Remove(probe));
        Assert.False(index.Contains(original));
    }

    [Fact]
    public void Subdivision_UsesFourChildrenParentFirstOrderAndUpperHalfSplitPoints()
    {
        var world = Box(0, 0, 16, 16);
        var index = new SwiftQuadtree<int>(world, new SwiftQuadtreeOptions(1, 1, false), 1);
        for (int child = 3; child >= 0; child--)
            index.Insert(child, Quadrant(child));
        index.Insert(99, Point(8, 8));
        index.Insert(10, Box(7, 1, 9, 2));
        index.Insert(11, Box(1, 7, 2, 9));
        Assert.True(index.DebugRootHasChildren);
        Assert.Equal(5, index.DebugNodeCount);
        Assert.Equal(1, index.DebugMaxDepth);
        var hits = new List<int>();
        index.Query(world, hits);
        Assert.Equal(new[] { 10, 11, 0, 1, 2, 3, 99 }, hits);
        var children = Children(Root(index));
        Assert.Equal(4, children.Length);
        for (int child = 0; child < 4; child++)
        {
            hits.Clear();
            index.Query(Quadrant(child), hits);
            Assert.Equal(new[] { child }, hits);
            var bounds = (BoundVolume2D)Property(children.GetValue(child), "Bounds");
            Assert.Equal(Box((child & 1) == 0 ? 0 : 8, (child & 2) == 0 ? 0 : 8,
                (child & 1) == 0 ? 8 : 16, (child & 2) == 0 ? 8 : 16), bounds);
        }
        Assert.Equal(new[] { 3, 99 }, KeysInNode(index, children.GetValue(3)));
        hits.Clear();
        index.Query(Point(8, 8), hits);
        Assert.Equal(new[] { 99 }, hits);
    }

    [Fact]
    public void Subdivision_PreservesExistingStraddlersAtParentAndFiltersThemExactly()
    {
        var index = new SwiftQuadtree<int>(Box(0, 0, 16, 16), new SwiftQuadtreeOptions(3, 1, false), 1);
        index.Insert(10, Box(7, 1, 9, 2));
        index.Insert(11, Box(1, 7, 2, 9));
        index.Insert(1, Quadrant(0));
        index.Insert(2, Quadrant(3));
        Assert.Equal(new[] { 10, 11 }, KeysInNode(index, Root(index)));
        var hits = new List<int>();
        index.Query(Box(7, 0, 9, 3), hits);
        Assert.Equal(new[] { 10 }, hits);
        hits.Clear();
        index.Query(Box(0, 7, 3, 9), hits);
        Assert.Equal(new[] { 11 }, hits);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RemovalAndRelocation_RespectMergeChoice(bool merge)
    {
        var index = new SwiftQuadtree<int>(Box(0, 0, 16, 16), new SwiftQuadtreeOptions(3, 1, merge), 1);
        Assert.Equal(new SwiftQuadtreeOptions(3, 1, merge), index.Options);
        Assert.Equal(Box(0, 0, 16, 16), index.WorldBounds);
        index.Insert(1, Quadrant(0));
        index.Insert(2, Quadrant(3));
        Assert.True(index.DebugRootHasChildren);
        Assert.True(index.UpdateEntryBounds(1, Quadrant(1)));
        Assert.True(index.Remove(2));
        Assert.Equal(!merge, index.DebugRootHasChildren);
        Assert.Equal(merge ? 1 : 5, index.DebugNodeCount);
        var hits = new List<int>();
        index.Query(Quadrant(0), hits);
        Assert.Empty(hits);
        index.Query(Quadrant(1), hits);
        Assert.Equal(new[] { 1 }, hits);
        Assert.True(index.Remove(1));
        Assert.False(index.Remove(1));
        Assert.Equal(0, index.Count);
    }

    [Fact]
    public void Clear_ReleasesKeysRetainsEntryArrayAndReplacesNonemptyTopology()
    {
        var world = Box(0, 0, 16, 16);
        var index = new SwiftQuadtree<CollisionKey>(world, new SwiftQuadtreeOptions(3, 1, false), 1);
        index.Insert(new CollisionKey(1), Quadrant(0));
        index.Insert(new CollisionKey(2), Quadrant(3));
        object before = Root(index);
        Array entries = Entries(index);
        index.Clear();
        Assert.Equal(0, index.Count);
        Assert.Equal(world, index.WorldBounds);
        Assert.False(index.DebugRootHasChildren);
        Assert.Equal(1, index.DebugNodeCount);
        Assert.NotSame(before, Root(index));
        Assert.Same(entries, Entries(index));
        foreach (object entry in entries)
            Assert.Null(entry.GetType().GetField("Key").GetValue(entry));
        var hits = new List<CollisionKey> { new CollisionKey(-1) };
        index.Query(world, hits);
        Assert.Single(hits);
        Assert.Equal(-1, hits[0].Id);
        Assert.True(index.Insert(new CollisionKey(3), Quadrant(1)));
        Assert.True(index.Contains(new CollisionKey(3)));
    }

    [Fact]
    public void Clear_WhenAlreadyEmptyPreservesRetainedEmptyTopology()
    {
        var index = new SwiftQuadtree<int>(Box(0, 0, 16, 16), new SwiftQuadtreeOptions(2, 1, false), 1);
        index.Insert(1, Quadrant(0));
        index.Insert(2, Quadrant(3));
        index.Remove(1);
        index.Remove(2);
        Assert.True(index.DebugRootHasChildren);
        object root = Root(index);
        index.Clear();
        Assert.Same(root, Root(index));
        Assert.Equal(5, index.DebugNodeCount);
        Assert.Equal(0, index.Count);
    }

    [Fact]
    public void BackendExceptions_PreserveValidationOrderAndExistingNontransactionalState()
    {
        var backend = new ControlledPartitioner { ThrowContains = true };
        var index = new SwiftQuadtree<int, BoundVolume2D>(Box(0, 0, 16, 16), new SwiftQuadtreeOptions(2, 1), backend);
        Assert.Throws<InvalidOperationException>(() => index.Insert(1, Quadrant(0)));
        Assert.Equal(0, index.Count);
        Assert.False(index.Contains(1));
        int calls = backend.ContainsCalls;
        Assert.False(index.UpdateEntryBounds(99, default));
        Assert.Equal(calls, backend.ContainsCalls);
        backend.ThrowContains = false;
        Assert.True(index.Insert(1, Quadrant(0)));
        backend.ThrowSubdivision = true;
        Assert.Throws<InvalidOperationException>(() => index.Insert(2, Quadrant(3)));
        Assert.Equal(1, index.Count);
        Assert.True(index.Contains(2));
        Assert.True(index.TryGetBounds(2, out var partiallyInserted));
        Assert.Equal(Quadrant(3), partiallyInserted);
        backend.ThrowSubdivision = false;
        index.Clear();
        Assert.False(index.Contains(1));
        Assert.False(index.Contains(2));
        Assert.Equal(0, index.Count);
        Assert.True(index.Insert(3, Quadrant(1)));
    }

    [Fact]
    public void DestinationExceptions_KeepAppendedPrefixAndAllowLaterCompleteQueries()
    {
        var index = new SwiftQuadtree<int>(Box(0, 0, 16, 16), default, 1);
        index.Insert(1, Quadrant(0));
        index.Insert(2, Quadrant(1));
        index.Insert(3, Quadrant(2));
        var destination = new ThrowingResults(2);
        destination.Items.Add(-1);
        Assert.Throws<InvalidOperationException>(() => index.Query(index.WorldBounds, destination));
        Assert.Equal(new[] { -1, 1 }, destination.Items);
        var hits = new List<int> { -1 };
        index.Query(Box(20, 20, 21, 21), hits);
        Assert.Equal(new[] { -1 }, hits);
        index.Query(index.WorldBounds, hits);
        Assert.Equal(new[] { -1, 1, 2, 3 }, hits);
    }

    [Fact]
    public void GenericBackend_RequiresOnlyBoundVolumeInterface()
    {
        var index = new SwiftQuadtree<int, InterfaceOnlyVolume>(new(Box(0, 0, 16, 16)), new SwiftQuadtreeOptions(2, 1), new InterfaceOnlyPartitioner());
        Assert.True(index.Insert(1, new(Quadrant(0))));
        Assert.True(index.Insert(2, new(Quadrant(3))));
        Assert.True(index.UpdateEntryBounds(1, new(Quadrant(1))));
        var hits = new List<int>();
        index.Query(new(Quadrant(1)), hits);
        Assert.Equal(new[] { 1 }, hits);
        Assert.True(index.Remove(2));
        Assert.True(index.TryGetBounds(1, out var value));
        Assert.True(value.BoundsEquals(new(Quadrant(1))));
    }

    [Theory]
    [InlineData(1e38f, 3e38f)]
    [InlineData(-3e38f, -1e38f)]
    [InlineData(float.MinValue, float.MaxValue)]
    public void NumericMidpoints_ExtremeWorldsProduceFourContainedShrinkingChildren(float min, float max)
    {
        var world = Box(min, min, max, max);
        var index = new SwiftQuadtree<int>(world, new SwiftQuadtreeOptions(2, 1), 1);
        index.Insert(1, new(world.Min, world.Min));
        index.Insert(2, new(world.Max, world.Max));
        index.Insert(3, new(world.Min, world.Min));
        Assert.True(index.DebugRootHasChildren);
        Assert.Equal(2, index.DebugMaxDepth);
        AssertSubdivision(Root(index), world, 1);
        var hits = new List<int>();
        index.Query(new(world.Min, world.Min), hits);
        Assert.Equal(new[] { 1, 3 }, hits);
        hits.Clear();
        index.Query(new(world.Max, world.Max), hits);
        Assert.Equal(new[] { 2 }, hits);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(0, 2)]
    [InlineData(1, 2)]
    [InlineData(0, 3)]
    [InlineData(1, 3)]
    public void NumericMidpoints_RequireBothRoundedHalvesOnBothAxes(int axis, int shape)
    {
        float min = shape < 2 ? 1 : 0;
        float max = shape switch
        {
            0 => BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(min) + 1),
            1 => BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(min) + 3),
            2 => 3 * float.Epsilon,
            _ => 0
        };
        float minimum = shape switch { 1 => 3f / 16777216f, 2 => 2 * float.Epsilon, _ => float.Epsilon };
        var low = Vector2.Zero;
        var high = new Vector2(16);
        low[axis] = min;
        high[axis] = max;
        var world = new BoundVolume2D(low, high);
        var index = new SwiftQuadtree<int>(world, new SwiftQuadtreeOptions(2, 1), minimum);
        index.Insert(1, new(low, low));
        index.Insert(2, new(high, high));
        Assert.False(index.DebugRootHasChildren);
        Assert.Equal(1, index.DebugNodeCount);
        var hits = new List<int>();
        index.Query(world, hits);
        Assert.Equal(new[] { 1, 2 }, hits);
    }

    [Fact]
    public void NumericMidpoints_SubnormalChildrenMeetingMinimumRemainQueryable()
    {
        var world = Box(0, 0, 2 * float.Epsilon, 2 * float.Epsilon);
        var index = new SwiftQuadtree<int>(world, new SwiftQuadtreeOptions(2, 1), float.Epsilon);
        index.Insert(1, new(world.Min, world.Min));
        index.Insert(2, new(world.Max, world.Max));
        Assert.True(index.DebugRootHasChildren);
        Assert.Equal(1, index.DebugMaxDepth);
        AssertSubdivision(Root(index), world, float.Epsilon);
        var hits = new List<int>();
        index.Query(new(world.Min, world.Min), hits);
        Assert.Equal(new[] { 1 }, hits);
        hits.Clear();
        index.Query(new(world.Max, world.Max), hits);
        Assert.Equal(new[] { 2 }, hits);
    }

    [Fact]
    public void NumericMidpoints_OverflowingParentSpanDoesNotBypassMinimum()
    {
        var world = Box(float.MinValue, float.MinValue, float.MaxValue * 0.5f, float.MaxValue * 0.5f);
        var index = new SwiftQuadtree<int>(world, new SwiftQuadtreeOptions(2, 1), float.MaxValue * 0.8f);
        index.Insert(1, new(world.Min, world.Min));
        index.Insert(2, new(world.Max, world.Max));
        Assert.False(index.DebugRootHasChildren);
    }

    [Fact]
    public void GrowthCollisionsRelocationAndSlotReuse_AgreeWithIndependentHitSets()
    {
        const int count = 257;
        var world = Box(0, 0, 128, 128);
        var index = new SwiftQuadtree<CollisionKey>(world, new SwiftQuadtreeOptions(5, 2), 1);
        var bounds = new BoundVolume2D[count];
        var alive = new bool[count];
        var keys = new CollisionKey[count];
        uint state = 0xC0FFEE;
        for (int i = 0; i < count; i++)
        {
            int x = (int)(Next(ref state) % 100), y = (int)(Next(ref state) % 100);
            int size = i % 7 == 0 ? 8 : 1;
            bounds[i] = Box(x, y, x + size, y + size);
            keys[i] = new CollisionKey(i * 1024);
            Assert.True(index.Insert(keys[i], bounds[i]));
            alive[i] = true;
        }
        VerifySets(index, bounds, alive);
        Array entries = Entries(index);
        for (int i = 0; i < count; i += 3)
        {
            Assert.True(index.Remove(new CollisionKey(i * 1024)));
            alive[i] = false;
        }
        VerifySets(index, bounds, alive);
        for (int i = 0; i < count; i += 3)
        {
            bounds[i] = new(bounds[i].Min + new Vector2(9), bounds[i].Max + new Vector2(9));
            Assert.True(index.Insert(keys[i], bounds[i]));
            alive[i] = true;
        }
        Assert.Same(entries, Entries(index));
        for (int i = 0; i < count; i += 5)
        {
            bounds[i] = new(bounds[i].Min + new Vector2(0.5f), bounds[i].Max + new Vector2(0.5f));
            Assert.True(index.UpdateEntryBounds(new CollisionKey(i * 1024), bounds[i]));
        }
        VerifySets(index, bounds, alive);
        Assert.Equal(count, index.Count);
        index.Clear();
        Assert.Same(entries, Entries(index));
        for (int i = count - 1; i >= 0; i--)
            Assert.True(index.Insert(keys[i], bounds[i]));
        VerifySets(index, bounds, alive);
    }

    [Theory]
    [InlineData("Query", 8000)]
    [InlineData("Contains", 1000)]
    [InlineData("TryGetBounds", 1000)]
    [InlineData("UpdateEntryBounds", 1000)]
    [InlineData("Insert", 1000)]
    public void WarmTypedOperations_AllocateNothing(string operation, int expected)
    {
        var world = Box(0, 0, 16, 16);
        var index = new SwiftQuadtree<int>(world, new SwiftQuadtreeOptions(4, 2), 1);
        for (int i = 0; i < 8; i++) index.Insert(i, Point(i + 1, i + 1));
        var bounds = Point(2, 2);
        var hits = new SwiftList<int>(16);
        int observed = 0;
        Action action = operation switch
        {
            "Query" => () =>
            {
                observed = 0;
                for (int i = 0; i < 1000; i++)
                {
                    hits.Clear();
                    index.Query(world, hits);
                    observed += hits.Count;
                }
            }
            ,
            "Contains" => () =>
            {
                observed = 0;
                for (int i = 0; i < 1000; i++) if (index.Contains(1)) observed++;
            }
            ,
            "TryGetBounds" => () =>
            {
                observed = 0;
                for (int i = 0; i < 1000; i++)
                    if (index.TryGetBounds(1, out var found) && found == bounds) observed++;
            }
            ,
            "UpdateEntryBounds" => () =>
            {
                observed = 0;
                for (int i = 0; i < 1000; i++) if (index.UpdateEntryBounds(1, bounds)) observed++;
            }
            ,
            "Insert" => () =>
            {
                observed = 0;
                for (int i = 0; i < 1000; i++) if (!index.Insert(1, bounds)) observed++;
            }
            ,
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
        long allocated = MeasureAllocations(action);
        Assert.Equal(expected, observed);
        Assert.True(allocated == 0, $"{operation} allocated {allocated} managed bytes across 1000 warmed operations.");
    }

    private static void VerifySets(SwiftQuadtree<CollisionKey> index, BoundVolume2D[] bounds, bool[] alive)
    {
        var queries = new[] { Box(0, 0, 128, 128), Box(0, 0, 32, 32), Box(32, 32, 64, 64), Box(64, 0, 128, 64), Point(0, 0) };
        foreach (var query in queries)
        {
            var prefix = new CollisionKey(-1);
            var results = new List<CollisionKey> { prefix };
            index.Query(query, results);
            Assert.Same(prefix, results[0]);
            var expected = new List<int>();
            for (int i = 0; i < bounds.Length; i++)
                if (alive[i] && Overlaps(query, bounds[i])) expected.Add(i * 1024);
            var actual = new List<int>();
            for (int i = 1; i < results.Count; i++) actual.Add(results[i].Id);
            Assert.Equal(actual.Count, new HashSet<int>(actual).Count);
            actual.Sort();
            Assert.Equal(expected, actual);
        }
    }

    private static bool Overlaps(BoundVolume2D a, BoundVolume2D b) =>
        a.Min.X <= b.Max.X && b.Min.X <= a.Max.X && a.Min.Y <= b.Max.Y && b.Min.Y <= a.Max.Y;

    private static BoundVolume2D Box(float x0, float y0, float x1, float y1) => new(new Vector2(x0, y0), new Vector2(x1, y1));
    private static BoundVolume2D Point(float x, float y) => Box(x, y, x, y);
    private static BoundVolume2D Quadrant(int index)
    {
        float x = (index & 1) == 0 ? 1 : 12, y = (index & 2) == 0 ? 1 : 12;
        return Box(x, y, x + 1, y + 1);
    }

    private static object Inner(object owner)
    {
        for (Type type = owner.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField("_core", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field != null) return field.GetValue(owner);
        }
        return owner;
    }
    private static object Root(object owner) => Field(Inner(owner), "_root");
    private static Array Entries(object owner) => (Array)Field(Inner(owner), "_entries");
    private static object Field(object value, string name) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(value);
    private static object Property(object value, string name) => value.GetType().GetProperty(name).GetValue(value);
    private static Array Children(object node) => (Array)Property(node, "Children");
    private static List<int> KeysInNode(object owner, object node)
    {
        Array entries = Entries(owner);
        var indices = (SwiftList<int>)Property(node, "EntryIndices");
        var keys = new List<int>();
        foreach (int index in indices)
            keys.Add((int)entries.GetValue(index).GetType().GetField("Key").GetValue(entries.GetValue(index)));
        return keys;
    }

    private static void AssertSubdivision(object node, BoundVolume2D bounds, float minimum)
    {
        Array children = Children(node);
        if (children == null) return;
        Assert.Equal(4, children.Length);
        var midpoint = new Vector2((float)(((double)bounds.Min.X + bounds.Max.X) / 2), (float)(((double)bounds.Min.Y + bounds.Max.Y) / 2));
        for (int i = 0; i < children.Length; i++)
        {
            object child = children.GetValue(i);
            var value = (BoundVolume2D)Property(child, "Bounds");
            for (int axis = 0; axis < 2; axis++)
            {
                bool upper = (i & (1 << axis)) != 0;
                Assert.Equal(upper ? midpoint[axis] : bounds.Min[axis], value.Min[axis]);
                Assert.Equal(upper ? bounds.Max[axis] : midpoint[axis], value.Max[axis]);
                Assert.True(float.IsFinite(value.Min[axis]));
                Assert.True(float.IsFinite(value.Max[axis]));
                Assert.InRange(value.Min[axis], bounds.Min[axis], bounds.Max[axis]);
                Assert.InRange(value.Max[axis], bounds.Min[axis], bounds.Max[axis]);
                double span = (double)value.Max[axis] - value.Min[axis];
                Assert.True(span >= minimum);
                Assert.True(span < (double)bounds.Max[axis] - bounds.Min[axis]);
            }
            AssertSubdivision(child, value, minimum);
        }
    }

    private static uint Next(ref uint value) { value ^= value << 13; value ^= value >> 17; value ^= value << 5; return value; }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long MeasureAllocations(Action action)
    {
        action();
        long before = GC.GetAllocatedBytesForCurrentThread();
        action();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private sealed class CollisionKey
    {
        public CollisionKey(int id) => Id = id;
        public int Id { get; }
        public override int GetHashCode() => 0;
        public override bool Equals(object obj) => obj is CollisionKey other && other.Id == Id;
    }

    private sealed class ControlledPartitioner : IQuadtreeBoundsPartitioner<BoundVolume2D>
    {
        public bool ThrowContains;
        public bool ThrowSubdivision;
        public int ContainsCalls;
        public bool ContainsBounds(BoundVolume2D outer, BoundVolume2D inner)
        {
            ContainsCalls++;
            if (ThrowContains) throw new InvalidOperationException("Containment control failure.");
            return inner.Min.X >= outer.Min.X && inner.Min.Y >= outer.Min.Y && inner.Max.X <= outer.Max.X && inner.Max.Y <= outer.Max.Y;
        }
        public bool CanSubdivide(BoundVolume2D bounds)
        {
            if (ThrowSubdivision) throw new InvalidOperationException("Subdivision control failure.");
            return bounds.Max.X - bounds.Min.X >= 2 && bounds.Max.Y - bounds.Min.Y >= 2;
        }
        public bool TryGetContainingChildIndex(BoundVolume2D nodeBounds, BoundVolume2D entryBounds, out int childIndex)
        {
            float x = (nodeBounds.Min.X + nodeBounds.Max.X) / 2, y = (nodeBounds.Min.Y + nodeBounds.Max.Y) / 2;
            int xb = entryBounds.Min.X >= x ? 1 : entryBounds.Max.X <= x ? 0 : -1;
            int yb = entryBounds.Min.Y >= y ? 1 : entryBounds.Max.Y <= y ? 0 : -1;
            childIndex = xb < 0 || yb < 0 ? -1 : xb | (yb << 1);
            return childIndex >= 0;
        }
        public BoundVolume2D CreateChildBounds(BoundVolume2D parentBounds, int childIndex)
        {
            float x = (parentBounds.Min.X + parentBounds.Max.X) / 2, y = (parentBounds.Min.Y + parentBounds.Max.Y) / 2;
            return Box((childIndex & 1) == 0 ? parentBounds.Min.X : x, (childIndex & 2) == 0 ? parentBounds.Min.Y : y,
                (childIndex & 1) == 0 ? x : parentBounds.Max.X, (childIndex & 2) == 0 ? y : parentBounds.Max.Y);
        }
    }

    private readonly struct InterfaceOnlyVolume : IBoundVolume<InterfaceOnlyVolume>
    {
        public InterfaceOnlyVolume(BoundVolume2D bounds) => Bounds = bounds;
        public BoundVolume2D Bounds { get; }
        public InterfaceOnlyVolume Union(InterfaceOnlyVolume other) => new(Bounds.Union(other.Bounds));
        public bool Intersects(InterfaceOnlyVolume other) => Bounds.Intersects(other.Bounds);
        public long GetCost(InterfaceOnlyVolume other) => Bounds.GetCost(other.Bounds);
        public bool BoundsEquals(InterfaceOnlyVolume other) => Bounds.BoundsEquals(other.Bounds);
    }

    private sealed class InterfaceOnlyPartitioner : IQuadtreeBoundsPartitioner<InterfaceOnlyVolume>
    {
        private readonly ControlledPartitioner _inner = new();
        public bool ContainsBounds(InterfaceOnlyVolume outer, InterfaceOnlyVolume inner) => _inner.ContainsBounds(outer.Bounds, inner.Bounds);
        public bool CanSubdivide(InterfaceOnlyVolume bounds) => _inner.CanSubdivide(bounds.Bounds);
        public bool TryGetContainingChildIndex(InterfaceOnlyVolume node, InterfaceOnlyVolume entry, out int childIndex) => _inner.TryGetContainingChildIndex(node.Bounds, entry.Bounds, out childIndex);
        public InterfaceOnlyVolume CreateChildBounds(InterfaceOnlyVolume parent, int childIndex) => new(_inner.CreateChildBounds(parent.Bounds, childIndex));
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
