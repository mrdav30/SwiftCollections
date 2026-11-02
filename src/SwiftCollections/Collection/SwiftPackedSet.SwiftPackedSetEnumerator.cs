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
    /// Enumerates the elements of a <see cref="SwiftPackedSet{T}"/> collection.
    /// </summary>
    /// <remarks>
    /// The enumerator is invalidated if the collection is modified after the enumerator is created.
    /// In such cases, calling MoveNext or Reset will throw an InvalidOperationException.
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

        /// <inheritdoc/>
        public T Current { get; private set; }

        object IEnumerator.Current
        {
            get
            {
                ChronicleGuard.ThrowIfTrue((uint)_index >= (uint)_set._count, message: "Enumerator is before the first element or after the last element.");
                return Current!;
            }
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public void Reset()
        {
            ChronicleGuard.ThrowIfTrue(_version != _set._version, message: "Collection was modified during enumeration.");

            _index = -1;
            Current = default!;
        }

        /// <inheritdoc/>
        public void Dispose() => _index = -1;
    }
}
