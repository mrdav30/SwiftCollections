using SwiftCollections.Utility;
using System;
using Xunit;

namespace SwiftCollections.Tests;

public class SwiftThrowHelperTests
{
    [Fact]
    public void ThrowIfNullAndNullsAreIllegal_OnlyThrowsForNonNullableDefaults()
    {
        SwiftThrowHelper.ThrowIfNullAndNullsAreIllegal("value", default(int));
        SwiftThrowHelper.ThrowIfNullAndNullsAreIllegal(null, default(string));
        SwiftThrowHelper.ThrowIfNullAndNullsAreIllegal(null, default(int?));

        Assert.Throws<ArgumentNullException>(() =>
            SwiftThrowHelper.ThrowIfNullAndNullsAreIllegal(null, default(int), "Required value."));
    }

    [Fact]
    public void ThrowIfTrue_WithoutMessage_UsesObjectNameInInvalidOperationMessage()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            SwiftThrowHelper.ThrowIfTrue(true, "Pool"));

        Assert.Contains("Pool", exception.Message);
    }
}
