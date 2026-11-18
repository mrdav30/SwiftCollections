//=======================================================================
// SwiftLazyDisposable.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Threading;

namespace SwiftCollections.Lazy;

/// <summary>
/// Lazily creates a value and supports disposal after initialization.
/// </summary>
/// <typeparam name="T">The type of the lazily initialized object, which must implement <see
/// cref="T:System.IDisposable" />.</typeparam>
/// <remarks>
/// Lazy initialization uses the selected Lazy&lt;T&gt; thread-safety mode. Disposal itself is not synchronized
/// with initialization or other disposal calls. Disposing before value creation does nothing and does not prevent
/// later initialization. Value remains accessible after disposal and can return a disposed object; coordinate
/// ownership externally.
/// </remarks>
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
    /// Initializes a new instance of the <see cref="T:SwiftCollections.Lazy.SwiftLazyDisposable`1" /> class. When
    /// lazy initialization occurs, the default constructor is used.
    /// </summary>
    public SwiftLazyDisposable() : base() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.Lazy.SwiftLazyDisposable`1" /> class. When
    /// lazy initialization occurs, the default constructor of the target type and the specified initialization
    /// mode are used.
    /// </summary>
    /// <param name="isThreadSafe">true selects ExecutionAndPublication for initialization; false selects None.
    /// This does not synchronize Dispose.</param>
    public SwiftLazyDisposable(bool isThreadSafe) : base(isThreadSafe) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.Lazy.SwiftLazyDisposable`1" /> class that
    /// uses the default constructor of T and the specified thread-safety mode.
    /// </summary>
    /// <param name="mode">The Lazy initialization mode; this does not define disposal synchronization.</param>
    /// <exception cref="T:System.ArgumentOutOfRangeException">mode is not a defined
    /// LazyThreadSafetyMode.</exception>
    public SwiftLazyDisposable(LazyThreadSafetyMode mode) : base(mode) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.Lazy.SwiftLazyDisposable`1" /> class. When
    /// lazy initialization occurs, the specified initialization function is used.
    /// </summary>
    /// <param name="valueFactory">The retained non-null factory; invoked according to Lazy initialization
    /// rules.</param>
    /// <exception cref="T:System.ArgumentNullException">valueFactory is null.</exception>
    public SwiftLazyDisposable(Func<T> valueFactory) : base(valueFactory) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.Lazy.SwiftLazyDisposable`1" /> class. When
    /// lazy initialization occurs, the specified initialization function and initialization mode are used.
    /// </summary>
    /// <param name="valueFactory">The retained non-null factory; invoked according to Lazy initialization
    /// rules.</param>
    /// <param name="isThreadSafe">true selects ExecutionAndPublication for initialization; false selects None.
    /// This does not synchronize Dispose.</param>
    /// <exception cref="T:System.ArgumentNullException">valueFactory is null.</exception>
    public SwiftLazyDisposable(Func<T> valueFactory, bool isThreadSafe) : base(valueFactory, isThreadSafe) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.Lazy.SwiftLazyDisposable`1" /> class using
    /// the specified initialization function and thread-safety mode.
    /// </summary>
    /// <param name="valueFactory">The retained non-null factory; invoked according to Lazy initialization
    /// rules.</param>
    /// <param name="mode">The Lazy initialization mode; this does not define disposal synchronization.</param>
    /// <exception cref="T:System.ArgumentNullException">valueFactory is null.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">mode is not a defined
    /// LazyThreadSafetyMode.</exception>
    public SwiftLazyDisposable(Func<T> valueFactory, LazyThreadSafetyMode mode) : base(valueFactory, mode) { }

    #endregion

    #region Utility

    /// <summary>
    /// Returns a string that represents the current state of the object.
    /// </summary>
    /// <returns> A string representation of the value if it has been created; otherwise, a string indicating that
    /// the value has not been created. </returns>
    /// <remarks>
    /// This method provides a user-friendly description of the object's state, which can be useful for debugging
    /// or logging purposes.
    /// </remarks>
    public override string ToString() => IsValueCreated ? Value.ToString() ?? string.Empty : "LazyDisposable (Not Created)";

    #endregion

    #region IDisposable Implementation

    /// <summary>
    /// Disposes an already created value on the first successful disposal path.
    /// </summary>
    /// <remarks>
    /// Does not force initialization. If no value exists, changes nothing; a later call can dispose a
    /// subsequently created value. Marks disposed before calling T.Dispose, so a throwing disposal is not
    /// retried. Concurrent Dispose calls are not guaranteed exactly once.
    /// </remarks>
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
    /// Attempts to dispose an already created value during finalization.
    /// </summary>
    ~SwiftLazyDisposable() => Dispose();

    #endregion
}
