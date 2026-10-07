using Xunit;

namespace SwiftCollections.Tests.Support;

// A second GC could reclaim the long weak references before finalizer effects are inspected.
[CollectionDefinition("PoolFinalization", DisableParallelization = true)]
public sealed class PoolFinalizationCollection
{
}
