//=======================================================================
// SwiftStack.SwiftStackEnumerator.cs
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

public sealed partial class SwiftStack<T>
{
    /// <summary>
    /// Supports simple iteration over the elements of a <see cref="T:SwiftCollections.SwiftStack`1" /> in
    /// last-in, first-out (LIFO) order.
    /// </summary>
    /// <remarks>
    /// Obtain this value-type enumerator from the owning collection; default construction is not an initialized
    /// enumeration. MoveNext and Reset check the captured structural version; Current does not validate the
    /// version. Do not mutate through backing storage or borrowed references while iterating. Copies of the
    /// enumerator advance independently.
    /// </remarks>
    public struct SwiftStackEnumerator : IEnumerator<T>, IEnumerator, IDisposable
    {
        private readonly SwiftStack<T> _stack;
        private readonly T[] _array;
        private readonly uint _version;
        private readonly int _count;
        private int _index;

        private T _current;

        internal SwiftStackEnumerator(SwiftStack<T> stack)
        {
            _stack = stack;
            _array = stack._innerArray;
            _count = stack._count;
            _version = stack._version;
            _index = -2; // Enumerator not started
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
        public T Current => _current;

        /// <summary>
        /// Gets the selected enumeration current.
        /// </summary>
        /// <value>The selected value, valid only after MoveNext returns true and before the next
        /// advance/reset/dispose.</value>
        /// <exception cref="T:System.InvalidOperationException">Enumeration is before its first element or past
        /// its last element.</exception>
        object IEnumerator.Current
        {
            get
            {
                ChronicleGuard.ThrowIfTrue((uint)_index > _count, message: "Bad enumeration");
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
            ChronicleGuard.ThrowIfTrue(_version != _stack._version, message: "Enumerator modified outside of enumeration!");

            if (_index == -2)
            {
                _index = _count - 1;
            }
            else
            {
                _index--;
            }

            if (_index >= 0)
            {
                _current = _array[_index];
                return true;
            }

            _index = -1;
            _current = default!;
            return false;
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
            ChronicleGuard.ThrowIfTrue(_version != _stack._version, message: "Enumerator modified outside of enumeration!");

            _index = -2;
            _current = default!;
        }

        /// <summary>
        /// Resets the position marker without releasing collection storage.
        /// </summary>
        /// <remarks>
        /// Does not dispose the collection or clear stored element references. The struct still retains its
        /// owner/storage references. Do not use Current after disposal.
        /// </remarks>
        public void Dispose() => _index = -1;
    }
}
