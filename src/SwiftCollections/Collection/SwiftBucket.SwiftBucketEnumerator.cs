//=======================================================================
// SwiftBucket.SwiftBucketEnumerator.cs
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

public sealed partial class SwiftBucket<T>
{
    /// <summary>
    /// Enumerates the elements of a <see cref="SwiftBucket{T}"/> collection.
    /// </summary>
    /// <remarks>
    /// The enumerator provides read-only, forward-only iteration over the elements in the <see cref="SwiftBucket{T}"/>.
    /// The enumerator is invalidated if the collection is modified after the enumerator is created.
    /// </remarks>
    public struct SwiftBucketEnumerator : IEnumerator<T>, IDisposable
    {
        private readonly SwiftBucket<T> _bucket;
        private readonly Entry[] _entries;
        private readonly uint _version;
        private int _index;
        private T _current;

        internal SwiftBucketEnumerator(SwiftBucket<T> bucket)
        {
            _bucket = bucket;
            _entries = bucket._innerArray;
            _version = bucket._version;
            _index = -1;
            _current = default!;
        }

        /// <inheritdoc/>
        public T Current => _current;

        object IEnumerator.Current
        {
            get
            {
                ChronicleGuard.ThrowIfTrue((uint)_index >= (uint)_bucket._peakCount, message: "Enumerator is before the first element or after the last element.");
                return _current!;
            }
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public void Reset()
        {
            ChronicleGuard.ThrowIfTrue(_version != _bucket._version, message: "Enumerator modified outside of enumeration!");

            _index = -1;
            _current = default!;
        }

        /// <inheritdoc/>
        public void Dispose() => _index = -1;
    }
}
