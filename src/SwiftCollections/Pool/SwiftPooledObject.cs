using System;

namespace SwiftCollections.Pool
{
    /// <summary>
    /// A struct that wraps an object rented from an <see cref="ISwiftObjectPool{T}"/> and ensures it is automatically
    /// released back to the pool when disposed. Designed to simplify resource management and avoid manual release errors.
    /// </summary>
    /// <typeparam name="T">The type of the object being pooled. Must be a reference type.</typeparam>
    public readonly struct SwiftPooledObject<T> : IDisposable where T : class
    {
        #region Fields

        /// <summary>
        /// The rented object that will be returned to the pool upon disposal.
        /// </summary>
        private readonly T _value;

        /// <summary>
        /// The pool from which the object was rented.
        /// </summary>
        private readonly ISwiftObjectPool<T> _pool;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="SwiftPooledObject{T}"/> struct.
        /// </summary>
        /// <param name="value">The rented object.</param>
        /// <param name="pool">The pool that owns the object.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> or <paramref name="pool"/> is null.</exception>
        internal SwiftPooledObject(T value, ISwiftObjectPool<T> pool)
        {
            if (value == null) ThrowHelper.ThrowArgumentNullException(nameof(value));
            if (pool == null) ThrowHelper.ThrowArgumentNullException(nameof(pool));

            _value = value;
            _pool = pool;
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Releases the rented object back to its pool.
        /// </summary>
        /// <remarks>
        /// This method is automatically called when the <see cref="SwiftPooledObject{T}"/> goes out of scope in a
        /// using block or when manually disposed.
        /// </remarks>
        public void Dispose()
        {
            if (_pool != null && _value != null)
                _pool.Release(_value);
        }

        #endregion
    }
}
