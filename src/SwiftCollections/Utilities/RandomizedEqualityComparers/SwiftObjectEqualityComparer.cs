using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace SwiftCollections
{
    /// <summary>
    /// Provides a randomized equality comparer for objects, enhancing hash code distribution to reduce collisions in hash-based collections.
    /// </summary>
    [Serializable]
    internal sealed class SwiftObjectEqualityComparer : IEqualityComparer<object>, IEqualityComparer, ISerializable, IRandomedEqualityComparer
    {
        /// <summary>
        /// A 64-bit entropy value used to randomize hash codes for better distribution.
        /// </summary>
        private readonly long _entropy;

        /// <summary>
        /// Initializes a new instance of the <see cref="SwiftStringEqualityComparer"/> class with a unique entropy value.
        /// </summary>
        public SwiftObjectEqualityComparer()
        {
            _entropy = HashTools.GetEntropy();
        }

        /// <summary>
        /// Determines whether the specified objects are equal.
        /// </summary>
        /// <param name="x">The first object to compare.</param>
        /// <param name="y">The second object to compare.</param>
        /// <returns><c>true</c> if the specified objects are equal; otherwise, <c>false</c>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public new bool Equals(object x, object y)
        {
            // Use a single null check for both objects to minimize branching
            return x == y || (x != null && y != null && x.Equals(y));
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current comparer.
        /// </summary>
        /// <param name="obj">The object to compare with the current comparer.</param>
        /// <returns><c>true</c> if the specified object is equal to the current comparer; otherwise, <c>false</c>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj) => obj is SwiftObjectEqualityComparer other && _entropy == other._entropy;

        /// <summary>
        /// Returns a hash code for the specified object, incorporating entropy for better distribution.
        /// </summary>
        /// <param name="obj">The object for which to get a hash code.</param>
        /// <returns>A hash code for the specified object.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode(object obj)
        {
            if (obj == null) return 0;
            return obj.GetHashCode() ^ (int)(_entropy & 0x7FFFFFFF);
        }

        /// <summary>
        /// Returns a hash code for the current comparer.
        /// </summary>
        /// <returns>A hash code for the current comparer.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public override int GetHashCode() => GetType().Name.GetHashCode() ^ (int)(_entropy & 0x7FFFFFFF);

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Entropy", _entropy);
        }
    }
}
