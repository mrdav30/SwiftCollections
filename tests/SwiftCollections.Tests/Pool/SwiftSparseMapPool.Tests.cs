using Xunit;

namespace SwiftCollections.Pool.Tests
{
    public class SwiftSparseMapPoolTests
    {
        [Fact]
        public void Rent_ShouldReturnSparseMapInstance()
        {
            var pool = new SwiftSparseMapPool<string>();

            var map = pool.Rent();

            Assert.NotNull(map);
            Assert.Equal(0, map.Count);
        }

        [Fact]
        public void Release_ShouldClearSparseMapAndReturnToPool()
        {
            var pool = new SwiftSparseMapPool<string>();
            var map = pool.Rent();
            map.Add(1, "One");

            pool.Release(map);
            var reusedMap = pool.Rent();

            Assert.Equal(0, reusedMap.Count);
            Assert.False(reusedMap.ContainsKey(1));
            Assert.Same(map, reusedMap);
        }

        [Fact]
        public void Clear_ShouldEmptyPool()
        {
            var pool = new SwiftSparseMapPool<string>();
            var map = pool.Rent();
            pool.Release(map);

            pool.Clear();

            var newMap = pool.Rent();
            Assert.NotSame(map, newMap);
        }
    }
}
