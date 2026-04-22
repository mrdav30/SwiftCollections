using System;
using System.Threading;

namespace SwiftCollections.Pool;

/// <summary>
/// A specialized pool for managing <see cref="SwiftStack{T}"/> instances,
/// providing efficient reuse to minimize memory allocations and improve performance.
/// </summary>
/// <typeparam name="T">The type of elements in the stack.</typeparam>
public sealed class SwiftStackPool<T> : SwiftCollectionPool<SwiftStack<T>, T>, IDisposable
{
    #region Singleton Instance

    /// <summary>
    /// Shared instance of the stack pool, providing a globally accessible pool.
    /// Uses <see cref="SwiftLazyDisposable{T}"/> to ensure lazy initialization and proper disposal.
    /// </summary>
    private readonly static SwiftLazyDisposable<SwiftStackPool<T>> _lazyInstance =
        new(() => new SwiftStackPool<T>(), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Gets the shared instance of the pool.
    /// </summary>
    public static SwiftStackPool<T> Shared => _lazyInstance.Value;

    #endregion

    #region Fields

    /// <summary>
    /// Tracks whether the pool has been disposed.
    /// </summary>
    private volatile bool _disposed;

    #endregion

    #region Methods

    /// <summary>
    /// Rents a stack instance from the pool. If the pool is empty, a new stack is created.
    /// </summary>
    /// <returns>A <see cref="SwiftStack{T}"/> instance.</returns>
    public override SwiftStack<T> Rent()
    {
        SwiftThrowHelper.ThrowIfDisposed(_disposed, nameof(SwiftStackPool<T>));

        return base.Rent();
    }

    /// <summary>
    /// Releases a stack instance back to the pool for reuse.
    /// </summary>
    /// <param name="stack">The stack to release.</param>
    public override void Release(SwiftStack<T> stack)
    {
        SwiftThrowHelper.ThrowIfDisposed(_disposed, nameof(SwiftStackPool<T>));

        if (stack == null) return;

        base.Release(stack);
    }

    /// <summary>
    /// Clears all pooled stacks from the pool.
    /// </summary>
    public override void Clear()
    {
        if (_disposed) return;

        base.Clear();
    }

    #endregion

    #region IDisposable Implementation

    /// <summary>
    /// Releases all resources used by the SwiftStackPool.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        Clear();
        base.Flush();

        _disposed = true;

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the resources used by the SwiftStackPool instance.
    /// </summary>
    /// <remarks>
    /// This finalizer ensures that unmanaged resources are released if Dispose was not called explicitly. 
    /// It is recommended to call Dispose to release resources deterministically.
    /// </remarks>
    ~SwiftStackPool() => Dispose();

    #endregion
}
