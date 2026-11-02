//=======================================================================
// SwiftSparseSet.SwiftSparseSetEnumerator.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System.Collections;
using System.Collections.Generic;
using Chronicler.Diagnostics;

namespace SwiftCollections;

public sealed partial class SwiftSparseSet
{
    /// <summary>
    /// Supports iteration over IDs in a <see cref="SwiftSparseSet"/>.
    /// </summary>
    public struct SwiftSparseSetEnumerator : IEnumerator<int>
    {
        private readonly SwiftSparseSet _set;
        private readonly int[] _keys;
        private readonly int _count;
        private readonly uint _version;
        private int _index;

        internal SwiftSparseSetEnumerator(SwiftSparseSet set)
        {
            _set = set;
            _keys = set._denseKeys;
            _count = set._count;
            _version = set._version;
            _index = -1;
            Current = default;
        }

        /// <inheritdoc/>
        public int Current { get; private set; }
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
            ChronicleGuard.ThrowIfTrue(_version != _set._version, message: "Collection was modified during enumeration.");

            int next = _index + 1;
            if (next >= _count)
            {
                _index = _count;
                Current = default;
                return false;
            }

            _index = next;
            Current = _keys[_index];
            return true;
        }

        /// <inheritdoc/>
        public void Reset()
        {
            ChronicleGuard.ThrowIfTrue(_version != _set._version, message: "Collection was modified during enumeration.");

            _index = -1;
            Current = default;
        }

        /// <inheritdoc/>
        public void Dispose() => _index = -1;
    }
}
