using System.Collections.Generic;

namespace SwiftCollections.Pool
{
    /// <summary>
    ///  A Collection such as List, HashSet, Dictionary etc can be pooled and reused by using a CollectionPool.
    /// </summary>
    /// <typeparam name="TCollection"></typeparam>
    /// <typeparam name="TItem"></typeparam>   
    public static class CollectionPool<TCollection, TItem> where TCollection : class, ICollection<TItem>, new()
    {
        internal static readonly SwiftObjectPool<TCollection> _collectionPool = new SwiftObjectPool<TCollection>(() => new TCollection(), null, delegate (TCollection l)
        {
            l.Clear();
        });

        public static TCollection Rent()
        {
            return _collectionPool.Rent();
        }

        public static SwiftPooledObject<TCollection> Get(out TCollection value)
        {
            return _collectionPool.Rent(out value);
        }

        public static void Release(TCollection toRelease)
        {
            _collectionPool.Release(toRelease);
        }
    }
}
