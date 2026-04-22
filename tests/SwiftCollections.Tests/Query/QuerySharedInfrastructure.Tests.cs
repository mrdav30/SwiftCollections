using SwiftCollections.Diagnostics;
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

        try
        {
            SwiftCollectionDiagnostics.Shared.MinimumLevel = DiagnosticLevel.Info;
            SwiftCollectionDiagnostics.Shared.Sink = static (in DiagnosticEvent diagnostic) =>
            {
                QuerySharedInfrastructureTestsState.Events.Add(diagnostic);
            };

            QuerySharedInfrastructureTestsState.Events.Clear();
            QueryCollectionDiagnostics.WriteInfo("QueryTests", "diagnostic message");

            Assert.Single(QuerySharedInfrastructureTestsState.Events);
            Assert.Equal("QueryTests", QuerySharedInfrastructureTestsState.Events[0].Source);
            Assert.Equal("diagnostic message", QuerySharedInfrastructureTestsState.Events[0].Message);
            Assert.Equal(DiagnosticLevel.Info, QuerySharedInfrastructureTestsState.Events[0].Level);
        }
        finally
        {
            SwiftCollectionDiagnostics.Shared.MinimumLevel = originalLevel;
            SwiftCollectionDiagnostics.Shared.Sink = originalSink;
            QuerySharedInfrastructureTestsState.Events.Clear();
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

    private static class QuerySharedInfrastructureTestsState
    {
        public static List<DiagnosticEvent> Events { get; } = new List<DiagnosticEvent>();
    }
}
