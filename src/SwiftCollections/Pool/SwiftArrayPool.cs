//=======================================================================
// SwiftArrayPool.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using Chronicler.Diagnostics;
using SwiftCollections.Lazy;

namespace SwiftCollections.Pool;

/// <summary>
/// Caches reusable arrays in exact-length buckets with caller-managed ownership and lifecycle.
/// </summary>
/// <typeparam name="T">The type of elements in the arrays being pooled. Must have a parameterless
/// constructor.</typeparam>
/// <remarks>
/// Rented objects have exclusive caller ownership until returned exactly once. Foreign objects and double returns
/// are not detected and can break ownership/counts. Callback exceptions propagate; operations are not
/// transactional. Concurrent storage does not make counters, max-size checks, callbacks or disposal atomic.
/// Synchronize lifecycle operations externally. Each exact array length has its own bucket. A custom factory must
/// return a non-null array of the requested length; length is not checked.
/// </remarks>
public sealed class SwiftArrayPool<T> : IDisposable where T : new()
{
    #region Singleton Instance

    /// <summary>
    /// A lazily initialized singleton instance of the array pool.
    /// </summary>
    private static readonly SwiftLazyDisposable<SwiftArrayPool<T>> _instance =
        new(() => new SwiftArrayPool<T>(), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Gets the shared instance of the pool.
    /// </summary>
    /// <value>The process-wide lazily created pool for this closed generic type.</value>
    /// <remarks>
    /// The shared instance is reused and is not recreated after disposal. Prefer Clear for reusable cleanup;
    /// disposing Shared affects every consumer.
    /// </remarks>
    public static SwiftArrayPool<T> Shared => _instance.Value;

    #endregion

    #region Fields

    /// <summary>
    /// A collection of object pools, keyed by the size of the arrays they manage.
    /// </summary>
    private readonly ConcurrentDictionary<int, SwiftObjectPool<T[]>> _sizePools;

    /// <summary>
    /// Tracks whether the pool has been disposed.
    /// </summary>
    private volatile bool _disposed;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.Pool.SwiftArrayPool`1" /> class with
    /// customizable behavior.
    /// </summary>
    /// <param name="createFunc">The retained factory, invoked only when a bucket is empty. Null uses new T[size];
    /// custom arrays must have exactly the requested length.</param>
    /// <param name="actionOnRelease">Optional retained callback invoked before an object is cached or destroyed.
    /// Null selects Array.Clear over the entire array.</param>
    /// <param name="actionOnDestroy">Optional retained callback invoked for overflow and Clear, not Dispose. Null
    /// performs no destruction.</param>
    /// <param name="poolMaxCapacity">The positive maximum number of inactive objects retained per exact-length
    /// bucket; active rentals are not limited. The concurrent check is not an atomic hard bound.</param>
    /// <exception cref="T:System.ArgumentOutOfRangeException">The configured inactive capacity is zero or
    /// negative.</exception>
    public SwiftArrayPool(
        Func<int, T[]>? createFunc = null,
        Action<T[]>? actionOnRelease = null,
        Action<T[]>? actionOnDestroy = null,
        int poolMaxCapacity = 100)
    {
        ChronicleGuard.ThrowIfNegativeOrZero(poolMaxCapacity, nameof(poolMaxCapacity));

        _sizePools = new ConcurrentDictionary<int, SwiftObjectPool<T[]>>();
        PoolMaxCapacity = poolMaxCapacity;

        CreateFunc = createFunc ?? (size => new T[size]);
        ActionOnRelease = actionOnRelease ?? (array =>
            Array.Clear(array, 0, array.Length));
        ActionOnDestroy = actionOnDestroy;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the function used to create new arrays.
    /// </summary>
    /// <value>The retained factory used when creating arrays for a length bucket.</value>
    public Func<int, T[]> CreateFunc { get; }

    /// <summary>
    /// Gets the action performed when an array is released back to the pool.
    /// </summary>
    /// <value>The retained reset callback; the default clears every array element.</value>
    public Action<T[]>? ActionOnRelease { get; }

    /// <summary>
    /// Gets the action performed when an array is removed from the pool.
    /// </summary>
    /// <value>The optional overflow/Clear callback; Dispose does not invoke it.</value>
    public Action<T[]>? ActionOnDestroy { get; }

    /// <summary>
    /// Gets the maximum number of arrays each pool can hold for a specific size.
    /// </summary>
    /// <value>The configured positive inactive limit for each exact-length bucket.</value>
    public int PoolMaxCapacity { get; }

    #endregion

    #region Collection Manipulation

    /// <summary>
    /// Rents an array of the specified size from the pool. If no pool exists for the size, a new one is created.
    /// </summary>
    /// <param name="size">The strictly positive requested array length; selects an exact-size bucket.</param>
    /// <returns>A cached or newly created array for exclusive caller use; return it once to this pool.</returns>
    /// <exception cref="T:System.ArgumentException">Thrown if the specified size is less than or equal to
    /// 0.</exception>
    /// <exception cref="T:System.ObjectDisposedException">This pool has been disposed.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">size is zero or negative.</exception>
    /// <remarks>
    /// Renting can invoke the factory and get callback. Collection wrappers ordinarily clear contents when
    /// released, preserving capacity.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T[] Rent(int size)
    {
        ChronicleGuard.ThrowIfDisposed(_disposed, nameof(SwiftArrayPool<T>));
        ChronicleGuard.ThrowIfNegativeOrZero(size, nameof(size));

        return _sizePools.GetOrAdd(size, key => CreatePoolForSize(key)).Rent();
    }

    /// <summary>
    /// Releases an array back to the pool for reuse. If no pool exists for the array's size, it is cleared and
    /// discarded.
    /// </summary>
    /// <param name="array">The object to return exactly once; do not use it afterward. Null is ignored by this
    /// wrapper.</param>
    /// <exception cref="T:System.ObjectDisposedException">The pool is disposed; wrapper null handling occurs
    /// after this check.</exception>
    /// <remarks>
    /// Ownership returns to the pool. The release callback runs before caching; overflow invokes the destroy
    /// callback. Unrecognized array lengths are cleared and discarded without the custom callback; null/empty
    /// arrays are ignored.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Release(T[] array)
    {
        ChronicleGuard.ThrowIfDisposed(_disposed, nameof(SwiftArrayPool<T>));

        if (array == null || array.Length == 0) return;

        if (_sizePools.TryGetValue(array.Length, out var pool))
            pool.Release(array);
        else
            Array.Clear(array, 0, array.Length);  // Handle large or unusual sizes by discarding
    }

    /// <summary>
    /// Clears all object pools, releasing any pooled arrays and resetting the state.
    /// </summary>
    /// <remarks>
    /// Drops cached inactive objects without reclaiming checked-out rentals. Does not instantiate an unused
    /// backing pool. Does nothing after disposal when the concrete wrapper checks it. Array pools also discard
    /// their length buckets.
    /// </remarks>
    public void Clear()
    {
        if (_disposed) return;

        foreach (SwiftObjectPool<T[]> pool in _sizePools.Values)
            pool.Clear();

        _sizePools.Clear();
    }

    /// <summary>
    /// Creates a new object pool for managing arrays of the specified size.
    /// </summary>
    /// <param name="size">The size of arrays to be managed by the pool.</param>
    /// <returns>A new instance of <see cref="SwiftObjectPool{T}"/> for managing arrays of the specified size.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private SwiftObjectPool<T[]> CreatePoolForSize(int size)
    {
        return new SwiftObjectPool<T[]>(
            createFunc: () => CreateFunc(size),
            actionOnRelease: ActionOnRelease,
            actionOnDestroy: ActionOnDestroy,
            maxSize: PoolMaxCapacity
        );
    }

    #endregion

    #region IDisposable Implementation

    /// <summary>
    /// Releases all resources used by the SwiftArrayPool. It is important to call Dispose() to release pooled
    /// arrays, preventing potential memory leaks.
    /// </summary>
    /// <remarks>
    /// Repeated calls do nothing. Clears initialized cached collections and disposes backing pools. Finish
    /// rentals/leases before disposal; returning to an old disposed pool throws. Shared singleton instances are
    /// not recreated.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        foreach (SwiftObjectPool<T[]> pool in _sizePools.Values)
            pool.Dispose();
        _sizePools.Clear();

        // Suppress finalization to prevent unnecessary GC overhead
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the resources used by the SwiftArrayPool instance.
    /// </summary>
    /// <remarks>
    /// This finalizer ensures that unmanaged resources are released if Dispose was not called explicitly. It is
    /// recommended to call Dispose to release resources deterministically.
    /// </remarks>
    ~SwiftArrayPool() => Dispose();

    #endregion
}
