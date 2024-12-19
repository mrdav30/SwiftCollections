#if UNITY_EDITOR

using Xunit;
using UnityEngine;

namespace SwiftCollections.Pool.Tests
{
    public class SwiftGameObjectPoolAssetTests
    {
        [Fact]
        public void Init_ShouldCreateParentTransform_AndInitializePools()
        {
            // Arrange
            var prefab = new GameObject("TestPrefab");
            var pool = new SwiftGameObjectPool("TestPrefabPool", prefab, 5, true);

            var asset = new SwiftGameObjectPoolAsset(new[] { pool });
 
            // Act
            asset.Init();

            // Assert
            Assert.NotNull(asset.ParentTransform); // Parent transform should be created
            Assert.Equal(5, pool.CreatedObjects.Count); // Objects should be prewarmed
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
            var obj = asset.GetObject("TestPool");

            // Assert
            Assert.NotNull(obj);
            Assert.Equal("TestPrefab(Clone)", obj.name);
        }

        [Fact]
        public void Dispose_ShouldDestroyAllPools_AndParentTransform()
        {
            // Arrange
            var prefab = new GameObject("TestPrefab");
            var pool = new SwiftGameObjectPool("TestPool", prefab, 5, true);

            var asset = new SwiftGameObjectPoolAsset(new[] { pool });
            asset.Init();

            // Act
            asset.Dispose();

            // Assert
            Assert.True(asset.ParentTransform == null); // Parent transform should be destroyed
        }
    }

}
#endif