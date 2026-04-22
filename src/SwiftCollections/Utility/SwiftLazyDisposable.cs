using System;
using System.Threading;

namespace SwiftCollections;

/// <summary>
/// A lazily initialized disposable object.
/// This class extends <see cref="Lazy{T}"/> to support <see cref="IDisposable"/> objects,
/// ensuring proper resource cleanup when disposed.
/// </summary>
/// <typeparam name="T">The type of the lazily initialized object, which must implement <see cref="IDisposable"/>.</typeparam>
public class SwiftLazyDisposable<T> : Lazy<T>, IDisposable where T : IDisposable
{
    #region Fields

    /// <summary>
    /// Indicates whether the lazy instance has been disposed.
    /// Prevents multiple disposal attempts, ensuring safe resource cleanup.
    /// </summary>
    private volatile bool _disposed;

    #endregion

    #region Constructors

    /// <summary>
    ///  Initializes a new instance of the <see cref="SwiftLazyDisposable{T}"/> class.
    ///  When lazy initialization occurs, the default constructor is used.
    /// </summary>
    public SwiftLazyDisposable() : base() { }

    /// <summary>
    ///  Initializes a new instance of the <see cref="SwiftLazyDisposable{T}"/> class.
    ///  When lazy initialization occurs, the default constructor of the target type
    ///  and the specified initialization mode are used.
    /// </summary>
    /// <param name="isThreadSafe">
    ///  true to make this instance usable concurrently by multiple threads;
    ///  false to make the instance usable by only one thread at a time. 
    /// </param>
    public SwiftLazyDisposable(bool isThreadSafe) : base(isThreadSafe) { }

    /// <summary>
    ///  Initializes a new instance of the <see cref="SwiftLazyDisposable{T}"/> class
    ///  that uses the default constructor of T and the specified thread-safety mode.
    /// </summary>
    /// <param name="mode">
    ///  One of the enumeration values that specifies the thread safety mode. 
    /// </param>
    public SwiftLazyDisposable(LazyThreadSafetyMode mode) : base(mode) { }

    /// <summary>
    ///  Initializes a new instance of the <see cref="SwiftLazyDisposable{T}"/> class.
    ///  When lazy initialization occurs, the specified initialization function is used.
    /// </summary>
    /// <param name="valueFactory">
    ///  The delegate that is invoked to produce the lazily initialized value when it is needed. 
    /// </param>
    public SwiftLazyDisposable(Func<T> valueFactory) : base(valueFactory) { }

    /// <summary>
    ///  Initializes a new instance of the <see cref="SwiftLazyDisposable{T}"/> class.
    ///  When lazy initialization occurs, the specified initialization function
    ///  and initialization mode are used.
    /// </summary>
    /// <param name="valueFactory">
    ///  The delegate that is invoked to produce the lazily initialized value when it is needed. 
    /// </param>
    /// <param name="isThreadSafe">
    ///  true to make this instance usable concurrently by multiple threads;
    ///  false to make this instance usable by only one thread at a time. 
    /// </param>
    public SwiftLazyDisposable(Func<T> valueFactory, bool isThreadSafe) : base(valueFactory, isThreadSafe) { }

    /// <summary>
    ///  Initializes a new instance of the <see cref="SwiftLazyDisposable{T}"/> class
    ///  using the specified initialization function and thread-safety mode.
    /// </summary>
    /// <param name="valueFactory">
    ///  The delegate that is invoked to produce the lazily initialized value when it is needed. 
    /// </param>
    /// <param name="mode">
    ///  One of the enumeration values that specifies the thread safety mode. 
    /// </param>
    public SwiftLazyDisposable(Func<T> valueFactory, LazyThreadSafetyMode mode) : base(valueFactory, mode) { }

    #endregion

    #region Utility

    /// <summary>
    /// Returns a string that represents the current state of the object.
    /// </summary>
    /// <remarks>
    /// This method provides a user-friendly description of the object's state, 
    /// which can be useful for debugging or logging purposes.
    /// </remarks>
    /// <returns>
    /// A string representation of the value if it has been created; otherwise, 
    /// a string indicating that the value has not been created.
    /// </returns>
    public override string ToString() => IsValueCreated ? Value.ToString() ?? string.Empty : "LazyDisposable (Not Created)";

    #endregion

    #region IDisposable Implementation

    /// <summary>
    /// Disposes the lazily initialized value if it has been created.
    /// Ensures that disposal is only performed once.
    /// </summary>
    public void Dispose()
    {
        if (_disposed || !this.IsValueCreated)
            return;

        _disposed = true;
        this.Value.Dispose();

        // Suppress finalization since we've manually disposed
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizer to ensure disposal if Dispose() is never called.
    /// </summary>
    ~SwiftLazyDisposable() => Dispose();

    #endregion
}
