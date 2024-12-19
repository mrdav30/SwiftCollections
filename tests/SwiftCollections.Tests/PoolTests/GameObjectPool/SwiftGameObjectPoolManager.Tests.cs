#if UNITY_EDITOR

using Xunit;
using UnityEngine;

namespace SwiftCollections.Pool.Tests
{
    public class SwiftGameObjectPoolManagerTests
    {
        [Fact]
        public void Shared_ShouldReturnSingletonInstance()
        {
            // Act
            var manager1 = SwiftGameObjectPoolManager.Shared;
            var manager2 = SwiftGameObjectPoolManager.Shared;

            // Assert
            Assert.Same(manager1, manager2); // Singleton instance
        }

        [Fact]
        public void GetObject_ShouldReturnObject_FromSpecifiedPool()
        {
            // Arrange
            var prefab = new GameObject("TestPrefab");
            var pool = new SwiftGameObjectPool("TestPool", prefab, 5, true);

            var asset = new SwiftGameObjectPoolAsset(new[] { pool });
            asset.Init();

            // Act
            var obj = SwiftGameObjectPoolManager.Shared.GetObject("TestPool");

            // Assert
            Assert.NotNull(obj);
            Assert.Equal("TestPrefab(Clone)", obj.name);
        }
    }
}
#endif