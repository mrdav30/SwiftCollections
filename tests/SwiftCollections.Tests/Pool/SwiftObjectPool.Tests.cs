using System;
using System.Collections.Generic;
using Xunit;

namespace SwiftCollections.Pool.Tests
{
    public class SwiftObjectPoolTests
    {
        [Fact]
        public void ClearAccounting_OutstandingRentalCanReturnAndBeReused()
        {
            using var pool = new SwiftObjectPool<object>(() => new object());
            object rented = pool.Rent();

            pool.Clear();

            Assert.Equal(1, pool.CountAll);
            Assert.Equal(1, pool.CountActive);
            Assert.Equal(0, pool.CountInactive);
            pool.Release(rented);
            Assert.Equal(1, pool.CountAll);
            Assert.Equal(0, pool.CountActive);
            Assert.Equal(1, pool.CountInactive);
            Assert.Same(rented, pool.Rent());
            Assert.Equal(1, pool.CountAll);
            Assert.Equal(1, pool.CountActive);
            Assert.Equal(0, pool.CountInactive);
            pool.Release(rented);
        }

        [Fact]
        public void ClearAccounting_DestroysOnlyInactiveObjectsAndPreservesOutstandingOwners()
        {
            var destroyed = new List<object>();
            using var pool = new SwiftObjectPool<object>(() => new object(), actionOnDestroy: destroyed.Add);
            object inactive = pool.Rent();
            object firstActive = pool.Rent();
            object secondActive = pool.Rent();
            pool.Release(inactive);

            pool.Clear();
            pool.Clear();

            Assert.Equal(new[] { inactive }, destroyed);
            Assert.Equal(2, pool.CountAll);
            Assert.Equal(2, pool.CountActive);
            Assert.Equal(0, pool.CountInactive);
            pool.Release(new[] { firstActive, secondActive });
            Assert.Equal(2, pool.CountAll);
            Assert.Equal(0, pool.CountActive);
            Assert.Equal(2, pool.CountInactive);
            Assert.Same(secondActive, pool.Rent());
            Assert.Same(firstActive, pool.Rent());
            pool.Release(new[] { firstActive, secondActive });
        }

        [Fact]
        public void ClearAccounting_NewRentAfterClearRetainsPreviousRentalInCounts()
        {
            using var pool = new SwiftObjectPool<object>(() => new object());
            object previous = pool.Rent();
            pool.Clear();

            object current = pool.Rent();

            Assert.NotSame(previous, current);
            Assert.Equal(2, pool.CountAll);
            Assert.Equal(2, pool.CountActive);
            Assert.Equal(0, pool.CountInactive);
            pool.Release(new[] { previous, current });
            Assert.Equal(2, pool.CountAll);
            Assert.Equal(0, pool.CountActive);
            Assert.Equal(2, pool.CountInactive);
            pool.Clear();
            Assert.Equal(0, pool.CountAll);
            Assert.Equal(0, pool.CountActive);
            Assert.Equal(0, pool.CountInactive);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ClearAccounting_OverflowAfterClearDeductsOnlyDiscardedReturns(bool hasDestroyCallback)
        {
            var destroyed = new List<object>();
            using var pool = new SwiftObjectPool<object>(
                () => new object(), actionOnDestroy: hasDestroyCallback ? destroyed.Add : null, maxSize: 1);
            object retained = pool.Rent();
            object firstOverflow = pool.Rent();
            object secondOverflow = pool.Rent();
            pool.Clear();
            pool.Release(retained);

            pool.Release(firstOverflow);

            Assert.Equal(2, pool.CountAll);
            Assert.Equal(1, pool.CountActive);
            Assert.Equal(1, pool.CountInactive);
            pool.Release(secondOverflow);
            Assert.Equal(1, pool.CountAll);
            Assert.Equal(0, pool.CountActive);
            Assert.Equal(1, pool.CountInactive);
            Assert.Equal(hasDestroyCallback ? new[] { firstOverflow, secondOverflow } : Array.Empty<object>(), destroyed);
            Assert.Same(retained, pool.Rent());
            pool.Release(retained);
        }

        [Fact]
        public void ClearAccounting_DestroyCallbackObservesUpdatedCounts()
        {
            var observed = new List<(int All, int Inactive, int Active)>();
            SwiftObjectPool<object> pool = null;
            using (pool = new SwiftObjectPool<object>(() => new object(), actionOnDestroy: _ =>
                observed.Add((pool.CountAll, pool.CountInactive, pool.CountActive))))
            {
                object active = pool.Rent();
                object firstInactive = pool.Rent();
                object secondInactive = pool.Rent();
                pool.Release(new[] { firstInactive, secondInactive });

                pool.Clear();

                Assert.Equal(new[] { (2, 1, 1), (1, 0, 1) }, observed);
                pool.Release(active);
            }
        }

        [Fact]
        public void ClearAccounting_ThrowingDestroyDeductsPoppedObjectAndLeavesRemainingOwnersCounted()
        {
            var attempted = new List<object>();
            var failure = new ArithmeticException("Destroy failed.");
            using var pool = new SwiftObjectPool<object>(() => new object(), actionOnDestroy: value =>
            {
                attempted.Add(value);
                if (attempted.Count == 1)
                    throw failure;
            });
            object active = pool.Rent();
            object remaining = pool.Rent();
            object popped = pool.Rent();
            pool.Release(remaining);
            pool.Release(popped);

            Assert.Same(failure, Assert.Throws<ArithmeticException>(() => pool.Clear()));

            Assert.Equal(new[] { popped }, attempted);
            Assert.Equal(2, pool.CountAll);
            Assert.Equal(1, pool.CountActive);
            Assert.Equal(1, pool.CountInactive);
            pool.Clear();
            Assert.Equal(new[] { popped, remaining }, attempted);
            Assert.Equal(1, pool.CountAll);
            Assert.Equal(1, pool.CountActive);
            Assert.Equal(0, pool.CountInactive);
            pool.Release(active);
            pool.Clear();
            Assert.Equal(new[] { popped, remaining, active }, attempted);
            Assert.Equal(0, pool.CountAll);
            Assert.Equal(0, pool.CountActive);
            Assert.Equal(0, pool.CountInactive);
        }

        [Fact]
        public void ClearAccounting_LeaseReturnAfterClearPreservesOutstandingCount()
        {
            using var pool = new SwiftObjectPool<object>(() => new object());
            using SwiftPooledObject<object> lease = pool.Rent(out object rented);

            pool.Clear();
            lease.Dispose();
            lease.Dispose();

            Assert.Equal(1, pool.CountAll);
            Assert.Equal(0, pool.CountActive);
            Assert.Equal(1, pool.CountInactive);
            Assert.Same(rented, pool.Rent());
            pool.Release(rented);
        }

        [Fact]
        public void ClearAccounting_CollectionWrapperPreservesRentalAndResetsOnReturn()
        {
            using var wrapper = new SwiftListPool<int>();
            SwiftList<int> rented = wrapper.Rent();
            rented.Add(42);

            wrapper.Clear();

            Assert.Equal(new[] { 42 }, rented);
            Assert.Equal(1, wrapper.CollectionPool.CountAll);
            Assert.Equal(1, wrapper.CollectionPool.CountActive);
            wrapper.Release(rented);
            Assert.Empty(rented);
            Assert.Equal(1, wrapper.CollectionPool.CountAll);
            Assert.Equal(0, wrapper.CollectionPool.CountActive);
            Assert.Equal(1, wrapper.CollectionPool.CountInactive);
            Assert.Same(rented, wrapper.Rent());
            wrapper.Release(rented);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(2)]
        public void ClearAccounting_EmptyOrFullyInactivePoolRemainsReusable(int inactiveCount)
        {
            using var pool = new SwiftObjectPool<object>(() => new object());
            var items = new object[inactiveCount];
            for (int i = 0; i < items.Length; i++)
                items[i] = pool.Rent();
            pool.Release(items);

            pool.Clear();
            pool.Clear();

            Assert.Equal(0, pool.CountAll);
            Assert.Equal(0, pool.CountActive);
            Assert.Equal(0, pool.CountInactive);
            object next = pool.Rent();
            Assert.DoesNotContain(next, items);
            Assert.Equal(1, pool.CountAll);
            Assert.Equal(1, pool.CountActive);
            Assert.Equal(0, pool.CountInactive);
            pool.Release(next);
        }

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
