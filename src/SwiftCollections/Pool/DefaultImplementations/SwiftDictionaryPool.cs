using System.Collections.Generic;

namespace SwiftCollections.Pool
{
    /// <summary>
    /// A specialized pool for managing <see cref="SwiftDictionary{TKey, TValue}"/> instances, 
    /// providing efficient reuse to minimize memory allocations and improve performance.
    /// </summary>
    /// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
    public sealed class SwiftDictionaryPool<TKey, TValue>
    {
        #region Methods

        /// <summary>
        /// Rents a dictionary instance from the pool. If the pool is empty, a new dictionary is created.
        /// </summary>
        /// <returns>A <see cref="SwiftDictionary{TKey, TValue}"/> instance.</returns>
        public SwiftDictionary<TKey, TValue> Rent()
        {
            return SwiftCollectionPool<SwiftDictionary<TKey, TValue>, KeyValuePair<TKey, TValue>>.Rent();
        }

        /// <summary>
        /// Releases a dictionary instance back to the pool for reuse.
        /// </summary>
        /// <param name="toRelease">The dictionary to release.</param>
        /// <remarks>
        /// The dictionary will be cleared before being returned to the pool to remove any existing data.
        /// </remarks>
        public void Release(SwiftDictionary<TKey, TValue> toRelease)
        {
            if (toRelease == null) return;

            // Ensure the dictionary is clear before returning it to the pool
            toRelease.Clear();
            SwiftCollectionPool<SwiftDictionary<TKey, TValue>, KeyValuePair<TKey, TValue>>.Release(toRelease);
        }

        /// <summary>
        /// Clears all pooled dictionaries from the pool.
        /// </summary>
        public void Clear()
        {
            SwiftCollectionPool<SwiftDictionary<TKey, TValue>, KeyValuePair<TKey, TValue>>.Clear();
        }

        #endregion
    }
}
