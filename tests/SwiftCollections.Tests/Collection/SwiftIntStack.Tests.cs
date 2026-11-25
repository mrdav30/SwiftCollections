using Xunit;

namespace SwiftCollections.Tests;

public class SwiftIntStackTests
{
    [Fact]
    public void Clear_EmptiesTheStackAndReusesItsStorage()
    {
        var stack = new SwiftIntStack(2);
        stack.Push(10);
        stack.Push(20);
        int[] storage = stack.Array;

        stack.Clear();

        Assert.Equal(0, stack.Count);
        Assert.Same(storage, stack.Array);
        Assert.Equal(2, stack.Array.Length);
        stack.Push(30);
        Assert.Equal(30, stack.Peek());
        Assert.Equal(30, stack.Pop());
        Assert.Equal(0, stack.Count);
        Assert.Same(storage, stack.Array);
    }

    [Fact]
    public void Constructor_WithArrayAndPeek_UsesProvidedState()
    {
        var stack = new SwiftIntStack(new[] { 1, 2, 3 }, 3);

        Assert.Equal(3, stack.Count);
        Assert.Equal(3, stack.Peek());
    }

    [Fact]
    public void EnsureCapacity_GrowsAndNoOpsWhenCapacityAlreadyExists()
    {
        var stack = new SwiftIntStack(2);

        stack.EnsureCapacity(2);

        Assert.Equal(2, stack.Array.Length);

        stack.EnsureCapacity(5);

        Assert.True(stack.Array.Length >= 5);

        var nonPowerOfTwo = new SwiftIntStack(10);

        nonPowerOfTwo.EnsureCapacity(11);

        Assert.Equal(20, nonPowerOfTwo.Array.Length);
    }
}
