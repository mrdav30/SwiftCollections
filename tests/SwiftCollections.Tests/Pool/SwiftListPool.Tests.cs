using Xunit;

namespace SwiftCollections.Pool.Tests
{
    public class SwiftListPoolTests
    {
        [Fact]
        public void Rent_ShouldReturnListInstance()
        {
            // Arrange
            var pool = new SwiftListPool<int>();

            // Act
            var list = pool.Rent();

            // Assert
            Assert.NotNull(list);
            Assert.Empty(list);
        }

        [Fact]
        public void Release_ShouldClearListAndReturnToPool()
        {
            // Arrange
            var pool = new SwiftListPool<int>();
            var list = pool.Rent();
            list.Add(99);

            // Act
            pool.Release(list);
            var reusedList = pool.Rent();

            // Assert
            Assert.Empty(reusedList); // List should be cleared
            Assert.Same(list, reusedList);
        }

        [Fact]
        public void Clear_ShouldEmptyPool()
        {
            // Arrange
            var pool = new SwiftListPool<int>();
            var list = pool.Rent();
            pool.Release(list);

            // Act
            pool.Clear();

            // Assert
            var newList = pool.Rent();
            Assert.NotSame(list, newList); // Cleared pool creates a new list
        }
    }
}