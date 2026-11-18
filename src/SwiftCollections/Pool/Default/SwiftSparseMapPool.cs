//=======================================================================
// SwiftSparseMapPool.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Threading;
using Chronicler.Diagnostics;
using SwiftCollections.Lazy;

namespace SwiftCollections.Pool;

/// <summary>
/// A specialized pool for managing <see cref="T:SwiftCollections.SwiftSparseMap`1" /> instances, providing
/// efficient reuse to minimize memory allocations and improve performance.
/// </summary>
/// <typeparam name="T">The type of values stored in the sparse map.</typeparam>
/// <remarks>
/// Rented objects have exclusive caller ownership until returned exactly once. Foreign objects and double returns
/// are not detected and can break ownership/counts. Callback exceptions propagate; operations are not
/// transactional. Use external synchronization for lifecycle changes and host callbacks.
/// </remarks>
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
    /// <value>The process-wide lazily created pool for this closed generic type.</value>
    /// <remarks>
    /// The shared instance is reused and is not recreated after disposal. Prefer Clear for reusable cleanup;
    /// disposing Shared affects every consumer.
    /// </remarks>
    public static SwiftSparseMapPool<T> Shared => _lazyInstance.Value;

    #endregion

    #region Fields

    private readonly SwiftLazyDisposable<SwiftObjectPool<SwiftSparseMap<T>>> _lazyCollectionPool =
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
    /// <value>The lazily created backing object pool; accessing this property can allocate it.</value>
    /// <remarks>
    /// The returned pool is owned by this wrapper. Retaining it across Flush or Dispose can leave a disposed
    /// backing pool. Direct access bypasses wrapper lifecycle checks.
    /// </remarks>
    public SwiftObjectPool<SwiftSparseMap<T>> CollectionPool => _lazyCollectionPool.Value;

    #endregion

    #region Methods

    /// <summary>
    /// Rents a sparse map instance from the pool. If the pool is empty, a new sparse map is created.
    /// </summary>
    /// <returns>A cached or newly created object for exclusive caller use; return it once to this pool.</returns>
    /// <exception cref="T:System.ObjectDisposedException">This pool has been disposed.</exception>
    /// <remarks>
    /// Renting can invoke the factory and get callback. Collection wrappers ordinarily clear contents when
    /// released, preserving capacity.
    /// </remarks>
    public SwiftSparseMap<T> Rent()
    {
        ChronicleGuard.ThrowIfDisposed(_disposed, nameof(SwiftSparseMapPool<T>));

        return CollectionPool.Rent();
    }

    /// <summary>
    /// Rents a sparse map from the pool and wraps it in a <see cref="T:SwiftCollections.Pool.SwiftPooledObject`1"
    /// /> for automatic release.
    /// </summary>
    /// <param name="value">Receives the rented object; use it only until the returned lease is disposed.</param>
    /// <returns>An owning lease whose disposal returns the out object at most once.</returns>
    /// <exception cref="T:System.ObjectDisposedException">This pool has been disposed.</exception>
    /// <remarks>
    /// Renting can invoke the factory and get callback. Collection wrappers ordinarily clear contents when
    /// released, preserving capacity.
    /// </remarks>
    public SwiftPooledObject<SwiftSparseMap<T>> Get(out SwiftSparseMap<T> value)
    {
        ChronicleGuard.ThrowIfDisposed(_disposed, nameof(SwiftSparseMapPool<T>));

        return CollectionPool.Rent(out value);
    }

    /// <summary>
    /// Releases a sparse map instance back to the pool for reuse.
    /// </summary>
    /// <param name="map">The object to return exactly once; do not use it afterward. Null is ignored by this
    /// wrapper.</param>
    /// <exception cref="T:System.ObjectDisposedException">The pool is disposed; wrapper null handling occurs
    /// after this check.</exception>
    /// <remarks>
    /// Ownership returns to the pool. The release callback runs before caching; overflow invokes the destroy
    /// callback. Collection wrappers clear logical contents and preserve capacity; this does not promise full
    /// retained-reference cleanup of unused storage.
    /// </remarks>
    public void Release(SwiftSparseMap<T> map)
    {
        ChronicleGuard.ThrowIfDisposed(_disposed, nameof(SwiftSparseMapPool<T>));

        if (map == null) return;

        CollectionPool.Release(map);
    }

    /// <summary>
    /// Clears all pooled sparse maps from the pool.
    /// </summary>
    /// <remarks>
    /// Drops cached inactive collections without reclaiming checked-out rentals or instantiating an unused
    /// backing pool. The wrapper remains reusable unless disposed. Complete outstanding rentals before Clear if
    /// diagnostic counts must remain consistent.
    /// </remarks>
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
    /// <remarks>
    /// Repeated calls do nothing. Clears initialized cached collections and disposes backing pools. Finish
    /// rentals/leases before disposal; returning to an old disposed pool throws. Shared singleton instances are
    /// not recreated.
    /// </remarks>
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

    /// <summary>
    /// Releases the resources used by the SwiftSparseMapPool instance.
    /// </summary>
    /// <remarks>
    /// This finalizer ensures that unmanaged resources are released if Dispose was not called explicitly. It is
    /// recommended to call Dispose to release resources deterministically.
    /// </remarks>
    ~SwiftSparseMapPool() => Dispose();

    #endregion
}
