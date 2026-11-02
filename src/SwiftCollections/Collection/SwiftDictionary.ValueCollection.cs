//=======================================================================
// SwiftDictionary.ValueCollection.cs
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
    /// Offers a dynamic, read-only collection of all values in the dictionary, supporting enumeration and copy operations.
    /// </summary>
    [Serializable]
    public sealed partial class ValueCollection : ICollection<TValue>, ICollection, IReadOnlyCollection<TValue>, IEnumerable<TValue>, IEnumerable
    {
        private readonly SwiftDictionary<TKey, TValue> _dictionary;
        private readonly Entry[] _entries;

        /// <summary>
        /// Initializes a new instance of the ValueCollection class that reflects the values in the specified dictionary.
        /// </summary>
        /// <param name="dictionary">The dictionary whose values are reflected in the new ValueCollection.</param>
        /// <exception cref="ArgumentNullException">The dictionary is null.</exception>
        public ValueCollection(SwiftDictionary<TKey, TValue> dictionary)
        {
            ChronicleGuard.ThrowIfNull(dictionary, nameof(dictionary));
            _dictionary = dictionary;
            _entries = dictionary._entries;
        }

        /// <inheritdoc/>
        public int Count => _dictionary._count;

        bool ICollection<TValue>.IsReadOnly => true;

        bool ICollection.IsSynchronized => false;

        object ICollection.SyncRoot => ((ICollection)_dictionary).SyncRoot;

        void ICollection<TValue>.Add(TValue item) => throw new NotSupportedException();

        void ICollection<TValue>.Clear() => throw new NotSupportedException();

        bool ICollection<TValue>.Contains(TValue item)
        {
            for (uint i = 0; i <= (uint)_dictionary._lastIndex; i++)
            {
                if (_entries[i].IsUsed && EqualityComparer<TValue>.Default.Equals(_entries[i].Value, item))
                    return true;
            }
            return false;
        }

        bool ICollection<TValue>.Remove(TValue item) => false;

        /// <inheritdoc/>
        public void CopyTo(TValue[] array, int arrayIndex)
        {
            ChronicleGuard.ThrowIfNull(array, nameof(array));
            ChronicleGuard.ThrowIfArrayIndexInvalid(arrayIndex, array.Length);
            ChronicleGuard.ThrowIfArgument(array.Length - arrayIndex < _dictionary._count, nameof(array), "Insufficient space in the target array.");

            for (int i = 0, j = arrayIndex; i <= _dictionary._lastIndex; i++)
            {
                if (_dictionary._entries[i].IsUsed)
                    array[j++] = _entries[i].Value;
            }
        }

        void ICollection.CopyTo(Array array, int arrayIndex)
        {
            ChronicleGuard.ThrowIfNull(array, nameof(array));
            ChronicleGuard.ThrowIfArgument(array.Rank != 1, nameof(array), "Multidimensional array not supported");
            ChronicleGuard.ThrowIfArgument(array.GetLowerBound(0) != 0, nameof(array), "Non-zero lower bound");
            ChronicleGuard.ThrowIfArrayIndexInvalid(arrayIndex, array.Length);
            ChronicleGuard.ThrowIfArgument(array.Length - arrayIndex < _dictionary._count, nameof(array), "Insufficient space in the target array.");

            if (array is TValue[] valuesArray)
                CopyTo(valuesArray, arrayIndex);
            else if (array is object[] objects)
            {
                try
                {
                    for (int i = 0, j = arrayIndex; i <= _dictionary._lastIndex; i++)
                        if (_entries[i].IsUsed)
                            objects[j++] = _entries[i].Value!;
                }
                catch (ArrayTypeMismatchException)
                {
                    throw new ArgumentException("Invalid array type", nameof(array));
                }
            }
            else throw new ArgumentException("Invalid array type", nameof(array));
        }

        /// <summary>
        /// Returns an enumerator that iterates through the values in the collection.
        /// </summary>
        /// <returns>An enumerator for the values in the collection.</returns>
        public ValueCollectionEnumerator GetEnumerator() => new(_dictionary);
        IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


    }
}
