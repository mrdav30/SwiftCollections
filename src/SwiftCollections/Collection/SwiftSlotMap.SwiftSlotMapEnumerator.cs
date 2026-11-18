//=======================================================================
// SwiftSlotMap.SwiftSlotMapEnumerator.cs
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

public sealed partial class SwiftSlotMap<T>
{
    /// <summary>
    /// Enumerates the elements of a <see cref="T:SwiftCollections.SwiftSlotMap`1" /> collection.
    /// </summary>
    /// <remarks>
    /// Obtain this value-type enumerator from the owning collection; default construction is not an initialized
    /// enumeration. MoveNext and Reset check the captured structural version; Current does not validate the
    /// version. Do not mutate through backing storage or borrowed references while iterating. Copies of the
    /// enumerator advance independently.
    /// </remarks>
    public struct SwiftSlotMapEnumerator : IEnumerator<T>, IDisposable
    {
        private readonly SwiftSlotMap<T> _bucket;
        private readonly Entry[] _entries;
        private readonly uint _version;
        private int _index;
        private T _current;

        internal SwiftSlotMapEnumerator(SwiftSlotMap<T> map)
        {
            _bucket = map;
            _entries = map._innerArray;
            _version = map._version;
            _index = -1;
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
                ChronicleGuard.ThrowIfTrue((uint)_index >= (uint)_bucket._peakCount, message: "Enumerator is before the first element or after the last element.");
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
            ChronicleGuard.ThrowIfTrue(_version != _bucket._version, message: "Enumerator modified outside of enumeration!");

            uint count = (uint)_bucket._peakCount;
            while (++_index < count)
            {
                if (_entries[_index].IsUsed)
                {
                    _current = _entries[_index].Value;
                    return true;
                }
            }
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
            ChronicleGuard.ThrowIfTrue(_version != _bucket._version, message: "Enumerator modified outside of enumeration!");

            _index = -1;
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
