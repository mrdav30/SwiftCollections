//=======================================================================
// SwiftListPool.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System.Threading;
using SwiftCollections.Lazy;

namespace SwiftCollections.Pool;

/// <summary>
/// A specialized pool for managing <see cref="T:SwiftCollections.SwiftList`1" /> instances, providing efficient
/// reuse to minimize memory allocations and improve performance.
/// </summary>
/// <typeparam name="T">The type of elements in the list.</typeparam>
/// <remarks>
/// Rented objects have exclusive caller ownership until returned exactly once. Foreign objects and double returns
/// are not detected and can break ownership/counts. Callback exceptions propagate; operations are not
/// transactional. Use external synchronization for lifecycle changes and host callbacks.
/// </remarks>
public sealed class SwiftListPool<T> : SwiftCollectionPool<SwiftList<T>, T>
{
    #region Singleton Instance

    /// <summary>
    /// Shared instance of the hash set pool, providing a globally accessible pool.
    /// Uses <see cref="SwiftLazyDisposable{T}"/> to ensure lazy initialization and proper disposal.
    /// </summary>
    private readonly static SwiftLazyDisposable<SwiftListPool<T>> _lazyInstance =
        new(() => new SwiftListPool<T>(), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Gets the shared instance of the pool.
    /// </summary>
    /// <value>The process-wide lazily created pool for this closed generic type.</value>
    /// <remarks>
    /// The shared instance is reused and is not recreated after disposal. Prefer Clear for reusable cleanup;
    /// disposing Shared affects every consumer.
    /// </remarks>
    public static SwiftListPool<T> Shared => _lazyInstance.Value;

    #endregion

    /// <summary>
    /// Creates a lazy pool for list instances.
    /// </summary>
    public SwiftListPool() { }

    /// <summary>
    /// Releases a list instance back to the pool for reuse.
    /// </summary>
    /// <param name="list">The object to return exactly once; do not use it afterward. Null is ignored by this
    /// wrapper.</param>
    /// <exception cref="T:System.ObjectDisposedException">The pool is disposed; wrapper null handling occurs
    /// after this check.</exception>
    /// <remarks>
    /// Ownership returns to the pool. The release callback runs before caching; overflow invokes the destroy
    /// callback. Collection wrappers clear logical contents and preserve capacity; this does not promise full
    /// retained-reference cleanup of unused storage.
    /// </remarks>
    public override void Release(SwiftList<T> list) => ReleaseTyped(list);
}
