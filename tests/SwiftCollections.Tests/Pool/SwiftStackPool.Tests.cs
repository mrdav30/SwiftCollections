using Xunit;

namespace SwiftCollections.Pool.Tests
{
    public class SwiftStackPoolTests
    {
        [Fact]
        public void Rent_ShouldReturnStackInstance()
        {
            var pool = new SwiftStackPool<int>();

            var stack = pool.Rent();

            Assert.NotNull(stack);
            Assert.Empty(stack);
        }

        [Fact]
        public void Release_ShouldClearStackAndReturnToPool()
        {
            var pool = new SwiftStackPool<int>();
            var stack = pool.Rent();
            stack.Push(1);

            pool.Release(stack);
            var reusedStack = pool.Rent();

            Assert.Empty(reusedStack);
            Assert.Same(stack, reusedStack);
        }

        [Fact]
        public void Clear_ShouldEmptyPool()
        {
            var pool = new SwiftStackPool<int>();
            var stack = pool.Rent();
            pool.Release(stack);

            pool.Clear();

            var newStack = pool.Rent();
            Assert.NotSame(stack, newStack);
        }
    }
}
