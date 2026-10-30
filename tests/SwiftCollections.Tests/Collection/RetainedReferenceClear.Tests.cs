using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using SwiftCollections.Observable;
using Xunit;

namespace SwiftCollections.Tests;

public sealed class RetainedReferenceClearTests
{
    [Theory]
    [InlineData(0, false, false)] [InlineData(0, false, true)] [InlineData(0, true, false)] [InlineData(0, true, true)]
    [InlineData(1, false, false)] [InlineData(1, false, true)] [InlineData(1, true, false)] [InlineData(1, true, true)]
    [InlineData(2, false, false)] [InlineData(2, false, true)] [InlineData(2, true, false)] [InlineData(2, true, true)]
    [InlineData(3, false, false)] [InlineData(3, false, true)] [InlineData(3, true, false)] [InlineData(3, true, true)]
    public void ArrayBuffers_ExplicitClearReleasesOldTailsAndPreservesCapacity(int kind, bool full, bool smaller)
    {
        Action<string> add;
        Action fastClear;
        Action<bool> clear;
        Func<int> count;
        string[] storage;
        if (kind == 0)
        {
            var list = new SwiftList<string>(32);
            add = list.Add; fastClear = list.FastClear; clear = list.Clear;
            count = () => list.Count; storage = list.InnerArray;
        }
        else if (kind == 1)
        {
            var stack = new SwiftStack<string>(32);
            add = stack.Push; fastClear = stack.FastClear; clear = stack.Clear;
            count = () => stack.Count; storage = stack.InnerArray;
        }
        else if (kind == 2)
        {
            var queue = new SwiftQueue<string>(32);
            add = queue.Enqueue; fastClear = queue.FastClear; clear = queue.Clear;
            count = () => queue.Count; storage = queue.InnerArray;
        }
        else
        {
            var sorted = new SwiftSortedList<string>(32, StringComparer.Ordinal);
            add = value => sorted.Add(value); fastClear = sorted.FastClear; clear = sorted.Clear;
            count = () => sorted.Count; storage = sorted.InnerArray;
        }
        foreach (string value in new[] { "a", "b", "c", "d", "e" }) add(value);
        fastClear();
        if (smaller) add("z");
        clear(full);
        Assert.Equal(0, count());
        Assert.Equal(32, storage.Length);
        if (full) Assert.All(storage, value => Assert.Null(value));
        else Assert.Contains(storage, value => value != null);
        add("reused");
        Assert.Equal(1, count());
        clear(true);
        Assert.All(storage, value => Assert.Null(value));
    }

    [Fact]
    public void Queue_FullClearResetsWrappedStorageAndIndices()
    {
        var queue = new SwiftQueue<object>(8);
        for (int i = 0; i < 6; i++) queue.Enqueue(new object());
        for (int i = 0; i < 4; i++) queue.Dequeue();
        for (int i = 0; i < 5; i++) queue.Enqueue(new object());
        object[] storage = queue.InnerArray;
        queue.Clear(true);
        Assert.Same(storage, queue.InnerArray);
        Assert.Empty(queue);
        Assert.All(storage, value => Assert.Null(value));
        var next = new object(); queue.Enqueue(next);
        Assert.Same(next, queue.Dequeue());
    }

    [Fact]
    public void ReferenceContainingStructs_AreClearedAcrossWholeStorage()
    {
        var item = new ReferenceValue(new object());
        var list = new SwiftList<ReferenceValue>(8); list.Add(item); list.FastClear(); list.Clear(true);
        var queue = new SwiftQueue<ReferenceValue>(8); queue.Enqueue(item); queue.FastClear(); queue.Clear(true);
        var stack = new SwiftStack<ReferenceValue>(8); stack.Push(item); stack.FastClear(); stack.Clear(true);
        var sorted = new SwiftSortedList<ReferenceValue>(8, Comparer<ReferenceValue>.Create((a, b) => 0));
        sorted.Add(item); sorted.FastClear(); sorted.Clear(true);
        Assert.All(list.InnerArray, value => Assert.Null(value.Value));
        Assert.All(queue.InnerArray, value => Assert.Null(value.Value));
        Assert.All(stack.InnerArray, value => Assert.Null(value.Value));
        Assert.All(sorted.InnerArray, value => Assert.Null(value.Value));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CollectionFamilies_ClearAndReuseWithoutLosingTheirPolicies(bool full)
    {
        var dictionary = new SwiftDictionary<string, string>(32, StringComparer.OrdinalIgnoreCase) { ["key"] = "value" };
        int capacity = dictionary.Capacity;
        dictionary.Clear(full); dictionary.Clear(full);
        Assert.Empty(dictionary); Assert.Equal(capacity, dictionary.Capacity);
        dictionary.Add("REUSED", "next"); Assert.Equal("next", dictionary["reused"]);

        var set = new SwiftHashSet<string>(32, StringComparer.OrdinalIgnoreCase); set.Add("key");
        set.Clear(full); set.Clear(full); Assert.Empty(set);
        set.Add("REUSED"); Assert.False(set.Add("reused"));

        var packed = new SwiftPackedSet<string>(32); packed.Add("key");
        string[] dense = packed.Dense;
        packed.Clear(full); packed.Clear(full); Assert.Empty(packed); Assert.Same(dense, packed.Dense);
        Assert.All(dense, value => Assert.Null(value));
        packed.Add("reused"); Assert.False(packed.Add("reused"));

        var sparse = new SwiftSparseMap<object>(32, 8); sparse.Add(17, new object());
        object[] values = sparse.DenseValues;
        values[7] = new object();
        sparse.Clear(full); Assert.Empty(sparse); Assert.False(sparse.ContainsKey(17));
        Assert.Equal(!full, values[7] != null);
        sparse.Clear(true); Assert.All(values, value => Assert.Null(value));
        sparse.Add(17, "reused"); Assert.Equal("reused", sparse[17]);

        var ids = new SwiftSparseSet(32, 8); ids.Add(17); ids.Clear(full); ids.Clear(full);
        Assert.Empty(ids); Assert.True(ids.Add(17));

        var bucket = new SwiftBucket<object>(32); int id = bucket.Add(new object());
        bucket.Clear(full); Assert.Empty(bucket); Assert.False(bucket.IsAllocated(id));
        bucket.Clear(full); Assert.Equal(32, bucket.Capacity);
        int next = bucket.Add("reused"); Assert.Equal("reused", bucket[next]);
        bucket.RemoveAt(next); bucket.Clear(full); Assert.Empty(bucket);

        var bi = new SwiftBiDictionary<string, string>(); bi.Add("a", "b");
        SwiftDictionary<string, string> baseBi = bi;
        baseBi.Clear(full); Assert.Empty(bi); Assert.False(bi.TryGetKey("b", out _));
        bi.Clear(full); bi.Add("a", "b"); Assert.Equal("a", bi.GetKey("b"));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ObservableAndCustomOverrides_KeepExistingClearDispatch(bool full)
    {
        int resets = 0, properties = 0;
        var custom = new TrackingList(); custom.Add("value"); custom.Clear(full);
        Assert.Equal(1, custom.Clears); Assert.Empty(custom);
        var populated = new SwiftObservableList<string>(new[] { "a", "b" });
        resets = properties = 0;
        populated.CollectionChanged += (_, args) => { Assert.Equal(NotifyCollectionChangedAction.Reset, args.Action); resets++; };
        populated.PropertyChanged += (_, _) => properties++;
        ((SwiftList<string>)populated).Clear(full);
        Assert.Equal(1, resets); Assert.Equal(1, properties);
        populated.Clear(full); Assert.Equal(1, resets); Assert.Equal(1, properties);
        var retained = new SwiftObservableList<string>(new[] { "retired" });
        retained.FastClear(); int emptyEvents = 0;
        retained.CollectionChanged += (_, _) => emptyEvents++;
        retained.Clear(true); Assert.All(retained.InnerArray, value => Assert.Null(value)); Assert.Equal(0, emptyEvents);

        var observedDictionary = new SwiftObservableDictionary<string, string>(); observedDictionary.Add("a", "b");
        int dictionaryEvents = 0;
        observedDictionary.CollectionChanged += (_, args) => { Assert.Equal(NotifyCollectionChangedAction.Reset, args.Action); dictionaryEvents++; };
        ((SwiftDictionary<string, string>)observedDictionary).Clear(full);
        observedDictionary.Clear(full); Assert.Equal(1, dictionaryEvents); Assert.Empty(observedDictionary);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void GenerationalClear_InvalidatesLiveAndPreviouslyRemovedHandles(bool full)
    {
        var bucket = new SwiftGenerationalBucket<object>(8);
        SwiftHandle removed = bucket.Add(new object());
        SwiftHandle live = bucket.Add(new object());
        Assert.True(bucket.Remove(removed));
        bucket.Clear(full);
        Assert.Empty(bucket); Assert.Equal(8, bucket.Capacity);
        for (int i = 0; i < 8; i++) bucket.Add(new object());
        Assert.False(bucket.IsValid(removed)); Assert.False(bucket.IsValid(live));
        var restored = new SwiftGenerationalBucket<object>(bucket.State);
        restored.Clear();
        for (int i = 0; i < 8; i++) restored.Add(new object());
        Assert.False(restored.IsValid(removed)); Assert.False(restored.IsValid(live));
        bucket.Clear(full); bucket.Clear(full); Assert.Empty(bucket);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void GenerationalClear_StateRoundTripPreservesSubsequentHandles(bool full)
    {
        var original = new SwiftGenerationalBucket<string>(8);
        SwiftHandle removed = original.Add("removed"), live = original.Add("live");
        original.Remove(removed);
        original.Clear(full);
        var restored = new SwiftGenerationalBucket<string>(original.State);
        for (int i = 0; i < 10; i++)
            Assert.Equal(original.Add("next"), restored.Add("next"));
        Assert.False(restored.IsValid(removed)); Assert.False(restored.IsValid(live));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void GenerationalClear_ReleasesValueGraphsWhileBucketStaysAlive(bool full)
    {
        var bucket = new SwiftGenerationalBucket<object>(8);
        WeakReference retired = PopulateGenerationValue(bucket);
        bucket.Clear(full);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.False(retired.IsAlive);
        GC.KeepAlive(bucket);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference PopulateGenerationValue(SwiftGenerationalBucket<object> bucket)
    {
        var value = new object();
        bucket.Add(value);
        return new WeakReference(value);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ValuePayloads_AndEmptyStorageSupportBothModes(bool full)
    {
        var list = new SwiftList<int>(); list.Clear(full); list.Add(1); list.Clear(full); Assert.Empty(list);
        var queue = new SwiftQueue<int>(); queue.Clear(full); queue.Enqueue(1); queue.Clear(full); Assert.Empty(queue);
        var stack = new SwiftStack<int>(); stack.Clear(full); stack.Push(1); stack.Clear(full); Assert.Empty(stack);
        var sorted = new SwiftSortedList<int>(); sorted.Clear(full); sorted.Add(1); sorted.Clear(full); Assert.Empty(sorted);
        var dictionary = new SwiftDictionary<int, int>(); dictionary.Clear(full); dictionary.Add(1, 2); dictionary.Clear(full); Assert.Empty(dictionary);
        var set = new SwiftHashSet<int>(); set.Clear(full); set.Add(1); set.Clear(full); Assert.Empty(set);
        var packed = new SwiftPackedSet<int>(); packed.Clear(full); packed.Add(1); packed.Clear(full); Assert.Empty(packed);
        var sparse = new SwiftSparseMap<int>(); sparse.Clear(full); sparse.Add(1, 2); sparse.Clear(full); Assert.Empty(sparse);
        var bucket = new SwiftBucket<int>(); bucket.Clear(full); bucket.Add(1); bucket.Clear(full); Assert.Empty(bucket);
        var handles = new SwiftGenerationalBucket<int>(); handles.Clear(full); var old = handles.Add(1); handles.Clear(full);
        handles.Add(2); Assert.False(handles.IsValid(old));
    }

    private readonly record struct ReferenceValue(object Value);

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public void Dictionary_FullClearReleasesProtectedTailAndPreservesOverride(bool full, bool empty)
    {
        var dictionary = new TrackingDictionary();
        dictionary.Add("key", "value");
        if (empty) dictionary.Clear();
        dictionary.RetainTail();
        int calls = dictionary.Clears;
        ((SwiftDictionary<object, object>)dictionary).Clear(full);
        Assert.Equal(calls + 1, dictionary.Clears);
        Assert.Empty(dictionary);
        Assert.Equal(!full, dictionary.HasRetainedTail);
        dictionary.Clear(true);
        Assert.False(dictionary.HasRetainedTail);
        dictionary.Add("reused", "next"); Assert.Equal("next", dictionary["reused"]);
    }

    [Fact]
    public void FullClearAcrossFamilies_IsWarmAllocationFree()
    {
        var list = new SwiftList<string>(8);
        var stack = new SwiftStack<string>(8);
        var queue = new SwiftQueue<string>(8);
        var sorted = new SwiftSortedList<string>(8, StringComparer.Ordinal);
        var dictionary = new SwiftDictionary<string, string>();
        var set = new SwiftHashSet<string>();
        var packed = new SwiftPackedSet<string>();
        var sparse = new SwiftSparseMap<string>();
        var ids = new SwiftSparseSet();
        var bucket = new SwiftBucket<string>();
        var handles = new SwiftGenerationalBucket<string>();
        void Cycle()
        {
            list.Add("value"); list.FastClear(); list.Clear(true);
            stack.Push("value"); stack.FastClear(); stack.Clear(true);
            queue.Enqueue("value"); queue.FastClear(); queue.Clear(true);
            sorted.Add("value"); sorted.FastClear(); sorted.Clear(true);
            dictionary.Add("key", "value"); dictionary.Clear(true);
            set.Add("value"); set.Clear(true);
            packed.Add("value"); packed.Clear(true);
            sparse.Add(1, "value"); sparse.Clear(true);
            ids.Add(1); ids.Clear(true);
            bucket.Add("value"); bucket.Clear(true);
            handles.Add("value"); handles.Clear(true);
        }
        for (int i = 0; i < 64; i++) Cycle();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 64; i++) Cycle();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }

    private sealed class TrackingDictionary : SwiftDictionary<object, object>
    {
        public TrackingDictionary() : base(32, new ZeroHashComparer()) { }
        public int Clears;
        public void RetainTail()
        {
            Assert.False(_entries[^1].IsUsed);
            _entries[^1].Key = new object(); _entries[^1].Value = new object();
        }
        public bool HasRetainedTail => _entries[^1].Key != null || _entries[^1].Value != null;
        public override void Clear() { Clears++; base.Clear(); }
    }

    private sealed class ZeroHashComparer : IEqualityComparer<object>
    {
        public new bool Equals(object x, object y) => object.Equals(x, y);
        public int GetHashCode(object value) => 0;
    }

    private sealed class TrackingList : SwiftList<string>
    {
        public int Clears;
        public override void Clear() { Clears++; base.Clear(); }
    }
}
