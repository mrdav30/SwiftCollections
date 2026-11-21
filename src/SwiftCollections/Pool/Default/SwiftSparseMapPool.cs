//=======================================================================
// SwiftSparseMapPool.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Threading;
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

    private static readonly Func<SwiftObjectPool<SwiftSparseMap<T>>> _createPool = static () =>
        new(static () => new SwiftSparseMap<T>(), actionOnRelease: static map => map.Clear());

    private SwiftPoolLifecycle<SwiftSparseMap<T>> _lifecycle;

    /// <summary>
    /// Creates a lazy pool for sparse map instances.
    /// </summary>
    public SwiftSparseMapPool()
    {
        _lifecycle = new SwiftPoolLifecycle<SwiftSparseMap<T>>(_createPool);
    }

    /// <summary>
    /// Gets this owner's lazily created backing object pool.
    /// </summary>
    /// <value>The current backing pool; first access initializes it.</value>
    /// <exception cref="ObjectDisposedException">This owner has been disposed.</exception>
    /// <remarks>
    /// Finish rentals before disposal. A retained backing pool is disposed with this owner. Direct access bypasses
    /// wrapper null handling; do not dispose the backing pool directly.
    /// </remarks>
    public SwiftObjectPool<SwiftSparseMap<T>> CollectionPool => _lifecycle.GetPool(this);

    /// <summary>
    /// Rents a sparse map for exclusive caller use.
    /// </summary>
    /// <returns>A cached or newly constructed map; return it once to this owner.</returns>
    /// <exception cref="ObjectDisposedException">This owner has been disposed.</exception>
    public SwiftSparseMap<T> Rent() => CollectionPool.Rent();

    /// <summary>
    /// Rents a sparse map with an owning lease that returns it when disposed.
    /// </summary>
    /// <param name="value">Receives the map, valid until the lease is disposed.</param>
    /// <returns>A lease that attempts to return the map at most once.</returns>
    /// <exception cref="ObjectDisposedException">This owner has been disposed.</exception>
    public SwiftPooledObject<SwiftSparseMap<T>> Get(out SwiftSparseMap<T> value) => CollectionPool.Rent(out value);

    /// <summary>
    /// Clears and returns a sparse map to its backing pool.
    /// </summary>
    /// <param name="map">The map to return exactly once; null is ignored while the owner is usable.</param>
    /// <exception cref="ObjectDisposedException">This owner has been disposed; checked before null handling.</exception>
    /// <remarks>
    /// Logical clearing preserves capacity and does not promise full retained-reference cleanup. Foreign and
    /// duplicate returns are not validated. Reset failures propagate without transactional rollback.
    /// </remarks>
    public void Release(SwiftSparseMap<T> map) => _lifecycle.Release(map, this, ignoreNullReturns: true);

    /// <summary>
    /// Removes cached inactive maps without initializing an unused backing pool.
    /// </summary>
    /// <remarks>
    /// Outstanding rentals remain valid and counted by the same backing pool. Does nothing after disposal.
    /// </remarks>
    public void Clear() => _lifecycle.Clear();

    /// <summary>
    /// Clears initialized cached maps and permanently closes this owner.
    /// </summary>
    /// <remarks>
    /// Repeated calls do nothing and an unused backing pool is not initialized. Finish rentals first; returning
    /// an old rental or accessing the backing pool after disposal throws. Shared owners are not recreated.
    /// </remarks>
    public void Dispose()
    {
        _lifecycle.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Closes an abandoned owner and disposes its initialized backing pool.
    /// </summary>
    ~SwiftSparseMapPool() => Dispose();
}
