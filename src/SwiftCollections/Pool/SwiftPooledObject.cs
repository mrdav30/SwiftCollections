//=======================================================================
// SwiftPooledObject.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Threading;
using Chronicler.Diagnostics;

namespace SwiftCollections.Pool;

/// <summary>
/// Represents a rental lease whose disposal attempts at most one return to its pool.
/// </summary>
/// <typeparam name="T">The type of the object being pooled. Must be a reference type.</typeparam>
/// <remarks>
/// A lease returns its object at most once. Dispose marks the lease disposed and drops its references before
/// Release; a throwing Release is not retried. Do not separately return the leased object or use it after
/// disposal.
/// </remarks>
public sealed class SwiftPooledObject<T> : IDisposable where T : class
{
    #region Fields

    /// <summary>
    /// The rented object that will be returned to the pool upon disposal.
    /// </summary>
    private T? _value;

    /// <summary>
    /// The pool from which the object was rented.
    /// </summary>
    private ISwiftObjectPool<T>? _pool;

    private int _disposed;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="SwiftPooledObject{T}"/> class.
    /// </summary>
    /// <param name="value">The rented object.</param>
    /// <param name="pool">The pool that owns the object.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> or <paramref name="pool"/> is null.</exception>
    internal SwiftPooledObject(T value, ISwiftObjectPool<T> pool)
    {
        ChronicleGuard.ThrowIfNull(value, nameof(value));
        ChronicleGuard.ThrowIfNull(pool, nameof(pool));

        _value = value;
        _pool = pool;
    }

    #endregion

    #region IDisposable Implementation

    /// <summary>
    /// Releases the rented object back to its pool.
    /// </summary>
    /// <remarks>
    /// Returns the rental at most once. Release exceptions propagate after the lease has irreversibly dropped
    /// ownership references; later Dispose calls do nothing.
    /// </remarks>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        ISwiftObjectPool<T>? pool = _pool;
        T? value = _value;

        _pool = null;
        _value = null;

        pool!.Release(value!);
    }

    #endregion
}
