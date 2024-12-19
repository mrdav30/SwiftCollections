using System.Collections.Generic;

namespace SwiftCollections.Pool
{
    /// <summary>
    /// A generic pool for managing collections such as <see cref="List{T}"/>, <see cref="HashSet{T}"/>, 
    /// <see cref="Dictionary{TKey, TValue}"/>, and other types implementing <see cref="ICollection{T}"/>.
    /// Provides efficient reuse of collection instances to reduce memory allocations.
    /// </summary>
    /// <typeparam name="TCollection">The type of the collection being pooled. Must implement <see cref="ICollection{TItem}"/> and have a parameterless constructor.</typeparam>
    /// <typeparam name="TItem">The type of items contained in the collection.</typeparam>
    public static class SwiftCollectionPool<TCollection, TItem> where TCollection : class, ICollection<TItem>, new()
    {
        #region Fields

        /// <summary>
        /// Internal object pool for managing the lifecycle of pooled collections.
        /// </summary>
        private static readonly SwiftObjectPool<TCollection> _collectionPool = new SwiftObjectPool<TCollection>(
            createFunc: () => new TCollection(),
            actionOnRelease: collection => collection.Clear()
        );

        #endregion

        #region Methods

        /// <summary>
        /// Rents a collection from the pool. If the pool is empty, a new collection is created.
        /// </summary>
        /// <returns>A collection instance of type <typeparamref name="TCollection"/>.</returns>
        public static TCollection Rent()
        {
            return _collectionPool.Rent();
        }

        /// <summary>
        /// Rents a collection from the pool and wraps it in a <see cref="SwiftPooledObject{TCollection}"/> for automatic release.
        /// </summary>
        /// <param name="value">The rented collection.</param>
        /// <returns>A <see cref="SwiftPooledObject{TCollection}"/> instance wrapping the rented collection.</returns>
        public static SwiftPooledObject<TCollection> Get(out TCollection value)
        {
            return _collectionPool.Rent(out value);
        }

        /// <summary>
        /// Releases a collection back to the pool for reuse.
        /// </summary>
        /// <param name="toRelease">The collection to release.</param>
        public static void Release(TCollection toRelease)
        {
            _collectionPool.Release(toRelease);
        }

        /// <summary>
        /// Clears all collections from the pool.
        /// </summary>
        public static void Clear()
        {
            _collectionPool.Clear();
        }

        #endregion
    }
}
