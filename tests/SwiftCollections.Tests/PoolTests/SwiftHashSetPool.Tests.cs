using Xunit;

namespace SwiftCollections.Pool.Tests
{
    public class SwiftHashSetPoolTests
    {
        [Fact]
        public void Rent_ShouldReturnHashSetInstance()
        {
            // Arrange
            var pool = new SwiftHashSetPool<int>();

            // Act
            var hashSet = pool.Rent();

            // Assert
            Assert.NotNull(hashSet);
            Assert.Empty(hashSet);
        }

        [Fact]
        public void Release_ShouldClearHashSetAndReturnToPool()
        {
            // Arrange
            var pool = new SwiftHashSetPool<int>();
            var hashSet = pool.Rent();
            hashSet.Add(42);

            // Act
            pool.Release(hashSet);
            var reusedHashSet = pool.Rent();

            // Assert
            Assert.Empty(reusedHashSet); // HashSet should be cleared
            Assert.Same(hashSet, reusedHashSet);
        }

        [Fact]
        public void Clear_ShouldEmptyPool()
        {
            // Arrange
            var pool = new SwiftHashSetPool<int>();
            var hashSet = pool.Rent();
            pool.Release(hashSet);

            // Act
            pool.Clear();

            // Assert
            var newHashSet = pool.Rent();
            Assert.NotSame(hashSet, newHashSet); // Cleared pool creates a new hash set
        }
    }
}