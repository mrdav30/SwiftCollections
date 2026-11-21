//=======================================================================
// SwiftCollectionPool.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Collections.Generic;

namespace SwiftCollections.Pool;

/// <summary>
/// Pools collections using their default constructors and collection clearing contracts.
/// </summary>
/// <typeparam name="TCollection">The collection type, with a public parameterless constructor.</typeparam>
/// <typeparam name="TItem">The type of items stored by the collection.</typeparam>
/// <remarks>
/// Rentals have exclusive caller ownership until returned exactly once. Lifecycle operations require external
/// synchronization. Flush replaces an initialized backing pool for reuse; Dispose permanently closes this owner.
/// </remarks>
public abstract class SwiftCollectionPool<TCollection, TItem> : IDisposable
    where TCollection : class, ICollection<TItem>, new()
{
    private static readonly Func<SwiftObjectPool<TCollection>> _createPool = static () =>
        new(static () => new TCollection(), actionOnRelease: static collection => collection.Clear());

    private SwiftPoolLifecycle<TCollection> _lifecycle;

    /// <summary>
    /// Creates a lazy pool that rejects null returns.
    /// </summary>
    protected SwiftCollectionPool()
    {
        _lifecycle = new SwiftPoolLifecycle<TCollection>(_createPool);
    }

    /// <summary>
    /// Gets this owner's lazily created backing object pool.
    /// </summary>
    /// <value>The current backing pool; first access initializes it.</value>
    /// <exception cref="ObjectDisposedException">This owner has been disposed.</exception>
    /// <remarks>
    /// A retained backing pool is disposed by Flush or Dispose. Outstanding rentals must finish before either
    /// operation. Direct access bypasses wrapper-specific return handling; do not dispose the backing pool directly.
    /// </remarks>
    public SwiftObjectPool<TCollection> CollectionPool => _lifecycle.GetPool(this);

    /// <summary>
    /// Rents a collection for exclusive caller use.
    /// </summary>
    /// <returns>A cached or newly constructed collection; return it once to this owner.</returns>
    /// <exception cref="ObjectDisposedException">This owner has been disposed.</exception>
    /// <remarks>Factory and backing-pool callback exceptions propagate without rollback.</remarks>
    public virtual TCollection Rent() => CollectionPool.Rent();

    /// <summary>
    /// Rents a collection with an owning lease that returns it when disposed.
    /// </summary>
    /// <param name="value">Receives the rented collection, valid until the lease is disposed.</param>
    /// <returns>A lease that attempts to return the collection at most once.</returns>
    /// <exception cref="ObjectDisposedException">This owner has been disposed.</exception>
    /// <remarks>Factory and backing-pool callback exceptions propagate without rollback.</remarks>
    public virtual SwiftPooledObject<TCollection> Get(out TCollection value) => CollectionPool.Rent(out value);

    /// <summary>
    /// Clears and returns a collection to its backing pool.
    /// </summary>
    /// <param name="toRelease">The collection to return exactly once. This generic base rejects null; typed
    /// wrappers ignore null while usable.</param>
    /// <exception cref="ObjectDisposedException">This owner has been disposed; checked before null handling.</exception>
    /// <exception cref="ArgumentNullException">toRelease is null and this owner rejects null returns.</exception>
    /// <remarks>
    /// Logical clearing preserves capacity and does not promise full retained-reference cleanup. Foreign and
    /// duplicate returns are not validated. Reset failures propagate without transactional rollback.
    /// </remarks>
    public virtual void Release(TCollection toRelease) =>
        _lifecycle.Release(toRelease, this, ignoreNullReturns: false);

    internal void ReleaseTyped(TCollection? value) =>
        _lifecycle.Release(value, this, ignoreNullReturns: true);

    /// <summary>
    /// Removes cached inactive collections without initializing an unused backing pool.
    /// </summary>
    /// <remarks>
    /// Outstanding rentals remain valid and counted by the same backing pool. Does nothing after disposal.
    /// </remarks>
    public virtual void Clear() => _lifecycle.Clear();

    /// <summary>
    /// Disposes an initialized backing pool and replaces it with a fresh lazy pool for reuse.
    /// </summary>
    /// <remarks>
    /// Does not initialize an unused pool and does nothing after disposal. Finish outstanding rentals first:
    /// retained backing-pool references and old leases still target the disposed pool. Does not close this owner.
    /// </remarks>
    public virtual void Flush() => _lifecycle.Flush(_createPool);

    /// <summary>
    /// Clears initialized cached collections and permanently closes this owner.
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
    ~SwiftCollectionPool() => Dispose();
}
