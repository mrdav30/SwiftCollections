using SwiftCollections.Diagnostics;
using SwiftCollections.Utility;
using System;
using System.Collections.Generic;
using System.Numerics;
using Xunit;

namespace SwiftCollections.Query.Tests;

[Collection("SharedDiagnostics")]
public class QuerySharedInfrastructureTests
{
    [Fact]
    public void SwiftBVHNode_DefaultAndResetExposeRelationshipFlags()
    {
        var node = SwiftBVHNode<int, BoundVolume>.Default;

        Assert.False(node.HasParent);
        Assert.False(node.HasLeftChild);
        Assert.False(node.HasRightChild);
        Assert.False(node.HasChildren);

        node.ParentIndex = 0;
        node.LeftChildIndex = 1;
        node.RightChildIndex = 2;
        node.Bounds = new BoundVolume(Vector3.Zero, Vector3.One);
        node.Value = 42;
        node.IsLeaf = true;
        node.IsAllocated = true;
        node.SubtreeSize = 3;

        Assert.True(node.HasParent);
        Assert.True(node.HasLeftChild);
        Assert.True(node.HasRightChild);
        Assert.True(node.HasChildren);

        node.Reset();

        Assert.False(node.HasParent);
        Assert.False(node.HasLeftChild);
        Assert.False(node.HasRightChild);
        Assert.False(node.HasChildren);
        Assert.False(node.IsAllocated);
        Assert.False(node.IsLeaf);
        Assert.Equal(-1, node.MyIndex);
    }

    [Fact]
    public void QueryKeyIndexMap_Remove_RehashesCollidingEntries()
    {
        var entries = new[]
        {
            new Entry(new CollidingKey(1), true),
            new Entry(new CollidingKey(2), true),
            new Entry(new CollidingKey(3), true)
        };
        var map = new QueryKeyIndexMap<CollidingKey>(
            4,
            (index, key) => entries[index].Active && entries[index].Key.Equals(key),
            index => entries[index].Active,
            index => entries[index].Key);

        map.Insert(entries[0].Key, 0);
        map.Insert(entries[1].Key, 1);
        map.Insert(entries[2].Key, 2);

        bool removed = map.Remove(entries[0].Key);

        Assert.True(removed);
        Assert.Equal(-1, map.Find(entries[0].Key));
        Assert.Equal(1, map.Find(entries[1].Key));
        Assert.Equal(2, map.Find(entries[2].Key));
    }

    [Fact]
    public void QueryKeyIndexMap_Remove_MissingKey_ReturnsFalse()
    {
        var entries = new[]
        {
            new Entry(new CollidingKey(1), true)
        };
        var map = new QueryKeyIndexMap<CollidingKey>(
            4,
            (index, key) => entries[index].Active && entries[index].Key.Equals(key),
            index => entries[index].Active,
            index => entries[index].Key);

        map.Insert(entries[0].Key, 0);

        bool removed = map.Remove(new CollidingKey(2));

        Assert.False(removed);
        Assert.Equal(0, map.Find(entries[0].Key));
    }

    [Fact]
    public void QueryKeyIndexMap_Remove_AfterWarmup_DoesNotAllocate()
    {
        const int EntryCount = 64;
        var keys = new int[EntryCount];
        var map = new QueryKeyIndexMap<int>(
            EntryCount,
            (index, key) => keys[index] == key,
            _ => true,
            index => keys[index]);

        for (int i = 0; i < EntryCount; i++)
        {
            keys[i] = i;
            map.Insert(i, i);
        }
        for (int i = 0; i < EntryCount; i++)
            Assert.True(map.Remove(i));
        for (int i = 0; i < EntryCount; i++)
            map.Insert(i, i);

        long before = GC.GetAllocatedBytesForCurrentThread();
        bool removed = true;
        for (int i = 0; i < EntryCount; i++)
            removed &= map.Remove(i);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(removed);
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void QueryTraversalScratch_RentIntStack_ReusesAndClearsThePerThreadStack()
    {
        var scratch = new QueryTraversalScratch();

        SwiftIntStack first = scratch.RentIntStack(2);
        first.Push(10);
        first.Push(20);

        SwiftIntStack second = scratch.RentIntStack(16);

        Assert.Same(first, second);
        Assert.Equal(0, second.Count);
        Assert.True(second.Array.Length >= 16);
    }

    [Fact]
    public void QueryCollectionDiagnostics_WriteInfo_EmitsThroughSharedChannel()
    {
        DiagnosticLevel originalLevel = SwiftCollectionDiagnostics.Shared.MinimumLevel;
        DiagnosticSink originalSink = SwiftCollectionDiagnostics.Shared.Sink;
        var gate = new object();
        var events = new List<DiagnosticEvent>();

        try
        {
            SwiftCollectionDiagnostics.Shared.MinimumLevel = DiagnosticLevel.Info;
            SwiftCollectionDiagnostics.Shared.Sink = (in DiagnosticEvent diagnostic) =>
            {
                lock (gate)
                    events.Add(diagnostic);
            };

            SwiftCollectionDiagnostics.Shared.Info($"diagnostic message", "QueryTests");

            DiagnosticEvent matched = default;
            int matchCount = 0;
            lock (gate)
            {
                for (int i = 0; i < events.Count; i++)
                {
                    DiagnosticEvent diagnostic = events[i];
                    if (diagnostic.Source == "QueryTests"
                        && diagnostic.Message == "diagnostic message"
                        && diagnostic.Level == DiagnosticLevel.Info)
                    {
                        matched = diagnostic;
                        matchCount++;
                    }
                }
            }

            Assert.Equal(1, matchCount);
            Assert.Equal("QueryTests", matched.Source);
            Assert.Equal("diagnostic message", matched.Message);
            Assert.Equal(DiagnosticLevel.Info, matched.Level);
        }
        finally
        {
            SwiftCollectionDiagnostics.Shared.MinimumLevel = originalLevel;
            SwiftCollectionDiagnostics.Shared.Sink = originalSink;
        }
    }

    [Fact]
    public void SwiftBvhDiagnostics_WhenResizeAndTraversalErrorOccur_EmitExpectedEvents()
    {
        DiagnosticLevel originalLevel = SwiftCollectionDiagnostics.Shared.MinimumLevel;
        DiagnosticSink originalSink = SwiftCollectionDiagnostics.Shared.Sink;
        var events = new List<DiagnosticEvent>();
        var bounds = new BoundVolume(new Vector3(0, 0, 0), new Vector3(1, 1, 1));

        try
        {
            SwiftCollectionDiagnostics.Shared.MinimumLevel = DiagnosticLevel.Info;
            SwiftCollectionDiagnostics.Shared.Sink = (in DiagnosticEvent diagnostic) => events.Add(diagnostic);

            var bvh = new SwiftBVH<int>(1);
            bvh.Insert(1, bounds);
            bvh.NodePool[bvh.RootNodeIndex].IsAllocated = false;

            Assert.Throws<InvalidOperationException>(() => bvh.Query(bounds, new List<int>()));

            Assert.Contains(events, diagnostic =>
                diagnostic.Level == DiagnosticLevel.Info
                && diagnostic.Source == "SwiftBVH"
                && diagnostic.Message.Contains("Resized BVH storage"));
            Assert.Contains(events, diagnostic =>
                diagnostic.Level == DiagnosticLevel.Error
                && diagnostic.Source == "SwiftBVH"
                && diagnostic.Message.Contains("Encountered an unallocated node"));
        }
        finally
        {
            SwiftCollectionDiagnostics.Shared.MinimumLevel = originalLevel;
            SwiftCollectionDiagnostics.Shared.Sink = originalSink;
        }
    }

    [Fact]
    public void SwiftOctreeDiagnostics_WhenEntryStorageResizes_EmitsExpectedEvent()
    {
        DiagnosticLevel originalLevel = SwiftCollectionDiagnostics.Shared.MinimumLevel;
        DiagnosticSink originalSink = SwiftCollectionDiagnostics.Shared.Sink;
        var events = new List<DiagnosticEvent>();
        var world = new BoundVolume(new Vector3(0, 0, 0), new Vector3(32, 32, 32));

        try
        {
            SwiftCollectionDiagnostics.Shared.MinimumLevel = DiagnosticLevel.Info;
            SwiftCollectionDiagnostics.Shared.Sink = (in DiagnosticEvent diagnostic) => events.Add(diagnostic);

            var octree = new SwiftOctree<int>(world, new SwiftOctreeOptions(4, 4), 1f);
            for (int i = 0; i < 5; i++)
            {
                float min = i + 1;
                octree.Insert(i, new BoundVolume(new Vector3(min, min, min), new Vector3(min + 0.25f, min + 0.25f, min + 0.25f)));
            }

            Assert.Contains(events, diagnostic =>
                diagnostic.Level == DiagnosticLevel.Info
                && diagnostic.Source == "SwiftOctree"
                && diagnostic.Message.Contains("Resized octree entry storage"));
        }
        finally
        {
            SwiftCollectionDiagnostics.Shared.MinimumLevel = originalLevel;
            SwiftCollectionDiagnostics.Shared.Sink = originalSink;
        }
    }

    [Fact]
    public void DeterministicBoundVolumeDataset_Create_ReturnsStableSequenceForSharedFixtures()
    {
        IReadOnlyList<BoundVolume> first = DeterministicBoundVolumeDataset.Create(3, 9876);
        IReadOnlyList<BoundVolume> second = DeterministicBoundVolumeDataset.Create(3, 9876);

        Assert.Equal(first.Count, second.Count);
        for (int i = 0; i < first.Count; i++)
            Assert.True(first[i].BoundsEquals(second[i]));
    }

    [Fact]
    public void BoundVolume_ObjectEquality_ComparesBounds()
    {
        var left = new BoundVolume(new Vector3(0, 0, 0), new Vector3(1, 1, 1));
        var same = new BoundVolume(new Vector3(0, 0, 0), new Vector3(1, 1, 1));
        var different = new BoundVolume(new Vector3(1, 1, 1), new Vector3(2, 2, 2));

        Assert.True(left.Equals((object)same));
        Assert.False(left.Equals((object)different));
        Assert.False(left.Equals((object)"not a volume"));
    }

    [Fact]
    public void SwiftOctreeOptions_ValidateAndCompareValues()
    {
        var options = new SwiftOctreeOptions(3, 2, false);
        var same = new SwiftOctreeOptions(3, 2, false);
        var differentDepth = new SwiftOctreeOptions(4, 2, false);
        var differentCapacity = new SwiftOctreeOptions(3, 3, false);
        var differentMerge = new SwiftOctreeOptions(3, 2, true);

        Assert.Equal(3, options.MaxDepth);
        Assert.Equal(2, options.NodeCapacity);
        Assert.False(options.EnableMergeOnRemove);
        Assert.True(options.Equals(same));
        Assert.True(options.Equals((object)same));
        Assert.False(options.Equals((object)"not options"));
        Assert.False(options.Equals(differentDepth));
        Assert.False(options.Equals(differentCapacity));
        Assert.False(options.Equals(differentMerge));
        Assert.True(options == same);
        Assert.True(options != differentDepth);
        Assert.Equal(options.GetHashCode(), same.GetHashCode());
        Assert.Throws<ArgumentOutOfRangeException>(() => new SwiftOctreeOptions(-1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SwiftOctreeOptions(1, 0));
    }

    [Fact]
    public void SwiftSpatialHashOptions_ValidateAndCompareValues()
    {
        var options = new SwiftSpatialHashOptions(2);
        var same = new SwiftSpatialHashOptions(2);
        var different = new SwiftSpatialHashOptions(3);

        Assert.Equal(1, SwiftSpatialHashOptions.Default.NeighborhoodPadding);
        Assert.Equal(2, options.NeighborhoodPadding);
        Assert.True(options.Equals(same));
        Assert.True(options.Equals((object)same));
        Assert.False(options.Equals((object)"not options"));
        Assert.False(options.Equals(different));
        Assert.True(options == same);
        Assert.True(options != different);
        Assert.Equal(options.GetHashCode(), same.GetHashCode());
        Assert.Throws<ArgumentOutOfRangeException>(() => new SwiftSpatialHashOptions(-1));
    }

    [Fact]
    public void SwiftSpatialHashCellIndex_EqualityOperatorsHashAndStringUseCoordinates()
    {
        var cell = new SwiftSpatialHashCellIndex(1, 2, 3);
        var same = new SwiftSpatialHashCellIndex(1, 2, 3);
        var differentX = new SwiftSpatialHashCellIndex(9, 2, 3);
        var differentY = new SwiftSpatialHashCellIndex(1, 9, 3);
        var different = new SwiftSpatialHashCellIndex(1, 2, 4);

        Assert.True(cell.Equals(same));
        Assert.True(cell.Equals((object)same));
        Assert.False(cell.Equals((object)"not a cell"));
        Assert.False(cell.Equals(differentX));
        Assert.False(cell.Equals(differentY));
        Assert.True(cell == same);
        Assert.True(cell != different);
        Assert.False(cell == different);
        Assert.Equal(cell.GetHashCode(), same.GetHashCode());
        Assert.Equal(SwiftHashTools.CombineHashCodes(1, 2, 3), cell.GetHashCode());
        Assert.Equal("(1, 2, 3)", cell.ToString());
    }

    private readonly struct Entry
    {
        public Entry(CollidingKey key, bool active)
        {
            Key = key;
            Active = active;
        }

        public CollidingKey Key { get; }

        public bool Active { get; }
    }

    private sealed class CollidingKey
    {
        public CollidingKey(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public override bool Equals(object obj)
        {
            return obj is CollidingKey other && other.Value == Value;
        }

        public override int GetHashCode() => 1;
    }

}
