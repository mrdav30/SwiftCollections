using System;
using Xunit;

namespace SwiftCollections.Pool.Tests;

public class SwiftQueuePoolTests
{
    [Fact]
    public void GetReleaseAndFlush_ReusesThenResetsQueues()
    {
        var pool = new SwiftQueuePool<int>();

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
        var pool = new SwiftQueuePool<int>();

        pool.Flush();
    }
}
