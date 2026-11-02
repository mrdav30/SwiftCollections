//=======================================================================
// SwiftDictionary.SwiftDictionaryEnumerator.cs
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
    /// <summary>
    /// Provides an efficient enumerator for iterating over the key-value pairs in the SwiftDictionary, enabling smooth traversal during enumeration.
    /// </summary>
    [Serializable]
    public struct SwiftDictionaryEnumerator : IEnumerator<KeyValuePair<TKey, TValue>>, IEnumerator, IDictionaryEnumerator, IDisposable
    {
        private readonly SwiftDictionary<TKey, TValue> _dictionary;
        private readonly Entry[] _entries;
        private readonly uint _version;
        private readonly bool _returnEntry;
        private int _index;
        private KeyValuePair<TKey, TValue> _current;

        internal SwiftDictionaryEnumerator(SwiftDictionary<TKey, TValue> dictionary, bool returnEntry = false)
        {
            _dictionary = dictionary;
            _entries = dictionary._entries;
            _version = dictionary._version;
            _returnEntry = returnEntry;
            _index = -1;
            _current = default;
        }

        object IDictionaryEnumerator.Key
        {
            get
            {
                ChronicleGuard.ThrowIfTrue((uint)_index > (uint)_dictionary._lastIndex, message: "Enumerator is positioned before the first element or after the last element.");
                return _current.Key;
            }
        }

        object IDictionaryEnumerator.Value
        {
            get
            {
                ChronicleGuard.ThrowIfTrue((uint)_index > (uint)_dictionary._lastIndex, message: "Enumerator is positioned before the first element or after the last element.");
                return _current.Value!;
            }
        }

        DictionaryEntry IDictionaryEnumerator.Entry
        {
            get
            {
                ChronicleGuard.ThrowIfTrue((uint)_index > (uint)_dictionary._lastIndex, message: "Enumerator is positioned before the first element or after the last element.");
                return new DictionaryEntry(_current.Key, _current.Value);
            }
        }

        /// <inheritdoc/>
        public KeyValuePair<TKey, TValue> Current => _current;

        object IEnumerator.Current
        {
            get
            {
                ChronicleGuard.ThrowIfTrue((uint)_index > (uint)_dictionary._lastIndex, message: "Enumerator is positioned before the first element or after the last element.");
                return _returnEntry
                    ? new DictionaryEntry(_current.Key, _current.Value)
                    : new KeyValuePair<TKey, TValue>(_current.Key, _current.Value);
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
                    _current = new KeyValuePair<TKey, TValue>(_entries[_index].Key, _entries[_index].Value);
                    return true;
                }
            }

            _current = default;
            return false;
        }

        /// <inheritdoc/>
        public void Reset()
        {
            ChronicleGuard.ThrowIfTrue(_version != _dictionary._version, message: "Enumerator modified outside of enumeration!");

            _index = -1;
            _current = default;
        }

        /// <inheritdoc/>
        public void Dispose() => _index = -1;
    }
}
