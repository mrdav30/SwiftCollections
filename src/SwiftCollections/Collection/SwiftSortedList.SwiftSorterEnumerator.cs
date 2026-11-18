//=======================================================================
// SwiftSortedList.SwiftSorterEnumerator.cs
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

public partial class SwiftSortedList<T>
{
    /// <summary>
    /// Supports simple iteration over the elements of a <see cref="T:SwiftCollections.SwiftSortedList`1" /> in
    /// sorted order.
    /// </summary>
    /// <remarks>
    /// Obtain this value-type enumerator from the owning collection; default construction is not an initialized
    /// enumeration. MoveNext and Reset check the captured structural version; Current does not validate the
    /// version. Do not mutate through backing storage or borrowed references while iterating. Copies of the
    /// enumerator advance independently.
    /// </remarks>
    public struct SwiftSorterEnumerator : IEnumerator<T>, IEnumerator, IDisposable
    {
        private readonly SwiftSortedList<T> _list;
        private readonly uint _count;
        private readonly uint _version;
        private int _index;

        private T _current;

        internal SwiftSorterEnumerator(SwiftSortedList<T> sortedList)
        {
            _list = sortedList;
            _count = (uint)sortedList._count;
            _version = sortedList._version;
            _index = 0;
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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                ChronicleGuard.ThrowIfTrue(_index == 0 || (uint)_index > _count, message: "Enumerator is past the end of the collection.");
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
            ChronicleGuard.ThrowIfTrue(_version != _list._version, message: "Enumerator modified outside of enumeration!");

            if (_index < _count)
            {
                _current = _list._innerArray[_list.GetPhysicalIndex(_index)];
                _index++;
                return true;
            }

            _index = _list._count + 1;
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
            ChronicleGuard.ThrowIfTrue(_version != _list._version, message: "Enumerator modified outside of enumeration!");

            _index = 0;
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
