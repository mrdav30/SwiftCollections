using System;
using System.Collections;
using System.Collections.Generic;
using Xunit;

namespace SwiftCollections.Tests;

public class SwiftTicketStoreTests
{
    [Fact]
    public void Attach_UsesAnIssuedTicketWithoutOwningItsLifetime()
    {
        var issuer = new SwiftDenseTicketMap<string>();
        SwiftTicket agent = issuer.Add("agent");
        var store = new SwiftTicketStore<int>();

        Assert.True(store.TryAdd(agent, 25));
        Assert.True(store.ContainsKey(agent));
        Assert.True(store.TryGet(agent, out int value));
        Assert.Equal(25, value);
        Assert.Equal(agent, store.GetTicketAt(0));
        Assert.True(store.Remove(agent));
        Assert.True(issuer.IsValid(agent));
        Assert.True(store.TryAdd(agent, 30));
        Assert.Equal(30, store.GetRef(agent));
    }

    [Fact]
    public void DefaultConstructor_ReservesEightSlotsWithoutAcceptingAnyKey()
    {
        var store = new SwiftTicketStore<int>();

        Assert.Equal(0, store.Count);
        Assert.Equal(8, store.SparseCapacity);
        Assert.Equal(8, store.DenseCapacity);
        Assert.Equal(0, store.Values.Length);
        Assert.False(store.ContainsKey(default));
        Assert.True(store.TryAdd(default, 7));
        Assert.Equal(7, store.GetRef(default));
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(0, 1, 0, 8)]
    [InlineData(1, 0, 8, 0)]
    [InlineData(1, 1, 8, 8)]
    [InlineData(8, 8, 8, 8)]
    [InlineData(9, 17, 16, 32)]
    [InlineData(17, 9, 32, 16)]
    public void CapacityConstructor_RoundsEachAxisIndependently(int sparse, int dense, int expectedSparse, int expectedDense)
    {
        var store = new SwiftTicketStore<int>(sparse, dense);

        Assert.Equal(expectedSparse, store.SparseCapacity);
        Assert.Equal(expectedDense, store.DenseCapacity);
        Assert.Equal(0, store.Count);
        Assert.Equal(0, store.Values.Length);
        Assert.True(store.TryAdd(new SwiftTicket(0, uint.MaxValue), 42));
        Assert.Equal(42, store.GetRef(new SwiftTicket(0, uint.MaxValue)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData((1 << 30) + 1)]
    [InlineData(int.MaxValue)]
    public void InvalidConstructorCapacity_ReportsItsAxisAndSparseValidationTakesPrecedence(int capacity)
    {
        Assert.Equal("sparseCapacity", Assert.Throws<ArgumentOutOfRangeException>(
            () => new SwiftTicketStore<int>(capacity, 0)).ParamName);
        Assert.Equal("denseCapacity", Assert.Throws<ArgumentOutOfRangeException>(
            () => new SwiftTicketStore<int>(0, capacity)).ParamName);
        Assert.Equal("sparseCapacity", Assert.Throws<ArgumentOutOfRangeException>(
            () => new SwiftTicketStore<int>(capacity, capacity)).ParamName);
    }

    [Theory]
    [InlineData(0U)]
    [InlineData(1U)]
    [InlineData(0x80000000U)]
    [InlineData(uint.MaxValue)]
    public void EveryGenerationIsKeyData_AndRemovalDoesNotAdvanceIt(uint generation)
    {
        var store = new SwiftTicketStore<int>();
        var key = new SwiftTicket(3, generation);

        store.Add(key, 17);

        Assert.True(store.ContainsKey(key));
        Assert.Equal(key, store.GetTicketAt(0));
        Assert.Equal(17, store.GetRef(key));
        Assert.True(store.Remove(key));
        Assert.False(store.ContainsKey(key));
        Assert.True(store.TryAdd(key, 19));
        Assert.Equal(key, store.GetTicketAt(0));
        Assert.Equal(19, store.GetRef(key));
    }

    [Theory]
    [InlineData(0U)]
    [InlineData(1U)]
    [InlineData(uint.MaxValue)]
    public void OccupiedIndex_RejectsExactAndDifferentGenerationsWithoutChangingValuesStorageOrEnumeration(uint attemptedGeneration)
    {
        var store = new SwiftTicketStore<int>(8, 8);
        var stored = new SwiftTicket(2, 0);
        store.Add(stored, 31);
        ref int borrowed = ref store.GetRef(stored);
        Span<int> span = store.Values;
        var enumerator = store.GetEnumerator();
        var attempted = new SwiftTicket(2, attemptedGeneration);

        Assert.False(store.TryAdd(attempted, 99));
        Assert.Equal("ticket", Assert.Throws<ArgumentException>(() => store.Add(attempted, 99)).ParamName);

        Assert.Equal(1, store.Count);
        Assert.Equal(8, store.SparseCapacity);
        Assert.Equal(8, store.DenseCapacity);
        Assert.Equal(stored, store.GetTicketAt(0));
        borrowed = 32;
        Assert.Equal(32, span[0]);
        Assert.Equal(32, store.GetRef(stored));
        Assert.True(enumerator.MoveNext());
        Assert.Equal(32, enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(1 << 30)]
    [InlineData(int.MaxValue)]
    public void InvalidInsertion_ThrowsBeforeChangingExistingMembershipOrBorrow(int index)
    {
        var store = new SwiftTicketStore<int>();
        store.Add(default, 7);
        var enumerator = store.GetEnumerator();
        var key = new SwiftTicket(index, 1);
        ref int borrowed = ref store.GetRef(default);

        var error = Assert.Throws<ArgumentOutOfRangeException>(() => store.TryAdd(key, 99));
        Assert.Equal("ticket", error.ParamName);
        Assert.Equal(index, Assert.IsType<int>(error.ActualValue));
        Assert.Equal("ticket", Assert.Throws<ArgumentOutOfRangeException>(() => store.Add(key, 99)).ParamName);

        borrowed = 8;
        Assert.Equal(8, store.GetRef(default));
        Assert.Equal(1, store.Count);
        Assert.Equal(8, store.SparseCapacity);
        Assert.Equal(8, store.DenseCapacity);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(8, enumerator.Current);
    }

    [Theory]
    [InlineData(-1, 0U)]
    [InlineData(int.MinValue, 1U)]
    [InlineData(1 << 30, 0U)]
    [InlineData(int.MaxValue, uint.MaxValue)]
    [InlineData(8, 1U)]
    [InlineData(7, 0U)]
    [InlineData(0, 1U)]
    public void MissingOrMismatchedKeys_ReturnFalseAndDefault_AndRefLookupThrows(int index, uint generation)
    {
        var store = new SwiftTicketStore<int>();
        store.Add(default, 13);
        var enumerator = store.GetEnumerator();
        var missing = new SwiftTicket(index, generation);

        Assert.False(store.ContainsKey(missing));
        Assert.False(store.TryGet(missing, out int value));
        Assert.Equal(0, value);
        Assert.False(store.Remove(missing));
        Assert.Throws<KeyNotFoundException>(() => { _ = store.GetRef(missing); });
        Assert.True(enumerator.MoveNext());
        Assert.Equal(13, enumerator.Current);
        Assert.Equal(1, store.Count);
        Assert.Equal(13, store.GetRef(default));
    }

    [Fact]
    public void EmptyReads_DoNotTreatDefaultAsAStoredSentinel()
    {
        var store = new SwiftTicketStore<string>(0, 0);

        Assert.False(store.ContainsKey(default));
        Assert.False(store.TryGet(default, out string missing));
        Assert.Null(missing);
        Assert.False(store.Remove(default));
        Assert.Throws<KeyNotFoundException>(() => { _ = store.GetRef(default); });
    }

    [Theory]
    [InlineData(0, 30, 20)]
    [InlineData(1, 10, 30)]
    [InlineData(2, 10, 20)]
    public void Remove_RepairsFirstMiddleAndLastPositionsWithoutChangingSurvivingTickets(int removedIndex, int firstValue, int secondValue)
    {
        var store = new SwiftTicketStore<int>();
        var tickets = new[] { new SwiftTicket(1, 0), new SwiftTicket(4, 9), new SwiftTicket(7, uint.MaxValue) };
        for (int i = 0; i < tickets.Length; i++)
            store.Add(tickets[i], (i + 1) * 10);

        Assert.True(store.Remove(tickets[removedIndex]));

        Assert.Equal(new[] { firstValue, secondValue }, store.Values.ToArray());
        Assert.Equal(tickets[removedIndex == 0 ? 2 : 0], store.GetTicketAt(0));
        Assert.Equal(tickets[removedIndex == 1 ? 2 : 1], store.GetTicketAt(1));
        Assert.False(store.ContainsKey(tickets[removedIndex]));
        for (int i = 0; i < store.Count; i++)
        {
            SwiftTicket key = store.GetTicketAt(i);
            Assert.True(store.TryGet(key, out int value));
            Assert.Equal(store.Values[i], value);
        }
        Assert.False(store.Remove(tickets[removedIndex]));
    }

    [Fact]
    public void Growth_ReservesSparseAndDenseAxesIndependentlyAndTogether()
    {
        var store = new SwiftTicketStore<int>(0, 0);
        var first = new SwiftTicket(0, 0);
        store.Add(first, 10);
        for (int i = 1; i < 8; i++)
            store.Add(new SwiftTicket(i, (uint)i), i + 10);

        store.EnsureCapacity(8, 9);
        Assert.Equal(8, store.SparseCapacity);
        Assert.Equal(16, store.DenseCapacity);
        store.Add(new SwiftTicket(32, 0), 42);
        Assert.Equal(64, store.SparseCapacity);
        Assert.Equal(16, store.DenseCapacity);
        store.EnsureCapacity(65, 17);
        Assert.Equal(128, store.SparseCapacity);
        Assert.Equal(32, store.DenseCapacity);
        Assert.Equal(10, store.GetRef(first));
        Assert.Equal(42, store.GetRef(new SwiftTicket(32, 0)));
        for (int i = 0; i < store.Count; i++)
            Assert.Equal(store.Values[i], store.GetRef(store.GetTicketAt(i)));
    }

    [Fact]
    public void Add_GrowsBothAxesWhenTheirReservationsAreExhausted()
    {
        var store = new SwiftTicketStore<int>(8, 8);
        for (int i = 0; i < 9; i++)
            Assert.True(store.TryAdd(new SwiftTicket(i, (uint)i), i));

        Assert.Equal(16, store.DenseCapacity);
        Assert.Equal(16, store.SparseCapacity);
        Assert.Equal(9, store.Count);
        for (int i = 0; i < 9; i++)
            Assert.Equal(i, store.GetRef(new SwiftTicket(i, (uint)i)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData((1 << 30) + 1)]
    [InlineData(int.MaxValue)]
    public void InvalidReservation_ValidatesBothAxesBeforePublishingAnything(int invalid)
    {
        var store = new SwiftTicketStore<int>();
        store.Add(default, 17);
        ref int borrowed = ref store.GetRef(default);
        var enumerator = store.GetEnumerator();

        Assert.Equal("denseCapacity", Assert.Throws<ArgumentOutOfRangeException>(
            () => store.EnsureCapacity(32, invalid)).ParamName);
        Assert.Equal("sparseCapacity", Assert.Throws<ArgumentOutOfRangeException>(
            () => store.EnsureCapacity(invalid, 32)).ParamName);
        Assert.Equal("sparseCapacity", Assert.Throws<ArgumentOutOfRangeException>(
            () => store.EnsureCapacity(invalid, invalid)).ParamName);

        Assert.Equal(8, store.SparseCapacity);
        Assert.Equal(8, store.DenseCapacity);
        borrowed = 18;
        Assert.Equal(18, store.GetRef(default));
        Assert.True(enumerator.MoveNext());
        Assert.Equal(18, enumerator.Current);
    }

    [Fact]
    public void ReservationNoOpsAndValueWrites_PreserveBorrowsAndEnumeratorVersion()
    {
        var store = new SwiftTicketStore<int>();
        store.Add(default, 10);
        var other = new SwiftTicket(7, uint.MaxValue);
        store.Add(other, 20);
        var enumerator = store.GetEnumerator();
        ref int borrowed = ref store.GetRef(default);
        Span<int> values = store.Values;

        store.EnsureCapacity(0, 0);
        store.EnsureCapacity(8, 8);
        store.EnsureCapacity(1, 1);
        borrowed = 11;
        values[1] = 22;

        Assert.True(enumerator.MoveNext());
        Assert.Equal(11, enumerator.Current);
        store.GetRef(default) = 12;
        Assert.Equal(11, enumerator.Current);
        Assert.Equal(12, borrowed);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(22, enumerator.Current);
        Assert.False(enumerator.MoveNext());
        Assert.Equal(new[] { 12, 22 }, store.Values.ToArray());
    }

    [Theory]
    [InlineData("add")]
    [InlineData("remove")]
    [InlineData("clear")]
    [InlineData("sparse")]
    [InlineData("dense")]
    [InlineData("both")]
    public void StructuralChanges_InvalidateMoveNextAndReset(string operation)
    {
        var store = new SwiftTicketStore<int>();
        store.Add(default, 10);
        var enumerator = store.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        switch (operation)
        {
            case "add": store.Add(new SwiftTicket(1, 0), 20); break;
            case "remove": store.Remove(default); break;
            case "clear": store.Clear(); break;
            case "sparse": store.EnsureCapacity(16, 8); break;
            case "dense": store.EnsureCapacity(8, 16); break;
            case "both": store.EnsureCapacity(16, 16); break;
        }

        Assert.Equal(10, enumerator.Current);
        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
        Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
    }

    [Fact]
    public void Clear_RetainsBothCapacities_AndEmptyClearIsANoOp()
    {
        var store = new SwiftTicketStore<int>(64, 16);
        var key = new SwiftTicket(40, uint.MaxValue);
        store.Add(key, 10);

        store.Clear();
        var enumerator = store.GetEnumerator();
        store.Clear();

        Assert.Equal(0, store.Count);
        Assert.Equal(64, store.SparseCapacity);
        Assert.Equal(16, store.DenseCapacity);
        Assert.Equal(0, store.Values.Length);
        Assert.False(store.ContainsKey(key));
        Assert.False(enumerator.MoveNext());
        Assert.True(store.TryAdd(key, 12));
        Assert.Equal(12, store.GetRef(key));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void GetTicketAt_RejectsEveryPositionOutsideTheLivePrefix(int denseIndex)
    {
        var store = new SwiftTicketStore<int>();
        store.Add(default, 8);

        var error = Assert.Throws<ArgumentOutOfRangeException>(() => store.GetTicketAt(denseIndex));
        Assert.Equal("denseIndex", error.ParamName);
        Assert.Equal(denseIndex, Assert.IsType<int>(error.ActualValue));
        Assert.Equal(default(SwiftTicket), store.GetTicketAt(0));
    }

    [Fact]
    public void Enumeration_CopiesAdvanceIndependently_AndResetRewinds()
    {
        var store = new SwiftTicketStore<int>();
        store.Add(default, 4);
        store.Add(new SwiftTicket(5, 0), 8);
        var original = store.GetEnumerator();
        Assert.Equal(0, original.Current);
        Assert.True(original.MoveNext());
        var copy = original;

        Assert.True(original.MoveNext());
        Assert.Equal(8, original.Current);
        Assert.Equal(4, copy.Current);
        Assert.True(copy.MoveNext());
        Assert.Equal(8, copy.Current);
        Assert.False(copy.MoveNext());
        Assert.False(copy.MoveNext());
        Assert.Equal(0, copy.Current);
        original.Reset();
        Assert.Equal(0, original.Current);
        Assert.True(original.MoveNext());
        Assert.Equal(4, original.Current);
        original.Dispose();
        Assert.Equal(0, original.Current);
        Assert.False(original.MoveNext());
        original.Reset();
        Assert.True(original.MoveNext());
        Assert.Equal(4, original.Current);
    }

    [Fact]
    public void InterfaceEnumeration_ReportsDenseOrderAndValidatesNonGenericCurrentPosition()
    {
        var store = new SwiftTicketStore<int>();
        store.Add(new SwiftTicket(9, 0), 11);
        store.Add(default, 22);

        using (IEnumerator<int> generic = ((IEnumerable<int>)store).GetEnumerator())
        {
            Assert.True(generic.MoveNext());
            Assert.Equal(11, generic.Current);
            Assert.True(generic.MoveNext());
            Assert.Equal(22, generic.Current);
            Assert.False(generic.MoveNext());
            generic.Reset();
            Assert.True(generic.MoveNext());
            Assert.Equal(11, generic.Current);
        }
        IEnumerator nongeneric = ((IEnumerable)store).GetEnumerator();
        Assert.Throws<InvalidOperationException>(() => { _ = nongeneric.Current; });
        Assert.True(nongeneric.MoveNext());
        Assert.Equal(11, nongeneric.Current);
        Assert.True(nongeneric.MoveNext());
        Assert.Equal(22, nongeneric.Current);
        Assert.False(nongeneric.MoveNext());
        Assert.Throws<InvalidOperationException>(() => { _ = nongeneric.Current; });
        nongeneric.Reset();
        Assert.True(nongeneric.MoveNext());
        Assert.Equal(11, nongeneric.Current);
        ((IDisposable)nongeneric).Dispose();
        Assert.Throws<InvalidOperationException>(() => { _ = nongeneric.Current; });
    }

    [Fact]
    public void PayloadIdentity_IsIndependentOfEqualityHashingAndNullValues()
    {
        var store = new SwiftTicketStore<HostilePayload>();
        var payload = new HostilePayload { Value = 1 };
        store.Add(default, payload);
        store.Add(new SwiftTicket(7, uint.MaxValue), payload);
        store.Add(new SwiftTicket(2, 0), null);
        payload.Value = 9;

        Assert.Equal(3, store.Count);
        Assert.Same(payload, store.GetRef(default));
        Assert.Same(payload, store.GetRef(new SwiftTicket(7, uint.MaxValue)));
        Assert.Equal(9, store.GetRef(default).Value);
        Assert.True(store.TryGet(new SwiftTicket(2, 0), out HostilePayload empty));
        Assert.Null(empty);
        Assert.True(store.ContainsKey(new SwiftTicket(2, 0)));
        Assert.True(store.Remove(default));
        Assert.Same(payload, store.GetRef(new SwiftTicket(7, uint.MaxValue)));
        store.Clear();
        Assert.Equal(0, store.Count);
    }

    private sealed class HostilePayload
    {
        public int Value;
        public override bool Equals(object other) => throw new InvalidOperationException("Payload equality must not be used for membership.");
        public override int GetHashCode() => throw new InvalidOperationException("Payload hashing must not be used for membership.");
    }
}
