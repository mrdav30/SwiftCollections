//=======================================================================
// SwiftListPool.cs
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
/// A specialized pool for managing <see cref="T:SwiftCollections.SwiftList`1" /> instances, providing efficient
/// reuse to minimize memory allocations and improve performance.
/// </summary>
/// <typeparam name="T">The type of elements in the list.</typeparam>
/// <remarks>
/// Rented objects have exclusive caller ownership until returned exactly once. Foreign objects and double returns
/// are not detected and can break ownership/counts. Callback exceptions propagate; operations are not
/// transactional. Use external synchronization for lifecycle changes and host callbacks.
/// </remarks>
public sealed class SwiftListPool<T> : SwiftCollectionPool<SwiftList<T>, T>, IDisposable
{
    #region Singleton Instance

    /// <summary>
    /// Shared instance of the hash set pool, providing a globally accessible pool.
    /// Uses <see cref="SwiftLazyDisposable{T}"/> to ensure lazy initialization and proper disposal.
    /// </summary>
    private readonly static SwiftLazyDisposable<SwiftListPool<T>> _lazyInstance =
        new(() => new SwiftListPool<T>(), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Gets the shared instance of the pool.
    /// </summary>
    /// <value>The process-wide lazily created pool for this closed generic type.</value>
    /// <remarks>
    /// The shared instance is reused and is not recreated after disposal. Prefer Clear for reusable cleanup;
    /// disposing Shared affects every consumer.
    /// </remarks>
    public static SwiftListPool<T> Shared => _lazyInstance.Value;

    #endregion

    #region Fields

    /// <summary>
    /// Tracks whether the pool has been disposed.
    /// </summary>
    private volatile bool _disposed;

    #endregion

    #region Methods

    /// <summary>
    /// Rents a list instance from the pool. If the pool is empty, a new list is created.
    /// </summary>
    /// <returns>A cached or newly created object for exclusive caller use; return it once to this pool.</returns>
    /// <exception cref="T:System.ObjectDisposedException">This pool has been disposed.</exception>
    /// <remarks>
    /// Renting can invoke the factory and get callback. Collection wrappers ordinarily clear contents when
    /// released, preserving capacity.
    /// </remarks>
    public override SwiftList<T> Rent()
    {
        ChronicleGuard.ThrowIfDisposed(_disposed, nameof(SwiftList<T>));

        return base.Rent();
    }

    /// <summary>
    /// Releases a list instance back to the pool for reuse.
    /// </summary>
    /// <param name="list">The object to return exactly once; do not use it afterward. Null is ignored by this
    /// wrapper.</param>
    /// <exception cref="T:System.ObjectDisposedException">The pool is disposed; wrapper null handling occurs
    /// after this check.</exception>
    /// <remarks>
    /// Ownership returns to the pool. The release callback runs before caching; overflow invokes the destroy
    /// callback. Collection wrappers clear logical contents and preserve capacity; this does not promise full
    /// retained-reference cleanup of unused storage.
    /// </remarks>
    public override void Release(SwiftList<T> list)
    {
        ChronicleGuard.ThrowIfDisposed(_disposed, nameof(SwiftList<T>));

        if (list == null) return;

        // Ensure the list is clear before returning it to the pool
        list.Clear();
        base.Release(list);
    }

    /// <summary>
    /// Clears all pooled lists from the pool.
    /// </summary>
    /// <remarks>
    /// Drops cached inactive collections without reclaiming checked-out rentals or instantiating an unused
    /// backing pool. The wrapper remains reusable unless disposed. Complete outstanding rentals before Clear if
    /// diagnostic counts must remain consistent.
    /// </remarks>
    public override void Clear()
    {
        if (_disposed) return;

        base.Clear();
    }

    #endregion

    #region IDisposable Implementation

    /// <summary>
    /// Releases all resources used by the SwiftListPool. It is important to call Dispose() to release pooled
    /// objects, preventing potential memory leaks.
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
        base.Flush();

        _disposed = true;

        // Suppress finalization to prevent unnecessary GC overhead
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the resources used by the SwiftListPool instance.
    /// </summary>
    /// <remarks>
    /// This finalizer ensures that unmanaged resources are released if Dispose was not called explicitly. It is
    /// recommended to call Dispose to release resources deterministically.
    /// </remarks>
    ~SwiftListPool() => Dispose();

    #endregion
}
