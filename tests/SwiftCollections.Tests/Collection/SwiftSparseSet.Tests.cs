using MemoryPack;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace SwiftCollections.Tests;

public class SwiftSparseSetTests
{
    [Fact]
    public void Constructor_WithExplicitZeroCapacities_UsesEmptyStorage()
    {
        var set = new SwiftSparseSet(0, 0);

        Assert.Empty(set);
        Assert.Equal(0, set.SparseCapacity);
        Assert.Equal(0, set.DenseCapacity);
        Assert.Empty(set.DenseKeys);
    }

    [Fact]
    public void Add_InsertsIdAndExposesDenseKeys()
    {
        var set = new SwiftSparseSet
        {
            5
        };

        Assert.Contains(5, set);
        Assert.True(set.ContainsKey(5));
        Assert.Single(set);
        Assert.Equal(5, set.DenseKeys[0]);
        Assert.Equal(new[] { 5 }, set.Keys.ToArray());
        Assert.Equal(new[] { 5 }, set.AsReadOnlySpan().ToArray());
    }

    [Fact]
    public void Add_DuplicateId_ReturnsFalseWithoutChangingCount()
    {
        var set = new SwiftSparseSet();

        Assert.True(set.Add(2));
        Assert.False(set.Add(2));

        Assert.Single(set);
        Assert.Equal(new[] { 2 }, set.Keys.ToArray());
    }

    [Fact]
    public void AddAliases_InsertIdsThroughTryAddAndCollectionInterface()
    {
        var set = new SwiftSparseSet();
        ICollection<int> collection = set;

        Assert.True(set.TryAdd(1));
        collection.Add(2);

        Assert.False(set.IsReadOnly);
        Assert.Contains(1, set);
        Assert.Contains(2, set);
    }

    [Fact]
    public void Add_InvalidIds_Throws()
    {
        var set = new SwiftSparseSet();

        Assert.Throws<ArgumentOutOfRangeException>(() => { set.Add(-1); });
        Assert.Throws<ArgumentOutOfRangeException>(() => { set.Add(int.MaxValue); });
    }

    [Fact]
    public void ContainsAndRemove_ReturnFalseForMissingOrNegativeIds()
    {
        var set = new SwiftSparseSet
        {
            1
        };

        Assert.False(set.ContainsKey(-1));
        Assert.False(set.ContainsKey(64));
        Assert.False(set.Remove(-1));
        Assert.False(set.Remove(64));
    }

    [Fact]
    public void Remove_PerformsSwapBackAndKeepsDenseStorageContiguous()
    {
        var set = new SwiftSparseSet
        {
            4,
            8,
            12
        };

        Assert.True(set.Remove(8));

        Assert.Equal(2, set.Count);
        Assert.Contains(4, set);
        Assert.Contains(12, set);
        Assert.DoesNotContain(8, set);
        Assert.All(set.Keys.ToArray(), key => Assert.True(set.ContainsKey(key)));
    }

    [Fact]
    public void Clear_RemovesAllIdsAndAllowsReAdd()
    {
        var set = new SwiftSparseSet
        {
            1,
            2
        };

        set.Clear();
        set.Add(2);

        Assert.Single(set);
        Assert.DoesNotContain(1, set);
        Assert.Contains(2, set);
    }

    [Fact]
    public void TrimExcess_ShrinksDenseAndSparseStorageWhilePreservingIds()
    {
        var set = new SwiftSparseSet();
        set.EnsureDenseCapacity(64);
        set.EnsureSparseCapacity(256);
        set.Add(1);
        set.Add(64);
        set.Remove(64);

        int denseCapacityBefore = set.DenseCapacity;
        int sparseCapacityBefore = set.SparseCapacity;

        set.TrimExcess();

        Assert.True(set.DenseCapacity < denseCapacityBefore);
        Assert.True(set.SparseCapacity < sparseCapacityBefore);
        Assert.Contains(1, set);
    }

    [Fact]
    public void Enumerator_ReturnsAllIdsAndResetRestartsIteration()
    {
        var set = new SwiftSparseSet
        {
            1,
            2
        };

        var enumerator = set.GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.Equal(1, enumerator.Current);

        enumerator.Reset();

        Assert.True(enumerator.MoveNext());
        Assert.Equal(1, enumerator.Current);
        Assert.Equal(new[] { 1, 2 }, set.ToArray());
    }

    [Fact]
    public void Enumerator_ModificationThrows()
    {
        var set = new SwiftSparseSet
        {
            1,
            2
        };

        var enumerator = set.GetEnumerator();

        set.Add(3);

        Assert.Throws<InvalidOperationException>(() => { enumerator.MoveNext(); });
    }

    [Fact]
    public void SetOperations_WorkAgainstEnumerableInputs()
    {
        var set = new SwiftSparseSet
        {
            1,
            2,
            3
        };

        set.ExceptWith(new[] { 2, 8 });
        Assert.True(set.SetEquals(new[] { 1, 3 }));

        set.UnionWith(new[] { 4, 5 });
        Assert.True(set.SetEquals(new[] { 1, 3, 4, 5 }));

        set.IntersectWith(new[] { 3, 4, 9 });
        Assert.True(set.SetEquals(new[] { 3, 4 }));

        set.SymmetricExceptWith(new[] { 4, 6 });
        Assert.True(set.SetEquals(new[] { 3, 6 }));
        Assert.True(set.Overlaps(new[] { 6, 10 }));
        Assert.True(set.IsSubsetOf(new[] { 3, 6, 9 }));
        Assert.True(set.IsSupersetOf(new[] { 3 }));
    }

    [Fact]
    public void SetOperations_WorkAgainstSparseSetInputs()
    {
        var set = new SwiftSparseSet
        {
            1,
            2,
            3
        };
        var other = new SwiftSparseSet
        {
            2,
            3,
            4
        };

        set.IntersectWith(other);
        Assert.True(set.SetEquals(new SwiftSparseSet { 2, 3 }));
        Assert.True(set.IsSubsetOf(new SwiftSparseSet { 2, 3, 5 }));
        Assert.False(set.IsSubsetOf(new SwiftSparseSet { 2 }));
        Assert.True(new SwiftSparseSet { 2, 3, 4 }.IsProperSupersetOf(set));
        Assert.True(set.IsProperSubsetOf(new SwiftSparseSet { 2, 3, 4 }));

        set.SymmetricExceptWith(new SwiftSparseSet { 3, 5 });

        Assert.True(set.SetEquals(new SwiftSparseSet { 2, 5 }));

        set.ExceptWith(set);

        Assert.Empty(set);
    }

    [Fact]
    public void DenseViewsCopyAndClone_ExposeCurrentState()
    {
        var set = new SwiftSparseSet
        {
            1,
            2
        };
        var target = new List<int> { 99 };
        var copied = new int[2];

        set.GetDense(out int[] denseKeys, out int count);
        set.CopyTo(copied, 0);
        set.CloneTo(target);
        IEnumerator enumerator = ((IEnumerable)set).GetEnumerator();

        Assert.Equal(set.DenseCapacity, denseKeys.Length);
        Assert.Equal(2, count);
        Assert.False(set.IsSynchronized);
        Assert.NotNull(set.SyncRoot);
        Assert.Equal(new[] { 1, 2 }, copied);
        Assert.Equal(new[] { 1, 2 }, target);
        Assert.True(enumerator.MoveNext());
        Assert.NotNull(enumerator.Current);
    }

    [Fact]
    public void Constructor_WithState_InvalidIds_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = new SwiftSparseSet(new SwiftArrayState<int>(new[] { -1 }));
        });

        Assert.Throws<ArgumentException>(() =>
        {
            _ = new SwiftSparseSet(new SwiftArrayState<int>(new[] { 1, 1 }));
        });
    }

    [Fact]
    public void Json_RoundTrip_PreservesIds()
    {
        var set = new SwiftSparseSet
        {
            5,
            10
        };

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(set);

        var result = JsonSerializer.Deserialize<SwiftSparseSet>(json);

        Assert.Equal(2, result.Count);
        Assert.Contains(5, result);
        Assert.Contains(10, result);
    }

#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
    [Fact]
    public void MemoryPack_RoundTrip_PreservesIds()
    {
        var set = new SwiftSparseSet
        {
            64
        };

        byte[] bytes = MemoryPackSerializer.Serialize(set);

        var result = MemoryPackSerializer.Deserialize<SwiftSparseSet>(bytes);

        Assert.Single(result);
        Assert.Contains(64, result);
    }
#endif
}
