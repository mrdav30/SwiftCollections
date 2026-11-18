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
    /// Offers a dynamic, read-only collection of all values in the dictionary, supporting enumeration and copy
    /// operations.
    /// </summary>
    /// <remarks>
    /// Retains the owner and captures its entry array at construction. Count reads current owner state, but
    /// copying/membership can use stale storage after reallocation; this is a tracked implementation defect.
    /// Enumeration constructs a fresh enumerator from the current owner. Mutation through ICollection Add/Clear
    /// throws; Remove returns false.
    /// </remarks>
    [Serializable]
    public sealed partial class ValueCollection : ICollection<TValue>, ICollection, IReadOnlyCollection<TValue>, IEnumerable<TValue>, IEnumerable
    {
        private readonly SwiftDictionary<TKey, TValue> _dictionary;
        private readonly Entry[] _entries;

        /// <summary>
        /// Initializes a new instance of the ValueCollection class that reflects the values in the specified
        /// dictionary.
        /// </summary>
        /// <param name="dictionary">The dictionary whose values are reflected in the new ValueCollection.</param>
        /// <exception cref="T:System.ArgumentNullException">The dictionary is null.</exception>
        public ValueCollection(SwiftDictionary<TKey, TValue> dictionary)
        {
            ChronicleGuard.ThrowIfNull(dictionary, nameof(dictionary));
            _dictionary = dictionary;
            _entries = dictionary._entries;
        }

        /// <summary>
        /// Gets the current number of entries in the owning dictionary.
        /// </summary>
        /// <value>The owner's live Count, even when the captured entry array is stale.</value>
        public int Count => _dictionary._count;

        /// <summary>
        /// Gets the interface IsReadOnly.
        /// </summary>
        /// <value>true for dictionary key/value views; false for the owning mutable collection.</value>
        bool ICollection<TValue>.IsReadOnly => true;

        /// <summary>
        /// Gets the interface IsSynchronized.
        /// </summary>
        /// <value>false; callers synchronize access externally.</value>
        bool ICollection.IsSynchronized => false;

        /// <summary>
        /// Gets the interface SyncRoot.
        /// </summary>
        /// <value>The owner's lazily allocated synchronization object.</value>
        object ICollection.SyncRoot => ((ICollection)_dictionary).SyncRoot;

        /// <summary>
        /// Adds an element through the implemented collection interface.
        /// </summary>
        /// <param name="item">The element to compare, add or remove; reference values are not cloned.</param>
        /// <exception cref="T:System.NotSupportedException">This dictionary view does not support adding or
        /// clearing.</exception>
        void ICollection<TValue>.Add(TValue item) => throw new NotSupportedException();

        /// <summary>
        /// Clears elements through the implemented interface.
        /// </summary>
        /// <exception cref="T:System.NotSupportedException">This dictionary view does not support adding or
        /// clearing.</exception>
        void ICollection<TValue>.Clear() => throw new NotSupportedException();

        /// <summary>
        /// Checks membership through the implemented interface.
        /// </summary>
        /// <param name="item">The element to compare, add or remove; reference values are not cloned.</param>
        /// <returns>true if an equal live entry exists; false otherwise. Uses the concrete collection's equality
        /// rules.</returns>
        bool ICollection<TValue>.Contains(TValue item)
        {
            for (uint i = 0; i <= (uint)_dictionary._lastIndex; i++)
            {
                if (_entries[i].IsUsed && EqualityComparer<TValue>.Default.Equals(_entries[i].Value, item))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Removes a matching element through the implemented interface.
        /// </summary>
        /// <param name="item">The element to compare, add or remove; reference values are not cloned.</param>
        /// <returns>false; removal through this read-only view changes nothing.</returns>
        bool ICollection<TValue>.Remove(TValue item) => false;

        /// <summary>
        /// Copies values from the captured entry storage into a caller array.
        /// </summary>
        /// <param name="array">The caller-owned destination array.</param>
        /// <param name="arrayIndex">The zero-based destination offset with room for the owner's Count.</param>
        /// <exception cref="T:System.ArgumentNullException">array is null.</exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException">arrayIndex is outside zero through
        /// array.Length.</exception>
        /// <exception cref="T:System.ArgumentException">The remaining destination space is too small.</exception>
        /// <remarks>
        /// Does not clear the destination. Do not use after owner storage replacement; Count and captured storage
        /// can disagree, causing stale values or range errors.
        /// </remarks>
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

        /// <summary>
        /// Copies live elements into a caller-owned array.
        /// </summary>
        /// <param name="array">The caller-owned, one-dimensional, zero-based destination array.</param>
        /// <param name="arrayIndex">The zero-based destination offset with space for the live count.</param>
        /// <remarks>
        /// Uses concrete collection copy order and validation. The destination is overwritten only in the copied
        /// range; incompatible elements can cause partial copying.
        /// </remarks>
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
        /// <summary>
        /// Creates an enumerator through the implemented interface.
        /// </summary>
        /// <returns>An enumerator with the concrete collection's ordering and mutation rules. Interface dispatch
        /// may box the value-type enumerator.</returns>
        IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator() => GetEnumerator();
        /// <summary>
        /// Creates an enumerator through the implemented interface.
        /// </summary>
        /// <returns>An enumerator with the concrete collection's ordering and mutation rules. Interface dispatch
        /// may box the value-type enumerator.</returns>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


    }
}
