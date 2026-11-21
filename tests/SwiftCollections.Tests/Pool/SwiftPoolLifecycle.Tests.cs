using System;
using System.Collections;
using System.Collections.Generic;
using Xunit;

namespace SwiftCollections.Pool.Tests;

public class SwiftPoolLifecycleTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    [InlineData(4, true)]
    [InlineData(5, false)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    [InlineData(6, true)]
    public void Disposal_GetRejectsWithoutCreatingAnotherRental(int kind, bool initialized)
    {
        using PoolCase pool = Create(kind);
        if (initialized)
        {
            object item = pool.Rent();
            pool.Release(item);
        }

        pool.Dispose();

        Assert.Throws<ObjectDisposedException>(() =>
        {
            using IDisposable lease = pool.Get();
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void Disposal_BackingPoolAccessCannotBypassTerminalState(int kind)
    {
        using PoolCase pool = Create(kind);
        Assert.NotNull(pool.BackingPool());

        pool.Dispose();

        Assert.Throws<ObjectDisposedException>(() => pool.BackingPool());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void Disposal_RepeatedDisposeAndClearAreSafeButRentAndReleaseReject(int kind)
    {
        using PoolCase pool = Create(kind);
        pool.Release(null);
        object item = pool.Rent();
        pool.Release(item);

        pool.Dispose();
        pool.Dispose();
        pool.Clear();

        Assert.Throws<ObjectDisposedException>(() => pool.Rent());
        Assert.Throws<ObjectDisposedException>(() => pool.Release(null));
        Assert.Throws<ObjectDisposedException>(() => pool.Release(item));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void Clear_PreservesOutstandingRentalAndSameBackingPool(int kind)
    {
        using PoolCase pool = Create(kind);
        object rented = pool.Rent();
        object backing = pool.BackingPool();
        pool.Fill(rented);

        pool.Clear();

        Assert.Same(backing, pool.BackingPool());
        Assert.Equal(1, pool.Count(rented));
        pool.Release(rented);
        Assert.Equal(0, pool.Count(rented));
        Assert.Same(rented, pool.Rent());
        pool.Release(rented);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void Clear_UnusedPoolDoesNotAllocateBackingStorage(int kind)
    {
        using (PoolCase warm = Create(kind))
            warm.Clear();
        using PoolCase pool = Create(kind);

        long before = GC.GetAllocatedBytesForCurrentThread();
        pool.Clear();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0L, allocated);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Flush_ReplacesInitializedBackingPoolAndRemainsReusable(int kind)
    {
        using PoolCase pool = Create(kind);
        pool.Flush();
        object first = pool.Rent();
        pool.Fill(first);
        pool.Release(first);
        object backing = pool.BackingPool();

        pool.Flush();

        Assert.NotSame(backing, pool.BackingPool());
        object next = pool.Rent();
        Assert.NotSame(first, next);
        Assert.Equal(0, pool.Count(next));
        pool.Release(next);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Flush_CannotReviveDisposedOwner(int kind)
    {
        using PoolCase pool = Create(kind);
        object first = pool.Rent();
        pool.Release(first);
        pool.Dispose();

        pool.Flush();
        pool.Clear();

        Assert.Throws<ObjectDisposedException>(() =>
        {
            using IDisposable lease = pool.Get();
        });
        Assert.Throws<ObjectDisposedException>(() => pool.Rent());
        Assert.Throws<ObjectDisposedException>(() => pool.BackingPool());
    }

    [Fact]
    public void GenericCollectionSubclass_RetainsLazyCreationSingleResetAndReusableFlush()
    {
        using var pool = new TrackingCollectionPool();
        int created = TrackingCollection.Created;

        pool.Clear();
        pool.Flush();

        Assert.Equal(created, TrackingCollection.Created);
        TrackingCollection first = pool.Rent();
        Assert.Equal(created + 1, TrackingCollection.Created);
        first.Add(42);
        pool.Release(first);
        Assert.Equal(1, first.ClearCalls);
        Assert.Empty(first);
        Assert.Same(first, pool.Rent());
        pool.Release(first);
        Assert.Equal(2, first.ClearCalls);
        pool.Flush();
        TrackingCollection next = pool.Rent();
        Assert.NotSame(first, next);
        Assert.Equal(created + 2, TrackingCollection.Created);
        pool.Release(next);
        pool.Flush();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GenericCollectionSubclass_DisposePermanentlyClosesEveryRentalPath(bool initialized)
    {
        using var pool = new TrackingCollectionPool();
        int created = TrackingCollection.Created;
        SwiftObjectPool<TrackingCollection> backing = null;
        TrackingCollection previous = null;
        if (initialized)
        {
            previous = pool.Rent();
            pool.Release(previous);
            backing = pool.CollectionPool;
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        pool.Dispose();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        pool.Dispose();
        pool.Clear();
        pool.Flush();

        Assert.Equal(0L, allocated);
        Assert.Equal(created + (initialized ? 1 : 0), TrackingCollection.Created);
        ObjectDisposedException failure = Assert.Throws<ObjectDisposedException>(() => pool.Rent());
        Assert.Equal(nameof(TrackingCollectionPool), failure.ObjectName);
        Assert.Throws<ObjectDisposedException>(() => pool.Get(out _));
        Assert.Throws<ObjectDisposedException>(() => pool.Release(null));
        Assert.Throws<ObjectDisposedException>(() => pool.Release(previous));
        Assert.Throws<ObjectDisposedException>(() => pool.CollectionPool);
        Assert.Equal(created + (initialized ? 1 : 0), TrackingCollection.Created);
        if (initialized)
            Assert.Throws<ObjectDisposedException>(() => backing.Rent());
    }

    [Fact]
    public void GenericCollectionSubclass_RejectsNullReturnWhileUsable()
    {
        using var pool = new TrackingCollectionPool();
        int created = TrackingCollection.Created;

        Assert.Throws<ArgumentNullException>(() => pool.Release(null));

        Assert.Equal(created, TrackingCollection.Created);
        TrackingCollection item = pool.Rent();
        Assert.Equal(created + 1, TrackingCollection.Created);
        pool.Release(item);
        Assert.Equal(1, item.ClearCalls);
    }

    private static PoolCase Create(int kind) => kind switch
    {
        0 => Adapt(new SwiftListPool<int>()),
        1 => Adapt(new SwiftDictionaryPool<int, int>()),
        2 => Adapt(new SwiftHashSetPool<int>()),
        3 => Adapt(new SwiftQueuePool<int>()),
        4 => Adapt(new SwiftStackPool<int>()),
        5 => Adapt(new SwiftPackedSetPool<int>()),
        _ => AdaptSparseMap()
    };

    private static PoolCase Adapt<TCollection, TItem>(SwiftCollectionPool<TCollection, TItem> pool)
        where TCollection : class, ICollection<TItem>, new() => new()
        {
            Rent = () => pool.Rent(),
            Get = () => pool.Get(out _),
            BackingPool = () => pool.CollectionPool,
            Release = item => pool.Release((TCollection)item),
            Fill = item => ((TCollection)item).Add(default),
            Count = item => ((TCollection)item).Count,
            Clear = pool.Clear,
            Flush = pool.Flush,
            DisposePool = ((IDisposable)pool).Dispose
        };

    private static PoolCase AdaptSparseMap()
    {
        var pool = new SwiftSparseMapPool<int>();
        return new PoolCase
        {
            Rent = () => pool.Rent(),
            Get = () => pool.Get(out _),
            BackingPool = () => pool.CollectionPool,
            Release = item => pool.Release((SwiftSparseMap<int>)item),
            Fill = item => ((SwiftSparseMap<int>)item).Add(0, 0),
            Count = item => ((SwiftSparseMap<int>)item).Count,
            Clear = pool.Clear,
            DisposePool = pool.Dispose
        };
    }

    private sealed class PoolCase : IDisposable
    {
        public Func<object> Rent { get; init; }
        public Func<IDisposable> Get { get; init; }
        public Func<object> BackingPool { get; init; }
        public Action<object> Release { get; init; }
        public Action<object> Fill { get; init; }
        public Func<object, int> Count { get; init; }
        public Action Clear { get; init; }
        public Action Flush { get; init; }
        public Action DisposePool { get; init; }
        public void Dispose() => DisposePool();
    }

    private sealed class TrackingCollectionPool : SwiftCollectionPool<TrackingCollection, int> { }

    private sealed class TrackingCollection : ICollection<int>
    {
        private readonly List<int> _items = new();
        public static int Created { get; private set; }
        public int ClearCalls { get; private set; }
        public int Count => _items.Count;
        public bool IsReadOnly => false;
        public TrackingCollection() => Created++;
        public void Add(int item) => _items.Add(item);
        public void Clear() { ClearCalls++; _items.Clear(); }
        public bool Contains(int item) => _items.Contains(item);
        public void CopyTo(int[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
        public bool Remove(int item) => _items.Remove(item);
        public IEnumerator<int> GetEnumerator() => _items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
