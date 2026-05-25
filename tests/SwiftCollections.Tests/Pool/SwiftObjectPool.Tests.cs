using System;
using Xunit;

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

        [Fact]
        public void RentAndRelease_InvokeGetAndDestroyCallbacksAcrossReuseAndOverflow()
        {
            int getCount = 0;
            int destroyCount = 0;
            var pool = new SwiftObjectPool<SwiftCollections.Tests.DisposableSpy>(
                () => new SwiftCollections.Tests.DisposableSpy(),
                actionOnGet: _ => getCount++,
                actionOnDestroy: _ => destroyCount++,
                maxSize: 1);

            var first = pool.Rent();
            pool.Release(first);
            var reused = pool.Rent();
            var overflow = pool.Rent();

            pool.Release(reused);
            pool.Release(overflow);

            Assert.Equal(3, getCount);
            Assert.Equal(1, destroyCount);
        }

        [Fact]
        public void Release_WhenFullWithoutDestroyCallback_DropsOverflowObject()
        {
            var pool = new SwiftObjectPool<SwiftCollections.Tests.DisposableSpy>(
                () => new SwiftCollections.Tests.DisposableSpy(),
                maxSize: 1);

            var first = pool.Rent();
            var overflow = pool.Rent();

            pool.Release(first);
            pool.Release(overflow);

            Assert.Equal(1, pool.CountInactive);
        }
    }
}
