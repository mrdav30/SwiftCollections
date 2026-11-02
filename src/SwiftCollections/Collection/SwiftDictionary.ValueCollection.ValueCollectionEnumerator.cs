//=======================================================================
// SwiftDictionary.ValueCollection.ValueCollectionEnumerator.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using Chronicler.Diagnostics;

namespace SwiftCollections;

public partial class SwiftDictionary<TKey, TValue>
    where TKey : notnull
{
    public sealed partial class ValueCollection
    {
        /// <summary>
        /// Enumerates the values in a SwiftDictionary collection.
        /// </summary>
        /// <remarks>
        /// The enumerator is invalidated if the collection is modified after the enumerator is created.
        /// Enumerators are typically used in a foreach statement to iterate through the collection values.
        /// </remarks>
        [Serializable]
        public struct ValueCollectionEnumerator : IEnumerator<TValue>, IEnumerator, IDisposable
        {
            private readonly SwiftDictionary<TKey, TValue> _dictionary;
            private readonly Entry[] _entries;
            private readonly uint _version;
            private int _index;
            private TValue _currentValue;

            internal ValueCollectionEnumerator(SwiftDictionary<TKey, TValue> dictionary)
            {
                _dictionary = dictionary;
                _entries = dictionary._entries;
                _version = dictionary._version;
                _index = -1;
                _currentValue = default!;
            }

            /// <inheritdoc/>
            public TValue Current => _currentValue;

            object IEnumerator.Current
            {
                get
                {
                    ChronicleGuard.ThrowIfTrue((uint)_index > (uint)_dictionary._lastIndex, message: "Enumerator is positioned before the first element or after the last element.");
                    return _currentValue!;
                }
            }

            /// <inheritdoc/>
            public bool MoveNext()
            {
                ChronicleGuard.ThrowIfTrue(_version != _dictionary._version, message: "Enumerator modified outside of enumeration!");

                while (++_index <= (uint)_dictionary._lastIndex)
                {
                    if (_entries[_index].IsUsed)
                    {
                        _currentValue = _entries[_index].Value;
                        return true;
                    }
                }

                _currentValue = default!;
                return false;
            }

            /// <inheritdoc/>
            public void Reset()
            {
                ChronicleGuard.ThrowIfTrue(_version != _dictionary._version, message: "Enumerator modified outside of enumeration!");

                _index = -1;
                _currentValue = default!;
            }

            /// <inheritdoc/>
            public void Dispose() => _index = -1;
        }
    }
}
