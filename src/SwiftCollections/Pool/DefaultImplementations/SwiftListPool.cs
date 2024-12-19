namespace SwiftCollections.Pool
{
    /// <summary>
    /// A specialized pool for managing <see cref="SwiftList{T}"/> instances,
    /// providing efficient reuse to minimize memory allocations and improve performance.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    public sealed class SwiftListPool<T>
    {
        #region Methods

        /// <summary>
        /// Rents a list instance from the pool. If the pool is empty, a new list is created.
        /// </summary>
        /// <returns>A <see cref="SwiftList{T}"/> instance.</returns>
        public SwiftList<T> Rent()
        {
            return SwiftCollectionPool<SwiftList<T>, T>.Rent();
        }

        /// <summary>
        /// Releases a list instance back to the pool for reuse.
        /// </summary>
        /// <param name="list">The list to release.</param>
        /// <remarks>
        /// The list will be cleared before being returned to the pool to ensure it contains no stale data.
        /// </remarks>
        public void Release(SwiftList<T> list)
        {
            if (list == null) return;

            // Ensure the list is clear before returning it to the pool
            list.Clear();
            SwiftCollectionPool<SwiftList<T>, T>.Release(list);
        }

        /// <summary>
        /// Clears all pooled lists from the pool.
        /// </summary>
        public void Clear()
        {
            SwiftCollectionPool<SwiftList<T>, T>.Clear();
        }

        #endregion
    }
}
