using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace SwiftCollections.Pool
{
    /// <summary>
    /// A generic object pooling class designed to efficiently reuse objects, reducing memory allocation overhead
    /// and improving performance. Provides thread-safe operations for creating, renting, and releasing objects.
    /// </summary>
    /// <typeparam name="T">The type of object to pool. Must be a reference type.</typeparam>
    public sealed class SwiftObjectPool<T> : IDisposable, ISwiftObjectPool<T> where T : class
    {
        #region Fields

        private readonly ConcurrentBag<T> _pool;
        private readonly Func<T> _createFunc;
        private readonly Action<T> _actionOnGet;
        private readonly Action<T> _actionOnRelease;
        private readonly Action<T> _actionOnDestroy;
        private readonly int _maxSize;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="SwiftObjectPool{T}"/> class.
        /// </summary>
        /// <param name="createFunc">A function used to create new instances of the object type.</param>
        /// <param name="actionOnGet">An optional action to perform when an object is rented from the pool.</param>
        /// <param name="actionOnRelease">An optional action to perform when an object is returned to the pool.</param>
        /// <param name="actionOnDestroy">An optional action to perform when an object is destroyed due to pool size constraints.</param>
        /// <param name="maxSize">The maximum number of objects the pool can hold.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="createFunc"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="maxSize"/> is less than or equal to 0.</exception>
        public SwiftObjectPool(
            Func<T> createFunc,
            Action<T> actionOnGet = null,
            Action<T> actionOnRelease = null,
            Action<T> actionOnDestroy = null,
            int maxSize = 100)
        {
            if (createFunc == null) ThrowHelper.ThrowArgumentNullException(nameof(createFunc));
            if (maxSize <= 0) ThrowHelper.ThrowArgumentException($"{nameof(maxSize)} must be greater than 0");

            _pool = new ConcurrentBag<T>();
            _createFunc = createFunc;
            _actionOnGet = actionOnGet;
            _actionOnRelease = actionOnRelease;
            _actionOnDestroy = actionOnDestroy;
            _maxSize = maxSize;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the total number of objects created by the pool, including both active and inactive objects.
        /// </summary>
        public int CountAll { get; private set; }

        /// <summary>
        /// Gets the number of objects currently in use (rented from the pool).
        /// </summary>
        public int CountActive => CountAll - CountInactive;

        /// <summary>
        /// Gets the number of objects currently available in the pool for rent.
        /// </summary>
        public int CountInactive => _pool.Count;

        #endregion

        #region Collection Manipulation

        /// <summary>
        /// Rents an object from the pool. If the pool is empty, a new object is created using the factory function.
        /// </summary>
        /// <returns>An instance of <typeparamref name="T"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if object creation fails.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Rent()
        {
            if (_pool.TryTake(out var obj))
            {
                _actionOnGet?.Invoke(obj);
                return obj;
            }

            var newObj = _createFunc();
            if (newObj == null) ThrowHelper.ThrowInvalidOperationException("Failed to create a new object.");
            CountAll++;
            _actionOnGet?.Invoke(newObj);
            return newObj;
        }

        /// <summary>
        /// Rents an object from the pool and wraps it in a <see cref="SwiftPooledObject{T}"/> for automatic release.
        /// </summary>
        /// <param name="value">The rented object.</param>
        /// <returns>A <see cref="SwiftPooledObject{T}"/> instance wrapping the rented object.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SwiftPooledObject<T> Rent(out T value) => new SwiftPooledObject<T>(value = Rent(), this);

        /// <summary>
        /// Releases an object back to the pool for reuse. If the pool has reached its maximum size, the object
        /// is destroyed using the configured destroy action.
        /// </summary>
        /// <param name="element">The object to release.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="element"/> is null.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Release(T element)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));

            _actionOnRelease?.Invoke(element);

            if (_pool.Count < _maxSize)
                _pool.Add(element);
            else
            {
                _actionOnDestroy?.Invoke(element);
                CountAll--;
            }
        }

        /// <summary>
        /// Clears all objects from the pool, destroying any active objects if a destroy action is configured.
        /// </summary>
        public void Clear()
        {
            while (_pool.TryTake(out var obj))
                _actionOnDestroy?.Invoke(obj);

            CountAll = 0;
        }

        #endregion

        #region IDisposable Implementation

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() => Clear();

        #endregion
    }
}
