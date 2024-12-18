using System.Collections.Generic;

namespace SwiftCollections.Pool
{
    /// <summary>
    /// A Pool for HashSets.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class SwiftHashSetPool<T>
    {
        public SwiftHashSet<T> Rent()
        {
            return CollectionPool<SwiftHashSet<T>, T>.Rent();
        }

        public void Release(SwiftHashSet<T> set)
        {
            if (set != null)
            {
                set.Clear();
                CollectionPool<SwiftHashSet<T>, T>.Release(set);
            }
        }
    }
}
