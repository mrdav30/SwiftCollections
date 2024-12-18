using System.Collections.Generic;

namespace SwiftCollections.Pool
{
    public sealed class SwiftDictionaryPool<TKey, TValue>
    {
        public SwiftDictionary<TKey, TValue> Rent()
        {
            return CollectionPool<SwiftDictionary<TKey, TValue>, KeyValuePair<TKey, TValue>>.Rent();
        }

        public void Release(SwiftDictionary<TKey, TValue> toRelease)
        {
            CollectionPool<SwiftDictionary<TKey, TValue>, KeyValuePair<TKey, TValue>>.Release(toRelease);
        }
    }
}
