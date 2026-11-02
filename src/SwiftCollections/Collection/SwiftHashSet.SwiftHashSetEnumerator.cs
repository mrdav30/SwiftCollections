//=======================================================================
// SwiftHashSet.SwiftHashSetEnumerator.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using Chronicler.Diagnostics;

namespace SwiftCollections;

public sealed partial class SwiftHashSet<T>
    where T : notnull
{
    /// <summary>
    /// Provides an enumerator for iterating through the elements of the hash set, ensuring consistency during enumeration.
    /// </summary>
    public struct SwiftHashSetEnumerator : IEnumerator<T>, IEnumerator, IDisposable
    {
        private readonly SwiftHashSet<T> _set;
        private readonly Entry[] _entries;
        private readonly uint _version;
        private int _index;
        private T _current;

        internal SwiftHashSetEnumerator(SwiftHashSet<T> set)
        {
            _set = set;
            _version = set._version;
            _entries = set._entries; // Cache the entry array
            _index = -1;
            _current = default!;
        }

        /// <inheritdoc/>
        public readonly T Current => _current;

        readonly object IEnumerator.Current
        {
            get
            {
                ChronicleGuard.ThrowIfTrue((uint)_index > (uint)_set._lastIndex, message: "Enumeration has either not started or has already finished.");
                return _current;
            }
        }

        /// <inheritdoc/>
        public bool MoveNext()
        {
            ChronicleGuard.ThrowIfTrue(_version != _set._version, message: "Collection was modified during enumeration.");

            uint last = (uint)_set._lastIndex;
            while (++_index <= last)
            {
                if (_entries[_index].IsUsed)
                {
                    _current = _entries[_index].Value;
                    return true;
                }
            }

            _current = default!;
            return false;
        }

        /// <inheritdoc/>
        public void Reset()
        {
            ChronicleGuard.ThrowIfTrue(_version != _set._version, message: "Collection was modified during enumeration.");

            _index = -1;
            _current = default!;
        }

        /// <inheritdoc/>
        public void Dispose() => _index = -1;
    }
}
