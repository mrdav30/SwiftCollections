//=======================================================================
// SwiftSortedList.SwiftSorterEnumerator.cs
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

public partial class SwiftSortedList<T>
{
    /// <summary>
    /// Supports simple iteration over the elements of a <see cref="SwiftSortedList{T}"/> in sorted order.
    /// </summary>
    /// <remarks>
    /// The enumerator is invalidated if the underlying collection is modified after the enumerator created.
    /// In this case, calling MoveNext or Reset will throw an InvalidOperationException.
    /// The enumerator does not support writing to the collection.
    /// </remarks>
    public struct SwiftSorterEnumerator : IEnumerator<T>, IEnumerator, IDisposable
    {
        private readonly SwiftSortedList<T> _list;
        private readonly uint _count;
        private readonly uint _version;
        private int _index;

        private T _current;

        internal SwiftSorterEnumerator(SwiftSortedList<T> sortedList)
        {
            _list = sortedList;
            _count = (uint)sortedList._count;
            _version = sortedList._version;
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
                ChronicleGuard.ThrowIfTrue(_index == 0 || (uint)_index > _count, message: "Enumerator is past the end of the collection.");
                return _current!;
            }

        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            ChronicleGuard.ThrowIfTrue(_version != _list._version, message: "Enumerator modified outside of enumeration!");

            if (_index < _count)
            {
                _current = _list._innerArray[_list.GetPhysicalIndex(_index)];
                _index++;
                return true;
            }

            _index = _list._count + 1;
            _current = default!;
            return false;
        }

        /// <inheritdoc/>
        public void Reset()
        {
            ChronicleGuard.ThrowIfTrue(_version != _list._version, message: "Enumerator modified outside of enumeration!");

            _index = 0;
            _current = default!;
        }

        /// <inheritdoc/>
        public void Dispose() => _index = 0;
    }
}
