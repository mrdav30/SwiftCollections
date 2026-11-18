//=======================================================================
// SwiftQueue.SwiftQueueEnumerator.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Chronicler.Diagnostics;

namespace SwiftCollections;

public sealed partial class SwiftQueue<T>
{
    /// <summary>
    /// Enumerates the elements of a <see cref="T:SwiftCollections.SwiftQueue`1" /> in the order they would be
    /// dequeued.
    /// </summary>
    /// <remarks>
    /// Obtain this value-type enumerator from the owning collection; default construction is not an initialized
    /// enumeration. MoveNext and Reset check the captured structural version; Current does not validate the
    /// version. Do not mutate through backing storage or borrowed references while iterating. Copies of the
    /// enumerator advance independently.
    /// </remarks>
    public struct SwiftQueueEnumerator : IEnumerator<T>, IEnumerator, IDisposable
    {
        private readonly SwiftQueue<T> _queue;
        private readonly T[] _array;
        private readonly uint _version;
        private uint _index;
        private uint _currentIndex;

        private T _current;

        internal SwiftQueueEnumerator(SwiftQueue<T> queue)
        {
            _queue = queue;
            _array = queue._innerArray;
            _version = queue._version;
            _index = 0;
            _currentIndex = (uint)queue._head - 1;
            _current = default!;
        }

        /// <summary>
        /// Gets the value cached by the last successful MoveNext.
        /// </summary>
        /// <value>The cached value. Read only after MoveNext returns true and before the next
        /// advance/reset/dispose.</value>
        /// <remarks>
        /// The generic getter does not check position or collection version. Outside a valid position it can
        /// return default or a previous value; no exception is guaranteed. The non-generic IEnumerator getter
        /// validates position.
        /// </remarks>
        public readonly T Current => _current;

        /// <summary>
        /// Gets the selected enumeration current.
        /// </summary>
        /// <value>The selected value, valid only after MoveNext returns true and before the next
        /// advance/reset/dispose.</value>
        /// <exception cref="T:System.InvalidOperationException">Enumeration is before its first element or past
        /// its last element.</exception>
        readonly object IEnumerator.Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                ChronicleGuard.ThrowIfTrue(_index == 0 || _index > (uint)_queue._count, message: "Bad enumeration");
                return _current!;
            }
        }

        /// <summary>
        /// Advances to the next live element.
        /// </summary>
        /// <returns>true if an element was selected and Current is valid; false when enumeration is
        /// exhausted.</returns>
        /// <exception cref="T:System.InvalidOperationException">The captured collection version differs from its
        /// current version.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            ChronicleGuard.ThrowIfTrue(_version != _queue._version, message: "Collection was modified during enumeration.");

            _index++;
            if (_index > (uint)_queue._count) return false;
            _currentIndex++;
            if (_currentIndex == _array.Length) _currentIndex = 0;
            _current = _array[_currentIndex];
            return true;
        }

        /// <summary>
        /// Rewinds the enumerator to before its first element.
        /// </summary>
        /// <exception cref="T:System.InvalidOperationException">The collection version changed since this
        /// enumerator was created.</exception>
        /// <remarks>
        /// Current becomes default. The same captured structural version is retained.
        /// </remarks>
        public void Reset()
        {
            ChronicleGuard.ThrowIfTrue(_version != _queue._version, message: "Collection was modified during enumeration.");

            _index = 0;
            _currentIndex = (uint)_queue._head - 1;
            _current = default!;
        }

        /// <summary>
        /// Resets the position marker without releasing collection storage.
        /// </summary>
        /// <remarks>
        /// Does not dispose the collection or clear stored element references. The struct still retains its
        /// owner/storage references. Do not use Current after disposal.
        /// </remarks>
        public void Dispose() => _index = 0;
    }
}
