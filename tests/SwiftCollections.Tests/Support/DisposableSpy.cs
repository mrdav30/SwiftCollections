using System;

namespace SwiftCollections.Tests;

public sealed class DisposableSpy : IDisposable
{
    public int DisposeCount { get; private set; }

    public string Name { get; set; } = "DisposableSpy";

    public void Dispose() => DisposeCount++;

    public override string ToString() => $"DisposableSpy:{Name}:{DisposeCount}";
}
