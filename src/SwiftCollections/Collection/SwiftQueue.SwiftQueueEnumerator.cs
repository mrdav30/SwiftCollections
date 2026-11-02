//=======================================================================
// SwiftQueue.SwiftQueueEnumerator.cs
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

public sealed partial class SwiftQueue<T>
{
    /// <summary>
    /// Enumerates the elements of a <see cref="SwiftQueue{T}"/> in the order they would be dequeued.
    /// </summary>
    public struct SwiftQueueEnumerator : IEnumerator<T>, IEnumerator, IDisposable
    {
        private readonly SwiftQueue<T> _queue;
        private readonly T[] _array;
        private readonly uint _version;
        private uint _index;
        private uint _currentIndex;

        private T _current;

        internal SwiftQueueEnumerator(SwiftQueue<T> queue)
        {
            _queue = queue;
            _array = queue._innerArray;
            _version = queue._version;
            _index = 0;
            _currentIndex = (uint)queue._head - 1;
            _current = default!;
        }

        /// <inheritdoc/>
        public readonly T Current => _current;

        readonly object IEnumerator.Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                ChronicleGuard.ThrowIfTrue(_index == 0 || _index > (uint)_queue._count, message: "Bad enumeration");
                return _current!;
            }
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            ChronicleGuard.ThrowIfTrue(_version != _queue._version, message: "Collection was modified during enumeration.");

            _index++;
            if (_index > (uint)_queue._count) return false;
            _currentIndex++;
            if (_currentIndex == _array.Length) _currentIndex = 0;
            _current = _array[_currentIndex];
            return true;
        }

        /// <inheritdoc/>
        public void Reset()
        {
            ChronicleGuard.ThrowIfTrue(_version != _queue._version, message: "Collection was modified during enumeration.");

            _index = 0;
            _currentIndex = (uint)_queue._head - 1;
            _current = default!;
        }

        /// <inheritdoc/>
        public void Dispose() => _index = 0;
    }
}
