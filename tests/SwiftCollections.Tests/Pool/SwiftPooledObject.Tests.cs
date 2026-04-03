using System;
using Xunit;

namespace SwiftCollections.Pool.Tests;

public class SwiftPooledObjectTests
{
    [Fact]
    public void Constructor_ThrowsForNullArguments()
    {
        var trackingPool = new TrackingPool();

        Assert.Throws<ArgumentNullException>(() => new SwiftPooledObject<SwiftCollections.Tests.DisposableSpy>(null, trackingPool));
        Assert.Throws<ArgumentNullException>(() => new SwiftPooledObject<SwiftCollections.Tests.DisposableSpy>(new SwiftCollections.Tests.DisposableSpy(), null));
    }

    [Fact]
    public void Dispose_ReleasesWrappedValue()
    {
        var trackingPool = new TrackingPool();
        var value = new SwiftCollections.Tests.DisposableSpy();
        var pooled = new SwiftPooledObject<SwiftCollections.Tests.DisposableSpy>(value, trackingPool);

        pooled.Dispose();

        Assert.Equal(1, trackingPool.ReleaseCount);
        Assert.Same(value, trackingPool.LastReleased);
    }

    [Fact]
    public void Dispose_IsIdempotentAcrossReferenceCopies()
    {
        var trackingPool = new TrackingPool();
        var value = new SwiftCollections.Tests.DisposableSpy();
        var pooled = new SwiftPooledObject<SwiftCollections.Tests.DisposableSpy>(value, trackingPool);
        SwiftPooledObject<SwiftCollections.Tests.DisposableSpy> alias = pooled;

        pooled.Dispose();
        alias.Dispose();

        Assert.Equal(1, trackingPool.ReleaseCount);
        Assert.Same(value, trackingPool.LastReleased);
    }

    private sealed class TrackingPool : ISwiftObjectPool<SwiftCollections.Tests.DisposableSpy>
    {
        public int CountInactive => 0;

        public SwiftCollections.Tests.DisposableSpy LastReleased { get; private set; }

        public int ReleaseCount { get; private set; }

        public void Clear()
        {
        }

        public SwiftCollections.Tests.DisposableSpy Rent() => new SwiftCollections.Tests.DisposableSpy();

        public SwiftPooledObject<SwiftCollections.Tests.DisposableSpy> Rent(out SwiftCollections.Tests.DisposableSpy v)
        {
            v = Rent();
            return new SwiftPooledObject<SwiftCollections.Tests.DisposableSpy>(v, this);
        }

        public void Release(SwiftCollections.Tests.DisposableSpy element)
        {
            ReleaseCount++;
            LastReleased = element;
        }
    }
}
