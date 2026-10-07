using System;
using System.Runtime.CompilerServices;
using Xunit;

namespace SwiftCollections.Pool.Tests;

[Collection("PoolFinalization")]
public class SwiftQueuePoolTests
{
    [Fact]
    public void GetReleaseAndFlush_ReusesThenResetsQueues()
    {
        using var pool = new SwiftQueuePool<int>();

        SwiftQueue<int> leasedQueue;

        {
            using SwiftPooledObject<SwiftQueue<int>> lease = pool.Get(out leasedQueue);
            leasedQueue.Enqueue(1);
            leasedQueue.Enqueue(2);

            Assert.Equal(2, leasedQueue.Count);
        }

        SwiftQueue<int> reusedQueue = pool.Rent();

        Assert.Same(leasedQueue, reusedQueue);
        Assert.Empty(reusedQueue);

        pool.Release(reusedQueue);
        pool.Flush();

        SwiftQueue<int> freshQueue = pool.Rent();

        Assert.NotSame(reusedQueue, freshQueue);
    }

    [Fact]
    public void ReleaseNullAndDispose_AreHandledSafely()
    {
        var pool = new SwiftQueuePool<int>();

        pool.Release(null);

        SwiftQueue<int> queue = pool.Rent();
        pool.Release(queue);

        pool.Dispose();
        pool.Dispose();
        pool.Clear();

        Assert.Throws<ObjectDisposedException>(() => pool.Rent());
        Assert.Throws<ObjectDisposedException>(() => pool.Release(new SwiftQueue<int>()));
    }

    [Fact]
    public void Flush_BeforeAnyRent_IsANoOp()
    {
        using var pool = new SwiftQueuePool<int>();

        pool.Flush();
    }

    [Fact]
    public void Finalizer_ShouldClearAndDisposeUnreleasedPool()
    {
        var reference = CreateUnreleasedPool(out var collectionPool);

        GC.Collect();
        GC.WaitForPendingFinalizers();

        Assert.True(reference.TryGetTarget(out var pool));
        Assert.Equal(0, collectionPool.CountInactive);
        Assert.Throws<ObjectDisposedException>(() => collectionPool.Rent());
        Assert.Equal(nameof(SwiftQueuePool<int>),
            Assert.Throws<ObjectDisposedException>(() => pool.Rent()).ObjectName);
        Assert.Equal(nameof(SwiftQueuePool<int>),
            Assert.Throws<ObjectDisposedException>(() => pool.Release(new SwiftQueue<int>())).ObjectName);

        pool.Dispose();
        pool.Clear();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<SwiftQueuePool<int>> CreateUnreleasedPool(
        out SwiftObjectPool<SwiftQueue<int>> collectionPool)
    {
        var pool = new SwiftQueuePool<int>();
        var queue = pool.Rent();
        queue.Enqueue(42);
        pool.Release(queue);
        collectionPool = pool.CollectionPool;
        Assert.Equal(1, collectionPool.CountInactive);

        // Keep a weak reference through finalization so the wrapper's disposal can be asserted.
        return new WeakReference<SwiftQueuePool<int>>(pool, trackResurrection: true);
    }
}
