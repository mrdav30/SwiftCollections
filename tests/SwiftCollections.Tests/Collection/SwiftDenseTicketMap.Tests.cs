using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace SwiftCollections.Tests;

public class SwiftDenseTicketMapTests
{
    [Fact]
    public void DefaultConstructor_ReservesDefaultCapacityWithoutIssuingTickets()
    {
        var map = new SwiftDenseTicketMap<int>();

        Assert.Empty(map);
        Assert.Equal(8, SwiftDenseTicketMap<int>.DefaultCapacity);
        Assert.Equal(8, map.DenseCapacity);
        Assert.Equal(8, map.SlotCapacity);
        Assert.False(map.IsValid(default));
        Assert.Equal(new SwiftTicket(0, 1), map.Add(7));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 8)]
    [InlineData(8, 8)]
    [InlineData(9, 16)]
    [InlineData(17, 32)]
    public void CapacityConstructor_ReservesRoundedStorage(int capacity, int expected)
    {
        var map = new SwiftDenseTicketMap<int>(capacity);

        Assert.Equal(expected, map.DenseCapacity);
        Assert.Equal(expected, map.SlotCapacity);
        Assert.Empty(map);
        Assert.Empty(map.Values.ToArray());
        Assert.Equal(new SwiftTicket(0, 1), map.Add(42));
        Assert.Equal(42, map.GetRef(new SwiftTicket(0, 1)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData((1 << 30) + 1)]
    [InlineData(int.MaxValue)]
    public void InvalidCapacity_ThrowsBeforeChangingStorageOrIdentity(int capacity)
    {
        Assert.Equal("capacity", Assert.Throws<ArgumentOutOfRangeException>(
            () => new SwiftDenseTicketMap<int>(capacity)).ParamName);
        var map = new SwiftDenseTicketMap<int>();
        SwiftTicket first = map.Add(11);
        var enumerator = map.GetEnumerator();

        Assert.Equal("capacity", Assert.Throws<ArgumentOutOfRangeException>(
            () => map.EnsureCapacity(capacity)).ParamName);

        Assert.Equal(8, map.DenseCapacity);
        Assert.Equal(8, map.SlotCapacity);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(11, enumerator.Current);
        Assert.Equal(11, map.GetRef(first));
        Assert.Equal(new SwiftTicket(1, 1), map.Add(22));
    }

    [Fact]
    public void Add_AcceptsDefaultNullDuplicateAndMutableValues()
    {
        var values = new SwiftDenseTicketMap<int>();
        SwiftTicket zero = values.Add(default);
        SwiftTicket duplicateZero = values.Add(0);
        Assert.NotEqual(zero, duplicateZero);
        Assert.Equal(new[] { 0, 0 }, values.Values.ToArray());

        var map = new SwiftDenseTicketMap<MutablePayload>();
        var payload = new MutablePayload { Value = 3 };
        SwiftTicket first = map.Add(payload);
        SwiftTicket second = map.Add(payload);
        SwiftTicket missingPayload = map.Add(null);
        payload.Value = 9;

        Assert.NotEqual(first, second);
        Assert.Same(payload, map.GetRef(first));
        Assert.Same(payload, map.GetRef(second));
        Assert.Equal(9, map.GetRef(second).Value);
        Assert.True(map.TryGet(missingPayload, out MutablePayload stored));
        Assert.Null(stored);
    }

    [Fact]
    public void RemoveMiddle_RepairsMovedTicketAndReusesOnlyRemovedIdentity()
    {
        var map = new SwiftDenseTicketMap<int>();
        SwiftTicket first = map.Add(10);
        SwiftTicket middle = map.Add(20);
        SwiftTicket last = map.Add(30);

        Assert.True(map.Remove(middle));
        Assert.Equal(new[] { 10, 30 }, map.Values.ToArray());
        Assert.Equal(first, map.GetTicketAt(0));
        Assert.Equal(last, map.GetTicketAt(1));
        map.GetRef(last) = 33;

        Assert.True(map.TryGet(last, out int movedValue));
        Assert.Equal(33, movedValue);
        Assert.False(map.TryGet(middle, out int removedValue));
        Assert.Equal(0, removedValue);
        Assert.Equal(new SwiftTicket(middle.Index, 2), map.Add(40));
        Assert.Equal(new[] { 10, 33, 40 }, map.Values.ToArray());
        Assert.True(map.IsValid(first));
        Assert.True(map.IsValid(last));
    }

    [Theory]
    [InlineData(0, new[] { 30, 20 })]
    [InlineData(1, new[] { 10, 30 })]
    [InlineData(2, new[] { 10, 20 })]
    public void Remove_FirstMiddleAndLast_PreserveEverySurvivingTicket(int removedIndex, int[] expected)
    {
        var map = new SwiftDenseTicketMap<int>();
        SwiftTicket[] tickets = { map.Add(10), map.Add(20), map.Add(30) };

        Assert.True(map.Remove(tickets[removedIndex]));

        Assert.Equal(expected, map.Values.ToArray());
        for (int i = 0; i < tickets.Length; i++)
        {
            Assert.Equal(i != removedIndex, map.TryGet(tickets[i], out int value));
            Assert.Equal(i == removedIndex ? 0 : (i + 1) * 10, value);
        }
        Assert.False(map.Remove(tickets[removedIndex]));
    }

    [Fact]
    public void InvalidTickets_FailUniformlyWithoutChangingDataOrEnumerator()
    {
        var map = new SwiftDenseTicketMap<int>();
        SwiftTicket removed = map.Add(10);
        SwiftTicket survivor = map.Add(20);
        map.Remove(removed);
        SwiftTicket replacement = map.Add(30);
        var enumerator = map.GetEnumerator();
        SwiftTicket[] invalid =
        {
            default, new(-1, 1), new(int.MinValue, 1), new(int.MaxValue, 1),
            new(2, 1), new(survivor.Index, 0), new(survivor.Index, 99), removed
        };

        foreach (SwiftTicket ticket in invalid)
        {
            Assert.False(map.IsValid(ticket));
            Assert.False(map.TryGet(ticket, out int value));
            Assert.Equal(0, value);
            Assert.False(map.Remove(ticket));
            Assert.Equal("ticket", Assert.Throws<ArgumentException>(() => map.GetRef(ticket) = -1).ParamName);
        }

        Assert.Equal(new[] { 20, 30 }, map.Values.ToArray());
        Assert.True(map.IsValid(survivor));
        Assert.True(map.IsValid(replacement));
        Assert.True(enumerator.MoveNext());
        Assert.Equal(20, enumerator.Current);
        map.EnsureCapacity(100);
        Assert.Equal("ticket", Assert.Throws<ArgumentException>(() => map.GetRef(new SwiftTicket(2, 1)) = -1).ParamName);
    }

    [Fact]
    public void Values_WritesLivePrefixAndGetTicketAtReturnsMatchingIdentity()
    {
        var map = new SwiftDenseTicketMap<int>();
        SwiftTicket first = map.Add(10);
        SwiftTicket second = map.Add(20);
        var enumerator = map.GetEnumerator();

        map.Values[0] = 100;
        map.GetRef(second) = 200;

        Assert.Equal(2, map.Values.Length);
        Assert.Equal(first, map.GetTicketAt(0));
        Assert.Equal(second, map.GetTicketAt(1));
        Assert.Equal(100, map.GetRef(first));
        Assert.True(enumerator.MoveNext());
        Assert.Equal(100, enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(200, enumerator.Current);
        Assert.Equal("denseIndex", Assert.Throws<ArgumentOutOfRangeException>(() => map.GetTicketAt(-1)).ParamName);
        Assert.Equal("denseIndex", Assert.Throws<ArgumentOutOfRangeException>(() => map.GetTicketAt(2)).ParamName);
        Assert.Equal("denseIndex", Assert.Throws<ArgumentOutOfRangeException>(() => map.GetTicketAt(int.MaxValue)).ParamName);
    }

    [Fact]
    public void Growth_PreservesLiveAndFreedIdentitiesAcrossDenseAndSlotExpansion()
    {
        var map = new SwiftDenseTicketMap<int>(0);
        var tickets = new List<SwiftTicket>();
        for (int i = 0; i < 65; i++)
            tickets.Add(map.Add(i));
        Assert.Equal(128, map.DenseCapacity);
        Assert.Equal(128, map.SlotCapacity);

        map.Remove(tickets[7]);
        map.Remove(tickets[33]);
        map.EnsureCapacity(129);

        Assert.Equal(256, map.DenseCapacity);
        Assert.Equal(256, map.SlotCapacity);
        Assert.Equal(new SwiftTicket(33, 2), map.Add(330));
        Assert.Equal(new SwiftTicket(7, 2), map.Add(70));
        for (int i = 0; i < tickets.Count; i++)
        {
            if (i == 7 || i == 33)
                Assert.False(map.IsValid(tickets[i]));
            else
                Assert.Equal(i, map.GetRef(tickets[i]));
        }
    }

    [Fact]
    public void Clear_PreservesPriorFreeOrderAndInvalidatesOnlyLiveAllocations()
    {
        var map = new SwiftDenseTicketMap<int>();
        SwiftTicket[] tickets = { map.Add(0), map.Add(1), map.Add(2), map.Add(3) };
        map.Remove(tickets[1]);
        map.Remove(tickets[3]);
        int denseCapacity = map.DenseCapacity;
        int slotCapacity = map.SlotCapacity;

        map.Clear();

        Assert.Empty(map);
        Assert.Empty(map.Values.ToArray());
        Assert.Equal(denseCapacity, map.DenseCapacity);
        Assert.Equal(slotCapacity, map.SlotCapacity);
        foreach (SwiftTicket ticket in tickets)
            Assert.False(map.IsValid(ticket));
        foreach (int slot in new[] { 2, 0, 3, 1 })
            Assert.Equal(new SwiftTicket(slot, 2), map.Add(slot));
    }

    [Fact]
    public void EmptyClearAndNoOpReservation_LeaveExistingEnumeratorsUsable()
    {
        var empty = new SwiftDenseTicketMap<int>(0);
        var emptyEnumerator = empty.GetEnumerator();
        empty.Clear();
        empty.EnsureCapacity(0);
        Assert.False(emptyEnumerator.MoveNext());
        Assert.Equal(0, empty.DenseCapacity);

        SwiftTicket ticket = empty.Add(5);
        var populated = empty.GetEnumerator();
        empty.EnsureCapacity(0);
        empty.EnsureCapacity(empty.DenseCapacity);
        Assert.False(empty.Remove(new SwiftTicket(ticket.Index, 99)));
        Assert.True(populated.MoveNext());
        Assert.Equal(5, populated.Current);

        empty.Remove(ticket);
        var freed = empty.GetEnumerator();
        empty.Clear();
        Assert.False(freed.MoveNext());
        Assert.Equal(new SwiftTicket(ticket.Index, 2), empty.Add(6));
    }

    [Theory]
    [InlineData("Add")]
    [InlineData("Remove")]
    [InlineData("Clear")]
    [InlineData("Grow")]
    public void StructuralChanges_InvalidateMoveNextAndReset(string operation)
    {
        var map = new SwiftDenseTicketMap<int>();
        SwiftTicket ticket = map.Add(7);
        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        switch (operation)
        {
            case "Add": map.Add(8); break;
            case "Remove": map.Remove(ticket); break;
            case "Clear": map.Clear(); break;
            default: map.EnsureCapacity(9); break;
        }

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
        Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
        Assert.Equal(7, enumerator.Current);
        enumerator.Dispose();
        Assert.Equal(0, enumerator.Current);
    }

    [Fact]
    public void Enumeration_ConcreteGenericAndNongenericFollowDenseOrder()
    {
        var map = new SwiftDenseTicketMap<int>();
        SwiftTicket first = map.Add(10);
        map.Add(20);
        map.Add(30);
        map.Remove(first);

        var enumerator = map.GetEnumerator();
        Assert.Equal(0, enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(30, enumerator.Current);
        var copy = enumerator;
        Assert.True(enumerator.MoveNext());
        Assert.Equal(20, enumerator.Current);
        Assert.Equal(30, copy.Current);
        Assert.False(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());
        enumerator.Reset();
        Assert.Equal(0, enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(30, enumerator.Current);
        enumerator.Dispose();
        Assert.Equal(0, enumerator.Current);
        Assert.False(enumerator.MoveNext());

        using IEnumerator<int> generic = ((IEnumerable<int>)map).GetEnumerator();
        Assert.True(generic.MoveNext());
        Assert.Equal(30, generic.Current);
        Assert.True(generic.MoveNext());
        Assert.Equal(20, generic.Current);
        Assert.False(generic.MoveNext());

        IEnumerator nongeneric = ((IEnumerable)map).GetEnumerator();
        Assert.Throws<InvalidOperationException>(() => _ = nongeneric.Current);
        Assert.True(nongeneric.MoveNext());
        Assert.Equal(30, nongeneric.Current);
        Assert.True(nongeneric.MoveNext());
        Assert.Equal(20, nongeneric.Current);
        Assert.False(nongeneric.MoveNext());
        Assert.Throws<InvalidOperationException>(() => _ = nongeneric.Current);
        nongeneric.Reset();
        Assert.Throws<InvalidOperationException>(() => _ = nongeneric.Current);
        Assert.True(nongeneric.MoveNext());
        Assert.Equal(30, nongeneric.Current);
        ((IDisposable)nongeneric).Dispose();
        Assert.Throws<InvalidOperationException>(() => _ = nongeneric.Current);
    }

    [Fact]
    public void CloneTo_ReplacesDestinationInDenseOrderAndCopiesReferencesShallowly()
    {
        var first = new MutablePayload { Value = 1 };
        var last = new MutablePayload { Value = 3 };
        var map = new SwiftDenseTicketMap<MutablePayload>();
        SwiftTicket removed = map.Add(first);
        map.Add(null);
        map.Add(last);
        map.Remove(removed);
        var output = new List<MutablePayload> { first };

        map.CloneTo(output);

        Assert.Equal(2, output.Count);
        Assert.Same(last, output[0]);
        Assert.Null(output[1]);
        Assert.Equal("output", Assert.Throws<ArgumentNullException>(() => map.CloneTo(null)).ParamName);
        map.Clear();
        map.CloneTo(output);
        Assert.Empty(output);
    }

    [Fact]
    public void CloneTo_DestinationFailuresLeaveSourceAndTicketsUnchanged()
    {
        var map = new SwiftDenseTicketMap<int>();
        SwiftTicket first = map.Add(1);
        SwiftTicket second = map.Add(2);
        var clearFailure = new FailingOutput(failClear: true);
        var addFailure = new FailingOutput(failClear: false);

        Assert.Throws<NotSupportedException>(() => map.CloneTo(clearFailure));
        Assert.Equal(new[] { 99 }, clearFailure);
        Assert.Throws<NotSupportedException>(() => map.CloneTo(addFailure));
        Assert.Equal(new[] { 1 }, addFailure);
        Assert.Equal(new[] { 1, 2 }, map.Values.ToArray());
        Assert.Equal(1, map.GetRef(first));
        Assert.Equal(2, map.GetRef(second));
    }

    [Fact]
    public void RemoveAndClear_ReleaseDiscardedReferenceValuesFromOwnedStorage()
    {
        var map = new SwiftDenseTicketMap<object>();
        var first = new object();
        var last = new object();
        SwiftTicket ticket = map.Add(first);
        map.Add(last);

        map.Remove(ticket);

        object[] values = OwnedValues(map);
        Assert.Same(last, values[0]);
        Assert.All(values.Skip(map.Count), Assert.Null);
        map.Clear();
        Assert.All(OwnedValues(map), Assert.Null);
    }

    [Fact]
    public void RemoveAndClear_ReleaseReferencesInsideStructPayloads()
    {
        var map = new SwiftDenseTicketMap<ReferencePayload>();
        var survivor = new object();
        SwiftTicket removed = map.Add(new ReferencePayload { Resource = new object(), Tag = 1 });
        SwiftTicket last = map.Add(new ReferencePayload { Resource = survivor, Tag = 2 });

        map.Remove(removed);

        Assert.Same(survivor, map.GetRef(last).Resource);
        Assert.Equal(2, map.GetRef(last).Tag);
        Assert.All(OwnedValues(map).Skip(map.Count), value => Assert.Null(value.Resource));
        map.Clear();
        Assert.All(OwnedValues(map), value => Assert.Null(value.Resource));
    }

    [Fact]
    public void ReservedTypedOperations_DoNotAllocate()
    {
        var map = new SwiftDenseTicketMap<int>(128);
        var tickets = new SwiftTicket[64];
        for (int i = 0; i < tickets.Length; i++)
            tickets[i] = map.Add(i);
        ExerciseReservedOperations(map, tickets, 256);
        _ = GC.GetAllocatedBytesForCurrentThread();

        long before = GC.GetAllocatedBytesForCurrentThread();
        long checksum = ExerciseReservedOperations(map, tickets, 512);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
        Assert.True(checksum > 0);
        Assert.Equal(tickets.Length, map.Count);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long ExerciseReservedOperations(SwiftDenseTicketMap<int> map, SwiftTicket[] tickets, int iterations)
    {
        long checksum = 0;
        for (int round = 0; round < iterations; round++)
        {
            int index = round % tickets.Length;
            SwiftTicket ticket = tickets[index];
            map.GetRef(ticket)++;
            map.TryGet(ticket, out int value);
            checksum += value;
            checksum += map.IsValid(ticket) ? 1 : 0;
            checksum += map.GetTicketAt(index).Generation;
            foreach (int item in map)
                checksum += item;
            foreach (int item in map.Values)
                checksum += item;
            map.Remove(ticket);
            tickets[index] = map.Add(value);
            if ((round & 15) == 15)
            {
                map.Clear();
                for (int i = 0; i < tickets.Length; i++)
                    tickets[i] = map.Add(i);
            }
        }
        return checksum;
    }

    private static T[] OwnedValues<T>(SwiftDenseTicketMap<T> map)
    {
        FieldInfo field = typeof(SwiftDenseTicketMap<T>).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate => candidate.FieldType == typeof(T[]));
        return (T[])field.GetValue(map);
    }

    private sealed class MutablePayload
    {
        public int Value;
    }

    private struct ReferencePayload
    {
        public object Resource;
        public int Tag;
    }

    private sealed class FailingOutput : ICollection<int>
    {
        private readonly List<int> _items = new() { 99 };
        private readonly bool _failClear;
        public FailingOutput(bool failClear) => _failClear = failClear;
        public int Count => _items.Count;
        public bool IsReadOnly => false;
        public void Add(int item)
        {
            if (_items.Count != 0)
                throw new NotSupportedException();
            _items.Add(item);
        }
        public void Clear()
        {
            if (_failClear)
                throw new NotSupportedException();
            _items.Clear();
        }
        public bool Contains(int item) => _items.Contains(item);
        public void CopyTo(int[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
        public bool Remove(int item) => _items.Remove(item);
        public IEnumerator<int> GetEnumerator() => _items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
