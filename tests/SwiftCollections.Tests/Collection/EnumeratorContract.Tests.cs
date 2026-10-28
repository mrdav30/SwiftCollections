using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SwiftCollections.Dimensions;
using SwiftCollections.Observable;
using Xunit;

namespace SwiftCollections.Tests;

public class EnumeratorContractTests
{
    public static IEnumerable<object[]> Cases()
    {
        string[] owners = { "List", "ObservableList", "SortedList", "Queue", "Stack",
            "Bucket", "GenerationalBucket", "HashSet", "PackedSet", "SparseSet",
            "SparseMap", "Dictionary", "Keys", "Values", "BiDictionary", "ObservableDictionary" };
        foreach (string owner in owners)
            foreach (int count in new[] { 0, 1, 3 })
                yield return new object[] { owner, count };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Current_BeforeMoveNext_Throws(string owner, int count)
    {
        var (values, _, _) = Create(owner, count);
        IEnumerator iterator = values.GetEnumerator();
        Assert.Throws<InvalidOperationException>(() => _ = iterator.Current);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Current_ReturnsEveryItemIncludingLast_Repeatedly(string owner, int count)
    {
        var (values, expected, _) = Create(owner, count);
        IEnumerator iterator = values.GetEnumerator();
        foreach (object item in expected)
        {
            Assert.True(iterator.MoveNext());
            Assert.Equal(item, iterator.Current);
            Assert.Equal(item, iterator.Current);
        }
        Assert.False(iterator.MoveNext());
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Current_AfterCompletionAndRepeatedMoveNext_Throws(string owner, int count)
    {
        var (values, _, _) = Create(owner, count);
        IEnumerator iterator = values.GetEnumerator();
        while (iterator.MoveNext()) { }
        Assert.Throws<InvalidOperationException>(() => _ = iterator.Current);
        Assert.False(iterator.MoveNext());
        Assert.Throws<InvalidOperationException>(() => _ = iterator.Current);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Reset_RestoresInitialStateAndRepeatsSequence(string owner, int count)
    {
        var (values, expected, _) = Create(owner, count);
        IEnumerator iterator = values.GetEnumerator();
        iterator.MoveNext();
        iterator.Reset();
        Assert.Throws<InvalidOperationException>(() => _ = iterator.Current);
        foreach (object item in expected)
        {
            Assert.True(iterator.MoveNext());
            Assert.Equal(item, iterator.Current);
        }
        Assert.False(iterator.MoveNext());
        iterator.Reset();
        Assert.Throws<InvalidOperationException>(() => _ = iterator.Current);
        Assert.Equal(count != 0, iterator.MoveNext());
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Dispose_InvalidatesNonGenericCurrent(string owner, int count)
    {
        var (values, _, _) = Create(owner, count);
        IEnumerator iterator = values.GetEnumerator();
        iterator.MoveNext();
        ((IDisposable)iterator).Dispose();
        Assert.Throws<InvalidOperationException>(() => _ = iterator.Current);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Mutation_InvalidatesMoveNextAndReset(string owner, int count)
    {
        var (values, _, mutate) = Create(owner, count);
        IEnumerator iterator = values.GetEnumerator();
        iterator.MoveNext();
        mutate();
        Assert.Throws<InvalidOperationException>(() => iterator.MoveNext());
        Assert.Throws<InvalidOperationException>(() => iterator.Reset());
    }

    [Fact]
    public void Bucket_CurrentUsesSlotExtentRatherThanLiveCount()
    {
        var bucket = new SwiftBucket<int>();
        for (int i = 0; i < 8; i++) bucket.Add(i);
        for (int i = 0; i < 7; i++) bucket.RemoveAt(i);
        IEnumerator iterator = ((IEnumerable)bucket).GetEnumerator();
        Assert.True(iterator.MoveNext());
        Assert.Equal(7, iterator.Current);
        Assert.False(iterator.MoveNext());
        Assert.Throws<InvalidOperationException>(() => _ = iterator.Current);
    }

    [Fact]
    public void GenerationalBucket_CurrentAcceptsNullAndRejectsInvalidPositions()
    {
        var bucket = new SwiftGenerationalBucket<string>();
        var removed = bucket.Add("removed");
        bucket.Add(null);
        bucket.Remove(removed);
        IEnumerator iterator = ((IEnumerable)bucket).GetEnumerator();
        Assert.True(iterator.MoveNext());
        Assert.Null(iterator.Current);
        Assert.False(iterator.MoveNext());
        Assert.Throws<InvalidOperationException>(() => _ = iterator.Current);
    }

    [Fact]
    public void DictionaryEnumerator_AllAccessorsFollowPositionIncludingNullValues()
    {
        IDictionary values = new SwiftDictionary<int, string> { { 7, null } };
        IDictionaryEnumerator iterator = values.GetEnumerator();
        AssertInvalidDictionaryPosition(iterator);
        Assert.True(iterator.MoveNext());
        Assert.Equal(7, iterator.Key);
        Assert.Null(iterator.Value);
        Assert.Equal(new DictionaryEntry(7, null), iterator.Entry);
        Assert.Equal(iterator.Entry, iterator.Current);
        Assert.False(iterator.MoveNext());
        AssertInvalidDictionaryPosition(iterator);
        iterator.Reset();
        AssertInvalidDictionaryPosition(iterator);
        Assert.True(iterator.MoveNext());
        ((IDisposable)iterator).Dispose();
        AssertInvalidDictionaryPosition(iterator);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void DimensionalArrays_NonGenericIterationReturnsEveryItemIncludingLast(int count)
    {
        var array2D = new SwiftArray2D<int>(count, 1);
        var array3D = new SwiftArray3D<int>(count, 1, 1);
        for (int i = 0; i < count; i++)
        {
            array2D[i, 0] = i + 7;
            array3D[i, 0, 0] = i + 7;
        }
        foreach (IEnumerable values in new IEnumerable[] { array2D, array3D })
        {
            IEnumerator iterator = values.GetEnumerator();
            for (int i = 0; i < count; i++)
            {
                Assert.True(iterator.MoveNext());
                Assert.Equal(i + 7, iterator.Current);
                Assert.Equal(i + 7, iterator.Current);
            }
            Assert.False(iterator.MoveNext());
            Assert.False(iterator.MoveNext());
        }
    }

    private static void AssertInvalidDictionaryPosition(IDictionaryEnumerator iterator)
    {
        Assert.Throws<InvalidOperationException>(() => _ = iterator.Key);
        Assert.Throws<InvalidOperationException>(() => _ = iterator.Value);
        Assert.Throws<InvalidOperationException>(() => _ = iterator.Entry);
        Assert.Throws<InvalidOperationException>(() => _ = iterator.Current);
    }

    private static (IEnumerable Values, object[] Expected, Action Mutate) Case<T>(IEnumerable<T> values, Action mutate)
        => (values, values.Select(item => (object)item).ToArray(), mutate);

    private static (IEnumerable Values, object[] Expected, Action Mutate) Create(string owner, int count)
    {
        int[] items = Enumerable.Range(7, count).ToArray();
        switch (owner)
        {
            case "List":
            case "ObservableList":
                SwiftList<int> list = owner == "List" ? new SwiftList<int>(items) : new SwiftObservableList<int>(items);
                return Case(list, () => list.Add(99));
            case "SortedList":
                var sorted = new SwiftSortedList<int>(items);
                return Case(sorted, () => sorted.Add(99));
            case "Queue":
                var queue = new SwiftQueue<int>(8);
                for (int i = 0; i < 6; i++) queue.Enqueue(i);
                for (int i = 0; i < 6; i++) queue.Dequeue();
                foreach (int item in items) queue.Enqueue(item);
                return Case(queue, () => queue.Enqueue(99));
            case "Stack":
                var stack = new SwiftStack<int>(items);
                return Case(stack, () => stack.Push(99));
            case "Bucket":
                var bucket = new SwiftBucket<int>();
                foreach (int item in items) bucket.Add(item);
                return Case(bucket, () => bucket.Add(99));
            case "GenerationalBucket":
                var generational = new SwiftGenerationalBucket<int>();
                foreach (int item in items) generational.Add(item);
                return Case(generational, () => generational.Add(99));
            case "HashSet":
                var hash = new SwiftHashSet<int>(items);
                return Case(hash, () => hash.Add(99));
            case "PackedSet":
                var packed = new SwiftPackedSet<int>();
                foreach (int item in items) packed.Add(item);
                return Case(packed, () => packed.Add(99));
            case "SparseSet":
                var sparse = new SwiftSparseSet();
                foreach (int item in items) sparse.Add(item);
                return Case(sparse, () => sparse.Add(99));
            case "SparseMap":
                var map = new SwiftSparseMap<int>();
                foreach (int item in items) map.Add(item, item);
                return Case(map, () => map.Add(99, 99));
            default:
                SwiftDictionary<int, int> dictionary = owner switch
                {
                    "BiDictionary" => new SwiftBiDictionary<int, int>(),
                    "ObservableDictionary" => new SwiftObservableDictionary<int, int>(),
                    _ => new SwiftDictionary<int, int>()
                };
                foreach (int item in items) dictionary.Add(item, item);
                Action mutate = () => dictionary.Add(99, 99);
                return owner switch
                {
                    "Keys" => Case(dictionary.Keys, mutate),
                    "Values" => Case(dictionary.Values, mutate),
                    _ => Case(dictionary, mutate)
                };
        }
    }
}
