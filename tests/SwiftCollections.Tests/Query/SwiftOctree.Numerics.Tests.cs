using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Xunit;

namespace SwiftCollections.Query.Tests;

public class SwiftOctreeNumericsTests
{
    [Theory]
    [InlineData(1e38f, 3e38f)]
    [InlineData(-3e38f, -1e38f)]
    [InlineData(float.MinValue, float.MaxValue)]
    public void Midpoint_ExtremeFiniteWorldSubdividesIntoContainedShrinkingChildren(float min, float max)
    {
        var world = new BoundVolume(new Vector3(min), new Vector3(max));
        var lowerPoint = new BoundVolume(world.Min, world.Min);
        var upperPoint = new BoundVolume(world.Max, world.Max);
        var octree = new SwiftOctree<int>(world, new SwiftOctreeOptions(2, 1), 1f);
        Assert.Equal(new SwiftOctreeOptions(2, 1), octree.Options);
        Assert.Equal(world, octree.WorldBounds);
        Assert.True(octree.Insert(1, lowerPoint));
        Assert.True(octree.Insert(2, upperPoint));
        Assert.True(octree.Insert(3, lowerPoint));

        Assert.True(octree.DebugRootHasChildren);
        Assert.Equal(2, octree.DebugMaxDepth);
        object core = typeof(SwiftOctree<int, BoundVolume>)
            .GetField("_core", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(octree);
        object root = core.GetType().GetField("_root", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(core);
        AssertFiniteSubdivision(root, world, 1f);

        var results = new List<int>();
        octree.Query(lowerPoint, results);
        Assert.Equal(2, results.Count);
        Assert.Contains(1, results);
        Assert.Contains(3, results);
        results.Clear();
        octree.Query(upperPoint, results);
        Assert.Equal(new[] { 2 }, results);
        Assert.True(octree.UpdateEntryBounds(1, upperPoint));
        Assert.True(octree.Remove(3));
        Assert.False(octree.Remove(3));
        results.Clear();
        octree.Query(lowerPoint, results);
        Assert.Empty(results);
        Assert.True(octree.Insert(3, lowerPoint));
        results.Clear();
        octree.Query(world, results);
        results.Sort();
        Assert.Equal(new[] { 1, 2, 3 }, results);
        Assert.Equal(3, octree.Count);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(0, 2)]
    [InlineData(1, 2)]
    [InlineData(2, 2)]
    [InlineData(0, 3)]
    [InlineData(1, 3)]
    [InlineData(2, 3)]
    public void Midpoint_RefusesSubdivisionWhenEitherRoundedChildEdgeIsTooSmall(int axis, int shape)
    {
        float min = shape < 2 ? 1f : 0f;
        float max = shape switch
        {
            0 => MathF.BitIncrement(min),
            1 => BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(min) + 3),
            2 => 3f * float.Epsilon,
            _ => min
        };
        float minNodeSize = shape switch
        {
            1 => 3f / 16777216f,
            2 => 2f * float.Epsilon,
            _ => float.Epsilon
        };
        var lower = Vector3.Zero;
        var upper = new Vector3(16f);
        lower[axis] = min;
        upper[axis] = max;
        var world = new BoundVolume(lower, upper);
        var octree = new SwiftOctree<int>(world, new SwiftOctreeOptions(2, 1), minNodeSize);
        Assert.True(octree.Insert(1, new BoundVolume(lower, lower)));
        Assert.True(octree.Insert(2, new BoundVolume(upper, upper)));

        Assert.False(octree.DebugRootHasChildren);
        Assert.Equal(1, octree.DebugNodeCount);
        var results = new List<int>();
        octree.Query(world, results);
        Assert.Equal(new[] { 1, 2 }, results);
    }

    [Fact]
    public void Midpoint_OverflowingParentSpanDoesNotBypassMinimumChildSize()
    {
        var world = new BoundVolume(new Vector3(float.MinValue), new Vector3(float.MaxValue * 0.5f));
        var octree = new SwiftOctree<int>(world, new SwiftOctreeOptions(2, 1), float.MaxValue * 0.8f);
        Assert.True(octree.Insert(1, new BoundVolume(world.Min, world.Min)));
        Assert.True(octree.Insert(2, new BoundVolume(world.Max, world.Max)));

        Assert.False(octree.DebugRootHasChildren);
        Assert.Equal(1, octree.DebugNodeCount);
        Assert.Equal(2, octree.Count);
    }

    [Fact]
    public void Midpoint_SubnormalChildrenMeetingMinimumSizeRemainQueryable()
    {
        var world = new BoundVolume(Vector3.Zero, new Vector3(2f * float.Epsilon));
        var octree = new SwiftOctree<int>(world, new SwiftOctreeOptions(2, 1), float.Epsilon);
        var lower = new BoundVolume(world.Min, world.Min);
        var upper = new BoundVolume(world.Max, world.Max);
        Assert.True(octree.Insert(1, lower));
        Assert.True(octree.Insert(2, upper));

        Assert.True(octree.DebugRootHasChildren);
        Assert.Equal(1, octree.DebugMaxDepth);
        var results = new List<int>();
        octree.Query(lower, results);
        Assert.Equal(new[] { 1 }, results);
        results.Clear();
        octree.Query(upper, results);
        Assert.Equal(new[] { 2 }, results);
    }

    private static void AssertFiniteSubdivision(object node, BoundVolume bounds, float minNodeSize)
    {
        var children = (Array)node.GetType().GetProperty("Children").GetValue(node);
        if (children is null)
            return;

        Assert.Equal(8, children.Length);
        var midpoint = new Vector3(
            (float)(((double)bounds.Min.X + bounds.Max.X) / 2d),
            (float)(((double)bounds.Min.Y + bounds.Max.Y) / 2d),
            (float)(((double)bounds.Min.Z + bounds.Max.Z) / 2d));
        for (int i = 0; i < children.Length; i++)
        {
            object child = children.GetValue(i);
            var childBounds = (BoundVolume)child.GetType().GetProperty("Bounds").GetValue(child);
            for (int axis = 0; axis < 3; axis++)
            {
                bool upper = (i & (1 << axis)) != 0;
                Assert.Equal(upper ? midpoint[axis] : bounds.Min[axis], childBounds.Min[axis]);
                Assert.Equal(upper ? bounds.Max[axis] : midpoint[axis], childBounds.Max[axis]);
                Assert.True(float.IsFinite(childBounds.Min[axis]));
                Assert.True(float.IsFinite(childBounds.Max[axis]));
                Assert.InRange(childBounds.Min[axis], bounds.Min[axis], bounds.Max[axis]);
                Assert.InRange(childBounds.Max[axis], bounds.Min[axis], bounds.Max[axis]);
                double length = (double)childBounds.Max[axis] - childBounds.Min[axis];
                Assert.True(length >= minNodeSize);
                Assert.True(length < (double)bounds.Max[axis] - bounds.Min[axis]);
            }
            AssertFiniteSubdivision(child, childBounds, minNodeSize);
        }
    }

    [Fact]
    public void NumericsWrapper_UsesBoundVolumeAdapter()
    {
        var worldBounds = new BoundVolume(new Vector3(0, 0, 0), new Vector3(16, 16, 16));
        var octree = new SwiftOctree<int>(worldBounds, new SwiftOctreeOptions(4, 2), 1f);
        octree.Insert(1, new BoundVolume(new Vector3(1, 1, 1), new Vector3(2, 2, 2)));

        var results = new List<int>();
        octree.Query(new BoundVolume(new Vector3(0, 0, 0), new Vector3(4, 4, 4)), results);

        Assert.Single(results);
        Assert.Equal(1, results[0]);
    }

    [Fact]
    public void NumericsWrapper_UpdateEntryBounds_MovesEntriesAcrossRegions()
    {
        var worldBounds = new BoundVolume(new Vector3(0, 0, 0), new Vector3(16, 16, 16));
        var octree = new SwiftOctree<int>(worldBounds, new SwiftOctreeOptions(5, 1), 1f);
        octree.Insert(1, new BoundVolume(new Vector3(1, 1, 1), new Vector3(2, 2, 2)));

        Assert.True(octree.UpdateEntryBounds(1, new BoundVolume(new Vector3(10, 10, 10), new Vector3(12, 12, 12))));

        var results = new List<int>();
        octree.Query(new BoundVolume(new Vector3(9, 9, 9), new Vector3(13, 13, 13)), results);

        Assert.Single(results);
        Assert.Equal(1, results[0]);
    }

    [Fact]
    public void NumericsWrapper_SubdividesIntoAllChildOctantsAndQueriesEachRegion()
    {
        var worldBounds = new BoundVolume(new Vector3(0, 0, 0), new Vector3(16, 16, 16));
        var octree = new SwiftOctree<int>(worldBounds, new SwiftOctreeOptions(4, 1), 1f);

        for (int childIndex = 0; childIndex < 8; childIndex++)
            Assert.True(octree.Insert(childIndex, CreateOctantVolume(childIndex)));

        Assert.True(octree.DebugRootHasChildren);
        Assert.True(octree.DebugNodeCount >= 9);

        for (int childIndex = 0; childIndex < 8; childIndex++)
        {
            var results = new List<int>();
            octree.Query(CreateOctantVolume(childIndex), results);

            Assert.Contains(childIndex, results);
        }
    }

    [Fact]
    public void NumericsWrapper_KeepsMidpointSpanningBoundsQueryableAtRoot()
    {
        var worldBounds = new BoundVolume(new Vector3(0, 0, 0), new Vector3(16, 16, 16));
        var octree = new SwiftOctree<int>(worldBounds, new SwiftOctreeOptions(4, 1), 1f);
        octree.Insert(1, new BoundVolume(new Vector3(1, 1, 1), new Vector3(2, 2, 2)));
        octree.Insert(2, new BoundVolume(new Vector3(12, 12, 12), new Vector3(13, 13, 13)));

        octree.Insert(10, new BoundVolume(new Vector3(7, 1, 1), new Vector3(9, 2, 2)));
        octree.Insert(11, new BoundVolume(new Vector3(1, 7, 1), new Vector3(2, 9, 2)));
        octree.Insert(12, new BoundVolume(new Vector3(1, 1, 7), new Vector3(2, 2, 9)));

        var results = new List<int>();
        octree.Query(new BoundVolume(new Vector3(0, 0, 0), new Vector3(10, 10, 10)), results);

        Assert.Contains(10, results);
        Assert.Contains(11, results);
        Assert.Contains(12, results);
    }

    [Fact]
    public void NumericsWrapper_MinNodeSizePreventsSubdividingSmallChildren()
    {
        var worldBounds = new BoundVolume(new Vector3(0, 0, 0), new Vector3(2, 2, 2));
        var octree = new SwiftOctree<int>(worldBounds, new SwiftOctreeOptions(4, 1), 2f);

        octree.Insert(1, new BoundVolume(new Vector3(0.1f, 0.1f, 0.1f), new Vector3(0.2f, 0.2f, 0.2f)));
        octree.Insert(2, new BoundVolume(new Vector3(1.6f, 1.6f, 1.6f), new Vector3(1.8f, 1.8f, 1.8f)));

        Assert.False(octree.DebugRootHasChildren);
        Assert.Equal(1, octree.DebugNodeCount);
    }

    [Fact]
    public void NumericsWrapper_ClearRemovesEntriesAndResetsTreeShape()
    {
        var worldBounds = new BoundVolume(new Vector3(0, 0, 0), new Vector3(16, 16, 16));
        var octree = new SwiftOctree<int>(worldBounds, new SwiftOctreeOptions(4, 1), 1f);
        octree.Insert(1, new BoundVolume(new Vector3(1, 1, 1), new Vector3(2, 2, 2)));
        octree.Insert(2, new BoundVolume(new Vector3(12, 12, 12), new Vector3(13, 13, 13)));

        octree.Clear();

        var results = new List<int>();
        octree.Query(worldBounds, results);

        Assert.Empty(results);
        Assert.Equal(0, octree.Count);
        Assert.False(octree.DebugRootHasChildren);
        Assert.True(octree.Insert(3, new BoundVolume(new Vector3(3, 3, 3), new Vector3(4, 4, 4))));
        Assert.True(octree.Contains(3));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void NumericsWrapper_RejectsInvalidMinimumNodeSize(float minNodeSize)
    {
        var worldBounds = new BoundVolume(new Vector3(0, 0, 0), new Vector3(16, 16, 16));

        Assert.Throws<ArgumentOutOfRangeException>(() => new SwiftOctree<int>(worldBounds, new SwiftOctreeOptions(4, 1), minNodeSize));
    }

    [Theory]
    [InlineData(-1f, 1f, 1f, 2f, 2f, 2f)]
    [InlineData(1f, -1f, 1f, 2f, 2f, 2f)]
    [InlineData(1f, 1f, -1f, 2f, 2f, 2f)]
    [InlineData(1f, 1f, 1f, 17f, 2f, 2f)]
    [InlineData(1f, 1f, 1f, 2f, 17f, 2f)]
    [InlineData(1f, 1f, 1f, 2f, 2f, 17f)]
    public void NumericsWrapper_InsertRejectsBoundsOutsideWorld(
        float minX,
        float minY,
        float minZ,
        float maxX,
        float maxY,
        float maxZ)
    {
        var worldBounds = new BoundVolume(new Vector3(0, 0, 0), new Vector3(16, 16, 16));
        var octree = new SwiftOctree<int>(worldBounds, new SwiftOctreeOptions(4, 1), 1f);
        var bounds = new BoundVolume(new Vector3(minX, minY, minZ), new Vector3(maxX, maxY, maxZ));

        Assert.Throws<ArgumentOutOfRangeException>(() => octree.Insert(1, bounds));
        Assert.Equal(0, octree.Count);
    }

    private static BoundVolume CreateOctantVolume(int childIndex)
    {
        float minX = (childIndex & 1) == 0 ? 1f : 10f;
        float minY = (childIndex & 2) == 0 ? 1f : 10f;
        float minZ = (childIndex & 4) == 0 ? 1f : 10f;

        return new BoundVolume(
            new Vector3(minX, minY, minZ),
            new Vector3(minX + 1f, minY + 1f, minZ + 1f));
    }
}
