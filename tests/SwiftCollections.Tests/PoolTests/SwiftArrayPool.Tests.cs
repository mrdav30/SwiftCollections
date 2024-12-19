using System;
using Xunit;

namespace SwiftCollections.Pool.Tests
{
    public class SwiftArrayPoolTests
    {
        [Fact]
        public void Rent_ShouldReturnArrayOfSpecifiedSize()
        {
            // Arrange
            var pool = new SwiftArrayPool<int>();

            // Act
            var array = pool.Rent(10);

            // Assert
            Assert.NotNull(array);
            Assert.Equal(10, array.Length);
        }

        [Fact]
        public void Release_ShouldReturnArrayToPool()
        {
            // Arrange
            var pool = new SwiftArrayPool<int>();
            var array = pool.Rent(5);

            // Act
            pool.Release(array);
            var reusedArray = pool.Rent(5);

            // Assert
            Assert.Same(array, reusedArray);
        }

        [Fact]
        public void Clear_ShouldEmptyAllPools()
        {
            // Arrange
            var pool = new SwiftArrayPool<int>();
            var array1 = pool.Rent(5);
            var array2 = pool.Rent(10);

            pool.Release(array1);
            pool.Release(array2);

            // Act
            pool.Clear();

            // Assert
            var newArray1 = pool.Rent(5); // Should create a new pool and new array
            Assert.NotSame(array1, newArray1);
            Assert.Equal(5, newArray1.Length);

            var newArray2 = pool.Rent(10); // Should create a new pool and new array
            Assert.NotSame(array2, newArray2);
            Assert.Equal(10, newArray2.Length);
        }

        [Fact]
        public void Dispose_ShouldPreventFurtherOperations()
        {
            // Arrange
            var pool = new SwiftArrayPool<int>();
            var array = pool.Rent(5);

            // Act
            pool.Dispose();

            // Assert
            Assert.Throws<ObjectDisposedException>(() => pool.Rent(5));
            Assert.Throws<ObjectDisposedException>(() => pool.Release(array));
        }

        [Fact]
        public void Rent_ShouldThrowException_ForInvalidSize()
        {
            // Arrange
            var pool = new SwiftArrayPool<int>();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => pool.Rent(0));
            Assert.Throws<ArgumentException>(() => pool.Rent(-1));
        }

        [Fact]
        public void Release_ShouldHandleNullOrEmptyArrayGracefully()
        {
            // Arrange
            var pool = new SwiftArrayPool<int>();

            // Act & Assert
            pool.Release(null);
            pool.Release(new int[0]); // Empty array
        }

        [Fact]
        public void Rent_ShouldCreateNewArray_WhenPoolIsEmpty()
        {
            // Arrange
            var pool = new SwiftArrayPool<int>();

            // Act
            var array1 = pool.Rent(5);
            pool.Release(array1);
            var array2 = pool.Rent(5);

            // Assert
            Assert.Same(array1, array2); // The same array should be reused
        }

        [Fact]
        public void Dispose_ShouldClearPoolAndPreventFurtherOperations()
        {
            // Arrange
            var pool = new SwiftArrayPool<int>();
            var array = pool.Rent(10);
            pool.Release(array);

            // Act
            pool.Dispose();

            // Assert
            Assert.Throws<ObjectDisposedException>(() => pool.Rent(10));
            Assert.Throws<ObjectDisposedException>(() => pool.Release(array));
        }

        [Fact]
        public void Rent_ShouldInvokeCustomCreateFunc()
        {
            // Arrange
            var pool = new SwiftArrayPool<int>(
                createFunc: size => new int[size + 1]
            );

            // Act
            var array = pool.Rent(5);

            // Assert
            Assert.NotNull(array);
            Assert.Equal(6, array.Length);
        }

        [Fact]
        public void Release_ShouldInvokeActionOnRelease()
        {
            // Arrange
            bool onReleaseCalled = false;
            var pool = new SwiftArrayPool<int>(
                actionOnRelease: array => onReleaseCalled = true
            );

            var array = pool.Rent(5);

            // Act
            pool.Release(array);

            // Assert
            Assert.True(onReleaseCalled);
        }

        [Fact]
        public void Clear_ShouldInvokeActionOnDestroy()
        {
            // Arrange
            bool onDestroyCalled = false;
            var pool = new SwiftArrayPool<int>(
                actionOnDestroy: array => onDestroyCalled = true
            );

            var array = pool.Rent(5);
            pool.Release(array);

            // Act
            pool.Clear();

            // Assert
            Assert.True(onDestroyCalled);
        }
    }

}