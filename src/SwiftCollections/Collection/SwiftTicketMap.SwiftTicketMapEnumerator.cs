//=======================================================================
// SwiftTicketMap.SwiftTicketMapEnumerator.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================


using System.Collections;
using System.Collections.Generic;
using Chronicler.Diagnostics;

namespace SwiftCollections;

public sealed partial class SwiftTicketMap<T>
{
    /// <summary>
    /// Enumerates the elements of a <see cref="SwiftTicketMap{T}"/> collection in a forward-only, read-only manner.
    /// </summary>
    /// <remarks>
    /// The enumerator is invalidated if the underlying collection is modified during enumeration.
    /// In such cases, subsequent calls to MoveNext will throw an InvalidOperationException.
    /// This enumerator is typically obtained by calling GetEnumerator on a <see cref="SwiftTicketMap{T}"/> instance.
    /// </remarks>
    public struct SwiftTicketMapEnumerator : IEnumerator<T>
    {
        private readonly SwiftTicketMap<T> _bucket;
        private readonly uint _version;
        private int _index;
        private T _current;

        internal SwiftTicketMapEnumerator(SwiftTicketMap<T> map)
        {
            _bucket = map;
            _version = map._version;
            _index = -1;
            _current = default!;
        }

        /// <inheritdoc/>
        public readonly T Current => _current;

        readonly object IEnumerator.Current
        {
            get
            {
                ChronicleGuard.ThrowIfTrue((uint)_index >= (uint)_bucket._peak, message: "Enumerator is before the first element or after the last element.");
                return _current!;
            }
        }

        /// <inheritdoc/>
        public bool MoveNext()
        {
            ChronicleGuard.ThrowIfTrue(_version != _bucket._version, message: "Collection modified");

            uint peak = (uint)_bucket._peak;
            while (++_index < peak)
            {
                if (_bucket._entries[_index].IsUsed)
                {
                    _current = _bucket._entries[_index].Value;
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc/>
        public void Reset()
        {
            ChronicleGuard.ThrowIfTrue(_version != _bucket._version, message: "Collection modified");

            _index = -1;
            _current = default!;
        }

        /// <inheritdoc/>
        public void Dispose() => _index = -1;
    }
}
