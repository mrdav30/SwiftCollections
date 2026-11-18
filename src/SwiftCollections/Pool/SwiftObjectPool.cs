//=======================================================================
// SwiftObjectPool.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Chronicler.Diagnostics;

namespace SwiftCollections.Pool;

/// <summary>
/// Caches reusable objects in concurrent storage with caller-managed ownership and lifecycle.
/// </summary>
/// <typeparam name="T">The type of object to pool. Must be a reference type.</typeparam>
/// <remarks>
/// Rented objects have exclusive caller ownership until returned exactly once. Foreign objects and double returns
/// are not detected and can break ownership/counts. Callback exceptions propagate; operations are not
/// transactional. Concurrent storage does not make counters, max-size checks, callbacks or disposal atomic.
/// Synchronize lifecycle operations externally.
/// </remarks>
public sealed class SwiftObjectPool<T> : IDisposable, ISwiftObjectPool<T> where T : class
{
    #region Fields

    private readonly ConcurrentStack<T> _pool;
    private readonly Func<T> _createFunc;
    private readonly Action<T>? _actionOnGet;
    private readonly Action<T>? _actionOnRelease;
    private readonly Action<T>? _actionOnDestroy;
    private readonly int _maxSize;

    private volatile bool _disposed;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.Pool.SwiftObjectPool`1" /> class.
    /// </summary>
    /// <param name="createFunc">The retained factory, invoked only when a bucket is empty. Must be non-null and
    /// return a non-null object.</param>
    /// <param name="actionOnGet">Optional retained callback invoked after renting, before the object is returned
    /// to the caller.</param>
    /// <param name="actionOnRelease">Optional retained callback invoked before an object is cached or destroyed.
    /// Null performs no reset.</param>
    /// <param name="actionOnDestroy">Optional retained callback invoked for overflow and Clear, not Dispose. Null
    /// performs no destruction.</param>
    /// <param name="maxSize">The positive maximum number of inactive objects retained; active rentals are not
    /// limited. The concurrent check is not an atomic hard bound.</param>
    /// <exception cref="T:System.ArgumentOutOfRangeException">The configured inactive capacity is zero or
    /// negative.</exception>
    /// <exception cref="T:System.ArgumentNullException">createFunc is null.</exception>
    public SwiftObjectPool(
        Func<T> createFunc,
        Action<T>? actionOnGet = null,
        Action<T>? actionOnRelease = null,
        Action<T>? actionOnDestroy = null,
        int maxSize = 100)
    {
        ChronicleGuard.ThrowIfNull(createFunc, nameof(createFunc));
        ChronicleGuard.ThrowIfNegativeOrZero(maxSize, nameof(maxSize));

        _pool = new ConcurrentStack<T>();
        _createFunc = createFunc;
        _actionOnGet = actionOnGet;
        _actionOnRelease = actionOnRelease;
        _actionOnDestroy = actionOnDestroy;
        _maxSize = maxSize;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the total number of objects created by the pool, including both active and inactive objects.
    /// </summary>
    /// <value>The pool's creation-minus-overflow counter; Clear resets it to zero even with active
    /// rentals.</value>
    /// <remarks>
    /// This is not an atomic concurrent counter. Foreign returns and Clear with outstanding rentals can make the
    /// active/inactive arithmetic inconsistent.
    /// </remarks>
    public int CountAll { get; private set; }

    /// <summary>
    /// Gets the number of objects currently in use (rented from the pool).
    /// </summary>
    /// <value>CountAll minus CountInactive at the time of reading; not an atomic snapshot.</value>
    /// <remarks>
    /// Can become negative after Clear followed by returning an outstanding rental. Treat counts as diagnostics,
    /// not ownership validation.
    /// </remarks>
    public int CountActive => CountAll - CountInactive;

    /// <summary>
    /// Gets the number of objects currently available in the pool for rent.
    /// </summary>
    /// <value>The current number of cached objects available to rent; checked-out objects are excluded.</value>
    public int CountInactive => _pool.Count;

    #endregion

    #region Collection Manipulation

    /// <summary>
    /// Rents an object from the pool. If the pool is empty, a new object is created using the factory function.
    /// </summary>
    /// <returns>A cached or newly created object for exclusive caller use; return it once to this pool.</returns>
    /// <exception cref="T:System.ObjectDisposedException">This pool has been disposed.</exception>
    /// <exception cref="T:System.InvalidOperationException">The factory returns null.</exception>
    /// <remarks>
    /// Renting can invoke the factory and get callback. A callback exception can remove an object from the cache
    /// without returning it to the caller.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Rent()
    {
        ChronicleGuard.ThrowIfDisposed(_disposed, nameof(SwiftObjectPool<T>));

        if (_pool.TryPop(out var obj))
        {
            _actionOnGet?.Invoke(obj);
            return obj;
        }

        var newObj = _createFunc();
        ChronicleGuard.ThrowIfTrue(newObj is null, message: "Failed to create a new object.");
        CountAll++;
        _actionOnGet?.Invoke(newObj);
        return newObj;
    }

    /// <summary>
    /// Rents an object from the pool and wraps it in a <see cref="T:SwiftCollections.Pool.SwiftPooledObject`1" />
    /// for automatic release.
    /// </summary>
    /// <param name="value">Receives the rented object; use it only until the returned lease is disposed.</param>
    /// <returns>An owning lease whose disposal returns the out object at most once.</returns>
    /// <exception cref="T:System.ObjectDisposedException">This pool has been disposed.</exception>
    /// <exception cref="T:System.InvalidOperationException">The factory returns null.</exception>
    /// <remarks>
    /// Renting can invoke the factory and get callback. A callback exception can remove an object from the cache
    /// without returning it to the caller.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SwiftPooledObject<T> Rent(out T value) => new(value = Rent(), this);

    /// <summary>
    /// Releases an object back to the pool for reuse. If the pool has reached its maximum size, the object is
    /// destroyed using the configured destroy action.
    /// </summary>
    /// <param name="element">The object to return exactly once; do not use it afterward. Must be
    /// non-null.</param>
    /// <exception cref="T:System.ObjectDisposedException">The pool is disposed; wrapper null handling occurs
    /// after this check.</exception>
    /// <exception cref="T:System.ArgumentNullException">element is null.</exception>
    /// <remarks>
    /// Ownership returns to the pool. The release callback runs before caching; overflow invokes the destroy
    /// callback. Collection wrappers clear logical contents and preserve capacity; this does not promise full
    /// retained-reference cleanup of unused storage.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Release(T element)
    {
        ChronicleGuard.ThrowIfDisposed(_disposed, nameof(SwiftObjectPool<T>));
        ChronicleGuard.ThrowIfNull(element, nameof(element));

        _actionOnRelease?.Invoke(element);

        if (_pool.Count < _maxSize)
            _pool.Push(element);
        else
        {
            _actionOnDestroy?.Invoke(element);
            CountAll--;
        }
    }

    /// <summary>
    /// Releases all objects back to the pool for reuse. If the pool has reached its maximum size, objects are
    /// destroyed using the configured destroy action.
    /// </summary>
    /// <param name="elements">The sequence of objects to return one at a time; source storage is not retained.
    /// Null is not accepted.</param>
    /// <exception cref="T:System.ArgumentNullException">Thrown if any object in <paramref name="elements" /> is
    /// null.</exception>
    /// <exception cref="T:System.ObjectDisposedException">The pool is disposed; wrapper null handling occurs
    /// after this check.</exception>
    /// <exception cref="T:System.NullReferenceException">elements is null.</exception>
    /// <remarks>
    /// Ownership returns to the pool. Objects are released sequentially; an exception leaves earlier returns
    /// completed.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Release(IEnumerable<T> elements)
    {
        ChronicleGuard.ThrowIfDisposed(_disposed, nameof(SwiftObjectPool<T>));

        foreach (T item in elements)
            Release(item);
    }

    /// <summary>
    /// Clears the inactive objects currently held by the pool, invoking the destroy action if configured.
    /// </summary>
    /// <remarks>
    /// Removes cached inactive objects and invokes the destroy callback for them; active rentals are not
    /// reclaimed. Resets CountAll to zero even if rentals are outstanding. A callback exception can leave partial
    /// cleanup. Does nothing after disposal.
    /// </remarks>
    public void Clear()
    {
        if (_disposed) return;

        while (_pool.TryPop(out var obj))
            _actionOnDestroy?.Invoke(obj);

        CountAll = 0;
    }

    #endregion

    #region IDisposable Implementation

    /// <summary>
    /// Releases all resources used by the SwiftObjectPool. It is important to call Dispose() to release pooled
    /// arrays, preventing potential memory leaks.
    /// </summary>
    /// <remarks>
    /// Repeated calls do nothing. Drops cached objects without invoking actionOnDestroy and rejects subsequent
    /// Rent/Release. Checked-out objects are not reclaimed.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
            return;

        _pool.Clear();

        _disposed = true;

        // Suppress finalization to prevent unnecessary GC overhead
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the resources used by the SwiftObjectPool instance.
    /// </summary>
    /// <remarks>
    /// This finalizer ensures that unmanaged resources are released if Dispose was not called explicitly. It is
    /// recommended to call Dispose to release resources deterministically.
    /// </remarks>
    ~SwiftObjectPool() => Dispose();

    #endregion
}
