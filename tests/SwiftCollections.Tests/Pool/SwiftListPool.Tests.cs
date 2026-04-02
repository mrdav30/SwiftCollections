using System.Reflection;
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

        [Fact]
        public void Clear_OnUnusedPool_ShouldNotCreateUnderlyingCollectionPool()
        {
            var pool = new SwiftListPool<int>();

            Assert.False(IsCollectionPoolCreated(pool));

            pool.Clear();

            Assert.False(IsCollectionPoolCreated(pool));
        }

        private static bool IsCollectionPoolCreated(SwiftListPool<int> pool)
        {
            FieldInfo field = typeof(SwiftCollectionPool<SwiftList<int>, int>)
                .GetField("_lazyCollectionPool", BindingFlags.Instance | BindingFlags.NonPublic);

            object lazyCollectionPool = field.GetValue(pool);

            return (bool)lazyCollectionPool.GetType().GetProperty("IsValueCreated").GetValue(lazyCollectionPool);
        }
    }
}
