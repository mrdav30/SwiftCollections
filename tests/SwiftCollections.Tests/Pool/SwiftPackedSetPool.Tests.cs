using Xunit;

namespace SwiftCollections.Pool.Tests
{
    public class SwiftPackedSetPoolTests
    {
        [Fact]
        public void Rent_ShouldReturnPackedSetInstance()
        {
            var pool = new SwiftPackedSetPool<int>();

            var set = pool.Rent();

            Assert.NotNull(set);
            Assert.Empty(set);
        }

        [Fact]
        public void Release_ShouldClearPackedSetAndReturnToPool()
        {
            var pool = new SwiftPackedSetPool<int>();
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
            var pool = new SwiftPackedSetPool<int>();
            var set = pool.Rent();
            pool.Release(set);

            pool.Clear();

            var newSet = pool.Rent();
            Assert.NotSame(set, newSet);
        }
    }
}
