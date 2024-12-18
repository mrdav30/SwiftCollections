using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SwiftCollections.Pool
{
    /// <summary>
    /// A thread-safe pool designed to manage arrays of a specific size. 
    /// The pool optimizes memory usage by reusing arrays, reducing allocations and improving performance.
    /// </summary>
    /// <typeparam name="T">The type of elements in the arrays being pooled. Must have a parameterless constructor.</typeparam>
    public sealed class SwiftArrayPool<T> : IDisposable where T : new()
    {
        #region Fields

        /// <summary>
        /// A lazily initialized singleton instance of the array pool.
        /// </summary>
        private static readonly Lazy<SwiftArrayPool<T>> _instance =
            new Lazy<SwiftArrayPool<T>>(() => new SwiftArrayPool<T>());

        /// <summary>
        /// A collection of object pools, keyed by the size of the arrays they manage.
        /// </summary>
        private readonly ConcurrentDictionary<int, SwiftObjectPool<T[]>> _sizePools;

        /// <summary>
        /// Tracks whether the pool has been disposed.
        /// </summary>
        private bool _disposed;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="SwiftArrayPool{T}"/> class with customizable behavior.
        /// </summary>
        /// <param name="createFunc">A function used to create new arrays (default: creates arrays of the specified size).</param>
        /// <param name="actionOnRelease">An action performed when an array is released back to the pool (default: clears the array).</param>
        /// <param name="actionOnDestroy">An action performed when an array is removed from the pool (default: no action).</param>
        /// <param name="poolMaxCapacity">The maximum number of arrays each pool can hold for a specific size (default: 100).</param>
        private SwiftArrayPool(
            Func<int, T[]> createFunc = null,
            Action<T[]> actionOnRelease = null,
            Action<T[]> actionOnDestroy = null,
            int poolMaxCapacity = 100)
        {
            if (poolMaxCapacity <= 0) ThrowHelper.ThrowArgumentException($"{nameof(poolMaxCapacity)} must be greater than 0");

            _sizePools = new ConcurrentDictionary<int, SwiftObjectPool<T[]>>();
            PoolMaxCapacity = poolMaxCapacity;

            CreateFunc = createFunc ?? (size => new T[size]);
            ActionOnRelease = actionOnRelease ?? (array => Array.Clear(array, 0, array.Length));
            ActionOnDestroy = actionOnDestroy;
        }

        #endregion

        #region Properties
        /// <summary>
        /// Gets the shared singleton instance of the array pool.
        /// </summary>
        public static SwiftArrayPool<T> Shared => _instance.Value;

        /// <summary>
        /// Gets the function used to create new arrays.
        /// </summary>
        public Func<int, T[]> CreateFunc { get; }

        /// <summary>
        /// Gets the action performed when an array is released back to the pool.
        /// </summary>
        public Action<T[]> ActionOnRelease { get; }

        /// <summary>
        /// Gets the action performed when an array is removed from the pool.
        /// </summary>
        public Action<T[]> ActionOnDestroy { get; }

        /// <summary>
        /// Gets the maximum number of arrays each pool can hold for a specific size.
        /// </summary>
        public int PoolMaxCapacity { get; }

        #endregion

        #region Collection Manipulation

        /// <summary>
        /// Rents an array of the specified size from the pool. If no pool exists for the size, a new one is created.
        /// </summary>
        /// <param name="size">The desired size of the array.</param>
        /// <returns>An array of the specified size, either newly created or retrieved from the pool.</returns>
        /// <exception cref="ArgumentException">Thrown if the specified size is less than or equal to 0.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T[] Rent(int size)
        {
            if (_disposed) ThrowHelper.ThrowObjectDisposedException(nameof(SwiftArrayPool<T>));
            if (size <= 0) ThrowHelper.ThrowArgumentException($"{nameof(size)} must be greater than 0");

            return _sizePools.GetOrAdd(size, key => CreatePoolForSize(key)).Rent();
        }

        /// <summary>
        /// Releases an array back to the pool for reuse. If no pool exists for the array's size, it is cleared and discarded.
        /// </summary>
        /// <param name="array">The array to release back to the pool.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Release(T[] array)
        {
            if (_disposed) ThrowHelper.ThrowObjectDisposedException(nameof(SwiftArrayPool<T>));

            if (array == null || array.Length == 0) return;

            if (_sizePools.TryGetValue(array.Length, out var pool))
                pool.Release(array);
            else
                Array.Clear(array, 0, array.Length);  // Handle large or unusual sizes by discarding
        }

        /// <summary>
        /// Clears all object pools, releasing any pooled arrays and resetting the state.
        /// </summary>
        public void Clear()
        {
            if (_disposed) ThrowHelper.ThrowObjectDisposedException(nameof(SwiftArrayPool<T>));

            foreach (var pool in _sizePools.Values)
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
        /// Releases all resources used by the SwiftArrayPool.
        /// It is important to call Dispose() to release pooled arrays, preventing potential memory leaks.
        /// </summary>
        public void Dispose()
        {
            OnDispose();
            GC.SuppressFinalize(this);  // Avoids calling the finalizer if already disposed.
        }

        private void OnDispose()
        {
            if (_disposed) return;

            Clear();
            _disposed = true;
        }

        ~SwiftArrayPool() => OnDispose();  // Called by GC if Dispose() wasn't called explicitly.

        #endregion
    }
}
