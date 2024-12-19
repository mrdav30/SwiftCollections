using System.Collections.Generic;

namespace SwiftCollections.Pool
{
    /// <summary>
    /// A specialized pool for managing <see cref="SwiftHashSet{T}"/> instances, 
    /// providing efficient reuse to minimize memory allocations and improve performance.
    /// </summary>
    /// <typeparam name="T">The type of elements in the hash set.</typeparam>
    public sealed class SwiftHashSetPool<T>
    {
        #region Methods

        /// <summary>
        /// Rents a hash set instance from the pool. If the pool is empty, a new hash set is created.
        /// </summary>
        /// <returns>A <see cref="HashSet{T}"/> instance.</returns>
        public SwiftHashSet<T> Rent()
        {
            return SwiftCollectionPool<SwiftHashSet<T>, T>.Rent();
        }

        /// <summary>
        /// Releases a hash set instance back to the pool for reuse.
        /// </summary>
        /// <param name="set">The hash set to release.</param>
        /// <remarks>
        /// The hash set will be cleared before being returned to the pool to ensure it contains no stale data.
        /// </remarks>
        public void Release(SwiftHashSet<T> set)
        {
            if (set == null) return;

            // Ensure the hash set is clear before returning it to the pool
            set.Clear();
            SwiftCollectionPool<SwiftHashSet<T>, T>.Release(set);
        }

        /// <summary>
        /// Clears all pooled hash sets from the pool.
        /// </summary>
        public void Clear()
        {
            SwiftCollectionPool<SwiftHashSet<T>, T>.Clear();
        }

        #endregion
    }
}
