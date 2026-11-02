//=======================================================================
// SwiftList.SwiftListEnumerator.cs
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

public partial class SwiftList<T>
{
    /// <summary>
    /// Enumerates the elements of a <see cref="SwiftList{T}"/> collection.
    /// </summary>
    /// <remarks>
    /// The enumerator provides read-only, forward-only iteration over the collection.
    /// The enumerator is invalidated if the collection is modified after the enumerator is created.
    /// In such cases, calling MoveNext or Reset will throw an InvalidOperationException.
    /// </remarks>
    public struct SwiftListEnumerator : IEnumerator<T>, IEnumerator, IDisposable
    {
        private readonly SwiftList<T> _list;
        private readonly T[] _array;
        private readonly uint _count;
        private readonly uint _version;
        private uint _index;

        private T _current;

        internal SwiftListEnumerator(SwiftList<T> list)
        {
            _list = list;
            _array = list._innerArray;
            _count = (uint)list._count;
            _version = list._version;
            _index = 0;
            _current = default!;
        }

        /// <inheritdoc/>
        public T Current => _current;

        object IEnumerator.Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                ChronicleGuard.ThrowIfTrue(_index == 0 || _index > _count, message: "Enumeration has either not started or has already finished.");
                return _current!;
            }
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            ChronicleGuard.ThrowIfTrue(_version != _list._version, message: "Collection was modified during enumeration.");

            if (_index >= _count)
            {
                _index = _count + 1;
                return false;
            }
            _current = _array[_index++];
            return true;
        }

        /// <inheritdoc/>
        public void Reset()
        {
            ChronicleGuard.ThrowIfTrue(_version != _list._version, message: "Collection was modified during enumeration.");

            _index = 0;
            _current = default!;
        }

        /// <inheritdoc/>
        public void Dispose() => _index = 0;
    }
}
