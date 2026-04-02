using System;
using System.Threading;

namespace SwiftCollections.Pool;

/// <summary>
/// A specialized pool for managing <see cref="SwiftSparseMap{T}"/> instances,
/// providing efficient reuse to minimize memory allocations and improve performance.
/// </summary>
/// <typeparam name="T">The type of values stored in the sparse map.</typeparam>
public sealed class SwiftSparseMapPool<T> : IDisposable
{
    #region Singleton Instance

    /// <summary>
    /// Shared instance of the sparse map pool, providing a globally accessible pool.
    /// Uses <see cref="SwiftLazyDisposable{T}"/> to ensure lazy initialization and proper disposal.
    /// </summary>
    private readonly static SwiftLazyDisposable<SwiftSparseMapPool<T>> _lazyInstance =
        new(() => new SwiftSparseMapPool<T>(), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Gets the shared instance of the pool.
    /// </summary>
    public static SwiftSparseMapPool<T> Shared => _lazyInstance.Value;

    #endregion

    #region Fields

    private SwiftLazyDisposable<SwiftObjectPool<SwiftSparseMap<T>>> _lazyCollectionPool =
        new(() =>
        {
            return new SwiftObjectPool<SwiftSparseMap<T>>(
                createFunc: () => new SwiftSparseMap<T>(),
                actionOnRelease: map => map.Clear()
            );
        });

    /// <summary>
    /// Tracks whether the pool has been disposed.
    /// </summary>
    private volatile bool _disposed;

    #endregion

    #region Properties

    /// <summary>
    /// Gets the underlying object pool used to manage sparse map instances.
    /// </summary>
    public SwiftObjectPool<SwiftSparseMap<T>> CollectionPool => _lazyCollectionPool.Value;

    #endregion

    #region Methods

    /// <summary>
    /// Rents a sparse map instance from the pool. If the pool is empty, a new sparse map is created.
    /// </summary>
    /// <returns>A <see cref="SwiftSparseMap{T}"/> instance.</returns>
    public SwiftSparseMap<T> Rent()
    {
        SwiftThrowHelper.ThrowIfDisposed(_disposed, nameof(SwiftSparseMapPool<T>));

        return CollectionPool.Rent();
    }

    /// <summary>
    /// Rents a sparse map from the pool and wraps it in a <see cref="SwiftPooledObject{T}"/> for automatic release.
    /// </summary>
    /// <param name="value">The rented sparse map.</param>
    /// <returns>A <see cref="SwiftPooledObject{T}"/> instance wrapping the rented sparse map.</returns>
    public SwiftPooledObject<SwiftSparseMap<T>> Get(out SwiftSparseMap<T> value)
    {
        SwiftThrowHelper.ThrowIfDisposed(_disposed, nameof(SwiftSparseMapPool<T>));

        return CollectionPool.Rent(out value);
    }

    /// <summary>
    /// Releases a sparse map instance back to the pool for reuse.
    /// </summary>
    /// <param name="map">The sparse map to release.</param>
    public void Release(SwiftSparseMap<T> map)
    {
        SwiftThrowHelper.ThrowIfDisposed(_disposed, nameof(SwiftSparseMapPool<T>));

        if (map == null) return;

        CollectionPool.Release(map);
    }

    /// <summary>
    /// Clears all pooled sparse maps from the pool.
    /// </summary>
    public void Clear()
    {
        if (_disposed || !_lazyCollectionPool.IsValueCreated)
            return;

        _lazyCollectionPool.Value.Clear();
    }

    #endregion

    #region IDisposable Implementation

    /// <summary>
    /// Releases all resources used by the SwiftSparseMapPool.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        Clear();

        if (_lazyCollectionPool.IsValueCreated)
            _lazyCollectionPool.Value.Dispose();

        _disposed = true;

        GC.SuppressFinalize(this);
    }

    ~SwiftSparseMapPool() => Dispose();

    #endregion
}
