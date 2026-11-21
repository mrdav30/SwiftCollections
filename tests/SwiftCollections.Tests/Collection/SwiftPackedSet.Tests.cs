using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Xunit;

#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
using MemoryPack;
#endif

namespace SwiftCollections.Tests;

public class SwiftPackedSetTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StateRestore_RejectsDuplicateItems(bool separated)
    {
        int[] items = separated ? new[] { 1, 2, 1 } : new[] { 1, 1 };
        int[] original = (int[])items.Clone();

        Assert.Throws<ArgumentException>(() => new SwiftPackedSet<int>(new SwiftArrayState<int>(items)));

        Assert.Equal(original, items);
    }

    [Fact]
    public void StateRestore_RejectsEqualReferenceItemsUsingDefaultEquality()
    {
        string first = new('x', 1);
        string second = new('x', 1);
        Assert.NotSame(first, second);

        Assert.Throws<ArgumentException>(() => new SwiftPackedSet<string>(
            new SwiftArrayState<string>(new[] { first, second })));
    }

    [Fact]
    public void StateRestore_DefaultStateRejectsMissingItems()
    {
        Assert.Throws<ArgumentNullException>(() => new SwiftPackedSet<int>(default(SwiftArrayState<int>)));
    }

    [Theory]
    [InlineData(0, typeof(ArgumentException))]
    [InlineData(1, typeof(ArgumentNullException))]
    [InlineData(2, typeof(ArgumentNullException))]
    public void StateRestore_InvalidReplacementPreservesExistingStorageAndIteration(int invalidKind, Type exceptionType)
    {
        var set = new SwiftPackedSet<string> { "Old", "Kept" };
        string[] storage = set.Dense;
        int capacity = set.Capacity;
        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.Equal("Old", enumerator.Current);
        SwiftArrayState<string> invalid = invalidKind switch
        {
            0 => new(new[] { "New", "New" }),
            1 => new(new[] { "New", (string)null }),
            _ => default
        };

        Assert.Throws(exceptionType, () => set.State = invalid);

        Assert.Same(storage, set.Dense);
        Assert.Equal(capacity, set.Capacity);
        Assert.Equal(new[] { "Old", "Kept" }, set.ToArray());
        Assert.Contains("Old", set);
        Assert.Contains("Kept", set);
        Assert.DoesNotContain("New", set);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("Kept", enumerator.Current);
        Assert.False(enumerator.MoveNext());
        Assert.True(set.Remove("Old"));
        Assert.True(set.Add("New"));
        Assert.Equal(new[] { "Kept", "New" }, set.ToArray());
    }

    [Fact]
    public void StateRestore_HashFailurePreservesExistingStorageAndIteration()
    {
        var first = new RestoreItem(1);
        var second = new RestoreItem(2);
        var set = new SwiftPackedSet<RestoreItem> { first, second };
        RestoreItem[] storage = set.Dense;
        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        var throwing = new RestoreItem(4) { OnHash = () => throw new ArithmeticException("Hash failed.") };
        var replacement = new SwiftArrayState<RestoreItem>(new[] { new RestoreItem(3), throwing });

        Assert.Throws<ArithmeticException>(() => set.State = replacement);

        Assert.Same(storage, set.Dense);
        Assert.Equal(new[] { first, second }, set.ToArray());
        Assert.Contains(first, set);
        Assert.Contains(second, set);
        Assert.True(enumerator.MoveNext());
        Assert.Same(second, enumerator.Current);
        Assert.False(enumerator.MoveNext());
        Assert.True(set.Remove(first));
        Assert.Single(set);
        Assert.Same(second, set.Dense[0]);
    }

    [Fact]
    public void StateRestore_IndexesCopiedSnapshotWhenHashCallbackReplacesInputItems()
    {
        var second = new RestoreItem(2);
        var outside = new RestoreItem(3);
        RestoreItem[] items = null;
        var first = new RestoreItem(1) { OnHash = () => items[1] = outside };
        items = new[] { first, second };

        var set = new SwiftPackedSet<RestoreItem>(new SwiftArrayState<RestoreItem>(items));

        Assert.Same(outside, items[1]);
        Assert.Equal(new[] { first, second }, set.ToArray());
        Assert.Contains(first, set);
        Assert.Contains(second, set);
        Assert.DoesNotContain(outside, set);
    }

    [Fact]
    public void StateRestore_ValidReplacementPreservesOrderAndOwnershipAndSupportsContinuation()
    {
        var set = new SwiftPackedSet<int> { 1, 2 };
        int[] oldStorage = set.Dense;
        var oldEnumerator = set.GetEnumerator();
        Assert.True(oldEnumerator.MoveNext());
        int[] items = { 9, 0, 7 };

        set.State = new SwiftArrayState<int>(items);

        Assert.NotSame(oldStorage, set.Dense);
        Assert.NotSame(items, set.Dense);
        Assert.Equal(new[] { 1, 2 }, oldStorage.Take(2));
        Assert.Equal(items, set.ToArray());
        Assert.Throws<InvalidOperationException>(() => oldEnumerator.MoveNext());
        items[0] = -9;
        Assert.Contains(9, set);
        Assert.DoesNotContain(-9, set);
        Assert.DoesNotContain(1, set);
        Assert.True(set.Remove(0));
        Assert.Equal(new[] { 9, 7 }, set.ToArray());
        Assert.True(set.Add(0));
        Assert.False(set.Add(7));
        Assert.Equal(new[] { 9, 7, 0 }, set.State.Items);
    }

    [Theory]
    [InlineData(false, typeof(ArgumentException))]
    [InlineData(true, typeof(ArgumentNullException))]
    public void StateRestore_JsonRejectsInvalidOwnerItems(bool containsNull, Type exceptionType)
    {
        string[] items = containsNull ? new[] { "First", null } : new[] { "First", "First" };
        string json = JsonSerializer.Serialize(new { State = new SwiftArrayState<string>(items) });

        Assert.Throws(exceptionType, () => JsonSerializer.Deserialize<SwiftPackedSet<string>>(json));
    }

    [Fact]
    public void StateRestore_RawJsonStateRemainsPassiveButOwnerRejectsDuplicates()
    {
        SwiftArrayState<int> state = JsonSerializer.Deserialize<SwiftArrayState<int>>("{\"Items\":[1,1]}");

        Assert.Equal(new[] { 1, 1 }, state.Items);
        Assert.Throws<ArgumentException>(() => new SwiftPackedSet<int>(state));
    }

#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
    [Theory]
    [InlineData(false, typeof(ArgumentException))]
    [InlineData(true, typeof(ArgumentNullException))]
    public void StateRestore_MemoryPackRejectsInvalidItemsWithoutReplacingExistingOwner(bool containsNull, Type exceptionType)
    {
        var malformed = new SwiftPackedSet<string> { "First", "Second" };
        // Serialize malformed items without relying on the state constructor accepting them.
        malformed.Dense[1] = containsNull ? null : "First";
        byte[] bytes = MemoryPackSerializer.Serialize(malformed);
        var set = new SwiftPackedSet<string> { "Old", "Kept" };
        var original = set;
        string[] storage = set.Dense;
        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.Throws(exceptionType, () => MemoryPackSerializer.Deserialize<SwiftPackedSet<string>>(bytes));
        Assert.Throws(exceptionType, () => MemoryPackSerializer.Deserialize(bytes, ref set));

        Assert.Same(original, set);
        Assert.Same(storage, set.Dense);
        Assert.Equal(new[] { "Old", "Kept" }, set.ToArray());
        Assert.Contains("Old", set);
        Assert.Contains("Kept", set);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("Kept", enumerator.Current);
        Assert.False(enumerator.MoveNext());
        Assert.True(set.Remove("Old"));
        Assert.True(set.Add("New"));
        Assert.Equal(new[] { "Kept", "New" }, set.ToArray());
    }
#endif

    [Fact]
    public void HandlesReferenceTypes()
    {
        var set = new SwiftPackedSet<string>
        {
            "Alpha",
            "Beta"
        };

        Assert.Contains("Alpha", set);
        Assert.True(set.Remove("Alpha"));
    }

    [Fact]
    public void Add_InsertsItem()
    {
        var set = new SwiftPackedSet<int>();

        bool added = set.Add(42);

        Assert.True(added);
        Assert.Contains(42, set);
        Assert.Single(set);
    }

    [Fact]
    public void Add_Duplicate_ReturnsFalse()
    {
        var set = new SwiftPackedSet<int>();

        Assert.True(set.Add(10));
        Assert.False(set.Add(10));

        Assert.Single(set);
    }

    [Fact]
    public void ICollectionAdd_InsertsItem()
    {
        ICollection<int> set = new SwiftPackedSet<int>();

        set.Add(42);

        Assert.Contains(42, set);
    }

    [Fact]
    public void Constructor_WithEmptyState_InitializesEmptyPackedSet()
    {
        var set = new SwiftPackedSet<int>(new SwiftArrayState<int>(Array.Empty<int>()));

        Assert.Empty(set);
        Assert.True(set.Capacity >= SwiftPackedSet<int>.DefaultCapacity);
    }

    [Fact]
    public void Constructor_WithLargeState_CopiesValuesAndRebuildsLookup()
    {
        int[] values = Enumerable.Range(1, SwiftPackedSet<int>.DefaultCapacity + 1).ToArray();
        var set = new SwiftPackedSet<int>(new SwiftArrayState<int>(values));

        Assert.Equal(values, set.ToArray());
        Assert.True(set.Capacity >= values.Length);
        values[0] = -1;
        Assert.Contains(1, set);
        Assert.DoesNotContain(-1, set);
        Assert.False(set.Add(1));
        Assert.True(set.Remove(1));
        Assert.DoesNotContain(1, set);
        Assert.Equal(values.Length - 1, set.Count);
        Assert.True(set.Add(1));
        Assert.Contains(1, set);
    }

    [Fact]
    public void Contains_ReturnsCorrectResult()
    {
        var set = new SwiftPackedSet<int>
        {
            5
        };

        Assert.Contains(5, set);
        Assert.DoesNotContain(10, set);
    }

    [Fact]
    public void Exists_ReturnsTrueIfMatchIsFound()
    {
        var set = new SwiftPackedSet<int>
        {
            1,
            2,
            3
        };

        Assert.True(set.Exists(i => i == 2));
    }

    [Fact]
    public void Exists_ReturnsFalseWhenMissing_AndThrowsForNullPredicate()
    {
        var set = new SwiftPackedSet<int>
        {
            1,
            2
        };

        Assert.False(set.Exists(i => i == 99));
        Assert.Throws<ArgumentNullException>(() => set.Exists(null));
    }

    [Fact]
    public void Find_ReturnsMatchingItem()
    {
        var set = new SwiftPackedSet<int>
        {
            1,
            2,
            3
        };

        Assert.Equal(2, set.Find(i => i == 2));
    }

    [Fact]
    public void Find_ReturnsDefaultIfMatchIsNotFound()
    {
        var set = new SwiftPackedSet<int>
        {
            1,
            2
        };

        Assert.Equal(default, set.Find(i => i > 10));
    }

    [Fact]
    public void Remove_RemovesItem()
    {
        var set = new SwiftPackedSet<int>
        {
            1,
            2
        };

        bool removed = set.Remove(1);

        Assert.True(removed);
        Assert.DoesNotContain(1, set);
        Assert.Single(set);
    }

    [Fact]
    public void Remove_ShouldClearReleasedReferenceSlot()
    {
        var set = new SwiftPackedSet<string>
        {
            "alpha",
            "beta"
        };

        Assert.True(set.Remove("beta"));
        Assert.Null(set.Dense[set.Count]);
    }

    [Fact]
    public void Remove_LastElement_ShouldNotPerformSwapBack()
    {
        var set = new SwiftPackedSet<string>
        {
            "alpha",
            "beta"
        };

        Assert.True(set.Remove("beta"));
        Assert.Single(set);
        Assert.Equal("alpha", set.Dense[0]);
        Assert.Null(set.Dense[1]);
    }

    [Fact]
    public void Remove_Nonexistent_ReturnsFalse()
    {
        var set = new SwiftPackedSet<int>();

        bool removed = set.Remove(99);

        Assert.False(removed);
    }

    [Fact]
    public void Remove_SwapBackMaintainsIntegrity()
    {
        var set = new SwiftPackedSet<int>
        {
            1,
            2,
            3
        };

        set.Remove(2);

        Assert.Equal(2, set.Count);
        Assert.Contains(1, set);
        Assert.Contains(3, set);
        Assert.DoesNotContain(2, set);
    }

    [Fact]
    public void Clear_RemovesAllItems()
    {
        var set = new SwiftPackedSet<int>
        {
            1,
            2
        };

        set.Clear();

        Assert.Empty(set);
        Assert.DoesNotContain(1, set);
    }

    [Fact]
    public void Clear_ShouldReleaseStoredReferences()
    {
        var set = new SwiftPackedSet<string>
        {
            "alpha",
            "beta"
        };

        set.Clear();

        Assert.Null(set.Dense[0]);
        Assert.Null(set.Dense[1]);
    }

    [Fact]
    public void DenseArray_ContainsAllValues()
    {
        var set = new SwiftPackedSet<int>
        {
            10,
            20,
            30
        };

        var dense = set.Dense;

        Assert.Contains(10, dense.Take(set.Count));
        Assert.Contains(20, dense.Take(set.Count));
        Assert.Contains(30, dense.Take(set.Count));
    }

    [Fact]
    public void AsReadOnlySpan_ExposesActiveDenseValues()
    {
        var set = new SwiftPackedSet<int>
        {
            10,
            20,
            30
        };

        Assert.Equal(set.Dense.AsSpan(0, set.Count).ToArray(), set.AsReadOnlySpan().ToArray());
    }

    [Fact]
    public void Enumerator_ReturnsAllValues()
    {
        var set = new SwiftPackedSet<int>
        {
            1,
            2
        };

        var values = set.ToList();

        Assert.Contains(1, values);
        Assert.Contains(2, values);
    }

    [Fact]
    public void Enumerator_ModificationThrows()
    {
        var set = new SwiftPackedSet<int>
        {
            1,
            2
        };

        var enumerator = set.GetEnumerator();

        set.Add(3);

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void LargeCollection_AddRemove()
    {
        var set = new SwiftPackedSet<int>();

        const int size = 10000;

        for (int i = 0; i < size; i++)
            set.Add(i);

        Assert.Equal(size, set.Count);

        for (int i = 0; i < size; i += 2)
            set.Remove(i);

        Assert.Equal(size / 2, set.Count);
    }

    [Fact]
    public void CloneTo_CopiesValues()
    {
        var set = new SwiftPackedSet<int>
        {
            1,
            2
        };

        var list = new List<int>();

        set.CloneTo(list);

        Assert.Equal(2, list.Count);
        Assert.Contains(1, list);
        Assert.Contains(2, list);
    }

    [Fact]
    public void EnsureCapacity_RequestWithinCurrentCapacity_IsNoOp()
    {
        var set = new SwiftPackedSet<int>(16);
        int capacityBefore = set.Capacity;

        set.EnsureCapacity(8);

        Assert.Equal(capacityBefore, set.Capacity);
    }

    [Fact]
    public void CopyTo_ThrowsForInvalidIndexOrInsufficientSpace()
    {
        var set = new SwiftPackedSet<int> { 1, 2, 3 };

        Assert.Throws<ArgumentOutOfRangeException>(() => set.CopyTo(new int[3], 4));
        Assert.Throws<ArgumentException>(() => set.CopyTo(new int[2], 0));
    }

    #region Serialization

    [Fact]
    public void Json_RoundTrip_PreservesValues()
    {
        var set = new SwiftPackedSet<int>
        {
            10,
            20
        };

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(set);

        var result = JsonSerializer.Deserialize<SwiftPackedSet<int>>(json);

        Assert.Equal(2, result.Count);
        Assert.Contains(10, result);
        Assert.Contains(20, result);
    }

    [Fact]
    public void Json_RoundTrip_PreservesDenseIntegrity()
    {
        var set = new SwiftPackedSet<int>
        {
            5,
            10,
            15
        };

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(set);

        var result = JsonSerializer.Deserialize<SwiftPackedSet<int>>(json);

        var dense = result.Dense;

        for (int i = 0; i < result.Count; i++)
            Assert.Contains(dense[i], result);
    }

    [Fact]
    public void Json_EmptySet_RoundTrip()
    {
        var set = new SwiftPackedSet<int>();

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(set);

        var result = JsonSerializer.Deserialize<SwiftPackedSet<int>>(json);

        Assert.Empty(result);
    }

#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
    [Fact]
    public void MemoryPack_RoundTrip_PreservesValues()
    {
        var set = new SwiftPackedSet<int>
        {
            1,
            2
        };

        byte[] bytes = MemoryPackSerializer.Serialize(set);

        var result = MemoryPackSerializer.Deserialize<SwiftPackedSet<int>>(bytes);

        Assert.Equal(2, result.Count);
        Assert.Contains(1, result);
        Assert.Contains(2, result);
    }
#endif

#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
    [Fact]
    public void MemoryPack_RoundTrip_PreservesLargeCollections()
    {
        var set = new SwiftPackedSet<int>();

        for (int i = 0; i < 1000; i++)
            set.Add(i);

        byte[] bytes = MemoryPackSerializer.Serialize(set);

        var result = MemoryPackSerializer.Deserialize<SwiftPackedSet<int>>(bytes);

        Assert.Equal(1000, result.Count);

        for (int i = 0; i < 1000; i++)
            Assert.Contains(i, result);
    }
#endif

#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
    [Fact]
    public void MemoryPack_EmptySet_RoundTrip()
    {
        var set = new SwiftPackedSet<int>();

        byte[] bytes = MemoryPackSerializer.Serialize(set);

        var result = MemoryPackSerializer.Deserialize<SwiftPackedSet<int>>(bytes);

        Assert.Empty(result);
    }
#endif

    [Fact]
    public void PackedSet_IsProperSupersetOf_IgnoresDuplicatesInOther()
    {
        var set = new SwiftPackedSet<int> { 1, 2 };

        Assert.True(set.IsProperSupersetOf(new[] { 1, 1 }));
    }

    [Fact]
    public void PackedSet_SymmetricExceptWith_Self_ClearsSet()
    {
        var set = new SwiftPackedSet<int> { 1, 2, 3 };

        set.SymmetricExceptWith(set);

        Assert.Empty(set);
    }

    [Fact]
    public void PackedSet_ExceptWith_Self_ClearsSet()
    {
        var set = new SwiftPackedSet<int> { 1, 2, 3 };

        set.ExceptWith(set);

        Assert.Empty(set);
    }

    [Fact]
    public void PackedSet_ExceptWith_RemovesSharedItems()
    {
        var set = new SwiftPackedSet<int> { 1, 2, 3, 4 };

        set.ExceptWith(new[] { 2, 4, 8 });

        Assert.Equal(2, set.Count);
        Assert.Contains(1, set);
        Assert.Contains(3, set);
        Assert.DoesNotContain(2, set);
        Assert.DoesNotContain(4, set);
    }

    [Fact]
    public void PackedSet_IntersectWith_RetainsOnlySharedItems()
    {
        var set = new SwiftPackedSet<int> { 1, 2, 3, 4 };

        set.IntersectWith(new[] { 2, 4, 8 });

        Assert.Equal(2, set.Count);
        Assert.Contains(2, set);
        Assert.Contains(4, set);
        Assert.DoesNotContain(1, set);
        Assert.DoesNotContain(3, set);
    }

    [Fact]
    public void PackedSet_IntersectWith_Self_LeavesSetUntouched()
    {
        var set = new SwiftPackedSet<int> { 1, 2, 3 };

        set.IntersectWith(set);

        Assert.True(set.SetEquals(new[] { 1, 2, 3 }));
    }

    [Fact]
    public void PackedSet_SetRelationships_ReportExpectedResults()
    {
        var set = new SwiftPackedSet<int> { 1, 2, 3 };

        Assert.True(set.IsProperSubsetOf(new[] { 1, 2, 3, 4 }));
        Assert.True(set.IsSubsetOf(new[] { 1, 2, 2, 3, 4 }));
        Assert.True(set.IsSupersetOf(new[] { 1, 1, 2 }));
        Assert.True(set.Overlaps(new[] { 3, 9 }));
        Assert.False(set.Overlaps(new[] { 7, 8 }));
        Assert.True(set.SetEquals(new[] { 3, 2, 1, 1 }));
        Assert.False(set.SetEquals(new[] { 1, 2 }));
    }

    [Fact]
    public void PackedSet_SetRelationships_ReportFalseForCountAndMembershipMismatches()
    {
        var set = new SwiftPackedSet<int> { 1, 2, 3 };

        Assert.False(set.IsProperSubsetOf(new[] { 1, 2, 3 }));
        Assert.False(set.IsProperSupersetOf(new[] { 1, 2, 3 }));
        Assert.False(set.IsSubsetOf(new[] { 1, 2 }));
        Assert.False(set.IsSupersetOf(new[] { 1, 4 }));
    }

    [Fact]
    public void PackedSet_SetRelationships_ReportFalseForMembershipMismatchesAtEqualCounts()
    {
        var set = new SwiftPackedSet<int> { 1, 2, 3 };

        Assert.False(set.IsProperSubsetOf(new[] { 1, 2, 4, 5 }));
        Assert.False(set.IsProperSupersetOf(new[] { 1, 4 }));
        Assert.False(set.IsSubsetOf(new[] { 1, 2, 4 }));
        Assert.False(set.SetEquals(new[] { 1, 2, 4 }));
    }

    [Fact]
    public void PackedSet_SymmetricExceptWith_MixedOverlap_RemovesSharedAndAddsMissing()
    {
        var set = new SwiftPackedSet<int> { 1, 2 };

        set.SymmetricExceptWith(new[] { 2, 3, 3 });

        Assert.True(set.SetEquals(new[] { 1, 3 }));
    }

    [Fact]
    public void PackedSet_EnumeratorReset_RestartsEnumeration()
    {
        IEnumerator enumerator = ((IEnumerable)new SwiftPackedSet<int> { 1, 2, 3 }).GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.NotNull(enumerator.Current);

        enumerator.Reset();

        Assert.True(enumerator.MoveNext());
        Assert.NotNull(enumerator.Current);
    }

    [Fact]
    public void PackedSet_EnumeratorReset_AfterMutation_Throws()
    {
        var set = new SwiftPackedSet<int> { 1, 2, 3 };
        var enumerator = set.GetEnumerator();

        set.Add(4);

        Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
    }

    [Fact]
    public void PackedSet_UnionWithAndCollectionMembers_Work()
    {
        ICollection<int> set = new SwiftPackedSet<int>
        {
            1,
            2
        };
        ((SwiftPackedSet<int>)set).UnionWith(new[] { 3, 4 });

        Assert.False(set.IsReadOnly);
        Assert.False(((SwiftPackedSet<int>)set).IsSynchronized);
        Assert.NotNull(((SwiftPackedSet<int>)set).SyncRoot);
        Assert.True(((SwiftPackedSet<int>)set).Capacity >= set.Count);
        Assert.True(((SwiftPackedSet<int>)set).SetEquals(new[] { 1, 2, 3, 4 }));
    }

    [Fact]
    public void SyncRoot_IsStable()
    {
        var collection = new SwiftPackedSet<int>();

        object first = collection.SyncRoot;
        object second = collection.SyncRoot;

        Assert.NotNull(first);
        Assert.Same(first, second);
    }

    #endregion
    private sealed class RestoreItem : IEquatable<RestoreItem>
    {
        private readonly int _id;
        internal Action OnHash { get; init; }

        internal RestoreItem(int id) => _id = id;
        public bool Equals(RestoreItem other) => other != null && _id == other._id;
        public override bool Equals(object obj) => obj is RestoreItem other && Equals(other);
        public override int GetHashCode()
        {
            OnHash?.Invoke();
            return _id;
        }
    }

}
