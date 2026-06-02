using Xunit;

namespace SwiftCollections.Tests;

public class SwiftIntStackTests
{
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
