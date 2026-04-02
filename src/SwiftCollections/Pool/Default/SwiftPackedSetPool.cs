using System;
using System.Threading;

namespace SwiftCollections.Pool;

/// <summary>
/// A specialized pool for managing <see cref="SwiftPackedSet{T}"/> instances,
/// providing efficient reuse to minimize memory allocations and improve performance.
/// </summary>
/// <typeparam name="T">The type of elements in the packed set.</typeparam>
public sealed class SwiftPackedSetPool<T> : SwiftCollectionPool<SwiftPackedSet<T>, T>, IDisposable
{
    #region Singleton Instance

    /// <summary>
    /// Shared instance of the packed set pool, providing a globally accessible pool.
    /// Uses <see cref="SwiftLazyDisposable{T}"/> to ensure lazy initialization and proper disposal.
    /// </summary>
    private readonly static SwiftLazyDisposable<SwiftPackedSetPool<T>> _lazyInstance =
        new(() => new SwiftPackedSetPool<T>(), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Gets the shared instance of the pool.
    /// </summary>
    public static SwiftPackedSetPool<T> Shared => _lazyInstance.Value;

    #endregion

    #region Fields

    /// <summary>
    /// Tracks whether the pool has been disposed.
    /// </summary>
    private volatile bool _disposed;

    #endregion

    #region Methods

    /// <summary>
    /// Rents a packed set instance from the pool. If the pool is empty, a new packed set is created.
    /// </summary>
    /// <returns>A <see cref="SwiftPackedSet{T}"/> instance.</returns>
    public override SwiftPackedSet<T> Rent()
    {
        SwiftThrowHelper.ThrowIfDisposed(_disposed, nameof(SwiftPackedSetPool<T>));

        return base.Rent();
    }

    /// <summary>
    /// Releases a packed set instance back to the pool for reuse.
    /// </summary>
    /// <param name="set">The packed set to release.</param>
    public override void Release(SwiftPackedSet<T> set)
    {
        SwiftThrowHelper.ThrowIfDisposed(_disposed, nameof(SwiftPackedSetPool<T>));

        if (set == null) return;

        base.Release(set);
    }

    /// <summary>
    /// Clears all pooled packed sets from the pool.
    /// </summary>
    public override void Clear()
    {
        if (_disposed) return;

        base.Clear();
    }

    #endregion

    #region IDisposable Implementation

    /// <summary>
    /// Releases all resources used by the SwiftPackedSetPool.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        Clear();
        base.Flush();

        _disposed = true;

        GC.SuppressFinalize(this);
    }

    ~SwiftPackedSetPool() => Dispose();

    #endregion
}
