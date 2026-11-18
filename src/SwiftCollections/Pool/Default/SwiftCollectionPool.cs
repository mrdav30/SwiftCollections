//=======================================================================
// SwiftCollectionPool.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Collections.Generic;
using SwiftCollections.Lazy;

namespace SwiftCollections.Pool;

/// <summary>
/// A generic pool for managing collections such as <see cref="T:System.Collections.Generic.List`1" />, <see
/// cref="T:System.Collections.Generic.HashSet`1" />, <see cref="T:System.Collections.Generic.Dictionary`2" />,
/// and other types implementing <see cref="T:System.Collections.Generic.ICollection`1" />. Provides efficient
/// reuse of collection instances to reduce memory allocations.
/// </summary>
/// <typeparam name="TCollection">The type of the collection being pooled. Must implement <see
/// cref="T:System.Collections.Generic.ICollection`1" /> and have a parameterless constructor.</typeparam>
/// <typeparam name="TItem">The type of items contained in the collection.</typeparam>
/// <remarks>
/// Rented objects have exclusive caller ownership until returned exactly once. Foreign objects and double returns
/// are not detected and can break ownership/counts. Callback exceptions propagate; operations are not
/// transactional. Use external synchronization for lifecycle changes and host callbacks.
/// </remarks>
public abstract class SwiftCollectionPool<TCollection, TItem> where TCollection : class, ICollection<TItem>, new()
{
    #region Singleton Instances

    /// <summary>
    /// Internal object pool for managing the lifecycle of pooled collections.
    /// Uses <see cref="Lazy{T}"/> to ensure lazy initialization.
    /// </summary>
    private SwiftLazyDisposable<SwiftObjectPool<TCollection>> _lazyCollectionPool =
        new(() =>
        {
            return new SwiftObjectPool<TCollection>(
                    createFunc: () => new TCollection(),
                    actionOnRelease: collection => collection.Clear()
                );
        });

    /// <summary>
    /// Gets the shared instance of the pool.
    /// </summary>
    /// <value>The lazily created backing object pool; accessing this property can allocate it.</value>
    /// <remarks>
    /// The returned pool is owned by this wrapper. Retaining it across Flush or Dispose can leave a disposed
    /// backing pool. Direct access bypasses wrapper lifecycle checks.
    /// </remarks>
    public SwiftObjectPool<TCollection> CollectionPool => _lazyCollectionPool.Value;

    #endregion

    #region Methods

    /// <summary>
    /// Rents a collection from the pool. If the pool is empty, a new collection is created.
    /// </summary>
    /// <returns>A cached or newly created object for exclusive caller use; return it once to this pool.</returns>
    /// <remarks>
    /// Renting can invoke the factory and get callback. Collection wrappers ordinarily clear contents when
    /// released, preserving capacity.
    /// </remarks>
    public virtual TCollection Rent()
    {
        return CollectionPool.Rent();
    }

    /// <summary>
    /// Rents a collection from the pool and wraps it in a <see cref="T:SwiftCollections.Pool.SwiftPooledObject`1"
    /// /> for automatic release.
    /// </summary>
    /// <param name="value">Receives the rented object; use it only until the returned lease is disposed.</param>
    /// <returns>An owning lease whose disposal returns the out object at most once.</returns>
    /// <remarks>
    /// Creates a lease from the backing pool without checking any derived wrapper disposed flag. After a wrapper
    /// Flush/Dispose has reset the lazy backing pool, this inherited path can still create a rental; keep all
    /// leases within the wrapper lifetime. Factory/callback exceptions propagate.
    /// </remarks>
    public virtual SwiftPooledObject<TCollection> Get(out TCollection value)
    {
        return CollectionPool.Rent(out value);
    }

    /// <summary>
    /// Releases a collection back to the pool for reuse.
    /// </summary>
    /// <param name="toRelease">The object to return exactly once; do not use it afterward. Must be
    /// non-null.</param>
    /// <remarks>
    /// Ownership returns to the pool. The release callback runs before caching; overflow invokes the destroy
    /// callback. Collection wrappers clear logical contents and preserve capacity; this does not promise full
    /// retained-reference cleanup of unused storage.
    /// </remarks>
    public virtual void Release(TCollection toRelease)
    {
        CollectionPool.Release(toRelease);
    }

    /// <summary>
    /// Clears all collections from the pool.
    /// </summary>
    /// <remarks>
    /// Drops cached inactive collections without reclaiming checked-out rentals or instantiating an unused
    /// backing pool. The wrapper remains reusable unless disposed. Complete outstanding rentals before Clear if
    /// diagnostic counts must remain consistent.
    /// </remarks>
    public virtual void Clear()
    {
        if (_lazyCollectionPool.IsValueCreated)
            _lazyCollectionPool.Value.Clear();
    }

    #endregion

    #region IDisposable Implementation

    /// <summary>
    /// Releases all resources used by the SwiftCollectionPool.
    /// </summary>
    /// <remarks>
    /// If initialized, disposes the backing pool and installs a new lazy pool. Cached backing-pool references and
    /// old leases still target the disposed pool; finish rentals first. If uninitialized, changes nothing.
    /// </remarks>
    public virtual void Flush()
    {
        if (_lazyCollectionPool.IsValueCreated)
        {
            _lazyCollectionPool.Value.Dispose();

            _lazyCollectionPool = new SwiftLazyDisposable<SwiftObjectPool<TCollection>>(() =>
            {
                return new SwiftObjectPool<TCollection>(
                        createFunc: () => new TCollection(),
                        actionOnRelease: collection => collection.Clear()
                    );
            });
        }

    }

    #endregion
}
