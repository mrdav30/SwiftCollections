//=======================================================================
// SwiftDictionary.Entry.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

namespace SwiftCollections;

public partial class SwiftDictionary<TKey, TValue>
    where TKey : notnull
{
    /// <summary>
    /// Represents a single key-value pair in the dictionary, including its hash code for quick access.
    /// </summary>
    protected internal struct Entry
    {
        /// <summary>
        /// Gets or sets the key associated with this instance.
        /// </summary>
        public TKey Key;

        /// <summary>
        /// Gets or sets the value associated with this instance.
        /// </summary>
        public TValue Value;

        /// <summary>
        /// Gets or sets the lower 31 bits of the hash code associated with this entry.
        /// </summary>
        /// <remarks>
        /// A value of -1 indicates that the entry is a deleted probe tombstone.
        /// Only the lower 31 bits are used; the highest bit is reserved.</remarks>
        public int HashCode;

        /// <summary>
        /// Indicates whether the item is currently in use.
        /// </summary>
        public bool IsUsed;
    }
}
