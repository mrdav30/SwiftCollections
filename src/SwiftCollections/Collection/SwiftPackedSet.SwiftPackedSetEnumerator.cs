//=======================================================================
// SwiftPackedSet.SwiftPackedSetEnumerator.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================


using System.Collections;
using System.Collections.Generic;
using Chronicler.Diagnostics;

namespace SwiftCollections;

public sealed partial class SwiftPackedSet<T>
    where T : notnull
{
    /// <summary>
    /// Enumerates the elements of a <see cref="T:SwiftCollections.SwiftPackedSet`1" /> collection.
    /// </summary>
    /// <remarks>
    /// Obtain this value-type enumerator from the owning collection; default construction is not an initialized
    /// enumeration. MoveNext and Reset check the captured structural version; Current does not validate the
    /// version. Do not mutate through backing storage or borrowed references while iterating. Copies of the
    /// enumerator advance independently.
    /// </remarks>
    public struct SwiftPackedSetEnumerator : IEnumerator<T>
    {
        private readonly SwiftPackedSet<T> _set;
        private readonly uint _version;
        private int _index;

        internal SwiftPackedSetEnumerator(SwiftPackedSet<T> set)
        {
            _set = set;
            _version = set._version;
            _index = -1;
            Current = default!;
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
        public T Current { get; private set; }

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
                ChronicleGuard.ThrowIfTrue((uint)_index >= (uint)_set._count, message: "Enumerator is before the first element or after the last element.");
                return Current!;
            }
        }

        /// <summary>
        /// Advances to the next live element.
        /// </summary>
        /// <returns>true if an element was selected and Current is valid; false when enumeration is
        /// exhausted.</returns>
        /// <exception cref="T:System.InvalidOperationException">The captured collection version differs from its
        /// current version.</exception>
        public bool MoveNext()
        {
            ChronicleGuard.ThrowIfTrue(_version != _set._version, message: "Collection was modified during enumeration.");

            int next = _index + 1;
            if (next >= _set._count)
            {
                _index = _set._count;
                Current = default!;
                return false;
            }

            _index = next;
            Current = _set._dense[next];
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
            ChronicleGuard.ThrowIfTrue(_version != _set._version, message: "Collection was modified during enumeration.");

            _index = -1;
            Current = default!;
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
