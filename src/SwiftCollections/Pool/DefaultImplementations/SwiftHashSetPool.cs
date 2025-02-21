using System;
using System.Threading;

namespace SwiftCollections.Pool
{
    /// <summary>
    /// A specialized pool for managing <see cref="SwiftHashSet{T}"/> instances, 
    /// providing efficient reuse to minimize memory allocations and improve performance.
    /// </summary>
    /// <typeparam name="T">The type of elements in the hash set.</typeparam>
    public sealed class SwiftHashSetPool<T> : SwiftCollectionPool<SwiftHashSet<T>, T>, IDisposable
    {
        #region Singleton Instance

        /// <summary>
        /// Shared instance of the hash set pool, providing a globally accessible pool.
        /// Uses <see cref="LazyDisposable{T}"/> to ensure lazy initialization and proper disposal.
        /// </summary>
        private readonly static LazyDisposable<SwiftHashSetPool<T>> _lazyInstance =
            new LazyDisposable<SwiftHashSetPool<T>>(() => new SwiftHashSetPool<T>(), LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>
        /// Gets the shared instance of the pool.
        /// </summary>
        public static SwiftHashSetPool<T> Shared => _lazyInstance.Value;

        #endregion

        #region Fields

        /// <summary>
        /// Tracks whether the pool has been disposed.
        /// </summary>
        private volatile bool _disposed;

        #endregion

        #region Methods

        /// <summary>
        /// Rents a hash set instance from the pool. If the pool is empty, a new hash set is created.
        /// </summary>
        /// <returns>A <see cref="SwiftHashSet{T}"/> instance.</returns>
        public override SwiftHashSet<T> Rent()
        {
            if (_disposed)
                ThrowHelper.ThrowObjectDisposedException(nameof(SwiftHashSetPool<T>));

            return base.Rent();
        }

        /// <summary>
        /// Releases a hash set instance back to the pool for reuse.
        /// </summary>
        /// <param name="set">The hash set to release.</param>
        /// <remarks>
        /// The hash set will be cleared before being returned to the pool to ensure it contains no stale data.
        /// </remarks>
        public override void Release(SwiftHashSet<T> set)
        {
            if (_disposed)
                ThrowHelper.ThrowObjectDisposedException(nameof(SwiftHashSetPool<T>));

            if (set == null) return;

            // SwiftCollectionPool clears pool before releasing
            base.Release(set);
        }

        /// <summary>
        /// Clears all pooled hash sets from the pool.
        /// </summary>
        public override void Clear()
        {
            if (_disposed) return;

            base.Clear();
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Releases all resources used by the SwiftHashSetPool.
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

        ~SwiftHashSetPool() => Dispose();

        #endregion
    }
}
