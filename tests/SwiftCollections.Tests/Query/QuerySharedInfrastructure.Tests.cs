using SwiftCollections.Diagnostics;
using System;
using System.Collections.Generic;
using Xunit;

namespace SwiftCollections.Query.Tests;

public class QuerySharedInfrastructureTests
{
    [Fact]
    public void QueryKeyIndexMap_Remove_RehashesCollidingEntries()
    {
        var map = new QueryKeyIndexMap<CollidingKey>(4);
        var entries = new[]
        {
            new Entry(new CollidingKey(1), true),
            new Entry(new CollidingKey(2), true),
            new Entry(new CollidingKey(3), true)
        };

        map.Insert(entries[0].Key, 0);
        map.Insert(entries[1].Key, 1);
        map.Insert(entries[2].Key, 2);

        bool removed = map.Remove(
            entries[0].Key,
            (index, key) => entries[index].Active && entries[index].Key.Equals(key),
            index => entries[index].Active,
            index => entries[index].Key);

        Assert.True(removed);
        Assert.Equal(-1, map.Find(entries[0].Key, (index, key) => entries[index].Active && entries[index].Key.Equals(key)));
        Assert.Equal(1, map.Find(entries[1].Key, (index, key) => entries[index].Active && entries[index].Key.Equals(key)));
        Assert.Equal(2, map.Find(entries[2].Key, (index, key) => entries[index].Active && entries[index].Key.Equals(key)));
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

            QueryCollectionDiagnostics.WriteInfo("QueryTests", "diagnostic message");

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
    public void DeterministicBoundVolumeDataset_Create_ReturnsStableSequenceForSharedFixtures()
    {
        IReadOnlyList<BoundVolume> first = DeterministicBoundVolumeDataset.Create(3, 9876);
        IReadOnlyList<BoundVolume> second = DeterministicBoundVolumeDataset.Create(3, 9876);

        Assert.Equal(first.Count, second.Count);
        for (int i = 0; i < first.Count; i++)
            Assert.True(first[i].BoundsEquals(second[i]));
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
        var different = new SwiftSpatialHashCellIndex(1, 2, 4);

        Assert.True(cell.Equals(same));
        Assert.True(cell.Equals((object)same));
        Assert.False(cell.Equals((object)"not a cell"));
        Assert.True(cell == same);
        Assert.True(cell != different);
        Assert.False(cell == different);
        Assert.Equal(cell.GetHashCode(), same.GetHashCode());
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
