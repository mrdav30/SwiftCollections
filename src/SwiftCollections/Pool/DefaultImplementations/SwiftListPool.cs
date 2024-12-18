namespace SwiftCollections.Pool
{
    /// <summary>
    /// A Pool for SwiftLists.
    /// </summary>
    /// <typeparam name="T"></typeparam>    
    public sealed class SwiftListPool<T>
    {
        public SwiftList<T> Rent()
        {
            return CollectionPool<SwiftList<T>, T>.Rent();
        }

        public void Release(SwiftList<T> list)
        {
            if (list != null)
            {
                list.Clear();
                CollectionPool<SwiftList<T>, T>.Release(list);
            }
        }
    }
}
