using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;

#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
using MemoryPack;
#endif

namespace SwiftCollections.Tests;

public class SwiftTicketMapTests
{
    #region Basic Add / Get

    [Fact]
    public void Add_ReturnsValidTicket_AndValueCanBeRetrieved()
    {
        var map = new SwiftTicketMap<string>();

        var ticket = map.Add("hello");

        Assert.True(map.TryGet(ticket, out var value));
        Assert.Equal("hello", value);
    }

    [Fact]
    public void Add_MultipleItems_AllAccessible()
    {
        var map = new SwiftTicketMap<int>();

        var tickets = new List<SwiftTicket>();

        for (int i = 0; i < 100; i++)
            tickets.Add(map.Add(i));

        for (int i = 0; i < 100; i++)
        {
            Assert.True(map.TryGet(tickets[i], out var value));
            Assert.Equal(i, value);
        }
    }

    #endregion

    #region Remove / Generation Safety

    [Fact]
    public void Remove_InvalidatesTicket()
    {
        var map = new SwiftTicketMap<string>();

        var ticket = map.Add("test");

        Assert.True(map.IsValid(ticket));
        Assert.True(map.Remove(ticket));

        Assert.False(map.TryGet(ticket, out _));
        Assert.False(map.IsValid(ticket));
    }

    [Fact]
    public void ReusedSlot_ChangesGeneration()
    {
        var map = new SwiftTicketMap<int>();

        var h1 = map.Add(1);

        map.Remove(h1);

        var h2 = map.Add(2);

        Assert.NotEqual(h1.Generation, h2.Generation);
        Assert.False(map.TryGet(h1, out _));

        Assert.True(map.TryGet(h2, out var value));
        Assert.Equal(2, value);
    }

    #endregion

    #region GetRef

    [Fact]
    public void GetRef_AllowsDirectMutation()
    {
        var map = new SwiftTicketMap<int>();

        var ticket = map.Add(10);

        ref var value = ref map.GetRef(ticket);

        value = 42;

        Assert.True(map.TryGet(ticket, out var result));
        Assert.Equal(42, result);
    }

    #endregion

    #region Enumeration

    [Fact]
    public void Enumerator_IteratesAllValues()
    {
        var map = new SwiftTicketMap<int>();

        for (int i = 0; i < 50; i++)
            map.Add(i);

        int count = 0;

        foreach (var value in map)
            count++;

        Assert.Equal(map.Count, count);
    }

    [Fact]
    public void Enumerator_SkipsRemovedSlots()
    {
        var map = new SwiftTicketMap<int>();
        SwiftTicket first = map.Add(1);
        map.Add(2);

        map.Remove(first);

        var values = new List<int>();
        foreach (int value in map)
            values.Add(value);

        Assert.Equal(new[] { 2 }, values);
    }

    [Fact]
    public void NonGenericEnumerator_CurrentBeforeMoveNext_ThrowsForReferenceTypes()
    {
        IEnumerator enumerator = ((IEnumerable)new SwiftTicketMap<string> { "value" }).GetEnumerator();

        Assert.Throws<InvalidOperationException>(() => _ = enumerator.Current);
    }

    [Fact]
    public void Enumerator_ThrowsIfModified()
    {
        var map = new SwiftTicketMap<int>
        {
            1,
            2
        };

        var enumerator = map.GetEnumerator();

        map.Add(3);

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    #endregion

    #region Capacity / Resize

    [Fact]
    public void Resize_PreservesItems()
    {
        var map = new SwiftTicketMap<int>(2);

        var tickets = new List<SwiftTicket>();

        for (int i = 0; i < 100; i++)
            tickets.Add(map.Add(i));

        for (int i = 0; i < 100; i++)
        {
            Assert.True(map.TryGet(tickets[i], out var value));
            Assert.Equal(i, value);
        }
    }

    [Fact]
    public void Constructor_WithSmallCapacity_UsesDefaultCapacity()
    {
        var map = new SwiftTicketMap<int>(1);

        Assert.Equal(SwiftTicketMap<int>.DefaultCapacity, map.Capacity);
    }

    [Fact]
    public void Constructor_WithLargeCapacity_UsesNextPowerOfTwo()
    {
        var map = new SwiftTicketMap<int>(9);

        Assert.Equal(16, map.Capacity);
    }

    #endregion

    #region Clone

    [Fact]
    public void CloneTo_CopiesValues()
    {
        var map = new SwiftTicketMap<int>();

        for (int i = 0; i < 20; i++)
            map.Add(i);

        var clone = new List<int>();

        map.CloneTo(clone);

        Assert.Equal(map.Count, clone.Count);

        foreach (var value in clone)
            Assert.Contains(value, clone);
    }

    [Fact]
    public void Exists_ReturnsTrueIfMatchIsFound()
    {
        var map = new SwiftTicketMap<int>
        {
            1,
            2,
            3
        };

        Assert.True(map.Exists(i => i == 2));
    }

    [Fact]
    public void Exists_SkipsRemovedSlotsBeforeMatchingLaterValue()
    {
        var map = new SwiftTicketMap<int>();
        SwiftTicket removed = map.Add(1);
        map.Add(2);

        Assert.True(map.Remove(removed));

        Assert.True(map.Exists(static value => value == 2));
    }

    [Fact]
    public void Find_ReturnsMatchingItem()
    {
        var map = new SwiftTicketMap<int>
        {
            1,
            2,
            3
        };

        Assert.Equal(2, map.Find(i => i == 2));
    }

    [Fact]
    public void Find_ReturnsDefaultIfMatchIsNotFound()
    {
        var map = new SwiftTicketMap<int>
        {
            1,
            2
        };

        Assert.Equal(default, map.Find(i => i > 10));
    }

    #endregion

    #region JSON Serialization

#if NET10_0_OR_GREATER

    [Fact]
    public void JsonSerialization_RoundTrip()
    {
        var map = new SwiftTicketMap<string>
        {
            "A",
            "B",
            "C"
        };

        string json = JsonSerializer.Serialize(map);

        var restored = JsonSerializer.Deserialize<SwiftTicketMap<string>>(json);

        Assert.Equal(map.Count, restored.Count);

        int count = 0;

        foreach (var item in restored)
            count++;

        Assert.Equal(map.Count, count);
    }

#endif

    #endregion

    #region MemoryPack Serialization

#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
    [Fact]
    public void MemoryPack_RoundTrip()
    {
        var map = new SwiftTicketMap<int>();

        for (int i = 0; i < 50; i++)
            map.Add(i);

        byte[] data = MemoryPackSerializer.Serialize(map);

        var restored = MemoryPackSerializer.Deserialize<SwiftTicketMap<int>>(data);

        Assert.Equal(map.Count, restored.Count);

        int count = 0;

        foreach (var value in restored)
            count++;

        Assert.Equal(map.Count, count);
    }
#endif

    [Fact]
    public void StateConstructor_AllowsNullFreeIndices()
    {
        var state = new SwiftTicketMapState<int>(
            new[] { 10, 20, 30 },
            new[] { true, false, true },
            new uint[] { 1, 2, 3 },
            null,
            3);

        var map = new SwiftTicketMap<int>(state);

        Assert.Equal(2, map.Count);
        Assert.True(map.TryGet(new SwiftTicket(0, 1), out var first));
        Assert.Equal(10, first);
        Assert.True(map.TryGet(new SwiftTicket(2, 3), out var third));
        Assert.Equal(30, third);
    }

    [Fact]
    public void StateConstructor_AllowsDefaultState()
    {
        var map = new SwiftTicketMap<int>(default(SwiftTicketMapState<int>));

        Assert.Equal(0, map.Count);
        Assert.Equal(SwiftTicketMap<int>.DefaultCapacity, map.Capacity);
    }

    [Fact]
    public void SwiftTicketMapState_Constructor_NormalizesNullArraysToEmptyArrays()
    {
        var state = new SwiftTicketMapState<int>(null, null, null, null, 5);

        Assert.Empty(state.Items);
        Assert.Empty(state.Allocated);
        Assert.Empty(state.Generations);
        Assert.Empty(state.FreeIndices);
        Assert.Equal(5, state.Peak);
    }

    [Fact]
    public void StateConstructor_AllowsShortAllocationAndGenerationArrays()
    {
        var state = new SwiftTicketMapState<int>(
            new[] { 10, 20, 30 },
            new[] { true },
            new uint[] { 7 },
            Array.Empty<int>(),
            3);

        var map = new SwiftTicketMap<int>(state);

        Assert.Equal(1, map.Count);
        Assert.True(map.TryGet(new SwiftTicket(0, 7), out int first));
        Assert.Equal(10, first);
        Assert.False(map.TryGet(new SwiftTicket(1, 0), out _));
    }

    [Fact]
    public void StateConstructor_RestoresGenerationsFreeIndicesAndNormalizesPeak()
    {
        var state = new SwiftTicketMapState<int>(
            new[] { 10 },
            new[] { true },
            new uint[] { 7 },
            new[] { 3 },
            -1);

        var map = new SwiftTicketMap<int>(state);

        SwiftTicket reused = map.Add(40);

        Assert.Equal(3, reused.Index);
        Assert.True(map.TryGet(new SwiftTicket(0, 7), out int first));
        Assert.Equal(10, first);
        Assert.True(map.TryGet(reused, out int added));
        Assert.Equal(40, added);
    }

    [Fact]
    public void StateConstructor_RestoresMissingItemsAndDescendingFreeIndices()
    {
        var state = new SwiftTicketMapState<int>(
            Array.Empty<int>(),
            new[] { true },
            new uint[] { 7 },
            new[] { 2, 1 },
            0);

        var map = new SwiftTicketMap<int>(state);

        Assert.True(map.TryGet(new SwiftTicket(0, 7), out int restored));
        Assert.Equal(0, restored);
        Assert.Equal(1, map.Add(42).Index);
    }

    [Fact]
    public void StateConstructor_ClampsLargePeakAndRejectsOutOfRangeFreeIndex()
    {
        var largePeakState = new SwiftTicketMapState<int>(
            new[] { 10 },
            new[] { true },
            new uint[] { 7 },
            Array.Empty<int>(),
            999);
        var invalidFreeState = new SwiftTicketMapState<int>(
            new[] { 10 },
            new[] { true },
            new uint[] { 7 },
            new[] { 99 },
            1);

        var map = new SwiftTicketMap<int>(largePeakState);

        Assert.Equal(map.Capacity, map.State.Peak);
        Assert.Throws<ArgumentException>(() => new SwiftTicketMap<int>(invalidFreeState));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Remove_InvalidTicket_ReturnsFalse()
    {
        var map = new SwiftTicketMap<int>();

        var ticket = new SwiftTicket(999, 0);

        Assert.False(map.Remove(ticket));
    }

    [Fact]
    public void TryGet_InvalidTicket_ReturnsFalse()
    {
        var map = new SwiftTicketMap<int>();

        var ticket = new SwiftTicket(999, 0);

        Assert.False(map.TryGet(ticket, out _));
    }

    [Fact]
    public void EnsureCapacity_TicketEqualityAndEnumerationAdapters_Work()
    {
        var map = new SwiftTicketMap<int>(2)
        {
            1,
            2
        };

        map.EnsureCapacity(64);
        map.EnsureCapacity(64);

        var left = new SwiftTicket(1, 2);
        var same = new SwiftTicket(1, 2);
        var differentIndex = new SwiftTicket(2, 2);
        var different = new SwiftTicket(1, 3);

        Assert.True(map.Capacity >= 64);
        Assert.True(left.Equals(same));
        Assert.False(left.Equals(differentIndex));
        Assert.True(left.Equals((object)same));
        Assert.False(left.Equals((object)"not a ticket"));
        Assert.True(left == same);
        Assert.True(left != different);
        Assert.Equal("Ticket(1:2)", left.ToString());
        Assert.NotEqual(left.GetHashCode(), different.GetHashCode());

        IEnumerator nongeneric = ((IEnumerable)map).GetEnumerator();
        IEnumerator<int> generic = ((IEnumerable<int>)map).GetEnumerator();

        Assert.True(generic.MoveNext());
        Assert.True(nongeneric.MoveNext());
        Assert.NotNull(nongeneric.Current);

        nongeneric.Reset();

        Assert.True(nongeneric.MoveNext());
    }

    [Fact]
    public void FindAndCloneTo_SkipFreedSlots()
    {
        var map = new SwiftTicketMap<string>();
        SwiftTicket removed = map.Add("remove");
        map.Add("keep");
        map.Remove(removed);

        Assert.Equal("keep", map.Find(item => item == "keep"));

        var clone = new List<string>();
        map.CloneTo(clone);
        Assert.Equal(new[] { "keep" }, clone);
    }

    [Fact]
    public void InvalidTicketPaths_ReturnExpectedResults()
    {
        var map = new SwiftTicketMap<int> { 10 };
        var stale = new SwiftTicket(0, 1);
        var outOfRange = new SwiftTicket(map.Capacity, 0);
        var removed = new SwiftTicket(0, 0);

        Assert.False(map.Remove(stale));
        Assert.True(map.Remove(removed));
        Assert.False(map.Remove(removed));
        Assert.False(map.IsValid(stale));
        Assert.False(map.IsValid(outOfRange));
        Assert.False(map.IsValid(new SwiftTicket(0, stale.Generation + 1)));
        Assert.Throws<InvalidOperationException>(() => map.GetRef(stale));
        Assert.Throws<InvalidOperationException>(() => map.GetRef(removed));
        Assert.False(map.Exists(static value => value == 99));
    }

    #endregion
}
