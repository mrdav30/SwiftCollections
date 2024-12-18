using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Threading;

namespace SwiftCollections
{
    /// <summary>
    /// Provides helper methods and constants for hashing and power of two calculations used in hash-based collections.
    /// </summary>
    internal static class HashHelper
    {
        /// <summary>
        /// Provides a cryptographic random number generator for generating entropy.
        /// </summary>
        private static RandomNumberGenerator rng;

        /// <summary>
        /// Stores random bytes used for entropy generation.
        /// </summary>
        private static byte[] data;

        /// <summary>
        /// Tracks the current index in the entropy data buffer.
        /// </summary>
        private static int currentIndex = 1024;

        /// <summary>
        /// Synchronization object for thread-safe operations.
        /// </summary>
        private static readonly object lockObj = new object();

        /// <summary>
        /// Holds serialization information for objects during the serialization process.
        /// </summary>
        private static ConditionalWeakTable<object, SerializationInfo> s_SerializationInfoTable;

        /// <summary>
        /// Gets the table that stores serialization information for objects.
        /// </summary>
        internal static ConditionalWeakTable<object, SerializationInfo> SerializationInfoTable
        {
            get
            {
                if (s_SerializationInfoTable == null)
                {
                    ConditionalWeakTable<object, SerializationInfo> value = new ConditionalWeakTable<object, SerializationInfo>();
                    Interlocked.CompareExchange(ref s_SerializationInfoTable, value, null);
                }

                return s_SerializationInfoTable;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPowerOfTwo(int x) => (x != 0) && ((x & (x - 1)) == 0);

        /// <summary>
        /// Calculates the smallest power of two that is greater than or equal to the specified integer.
        /// This method is used to ensure that capacities are powers of two, enabling optimizations
        /// in indexing operations through bitwise arithmetic.
        /// </summary>
        /// <param name="value">The integer value for which to find the next power of two.</param>
        /// <returns>The smallest power of two greater than or equal to <paramref name="value"/>.</returns>
        /// <remarks>
        /// If <paramref name="value"/> is less than or equal to zero, the method returns 1.
        /// If <paramref name="value"/> is too large to represent as a power of two within an <c>int</c>,
        /// the methods returns `int.Max`.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int NextPowerOfTwo(int value)
        {
            if (value <= 0) return 1;
            if (value >= (1 << 30)) return int.MaxValue;

            value--;
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            value++;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsWellKnownEqualityComparer(object comparer)
        {
            return comparer == null || comparer == EqualityComparer<string>.Default;
        }

        public static IEqualityComparer GetSwiftEqualityComparer(object comparer)
        {
            if (comparer != null && comparer == EqualityComparer<string>.Default)
                return new SwiftStringEqualityComparer();
            return new SwiftObjectEqualityComparer();
        }

        /// <summary>
        /// Generates a cryptographically strong random 64-bit integer for use as entropy.
        /// </summary>
        /// <returns>A 64-bit integer filled with cryptographically strong random bytes.</returns>
        internal static long GetEntropy()
        {
            lock (lockObj)
            {
                if (currentIndex == 1024)
                {
                    if (rng == null)
                    {
                        rng = RandomNumberGenerator.Create();
                        data = new byte[1024];
                    }

                    rng.GetBytes(data);
                    currentIndex = 0;
                }

                long result = BitConverter.ToInt64(data, currentIndex);
                currentIndex += 8;
                return result;
            }
        }


        /// <summary>
        /// Computes a hash code for a string using the MurmurHash3 algorithm, incorporating entropy for randomization.
        /// </summary>
        /// <param name="key">The string to hash.</param>
        /// <param name="seed">The seed value for the hash function.</param>
        /// <returns>An integer hash code for the string.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int MurmurHash3(string key, int seed)
        {
            const uint c1 = 0xcc9e2d51;
            const uint c2 = 0x1b873593;
            uint h1 = (uint)seed;

            fixed (char* ptr = key)
            {
                int length = key.Length;
                int blockCount = length / 2;

                unchecked
                {
                    // Process 32-bit blocks
                    for (int i = 0; i < blockCount; i++)
                    {
                        uint k1 = *(uint*)(ptr + (i * 2));
                        k1 *= c1;
                        k1 = RotateLeft(k1, 15);
                        k1 *= c2;
                        h1 ^= k1;
                        h1 = RotateLeft(h1, 13);
                        h1 = h1 * 5 + 0xe6546b64;
                    }

                    // Handle remaining characters if odd length
                    if (length % 2 != 0)
                    {
                        uint k1 = ptr[length - 1];
                        k1 *= c1;
                        k1 = RotateLeft(k1, 15);
                        k1 *= c2;
                        h1 ^= k1;
                    }
                }

                h1 ^= (uint)length;
                h1 = FMix(h1);
                return (int)(h1 & 0x7FFFFFFF);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint RotateLeft(uint x, int r) => (x << r) | (x >> (32 - r));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint FMix(uint h)
        {
            h ^= h >> 16;
            h *= 0x85ebca6b;
            h ^= h >> 13;
            h *= 0xc2b2ae35;
            h ^= h >> 16;
            return h;
        }
    }
}
