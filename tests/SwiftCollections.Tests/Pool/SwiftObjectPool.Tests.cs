using Xunit;
using System;

namespace SwiftCollections.Pool.Tests
{
    public class SwiftObjectPoolTests
    {
        [Fact]
        public void Rent_ShouldReturnNewObjectWhenPoolIsEmpty()
        {
            // Arrange
            var pool = new SwiftObjectPool<string>(() => "New Object");

            // Act
            var obj = pool.Rent();

            // Assert
            Assert.Equal("New Object", obj);
        }

        [Fact]
        public void Rent_ShouldReuseReleasedObject()
        {
            // Arrange
            var pool = new SwiftObjectPool<string>(() => "New Object");
            var obj = pool.Rent();
            pool.Release(obj);

            // Act
            var reusedObj = pool.Rent();

            // Assert
            Assert.Same(obj, reusedObj);
        }

        [Fact]
        public void Release_ShouldInvokeActionOnRelease()
        {
            // Arrange
            bool actionCalled = false;
            var pool = new SwiftObjectPool<string>(() => "New Object", actionOnRelease: _ => actionCalled = true);

            var obj = pool.Rent();

            // Act
            pool.Release(obj);

            // Assert
            Assert.True(actionCalled);
        }

        [Fact]
        public void Clear_ShouldInvokeActionOnDestroy()
        {
            // Arrange
            bool actionCalled = false;
            var pool = new SwiftObjectPool<string>(() => "New Object", actionOnDestroy: _ => actionCalled = true);

            var obj = pool.Rent();
            pool.Release(obj);

            // Act
            pool.Clear();

            // Assert
            Assert.True(actionCalled);
        }

        [Fact]
        public void Rent_ShouldThrowWhenCreateFuncReturnsNull()
        {
            // Arrange
            var pool = new SwiftObjectPool<string>(() => null);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => pool.Rent());
        }
    }
}