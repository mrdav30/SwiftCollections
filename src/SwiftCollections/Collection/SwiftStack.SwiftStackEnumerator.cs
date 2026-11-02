//=======================================================================
// SwiftStack.SwiftStackEnumerator.cs
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

public sealed partial class SwiftStack<T>
{
    /// <summary>
    /// Supports simple iteration over the elements of a <see cref="SwiftStack{T}"/> in last-in, first-out (LIFO) order.
    /// </summary>
    /// <remarks>
    /// The enumerator is invalidated if the collection is modified after the enumerator is created.
    /// This type is not thread-safe.
    /// </remarks>
    public struct SwiftStackEnumerator : IEnumerator<T>, IEnumerator, IDisposable
    {
        private readonly SwiftStack<T> _stack;
        private readonly T[] _array;
        private readonly uint _version;
        private readonly int _count;
        private int _index;

        private T _current;

        internal SwiftStackEnumerator(SwiftStack<T> stack)
        {
            _stack = stack;
            _array = stack._innerArray;
            _count = stack._count;
            _version = stack._version;
            _index = -2; // Enumerator not started
            _current = default!;
        }

        /// <inheritdoc/>
        public T Current => _current;

        object IEnumerator.Current
        {
            get
            {
                ChronicleGuard.ThrowIfTrue((uint)_index > _count, message: "Bad enumeration");
                return _current!;
            }
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            ChronicleGuard.ThrowIfTrue(_version != _stack._version, message: "Enumerator modified outside of enumeration!");

            if (_index == -2)
            {
                _index = _count - 1;
            }
            else
            {
                _index--;
            }

            if (_index >= 0)
            {
                _current = _array[_index];
                return true;
            }

            _index = -1;
            _current = default!;
            return false;
        }

        /// <inheritdoc/>
        public void Reset()
        {
            ChronicleGuard.ThrowIfTrue(_version != _stack._version, message: "Enumerator modified outside of enumeration!");

            _index = -2;
            _current = default!;
        }

        /// <inheritdoc/>
        public void Dispose() => _index = -1;
    }
}
