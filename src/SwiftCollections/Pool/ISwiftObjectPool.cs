//=======================================================================
// ISwiftObjectPool.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

namespace SwiftCollections.Pool;

/// <summary>
/// Defines a contract for an object pool that manages reusable instances of type T. Provides methods to rent and
/// release objects to minimize allocations and improve performance.
/// </summary>
/// <typeparam name="T">The type of objects managed by the pool. Must be a reference type.</typeparam>
/// <remarks>
/// Rented objects have exclusive caller ownership until returned exactly once. Foreign objects and double returns
/// are not detected and can break ownership/counts. Callback exceptions propagate; operations are not
/// transactional. Use external synchronization for lifecycle changes and host callbacks.
/// </remarks>
public interface ISwiftObjectPool<T> where T : class
{
    /// <summary>
    /// Gets the number of inactive items in the collection.
    /// </summary>
    /// <value>The current number of cached objects available to rent; checked-out objects are excluded.</value>
    int CountInactive { get; }

    /// <summary>
    /// Retrieves an instance of type T from the pool for use by the caller.
    /// </summary>
    /// <returns>A cached or newly created object for exclusive caller use; return it once to this pool.</returns>
    /// <remarks>
    /// Implementations define creation/reset behavior; the caller must return each object exactly once.
    /// </remarks>
    T Rent();

    /// <summary>
    /// Rents an object from the pool and assigns it to the specified output parameter.
    /// </summary>
    /// <param name="v">Receives the rented object; use it only until the returned lease is disposed.</param>
    /// <returns>An owning lease whose disposal returns the out object at most once.</returns>
    /// <remarks>
    /// Implementations define creation/reset behavior; the caller must return each object exactly once.
    /// </remarks>
    SwiftPooledObject<T> Rent(out T v);

    /// <summary>
    /// Releases the specified element back to the pool or resource manager for reuse.
    /// </summary>
    /// <param name="element">The object to return exactly once; do not use it afterward. Must be
    /// non-null.</param>
    /// <remarks>
    /// After return, the caller must stop using the object. Concrete implementations decide whether to reset,
    /// cache or discard it and which callbacks to invoke. Do not return a foreign object or return the same
    /// rental twice; the interface does not require ownership validation.
    /// </remarks>
    void Release(T element);

    /// <summary>
    /// Removes all items from the collection.
    /// </summary>
    /// <remarks>
    /// Releases cached inactive objects according to the concrete implementation. Checked-out objects remain
    /// owned by callers. The interface does not define diagnostic counter reset or disposal-callback policy;
    /// consult the implementation before clearing with outstanding rentals.
    /// </remarks>
    void Clear();
}
