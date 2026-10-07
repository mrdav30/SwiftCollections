using System;
using System.Runtime.CompilerServices;
using Xunit;

namespace SwiftCollections.Pool.Tests
{
    [Collection("PoolFinalization")]
    public class SwiftPackedSetPoolTests
    {
        [Fact]
        public void Rent_ShouldReturnPackedSetInstance()
        {
            using var pool = new SwiftPackedSetPool<int>();

            var set = pool.Rent();

            Assert.NotNull(set);
            Assert.Empty(set);
        }

        [Fact]
        public void Release_ShouldClearPackedSetAndReturnToPool()
        {
            using var pool = new SwiftPackedSetPool<int>();
            var set = pool.Rent();
            set.Add(42);

            pool.Release(set);
            var reusedSet = pool.Rent();

            Assert.Empty(reusedSet);
            Assert.Same(set, reusedSet);
        }

        [Fact]
        public void Clear_ShouldEmptyPool()
        {
            using var pool = new SwiftPackedSetPool<int>();
            var set = pool.Rent();
            pool.Release(set);

            pool.Clear();

            var newSet = pool.Rent();
            Assert.NotSame(set, newSet);
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
            Assert.Equal(nameof(SwiftPackedSetPool<int>),
                Assert.Throws<ObjectDisposedException>(() => pool.Rent()).ObjectName);
            Assert.Equal(nameof(SwiftPackedSetPool<int>),
                Assert.Throws<ObjectDisposedException>(() => pool.Release(new SwiftPackedSet<int>())).ObjectName);

            pool.Dispose();
            pool.Clear();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference<SwiftPackedSetPool<int>> CreateUnreleasedPool(
            out SwiftObjectPool<SwiftPackedSet<int>> collectionPool)
        {
            var pool = new SwiftPackedSetPool<int>();
            var set = pool.Rent();
            set.Add(42);
            pool.Release(set);
            collectionPool = pool.CollectionPool;
            Assert.Equal(1, collectionPool.CountInactive);

            // Keep a weak reference through finalization so the wrapper's disposal can be asserted.
            return new WeakReference<SwiftPackedSetPool<int>>(pool, trackResurrection: true);
        }
    }
}
