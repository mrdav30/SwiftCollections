//=======================================================================
// SwiftDictionary.KeyCollection.cs
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
    /// Provides a dynamic, read-only collection of all keys in the dictionary, supporting enumeration and copy operations.
    /// </summary>
    [Serializable]
    public sealed partial class KeyCollection : ICollection<TKey>, ICollection, IReadOnlyCollection<TKey>, IEnumerable<TKey>, IEnumerable
    {
        private readonly SwiftDictionary<TKey, TValue> _dictionary;
        private readonly Entry[] _entries;

        /// <summary>
        /// Initializes a new instance of the KeyCollection class that reflects the keys in the specified dictionary.
        /// </summary>
        /// <param name="dictionary">The dictionary whose keys are reflected in the new KeyCollection.</param>
        /// <exception cref="ArgumentNullException">The dictionary is null.</exception>
        public KeyCollection(SwiftDictionary<TKey, TValue> dictionary)
        {
            ChronicleGuard.ThrowIfNull(dictionary, nameof(dictionary));
            _dictionary = dictionary;
            _entries = dictionary._entries;
        }

        /// <inheritdoc/>
        public int Count => _dictionary._count;

        bool ICollection.IsSynchronized => false;

        object ICollection.SyncRoot => ((ICollection)_dictionary).SyncRoot;

        bool ICollection<TKey>.IsReadOnly => true;

        void ICollection<TKey>.Add(TKey item) => throw new NotSupportedException();

        void ICollection<TKey>.Clear() => throw new NotSupportedException();

        bool ICollection<TKey>.Contains(TKey item) => _dictionary.ContainsKey(item);

        bool ICollection<TKey>.Remove(TKey item) => false;

        /// <inheritdoc/>
        public void CopyTo(TKey[] array, int arrayIndex)
        {
            ChronicleGuard.ThrowIfNull(array, nameof(array));
            ChronicleGuard.ThrowIfArrayIndexInvalid(arrayIndex, array.Length);
            ChronicleGuard.ThrowIfArgument(array.Length - arrayIndex < _dictionary._count, nameof(array), "Insufficient space in the target array.");

            for (int i = 0, j = arrayIndex; i < _entries.Length; i++)
            {
                if (_entries[i].IsUsed)
                    array[j++] = _entries[i].Key;
            }
        }

        void ICollection.CopyTo(Array array, int arrayIndex)
        {
            ChronicleGuard.ThrowIfNull(array, nameof(array));
            ChronicleGuard.ThrowIfArgument(array.Rank != 1, nameof(array), "Multidimensional array not supported");
            ChronicleGuard.ThrowIfArgument(array.GetLowerBound(0) != 0, nameof(array), "Non-zero lower bound");
            ChronicleGuard.ThrowIfArrayIndexInvalid(arrayIndex, array.Length);
            ChronicleGuard.ThrowIfArgument(array.Length - arrayIndex < _dictionary._count, nameof(array), "Insufficient space in the target array.");

            if (array is TKey[] keysArray)
                CopyTo(keysArray, arrayIndex);
            else if (array is object[] objects)
            {
                try
                {
                    for (int i = 0, j = arrayIndex; i < _entries.Length; i++)
                    {
                        if (_entries[i].IsUsed)
                            objects[j++] = _entries[i].Key;
                    }
                }
                catch (ArrayTypeMismatchException)
                {
                    throw new ArgumentException("Invalid array type", nameof(array));
                }
            }
            else
            {
                throw new ArgumentException("Invalid array type", nameof(array));
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through the keys in the collection.
        /// </summary>
        /// <returns>An enumerator for the keys in the collection.</returns>
        public KeyCollectionEnumerator GetEnumerator() => new(_dictionary);
        IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


    }
}
