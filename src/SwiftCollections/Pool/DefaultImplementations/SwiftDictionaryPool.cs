using System;
using System.Collections.Generic;
using System.Threading;

namespace SwiftCollections.Pool
{
    /// <summary>
    /// A specialized pool for managing <see cref="SwiftDictionary{TKey, TValue}"/> instances, 
    /// providing efficient reuse to minimize memory allocations and improve performance.
    /// </summary>
    /// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
    public sealed class SwiftDictionaryPool<TKey, TValue> : 
        SwiftCollectionPool<SwiftDictionary<TKey, TValue>, KeyValuePair<TKey, TValue>>, IDisposable
    {
        #region Singleton Instance

        /// <summary>
        /// Shared instance of the dictionary pool, providing a globally accessible pool.
        /// Uses <see cref="LazyDisposable{T}"/> to ensure lazy initialization and proper disposal.
        /// </summary>
        private readonly static LazyDisposable<SwiftDictionaryPool<TKey, TValue>> _lazyInstance =
            new LazyDisposable<SwiftDictionaryPool<TKey, TValue>>(
                    () => new SwiftDictionaryPool<TKey, TValue>(), LazyThreadSafetyMode.ExecutionAndPublication
                );

        /// <summary>
        /// Gets the shared instance of the pool.
        /// </summary>
        public static SwiftDictionaryPool<TKey, TValue> Shared => _lazyInstance.Value;

        #endregion

        #region Fields

        /// <summary>
        /// Tracks whether the pool has been disposed.
        /// </summary>
        private volatile bool _disposed;

        #endregion

        #region Methods

        /// <summary>
        /// Rents a dictionary instance from the pool. If the pool is empty, a new dictionary is created.
        /// </summary>
        /// <returns>A <see cref="SwiftDictionary{TKey, TValue}"/> instance.</returns>
        public override SwiftDictionary<TKey, TValue> Rent()
        {
            if (_disposed)
                ThrowHelper.ThrowObjectDisposedException(nameof(SwiftDictionaryPool<TKey, TValue>));

            return base.Rent();
        }

        /// <summary>
        /// Releases a dictionary instance back to the pool for reuse.
        /// </summary>
        /// <param name="dictionary">The dictionary to release.</param>
        /// <remarks>
        /// The dictionary will be cleared before being returned to the pool to remove any existing data.
        /// </remarks>
        public override void Release(SwiftDictionary<TKey, TValue> dictionary)
        {
            if (_disposed)
                ThrowHelper.ThrowObjectDisposedException(nameof(SwiftDictionaryPool<TKey, TValue>));

            if (dictionary == null) return;

            base.Release(dictionary);
        }

        /// <summary>
        /// Clears all pooled dictionaries from the pool.
        /// </summary>
        public override void Clear()
        {
            if (_disposed) return;

            base.Clear();
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Releases all resources used by the SwiftDictionaryPool.
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

        ~SwiftDictionaryPool() => Dispose();

        #endregion
    }
}
