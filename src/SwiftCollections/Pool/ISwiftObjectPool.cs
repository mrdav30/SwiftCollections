namespace SwiftCollections.Pool
{
    public interface ISwiftObjectPool<T> where T : class
    {
        int CountInactive { get; }

        T Rent();

        SwiftPooledObject<T> Rent(out T v);

        void Release(T element);

        void Clear();
    }
}
