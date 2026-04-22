namespace SwiftCollections.Pool;

/// <summary>
/// Defines a contract for an object pool that manages reusable instances of type T. 
/// Provides methods to rent and release objects to minimize allocations and improve performance.
/// </summary>
/// <remarks>
/// Implementations are not required to be thread-safe unless explicitly documented.
/// </remarks>
/// <typeparam name="T">The type of objects managed by the pool. Must be a reference type.</typeparam>
public interface ISwiftObjectPool<T> where T : class
{
    /// <summary>
    /// Gets the number of inactive items in the collection.
    /// </summary>
    int CountInactive { get; }

    /// <summary>
    /// Retrieves an instance of type T from the pool for use by the caller.
    /// </summary>
    /// <remarks>
    /// The caller is responsible for returning the rented instance to the pool to avoid resource leaks. 
    /// The specific behavior when the pool is empty depends on the implementation.
    /// </remarks>
    /// <returns>
    /// An instance of type T that is available for use. 
    /// The returned object should be returned to the pool when no longer needed.
    /// </returns>
    T Rent();

    /// <summary>
    /// Rents an object from the pool and assigns it to the specified output parameter.
    /// </summary>
    /// <remarks>
    /// The caller is responsible for disposing the returned <see cref="SwiftPooledObject{T}"/> to ensure the object is returned to the pool. 
    /// The output parameter provides direct access to the rented object for convenience.
    /// </remarks>
    /// <param name="v">When this method returns, contains the rented object of type T from the pool.</param>
    /// <returns>
    /// A <see cref="SwiftPooledObject{T}"/> instance that manages the lifetime of the rented object. 
    /// Disposing the returned value returns the object to the pool.
    /// </returns>
    SwiftPooledObject<T> Rent(out T v);

    /// <summary>
    /// Releases the specified element back to the pool or resource manager for reuse.
    /// </summary>
    /// <remarks>
    /// After calling this method, the element should not be used by the caller unless it is reacquired from the pool. 
    /// The behavior when releasing the same element multiple times depends on the implementation.
    /// </remarks>
    /// <param name="element">The element to release. Must not be null if the pool does not accept null values.</param>
    void Release(T element);

    /// <summary>
    /// Removes all items from the collection.
    /// </summary>
    /// <remarks>
    /// After calling this method, the collection will be empty. 
    /// This method should not modify the capacity of the collection, if applicable.
    /// </remarks>
    void Clear();
}
