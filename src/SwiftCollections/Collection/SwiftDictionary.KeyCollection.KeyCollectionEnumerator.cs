//=======================================================================
// SwiftDictionary.KeyCollection.KeyCollectionEnumerator.cs
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
    public sealed partial class KeyCollection
    {
        /// <summary>
        /// Enumerates the keys of a <see cref="SwiftDictionary{TKey, TValue}"/> collection.
        /// </summary>
        /// <remarks>
        /// The enumerator provides read-only, forward-only iteration over the keys in the dictionary.
        /// The enumerator is invalidated if the dictionary is modified after the enumerator is created.
        /// In such cases, calling MoveNext or Reset will throw an InvalidOperationException.
        /// </remarks>
        [Serializable]
        public struct KeyCollectionEnumerator : IEnumerator<TKey>, IEnumerator, IDisposable
        {
            private readonly SwiftDictionary<TKey, TValue> _dictionary;
            private readonly Entry[] _entries;
            private readonly uint _version;
            private int _index;
            private TKey _currentKey;

            internal KeyCollectionEnumerator(SwiftDictionary<TKey, TValue> dictionary)
            {
                _dictionary = dictionary;
                _entries = dictionary._entries;
                _version = dictionary._version;
                _index = -1;
                _currentKey = default!;
            }

            /// <inheritdoc/>
            public TKey Current => _currentKey;

            object IEnumerator.Current
            {
                get
                {
                    ChronicleGuard.ThrowIfTrue((uint)_index > (uint)_dictionary._lastIndex, message: "Enumerator is positioned before the first element or after the last element.");
                    return _currentKey;
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
                        _currentKey = _entries[_index].Key;
                        return true;
                    }
                }

                _currentKey = default!;
                return false;
            }

            /// <inheritdoc/>
            public void Reset()
            {
                ChronicleGuard.ThrowIfTrue(_version != _dictionary._version, message: "Enumerator modified outside of enumeration!");

                _index = -1;
                _currentKey = default!;
            }

            /// <inheritdoc/>
            public void Dispose() => _index = -1;
        }
    }
}
