using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Security;

namespace SwiftCollections
{
    /// <summary>
    /// Represents a bidirectional dictionary that allows for efficient lookups in both directions,
    /// mapping keys to values and values back to keys. Both keys and values must be unique to maintain
    /// the integrity of the bidirectional relationship.
    /// Inherits from <see cref="SwiftDictionary{TKey, TValue}"/> and maintains a reverse map for reverse lookups.
    /// </summary>
    /// <typeparam name="T1">The type of the keys in the forward dictionary.</typeparam>
    /// <typeparam name="T2">The type of the values in the forward dictionary.</typeparam>
    [Serializable]
    public class SwiftBiDictionary<T1, T2> : SwiftDictionary<T1, T2>
    {
        #region Fields

        /// <summary>
        /// The reverse map for bidirectional lookup, mapping from <typeparamref name="T2"/> to <typeparamref name="T1"/>.
        /// </summary>
        private readonly SwiftDictionary<T2, T1> _reverseMap;

        /// <summary>
        /// An object used to synchronize access to the reverse map during serialization and deserialization.
        /// </summary>
        [NonSerialized]
        private readonly object _reverseMapSyncRoot;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="SwiftBiDictionary{T1, T2}"/> class that is empty and uses the default equality comparers for the key and value types.
        /// </summary>
        public SwiftBiDictionary() : this(null, null) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="SwiftBiDictionary{T1, T2}"/> class that is empty and uses the specified equality comparers for the key and value types.
        /// </summary>
        /// <param name="comparer1">The comparer to use for the keys.</param>
        /// <param name="comparer2">The comparer to use for the values.</param>
        public SwiftBiDictionary(IEqualityComparer<T1> comparer1, IEqualityComparer<T2> comparer2) : base(DefaultCapacity, comparer1)
        {
            _reverseMap = new SwiftDictionary<T2, T1>(DefaultCapacity, comparer2);
            _reverseMapSyncRoot = new object();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SwiftBiDictionary{T1, T2}"/> class that contains elements copied from the specified dictionary and uses the default equality comparers for the key and value types.
        /// </summary>
        /// <param name="dictionary">The dictionary whose elements are copied to the new <see cref="SwiftBiDictionary{T1, T2}"/>.</param>
        public SwiftBiDictionary(IDictionary<T1, T2> dictionary) : this(dictionary, null, null) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="SwiftBiDictionary{T1, T2}"/> class that contains elements copied from the specified dictionary and uses the specified equality comparers for the key and value types.
        /// </summary>
        /// <param name="dictionary">The dictionary whose elements are copied to the new <see cref="SwiftBiDictionary{T1, T2}"/>.</param>
        /// <param name="comparer1">The comparer to use for the keys.</param>
        /// <param name="comparer2">The comparer to use for the values.</param>
        public SwiftBiDictionary(IDictionary<T1, T2> dictionary, IEqualityComparer<T1> comparer1, IEqualityComparer<T2> comparer2)
            : this(comparer1, comparer2)
        {
            if (dictionary == null)
                ThrowHelper.ThrowArgumentNullException(nameof(dictionary));

            foreach (var kvp in dictionary)
                Add(kvp.Key, kvp.Value);
        }

        /// <summary>
        /// Deserialization constructor used during deserialization to reconstruct the <see cref="SwiftBiDictionary{T1, T2}"/> instance.
        /// </summary>
        /// <param name="info">The <see cref="SerializationInfo"/> holding the serialized object data.</param>
        /// <param name="context">The contextual information about the source or destination.</param>
        protected SwiftBiDictionary(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            _reverseMap = new SwiftDictionary<T2, T1>();
            _reverseMapSyncRoot = new object();
        }

        #endregion

        #region Collection Manipulation

        /// <summary>
        /// Removes the key-value pair from the <see cref="SwiftBiDictionary{T1, T2}"/>.
        /// Also removes the corresponding value-key pair from the reverse map.
        /// </summary>
        /// <param name="key">The key of the element to remove.</param>
        /// <param name="value">The value of the element to remove.</param>
        /// <returns><c>true</c> if the element is successfully found and removed; otherwise, <c>false</c>.</returns>
        public bool Remove(T1 key, T2 value)
        {
            if (key == null) ThrowHelper.ThrowArgumentNullException(nameof(key));
            if (value == null) ThrowHelper.ThrowArgumentNullException(nameof(value));

            if (TryGetValue(key, out T2 existingValue))
            {
                if (EqualityComparer<T2>.Default.Equals(existingValue, value))
                {
                    bool removed = base.Remove(key);
                    if (removed)
                    {
                        lock (_reverseMapSyncRoot)
                            _reverseMap.Remove(value);
                    }
                    return removed;
                }
            }
            return false;
        }

        #region Overrides

        /// <summary>
        /// Adds the specified key and value to the <see cref="SwiftBiDictionary{T1, T2}"/>.
        /// Also adds the value-key pair to the reverse map.
        /// </summary>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="value">The value of the element to add.</param>
        /// <exception cref="ArgumentException">An element with the same key or value already exists.</exception>
        public override bool Add(T1 key, T2 value)
        {
            if (key == null) ThrowHelper.ThrowArgumentNullException(nameof(key));

            bool added = base.Add(key, value);
            if (added)
            {
                lock (_reverseMapSyncRoot)
                    _reverseMap.Add(value, key);
            }
            return added;
        }

        /// <summary>
        /// Overrides the base class's <see cref="SwiftDictionary{TKey, TValue}.InsertIfNotExist"/> method to ensure synchronization with the reverse map.
        /// </summary>
        /// <param name="key">The key to insert.</param>
        /// <param name="value">The value to insert.</param>
        /// <returns><c>true</c> if a new entry was added; otherwise, <c>false</c>.</returns>
        internal override bool InsertIfNotExist(T1 key, T2 value)
        {
            bool result = base.InsertIfNotExist(key, value);
            if (result)
            {
                lock (_reverseMapSyncRoot)
                    _reverseMap.Add(value, key);
            }
            else
            {
                // Update the reverse map if the value has changed
                // Assuming the base class handles value updates
                lock (_reverseMapSyncRoot)
                {
                    // Find the old value and update the reverse map accordingly
                    foreach (var pair in _reverseMap)
                    {
                        if (EqualityComparer<T1>.Default.Equals(pair.Value, key))
                        {
                            _reverseMap.Remove(pair.Key);
                            _reverseMap.Add(value, key);
                            break;
                        }
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Removes the value with the specified key from the <see cref="SwiftBiDictionary{T1, T2}"/>.
        /// Also removes the corresponding key from the reverse map.
        /// </summary>
        /// <param name="key">The key of the element to remove.</param>
        /// <returns><c>true</c> if the element is successfully found and removed; otherwise, <c>false</c>.</returns>
        public override bool Remove(T1 key)
        {
            if (key == null) ThrowHelper.ThrowArgumentNullException(nameof(key));

            if (TryGetValue(key, out T2 value))
            {
                bool removed = base.Remove(key);
                if (removed)
                {
                    lock (_reverseMapSyncRoot)
                        _reverseMap.Remove(value);
                }
                return removed;
            }
            return false;
        }

        /// <summary>
        /// Removes all keys and values from the <see cref="SwiftBiDictionary{T1, T2}"/>.
        /// Also clears the reverse map.
        /// </summary>
        public override void Clear()
        {
            base.Clear();
            lock (_reverseMapSyncRoot)
                _reverseMap.Clear();
        }

        #endregion

        #endregion

        #region Utility Methods

        /// <summary>
        /// Attempts to get the key associated with the specified value.
        /// </summary>
        /// <param name="value">The value whose associated key is to be retrieved.</param>
        /// <param name="key">When this method returns, contains the key associated with the specified value, if the key is found; otherwise, the default value for the type of the key.</param>
        /// <returns><c>true</c> if the key was found; otherwise, <c>false</c>.</returns>
        public bool TryGetKey(T2 value, out T1 key)
        {
            if (value == null) ThrowHelper.ThrowArgumentNullException(nameof(value));

            lock (_reverseMapSyncRoot)
                return _reverseMap.TryGetValue(value, out key);
        }

        /// <summary>
        /// Gets the key associated with the specified value.
        /// </summary>
        /// <param name="value">The value whose associated key is to be retrieved.</param>
        /// <returns>The key associated with the specified value.</returns>
        /// <exception cref="KeyNotFoundException">The property is retrieved and <paramref name="value"/> does not exist in the reverse map.</exception>
        public T1 GetKey(T2 value)
        {
            if (value == null) ThrowHelper.ThrowArgumentNullException(nameof(value));

            lock (_reverseMapSyncRoot)
                return _reverseMap[value];
        }

        /// <summary>
        /// Determines whether the <see cref="SwiftBiDictionary{T1, T2}"/> contains the specified value.
        /// </summary>
        /// <param name="value">The value to locate in the dictionary.</param>
        /// <returns><c>true</c> if the dictionary contains an element with the specified value; otherwise, <c>false</c>.</returns>
        public bool ContainsValue(T2 value)
        {
            if (value == null) ThrowHelper.ThrowArgumentNullException(nameof(value));

            lock (_reverseMapSyncRoot)
                return _reverseMap.ContainsKey(value);
        }

        #endregion

        #region Serialization

        /// <summary>
        /// Populates a <see cref="SerializationInfo"/> with the data needed to serialize the <see cref="SwiftBiDictionary{T1, T2}"/>.
        /// </summary>
        /// <param name="info">The <see cref="SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination and source of the serialized stream.</param>
        [SecurityCritical]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                ThrowHelper.ThrowArgumentNullException(nameof(info));

            // Serialize the base dictionary
            base.GetObjectData(info, context);

            // Serialize the reverse map
            lock (_reverseMapSyncRoot)
            {
                KeyValuePair<T2, T1>[] reverseArray = new KeyValuePair<T2, T1>[_reverseMap.Count];
                _reverseMap.CopyTo(reverseArray, 0);
                info.AddValue("ReverseMap", reverseArray, typeof(KeyValuePair<T2, T1>[]));
            }
        }

        /// <summary>
        /// Implements the <see cref="IDeserializationCallback.OnDeserialization"/> method.
        /// Reconstructs the reverse map after deserialization.
        /// </summary>
        /// <param name="sender">The source of the deserialization event.</param>
        public override void OnDeserialization(object sender)
        {
            // Call base OnDeserialization to handle the primary dictionary
            base.OnDeserialization(sender);

            // Retrieve the SerializationInfo from a SerializationInfo table or similar mechanism
            HashHelper.SerializationInfoTable.TryGetValue(this, out SerializationInfo siInfo);
            if (siInfo == null) return;

            // Deserialize the reverse map
            KeyValuePair<T2, T1>[] reverseArray = (KeyValuePair<T2, T1>[])siInfo.GetValue("ReverseMap", typeof(KeyValuePair<T2, T1>[]));
            if (reverseArray != null)
            {
                foreach (var pair in reverseArray)
                    _reverseMap.Add(pair.Key, pair.Value);
            }

            // Validation
            foreach (var kvp in this)
            {
                if (!_reverseMap.TryGetValue(kvp.Value, out T1 key) || !EqualityComparer<T1>.Default.Equals(key, kvp.Key))
                    ThrowHelper.ThrowSerializationException("Reverse map is inconsistent with the primary dictionary.");
            }

            // Remove the SerializationInfo from the table
            HashHelper.SerializationInfoTable.Remove(this);
        }


        #endregion
    }
}
