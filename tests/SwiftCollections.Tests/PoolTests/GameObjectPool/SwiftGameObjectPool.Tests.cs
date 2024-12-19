#if UNITY_EDITOR

using Xunit;
using UnityEngine;

namespace SwiftCollections.Pool.Tests
{
    public class SwiftGameObjectPoolTests
    {
        [Fact]
        public void GetObject_ShouldInstantiateNewObject_WhenPoolIsEmpty()
        {
            // Arrange
            var prefab = new GameObject("TestPrefab");
            var pool = new SwiftGameObjectPool(prefab, 10);

            // Act
            var obj = pool.GetObject(null);

            // Assert
            Assert.NotNull(obj);
            Assert.Equal("TestPrefab(Clone)", obj.name);
            Assert.False(obj.activeSelf); // Should be inactive by default
        }

        [Fact]
        public void GetObject_ShouldReuseObjects_WhenPoolIsFull()
        {
            // Arrange
            var prefab = new GameObject("TestPrefab");
            var pool = new SwiftGameObjectPool(prefab, 1);

            // Act
            var firstObject = pool.GetObject(null);
            var secondObject = pool.GetObject(null);

            // Assert
            Assert.Same(firstObject, secondObject); // Should reuse the same object
        }

        [Fact]
        public void PrewarmObject_ShouldInstantiateObjectsUpToBudget()
        {
            // Arrange
            var prefab = new GameObject("TestPrefab");
            var pool = new SwiftGameObjectPool(prefab, 5, true);

            // Act
            pool.PrewarmObject(null);

            // Assert
            Assert.Equal(5, pool.CreatedObjects.Count); // Should prewarm up to the budget
        }

        [Fact]
        public void Dispose_ShouldDestroyAllObjects()
        {
            // Arrange
            var prefab = new GameObject("TestPrefab");
            var pool = new SwiftGameObjectPool(prefab, 2);

            var obj1 = pool.GetObject(null);
            var obj2 = pool.GetObject(null);

            // Act
            pool.Dispose();

            // Assert
            Assert.True(obj1 == null); // Objects should be destroyed
            Assert.True(obj2 == null);
        }
    }
}
#endif