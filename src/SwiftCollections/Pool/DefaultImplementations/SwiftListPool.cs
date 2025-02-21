using System;
using System.Threading;

namespace SwiftCollections.Pool
{
    /// <summary>
    /// A specialized pool for managing <see cref="SwiftList{T}"/> instances,
    /// providing efficient reuse to minimize memory allocations and improve performance.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    public sealed class SwiftListPool<T> : SwiftCollectionPool<SwiftList<T>, T>, IDisposable
    {
        #region Singleton Instance

        /// <summary>
        /// Shared instance of the hash set pool, providing a globally accessible pool.
        /// Uses <see cref="LazyDisposable{T}"/> to ensure lazy initialization and proper disposal.
        /// </summary>
        private readonly static LazyDisposable<SwiftListPool<T>> _lazyInstance =
            new LazyDisposable<SwiftListPool<T>>(() => new SwiftListPool<T>(), LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>
        /// Gets the shared instance of the pool.
        /// </summary>
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
        /// <returns>A <see cref="SwiftList{T}"/> instance.</returns>
        public override SwiftList<T> Rent()
        {
            if (_disposed)
                ThrowHelper.ThrowObjectDisposedException(nameof(SwiftList<T>));

            return base.Rent();
        }

        /// <summary>
        /// Releases a list instance back to the pool for reuse.
        /// </summary>
        /// <param name="list">The list to release.</param>
        /// <remarks>
        /// The list will be cleared before being returned to the pool to ensure it contains no stale data.
        /// </remarks>
        public override void Release(SwiftList<T> list)
        {
            if (_disposed)
                ThrowHelper.ThrowObjectDisposedException(nameof(SwiftList<T>));

            if (list == null) return;

            // Ensure the list is clear before returning it to the pool
            list.Clear();
            base.Release(list);
        }

        /// <summary>
        /// Clears all pooled lists from the pool.
        /// </summary>
        public override void Clear()
        {
            if (_disposed) return;

            base.Clear();
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Releases all resources used by the SwiftListPool.
        /// It is important to call Dispose() to release pooled objects, preventing potential memory leaks.
        /// </summary>
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

        ~SwiftListPool() => Dispose();

        #endregion
    }
}
