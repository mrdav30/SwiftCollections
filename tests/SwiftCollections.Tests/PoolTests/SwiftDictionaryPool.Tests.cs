using Xunit;

namespace SwiftCollections.Pool.Tests
{
    public class SwiftDictionaryPoolTests
    {
        [Fact]
        public void Rent_ShouldReturnDictionaryInstance()
        {
            // Arrange
            var pool = new SwiftDictionaryPool<int, string>();

            // Act
            var dictionary = pool.Rent();

            // Assert
            Assert.NotNull(dictionary);
            Assert.Empty(dictionary);
        }

        [Fact]
        public void Release_ShouldClearDictionaryAndReturnToPool()
        {
            // Arrange
            var pool = new SwiftDictionaryPool<int, string>();
            var dictionary = pool.Rent();
            dictionary.Add(1, "One");

            // Act
            pool.Release(dictionary);
            var reusedDictionary = pool.Rent();

            // Assert
            Assert.Empty(reusedDictionary); // Dictionary should be cleared
            Assert.Same(dictionary, reusedDictionary);
        }

        [Fact]
        public void Clear_ShouldEmptyPool()
        {
            // Arrange
            var pool = new SwiftDictionaryPool<int, string>();
            var dictionary = pool.Rent();
            pool.Release(dictionary);

            // Act
            pool.Clear();

            // Assert
            var newDictionary = pool.Rent();
            Assert.NotSame(dictionary, newDictionary); // Cleared pool creates a new dictionary
        }
    }
}
