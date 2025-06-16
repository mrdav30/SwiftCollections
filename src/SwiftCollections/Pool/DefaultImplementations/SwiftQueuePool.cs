using System;
using System.Threading;

namespace SwiftCollections.Pool
{
    /// <summary>
    /// A specialized pool for managing <see cref="SwiftQueue{T}"/> instances, 
    /// providing efficient reuse to minimize memory allocations and improve performance.
    /// </summary>
    /// <typeparam name="T">The type of elements in the queue.</typeparam>
    public sealed class SwiftQueuePool<T> : SwiftCollectionPool<SwiftQueue<T>, T>, IDisposable
    {
        #region Singleton Instance

        /// <summary>
        /// Shared instance of the queue pool, providing a globally accessible pool.
        /// Uses <see cref="LazyDisposable{T}"/> to ensure lazy initialization and proper disposal.
        /// </summary>
        private readonly static LazyDisposable<SwiftQueuePool<T>> _lazyInstance =
            new(() => new SwiftQueuePool<T>(), LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>
        /// Gets the shared instance of the pool.
        /// </summary>
        public static SwiftQueuePool<T> Shared => _lazyInstance.Value;

        #endregion

        #region Fields

        /// <summary>
        /// Tracks whether the pool has been disposed.
        /// </summary>
        private volatile bool _disposed;

        #endregion

        #region Methods

        /// <summary>
        /// Rents a queue instance from the pool. If the pool is empty, a new queue is created.
        /// </summary>
        /// <returns>A <see cref="SwiftQueue{T}"/> instance.</returns>
        public override SwiftQueue<T> Rent()
        {
            if (_disposed)
                ThrowHelper.ThrowObjectDisposedException(nameof(SwiftQueuePool<T>));

            return base.Rent();
        }

        /// <summary>
        /// Releases a queue instance back to the pool for reuse.
        /// </summary>
        /// <param name="queue">The queue to release.</param>
        /// <remarks>
        /// The queue will be cleared before being returned to the pool to ensure it contains no stale data.
        /// </remarks>
        public override void Release(SwiftQueue<T> queue)
        {
            if (_disposed)
                ThrowHelper.ThrowObjectDisposedException(nameof(SwiftQueuePool<T>));

            if (queue == null) return;

            // SwiftCollectionPool clears pool before releasing
            base.Release(queue);
        }

        /// <summary>
        /// Clears all pooled queues from the pool.
        /// </summary>
        public override void Clear()
        {
            if (_disposed) return;

            base.Clear();
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Releases all resources used by the SwiftQueuePool.
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

        ~SwiftQueuePool() => Dispose();

        #endregion
    }
}
