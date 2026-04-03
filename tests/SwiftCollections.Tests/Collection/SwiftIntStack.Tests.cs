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
}
