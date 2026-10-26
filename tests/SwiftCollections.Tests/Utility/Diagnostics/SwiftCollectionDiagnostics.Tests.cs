using SwiftCollections.Diagnostics;
using Xunit;

namespace SwiftCollections.Tests;

public class SwiftCollectionDiagnosticsTests
{
    [Fact]
    public void Shared_ReturnsSingletonSwiftCollectionsChannel()
    {
        Assert.Same(SwiftCollectionDiagnostics.Shared, SwiftCollectionDiagnostics.Shared);
        Assert.Equal("SwiftCollections", SwiftCollectionDiagnostics.Shared.Name);
    }
}
