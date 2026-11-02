//=======================================================================
// SwiftSparseMap.SwiftSparseMapEnumerator.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================


using System.Collections;
using System.Collections.Generic;
using Chronicler.Diagnostics;

namespace SwiftCollections;

public sealed partial class SwiftSparseMap<T>
{
    /// <summary>
    /// Supports iteration over the key/value pairs in a <see cref="SwiftSparseMap{T}"/> collection.
    /// </summary>
    /// <remarks>
    /// The enumerator provides a forward-only, read-only traversal of the collection.
    /// It is invalidated if the underlying collection is modified during enumeration, and any such modification will cause
    /// subsequent operations to throw an InvalidOperationException.
    /// </remarks>
    public struct SwiftSparseMapEnumerator : IEnumerator<KeyValuePair<int, T>>
    {
        private readonly SwiftSparseMap<T> _map;
        private readonly int[] _keys;
        private readonly T[] _values;
        private readonly int _count;
        private readonly uint _version;
        private int _index;

        internal SwiftSparseMapEnumerator(SwiftSparseMap<T> map)
        {
            _map = map;
            _keys = map._denseKeys;
            _values = map._denseValues;
            _count = map._count;
            _version = map._version;
            _index = -1;
            Current = default;
        }

        /// <inheritdoc/>
        public KeyValuePair<int, T> Current { get; private set; }
        object IEnumerator.Current
        {
            get
            {
                ChronicleGuard.ThrowIfTrue((uint)_index >= (uint)_count, message: "Enumerator is before the first element or after the last element.");
                return Current;
            }
        }

        /// <inheritdoc/>
        public bool MoveNext()
        {
            ChronicleGuard.ThrowIfTrue(_version != _map._version, message: "Collection was modified during enumeration.");

            int next = _index + 1;
            if (next >= _count)
            {
                _index = _count;
                Current = default;
                return false;
            }

            _index = next;
            Current = new KeyValuePair<int, T>(_keys[_index], _values[_index]);
            return true;
        }

        /// <inheritdoc/>
        public void Reset()
        {
            ChronicleGuard.ThrowIfTrue(_version != _map._version, message: "Collection was modified during enumeration.");

            _index = -1;
            Current = default;
        }

        /// <inheritdoc/>
        public void Dispose() => _index = -1;
    }
}
