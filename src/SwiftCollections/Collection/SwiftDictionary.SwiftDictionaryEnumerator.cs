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
    /// Enumerates current dictionary entries in increasing hash-slot order.
    /// </summary>
    /// <remarks>
    /// Obtain this value-type enumerator from the owning collection; default construction is not an initialized
    /// enumeration. MoveNext and Reset check the captured structural version; Current does not validate the
    /// version. Do not mutate through backing storage or borrowed references while iterating. Copies of the
    /// enumerator advance independently.
    /// </remarks>
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

        /// <summary>
        /// Gets the selected enumeration key.
        /// </summary>
        /// <value>The selected value, valid only after MoveNext returns true and before the next
        /// advance/reset/dispose.</value>
        /// <exception cref="T:System.InvalidOperationException">Enumeration is before its first element or past
        /// its last element.</exception>
        object IDictionaryEnumerator.Key
        {
            get
            {
                ChronicleGuard.ThrowIfTrue((uint)_index > (uint)_dictionary._lastIndex, message: "Enumerator is positioned before the first element or after the last element.");
                return _current.Key;
            }
        }

        /// <summary>
        /// Gets the selected enumeration value.
        /// </summary>
        /// <value>The selected value, valid only after MoveNext returns true and before the next
        /// advance/reset/dispose.</value>
        /// <exception cref="T:System.InvalidOperationException">Enumeration is before its first element or past
        /// its last element.</exception>
        object IDictionaryEnumerator.Value
        {
            get
            {
                ChronicleGuard.ThrowIfTrue((uint)_index > (uint)_dictionary._lastIndex, message: "Enumerator is positioned before the first element or after the last element.");
                return _current.Value!;
            }
        }

        /// <summary>
        /// Gets the selected enumeration entry.
        /// </summary>
        /// <value>The selected value, valid only after MoveNext returns true and before the next
        /// advance/reset/dispose.</value>
        /// <exception cref="T:System.InvalidOperationException">Enumeration is before its first element or past
        /// its last element.</exception>
        DictionaryEntry IDictionaryEnumerator.Entry
        {
            get
            {
                ChronicleGuard.ThrowIfTrue((uint)_index > (uint)_dictionary._lastIndex, message: "Enumerator is positioned before the first element or after the last element.");
                return new DictionaryEntry(_current.Key, _current.Value);
            }
        }

        /// <summary>
        /// Gets the value cached by the last successful MoveNext.
        /// </summary>
        /// <value>The cached value. Read only after MoveNext returns true and before the next
        /// advance/reset/dispose.</value>
        /// <remarks>
        /// The generic getter does not check position or collection version. Outside a valid position it can
        /// return default or a previous value; no exception is guaranteed. The non-generic IEnumerator getter
        /// validates position.
        /// </remarks>
        public KeyValuePair<TKey, TValue> Current => _current;

        /// <summary>
        /// Gets the selected enumeration current.
        /// </summary>
        /// <value>The selected value, valid only after MoveNext returns true and before the next
        /// advance/reset/dispose.</value>
        /// <exception cref="T:System.InvalidOperationException">Enumeration is before its first element or past
        /// its last element.</exception>
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

        /// <summary>
        /// Advances to the next live element.
        /// </summary>
        /// <returns>true if an element was selected and Current is valid; false when enumeration is
        /// exhausted.</returns>
        /// <exception cref="T:System.InvalidOperationException">The captured collection version differs from its
        /// current version.</exception>
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

        /// <summary>
        /// Rewinds the enumerator to before its first element.
        /// </summary>
        /// <exception cref="T:System.InvalidOperationException">The collection version changed since this
        /// enumerator was created.</exception>
        /// <remarks>
        /// Current becomes default. The same captured structural version is retained.
        /// </remarks>
        public void Reset()
        {
            ChronicleGuard.ThrowIfTrue(_version != _dictionary._version, message: "Enumerator modified outside of enumeration!");

            _index = -1;
            _current = default;
        }

        /// <summary>
        /// Resets the position marker without releasing collection storage.
        /// </summary>
        /// <remarks>
        /// Does not dispose the collection or clear stored element references. The struct still retains its
        /// owner/storage references. Do not use Current after disposal.
        /// </remarks>
        public void Dispose() => _index = -1;
    }
}
