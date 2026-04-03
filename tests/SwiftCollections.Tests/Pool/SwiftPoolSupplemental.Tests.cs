using System;
using Xunit;

namespace SwiftCollections.Pool.Tests;

public class SwiftPoolSupplementalTests
{
    [Fact]
    public void SharedProperties_ReturnSingletonInstances()
    {
        Assert.Same(SwiftArrayPool<int>.Shared, SwiftArrayPool<int>.Shared);
        Assert.Same(SwiftDictionaryPool<int, int>.Shared, SwiftDictionaryPool<int, int>.Shared);
        Assert.Same(SwiftHashSetPool<int>.Shared, SwiftHashSetPool<int>.Shared);
        Assert.Same(SwiftListPool<int>.Shared, SwiftListPool<int>.Shared);
        Assert.Same(SwiftPackedSetPool<int>.Shared, SwiftPackedSetPool<int>.Shared);
        Assert.Same(SwiftQueuePool<int>.Shared, SwiftQueuePool<int>.Shared);
        Assert.Same(SwiftSparseMapPool<int>.Shared, SwiftSparseMapPool<int>.Shared);
        Assert.Same(SwiftStackPool<int>.Shared, SwiftStackPool<int>.Shared);
    }

    [Fact]
    public void SwiftArrayPool_ReleaseWithoutMatchingSizePool_ClearsTheArray()
    {
        var pool = new SwiftArrayPool<int>();
        int[] values = { 1, 2, 3 };

        pool.Release(values);

        Assert.Equal(new[] { 0, 0, 0 }, values);
    }

    [Fact]
    public void SwiftObjectPool_TracksCountsAndSupportsBulkRelease()
    {
        int destroyed = 0;
        var pool = new SwiftObjectPool<SwiftCollections.Tests.DisposableSpy>(
            createFunc: () => new SwiftCollections.Tests.DisposableSpy(),
            actionOnDestroy: _ => destroyed++,
            maxSize: 1);

        {
            using SwiftPooledObject<SwiftCollections.Tests.DisposableSpy> lease =
                pool.Rent(out SwiftCollections.Tests.DisposableSpy leased);

            Assert.NotNull(leased);
            Assert.Equal(1, pool.CountAll);
            Assert.Equal(1, pool.CountActive);
            Assert.Equal(0, pool.CountInactive);
        }

        Assert.Equal(1, pool.CountAll);
        Assert.Equal(0, pool.CountActive);
        Assert.Equal(1, pool.CountInactive);

        SwiftCollections.Tests.DisposableSpy first = pool.Rent();
        SwiftCollections.Tests.DisposableSpy second = pool.Rent();

        Assert.Equal(2, pool.CountAll);
        Assert.Equal(2, pool.CountActive);

        pool.Release(new[] { first, second });

        Assert.Equal(1, pool.CountAll);
        Assert.Equal(0, pool.CountActive);
        Assert.Equal(1, pool.CountInactive);
        Assert.Equal(1, destroyed);
    }

    [Fact]
    public void SwiftObjectPool_RejectsNullReleaseAndDisposingTwiceIsSafe()
    {
        var pool = new SwiftObjectPool<SwiftCollections.Tests.DisposableSpy>(() => new SwiftCollections.Tests.DisposableSpy());
        SwiftCollections.Tests.DisposableSpy item = pool.Rent();

        Assert.Throws<ArgumentNullException>(() => pool.Release((SwiftCollections.Tests.DisposableSpy)null));

        pool.Release(item);
        pool.Dispose();
        pool.Dispose();

        Assert.Throws<ObjectDisposedException>(() => pool.Rent());
    }

    [Fact]
    public void SwiftSparseMapPool_GetReturnsReusableLeaseAndDisposeIsIdempotent()
    {
        var pool = new SwiftSparseMapPool<int>();

        {
            using SwiftPooledObject<SwiftSparseMap<int>> lease = pool.Get(out SwiftSparseMap<int> map);

            map.Add(1, 10);
            Assert.Equal(10, map[1]);
        }

        SwiftSparseMap<int> reusedMap = pool.Rent();

        Assert.Empty(reusedMap);

        pool.Release(reusedMap);
        pool.Dispose();
        pool.Dispose();
        pool.Clear();

        Assert.Throws<ObjectDisposedException>(() => pool.Rent());
    }

    [Fact]
    public void CollectionPoolWrappers_DisposeTwiceAndRejectFurtherRent()
    {
        AssertDisposed(
            pool: new SwiftDictionaryPool<int, int>(),
            prime: pool =>
            {
                SwiftDictionary<int, int> dictionary = pool.Rent();
                dictionary.Add(1, 1);
                pool.Release(dictionary);
            },
            dispose: pool => pool.Dispose(),
            useAfterDispose: pool => pool.Rent());

        AssertDisposed(
            pool: new SwiftHashSetPool<int>(),
            prime: pool =>
            {
                SwiftHashSet<int> set = pool.Rent();
                set.Add(1);
                pool.Release(set);
            },
            dispose: pool => pool.Dispose(),
            useAfterDispose: pool => pool.Rent());

        AssertDisposed(
            pool: new SwiftListPool<int>(),
            prime: pool =>
            {
                SwiftList<int> list = pool.Rent();
                list.Add(1);
                pool.Release(list);
            },
            dispose: pool => pool.Dispose(),
            useAfterDispose: pool => pool.Rent());

        AssertDisposed(
            pool: new SwiftPackedSetPool<int>(),
            prime: pool =>
            {
                SwiftPackedSet<int> set = pool.Rent();
                set.Add(1);
                pool.Release(set);
            },
            dispose: pool => pool.Dispose(),
            useAfterDispose: pool => pool.Rent());

        AssertDisposed(
            pool: new SwiftStackPool<int>(),
            prime: pool =>
            {
                SwiftStack<int> stack = pool.Rent();
                stack.Push(1);
                pool.Release(stack);
            },
            dispose: pool => pool.Dispose(),
            useAfterDispose: pool => pool.Rent());
    }

    private static void AssertDisposed<TPool, TValue>(TPool pool, Action<TPool> prime, Action<TPool> dispose, Func<TPool, TValue> useAfterDispose)
    {
        prime(pool);

        dispose(pool);
        dispose(pool);

        Assert.Throws<ObjectDisposedException>(() => _ = useAfterDispose(pool));
    }
}
