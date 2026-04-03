using System.Threading;
using Xunit;

namespace SwiftCollections.Tests;

public class SwiftLazyDisposableTests
{
    [Fact]
    public void ParameterlessConstructors_RemainLazyUntilValueIsRequested()
    {
        var defaultLazy = new SwiftLazyDisposable<DisposableSpy>();
        var threadSafeLazy = new SwiftLazyDisposable<DisposableSpy>(isThreadSafe: true);
        var modeLazy = new SwiftLazyDisposable<DisposableSpy>(LazyThreadSafetyMode.ExecutionAndPublication);

        Assert.False(defaultLazy.IsValueCreated);
        Assert.False(threadSafeLazy.IsValueCreated);
        Assert.False(modeLazy.IsValueCreated);
        Assert.Equal("LazyDisposable (Not Created)", defaultLazy.ToString());

        DisposableSpy defaultValue = defaultLazy.Value;
        DisposableSpy threadSafeValue = threadSafeLazy.Value;
        DisposableSpy modeValue = modeLazy.Value;

        Assert.True(defaultLazy.IsValueCreated);
        Assert.True(threadSafeLazy.IsValueCreated);
        Assert.True(modeLazy.IsValueCreated);
        Assert.Same(defaultValue, defaultLazy.Value);
        Assert.Same(threadSafeValue, threadSafeLazy.Value);
        Assert.Same(modeValue, modeLazy.Value);
        Assert.Equal(defaultValue.ToString(), defaultLazy.ToString());
    }

    [Fact]
    public void FactoryConstructors_InvokeFactoryOnlyWhenNeeded()
    {
        int created = 0;
        var lazy = new SwiftLazyDisposable<DisposableSpy>(() => CreateSpy(ref created, "factory"));
        var threadSafeLazy = new SwiftLazyDisposable<DisposableSpy>(() => CreateSpy(ref created, "threadSafe"), isThreadSafe: false);
        var modeLazy = new SwiftLazyDisposable<DisposableSpy>(() => CreateSpy(ref created, "mode"), LazyThreadSafetyMode.PublicationOnly);

        Assert.Equal(0, created);

        _ = lazy.Value;
        _ = threadSafeLazy.Value;
        _ = modeLazy.Value;

        Assert.Equal(3, created);
        Assert.Equal("DisposableSpy:factory:0", lazy.ToString());
    }

    [Fact]
    public void Dispose_OnlyDisposesCreatedValueOnce()
    {
        var notCreated = new SwiftLazyDisposable<DisposableSpy>(() => new DisposableSpy());
        var created = new SwiftLazyDisposable<DisposableSpy>(() => new DisposableSpy { Name = "created" });

        DisposableSpy value = created.Value;

        notCreated.Dispose();
        created.Dispose();
        created.Dispose();

        Assert.False(notCreated.IsValueCreated);
        Assert.Equal(1, value.DisposeCount);
    }

    private static DisposableSpy CreateSpy(ref int created, string name)
    {
        created++;
        return new DisposableSpy { Name = name };
    }
}
