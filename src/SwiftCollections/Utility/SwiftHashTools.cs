//=======================================================================
// SwiftHashTools.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Threading;
using Chronicler.Diagnostics;

namespace SwiftCollections.Utility;

/// <summary>
/// Provides utility functions for generating, combining, and working with hash codes.
/// </summary>
public static class SwiftHashTools
{
    // Bulk persistence sizes once without invoking insertion or collision-hardening entropy.
    internal static int GetPersistenceCapacity(int count, int minimum, int loadPercent)
    {
        long requested = (long)count * 100 / loadPercent;
        if (requested >= 1L << 30)
            throw new InvalidOperationException("Persisted hash state exceeds the supported capacity range.");
        return Math.Max(minimum, NextPowerOfTwo((int)requested));
    }

    internal const int DefaultDeterministicStringHashSeed = 0x51C4B5D;
    internal const int DefaultDeterministicObjectHashSeed = 0x1F6E4D75;

    private static readonly IEqualityComparer<string> s_deterministicStringComparer =
        new SwiftDeterministicStringEqualityComparer(DefaultDeterministicStringHashSeed);

    private static readonly IEqualityComparer<object> s_deterministicObjectComparer =
        new SwiftDeterministicObjectEqualityComparer(DefaultDeterministicObjectHashSeed);

    /// <summary>
    /// Provides a cryptographic random number generator for collision-hardening entropy.
    /// </summary>
    private static RandomNumberGenerator? rng;

    /// <summary>
    /// Stores random bytes used for entropy generation.
    /// </summary>
    private static byte[]? data;

    /// <summary>
    /// Tracks the current index in the entropy data buffer.
    /// </summary>
    private static int currentIndex = 1024;

    /// <summary>
    /// Synchronization object for thread-safe operations.
    /// </summary>
    private static readonly object lockObj = new();

    /// <summary>
    /// Holds serialization information for objects during the serialization process.
    /// </summary>
    private static ConditionalWeakTable<object, SerializationInfo>? s_SerializationInfoTable;

    /// <summary>
    /// Gets the table that stores serialization information for objects.
    /// </summary>
    internal static ConditionalWeakTable<object, SerializationInfo> SerializationInfoTable
    {
        get
        {
            if (s_SerializationInfoTable == null)
            {
                ConditionalWeakTable<object, SerializationInfo> value = new();
                Interlocked.CompareExchange(ref s_SerializationInfoTable, value, null);
            }

            return s_SerializationInfoTable;
        }
    }

    /// <summary>
    /// Determines whether the specified integer is a positive power of two.
    /// </summary>
    /// <param name="x">The signed integer to test.</param>
    /// <returns>true when x is positive and has exactly one set bit; otherwise, false. Zero and all negative
    /// inputs return false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPowerOfTwo(int x) => (x > 0) && ((x & (x - 1)) == 0);

    /// <summary>
    /// Calculates the smallest power of two that is greater than or equal to the specified integer. This method
    /// is used to ensure that capacities are powers of two, enabling optimizations in indexing operations through
    /// bitwise arithmetic.
    /// </summary>
    /// <param name="value">The signed requested size; non-positive inputs normalize to one.</param>
    /// <returns>One for non-positive inputs; the least power of two greater than or equal to positive inputs
    /// below 2^30 (the result can equal 2^30); Int32.MaxValue for inputs at or above 2^30.</returns>
    /// <remarks>
    /// Int32.MaxValue is a sentinel and is not a power of two. Even exactly 2^30 returns that sentinel. Callers
    /// that require bit-maskable capacities must handle this boundary explicitly.
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

    /// <summary>
    /// Determines whether the specified comparer is a recognized, well-known equality comparer supported by the
    /// system.
    /// </summary>
    /// <param name="comparer">The comparer object to inspect; null is accepted and selects fallback
    /// behavior.</param>
    /// <returns>true if the comparer is a well-known equality comparer; otherwise, false.</returns>
    /// <remarks>
    /// A well-known equality comparer is one that is commonly used and recognized by the system, such as the
    /// default equality comparers for string and object, or specific deterministic comparers. Use this method to
    /// check if a comparer is supported for optimized or special handling.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsWellKnownEqualityComparer(object comparer)
    {
        return comparer == null
            || comparer == EqualityComparer<string>.Default
            || comparer == EqualityComparer<object>.Default
            || comparer is SwiftDeterministicStringEqualityComparer
            || comparer is SwiftDeterministicObjectEqualityComparer;
    }

    /// <summary>
    /// Gets the default comparer used by SwiftCollections for the specified type when no comparer is supplied.
    /// String keys are hashed deterministically. Object keys hash strings deterministically, while other
    /// object-key determinism still depends on the underlying key type's <see cref="M:System.Object.GetHashCode"
    /// /> implementation.
    /// </summary>
    /// <typeparam name="T">The element/key type whose default comparer is requested.</typeparam>
    /// <returns>The shared deterministic string/object comparer for those exact types;
    /// EqualityComparer&lt;T&gt;.Default for every other type.</returns>
    /// <remarks>
    /// String equality is ordinal and hashes use the explicit deterministic seed. Object comparers hash strings
    /// deterministically, but other values rely on their own GetHashCode. Generic non-string types inherit their
    /// runtime default hashing; stable cross-process hashes are not guaranteed for those types.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEqualityComparer<T> GetDeterministicEqualityComparer<T>()
    {
        if (typeof(T) == typeof(string))
            return (IEqualityComparer<T>)GetDeterministicStringEqualityComparer();

        if (typeof(T) == typeof(object))
            return (IEqualityComparer<T>)GetDeterministicObjectEqualityComparer();

        return EqualityComparer<T>.Default;
    }

    /// <summary>
    /// Gets the deterministic default comparer used by SwiftCollections for strings.
    /// </summary>
    /// <returns>The shared default comparer when no seed or the built-in seed is used; otherwise a new comparer
    /// with the supplied seed.</returns>
    /// <remarks>
    /// String equality is ordinal and hashes use the explicit deterministic seed. Object comparers hash strings
    /// deterministically, but other values rely on their own GetHashCode. Generic non-string types inherit their
    /// runtime default hashing; stable cross-process hashes are not guaranteed for those types.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEqualityComparer<string> GetDeterministicStringEqualityComparer()
        => s_deterministicStringComparer;

    /// <summary>
    /// Gets a deterministic string comparer using the supplied seed.
    /// </summary>
    /// <param name="seed">The explicit signed hash seed; no entropy is added by the deterministic comparer or
    /// MurmurHash3. For CombineHashCodes the default is 5381.</param>
    /// <returns>The shared default comparer when no seed or the built-in seed is used; otherwise a new comparer
    /// with the supplied seed.</returns>
    /// <remarks>
    /// String equality is ordinal and hashes use the explicit deterministic seed. Object comparers hash strings
    /// deterministically, but other values rely on their own GetHashCode. Generic non-string types inherit their
    /// runtime default hashing; stable cross-process hashes are not guaranteed for those types.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEqualityComparer<string> GetDeterministicStringEqualityComparer(int seed)
        => seed == DefaultDeterministicStringHashSeed
            ? s_deterministicStringComparer
            : new SwiftDeterministicStringEqualityComparer(seed);

    /// <summary>
    /// Gets the default comparer used by SwiftCollections for object keys. Strings are hashed deterministically
    /// even when stored as objects. Other object keys still depend on their runtime <see
    /// cref="M:System.Object.GetHashCode" /> implementations.
    /// </summary>
    /// <returns>The shared default comparer when no seed or the built-in seed is used; otherwise a new comparer
    /// with the supplied seed.</returns>
    /// <remarks>
    /// String equality is ordinal and hashes use the explicit deterministic seed. Object comparers hash strings
    /// deterministically, but other values rely on their own GetHashCode. Generic non-string types inherit their
    /// runtime default hashing; stable cross-process hashes are not guaranteed for those types.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEqualityComparer<object> GetDeterministicObjectEqualityComparer()
        => s_deterministicObjectComparer;

    /// <summary>
    /// Gets an object comparer using the supplied seed. Strings are hashed deterministically even when stored as
    /// objects. Other object keys still depend on their runtime <see cref="M:System.Object.GetHashCode" />
    /// implementations.
    /// </summary>
    /// <param name="seed">The explicit signed hash seed; no entropy is added by the deterministic comparer or
    /// MurmurHash3. For CombineHashCodes the default is 5381.</param>
    /// <returns>The shared default comparer when no seed or the built-in seed is used; otherwise a new comparer
    /// with the supplied seed.</returns>
    /// <remarks>
    /// String equality is ordinal and hashes use the explicit deterministic seed. Object comparers hash strings
    /// deterministically, but other values rely on their own GetHashCode. Generic non-string types inherit their
    /// runtime default hashing; stable cross-process hashes are not guaranteed for those types.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEqualityComparer<object> GetDeterministicObjectEqualityComparer(int seed)
        => seed == DefaultDeterministicObjectHashSeed
            ? s_deterministicObjectComparer
            : new SwiftDeterministicObjectEqualityComparer(seed);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static IEqualityComparer<T> GetDefaultEqualityComparer<T>(IEqualityComparer<T>? comparer = null)
        => comparer ?? GetDeterministicEqualityComparer<T>();

    /// <summary>
    /// Creates a randomized non-generic comparer for collision hardening.
    /// </summary>
    /// <param name="comparer">The comparer object to inspect; null is accepted and selects fallback
    /// behavior.</param>
    /// <returns>A new randomized string comparer for recognized string comparers; otherwise a new randomized
    /// object comparer, including null/unrecognized inputs.</returns>
    /// <remarks>
    /// Uses cryptographic entropy and does not preserve custom comparer equality semantics or deterministic hash
    /// codes. This is the collision-hardening fallback used by recognized default table comparers, not a
    /// serialization determinism helper.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEqualityComparer GetSwiftEqualityComparer(object comparer)
    {
        if (comparer == EqualityComparer<string>.Default || comparer is SwiftDeterministicStringEqualityComparer)
            return new SwiftStringEqualityComparer();

        if (comparer == EqualityComparer<object>.Default || comparer is SwiftDeterministicObjectEqualityComparer)
            return new SwiftObjectEqualityComparer();

        return new SwiftObjectEqualityComparer();
    }

    /// <summary>
    /// Generates a cryptographically strong random 64-bit integer for collision-hardening entropy.
    /// </summary>
    /// <returns>A 64-bit integer filled with cryptographically strong random bytes.</returns>
    internal static long GetEntropy()
    {
        lock (lockObj)
        {
            if (currentIndex == 1024 || rng == null || data == null)
            {
                rng ??= RandomNumberGenerator.Create();
                data ??= new byte[1024];

                rng.GetBytes(data);
                currentIndex = 0;
            }

            long result = BitConverter.ToInt64(data, currentIndex);
            currentIndex += 8;
            return result;
        }
    }

    #region Hash Code Combination

    /// <summary>
    /// Combines the hash codes of the elements in a tuple using a DJB2-inspired mixing strategy.
    /// </summary>
    /// <param name="tupled">The non-null tuple whose items are read in index order; null elements contribute
    /// zero. The tuple is not retained or modified.</param>
    /// <param name="seed">The explicit signed hash seed; no entropy is added by the deterministic comparer or
    /// MurmurHash3. For CombineHashCodes the default is 5381.</param>
    /// <param name="shift1">The initial left-shift count (default 16); C# masks Int32 shift counts to five bits,
    /// including negative counts.</param>
    /// <param name="shift2">The per-item left-shift count (default 5), masked to five bits.</param>
    /// <param name="shift3">The arithmetic right-shift count (default 27), masked to five bits.</param>
    /// <param name="factor3">The final signed mixing factor (default 1566083941); arithmetic wraps as
    /// Int32.</param>
    /// <returns>The signed Int32 mixed hash, with wrapping arithmetic; it is not guaranteed non-negative or
    /// collision-free.</returns>
    /// <exception cref="T:System.NullReferenceException">The tuple or array is null.</exception>
    /// <remarks>
    /// Results depend on input order and the input objects' GetHashCode implementations. Tuple mixing ends with
    /// XOR, while array and integer overloads end with addition, so their results need not agree for the same
    /// item hashes. Default object/string hashes can vary across processes; do not serialize this result as a
    /// stable identity.
    /// </remarks>
    public static int CombineHashCodes(
        this ITuple tupled,
        int seed = 5381,
        int shift1 = 16,
        int shift2 = 5,
        int shift3 = 27,
        int factor3 = 1566083941)
    {
        int hash1 = (seed << shift1) + seed;
        int hash2 = hash1;

        for (int i = 0; i < tupled.Length; i++)
        {
            int itemHash = tupled[i]?.GetHashCode() ?? 0;
            unchecked
            {
                if (i % 2 == 0)
                    hash1 = ((hash1 << shift2) + hash1 + (hash1 >> shift3)) ^ itemHash;
                else
                    hash2 = ((hash2 << shift2) + hash2 + (hash2 >> shift3)) ^ itemHash;
            }
        }

        return hash1 ^ (hash2 * factor3);
    }

    /// <summary>
    /// Combines the hash codes of a set of objects using a DJB2-inspired mixing strategy.
    /// </summary>
    /// <param name="values">The non-null object array, read in order without retaining or modifying it. Null
    /// elements contribute zero; the params form can allocate an array at the call site.</param>
    /// <param name="seed">The explicit signed hash seed; no entropy is added by the deterministic comparer or
    /// MurmurHash3. For CombineHashCodes the default is 5381.</param>
    /// <param name="shift1">The initial left-shift count (default 16); C# masks Int32 shift counts to five bits,
    /// including negative counts.</param>
    /// <param name="shift2">The per-item left-shift count (default 5), masked to five bits.</param>
    /// <param name="shift3">The arithmetic right-shift count (default 27), masked to five bits.</param>
    /// <param name="factor3">The final signed mixing factor (default 1566083941); arithmetic wraps as
    /// Int32.</param>
    /// <returns>The signed Int32 mixed hash, with wrapping arithmetic; it is not guaranteed non-negative or
    /// collision-free.</returns>
    /// <exception cref="T:System.NullReferenceException">The tuple or array is null.</exception>
    /// <remarks>
    /// Results depend on input order and the input objects' GetHashCode implementations. Tuple mixing ends with
    /// XOR, while array and integer overloads end with addition, so their results need not agree for the same
    /// item hashes. Default object/string hashes can vary across processes; do not serialize this result as a
    /// stable identity.
    /// </remarks>
    public static int CombineHashCodes(
        this object[] values,
        int seed = 5381,
        int shift1 = 16,
        int shift2 = 5,
        int shift3 = 27,
        int factor3 = 1566083941)
    {
        int hash1 = (seed << shift1) + seed;
        int hash2 = hash1;

        for (int i = 0; i < values.Length; i++)
        {
            var itemHash = values[i]?.GetHashCode() ?? 0;
            unchecked
            {
                if ((i & 1) == 0)
                    hash1 = ((hash1 << shift2) + hash1 + (hash1 >> shift3)) ^ itemHash;
                else
                    hash2 = ((hash2 << shift2) + hash2 + (hash2 >> shift3)) ^ itemHash;
            }
        }

        return hash1 + (hash2 * factor3);
    }

    /// <summary>
    /// Combines the hash codes of the provided objects using a DJB2-inspired mixing strategy.
    /// </summary>
    /// <param name="values">The non-null object array, read in order without retaining or modifying it. Null
    /// elements contribute zero; the params form can allocate an array at the call site.</param>
    /// <returns>The signed Int32 mixed hash, with wrapping arithmetic; it is not guaranteed non-negative or
    /// collision-free.</returns>
    /// <exception cref="T:System.NullReferenceException">The tuple or array is null.</exception>
    /// <remarks>
    /// Results depend on input order and the input objects' GetHashCode implementations. Tuple mixing ends with
    /// XOR, while array and integer overloads end with addition, so their results need not agree for the same
    /// item hashes. Default object/string hashes can vary across processes; do not serialize this result as a
    /// stable identity.
    /// </remarks>
    public static int CombineHashCodes(params object[] values) => values.CombineHashCodes();

    /// <summary>
    /// Combines two integer hash codes without allocating the object-array params path.
    /// </summary>
    /// <param name="value1">The first already computed integer hash code.</param>
    /// <param name="value2">The second already computed integer hash code.</param>
    /// <returns>The signed Int32 mixed hash, with wrapping arithmetic; it is not guaranteed non-negative or
    /// collision-free.</returns>
    /// <remarks>
    /// Results depend on input order and the input objects' GetHashCode implementations. Tuple mixing ends with
    /// XOR, while array and integer overloads end with addition, so their results need not agree for the same
    /// item hashes. Default object/string hashes can vary across processes; do not serialize this result as a
    /// stable identity.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CombineHashCodes(
        int value1,
        int value2)
    {
        const int seed = 5381;
        const int shift1 = 16;
        const int shift2 = 5;
        const int shift3 = 27;
        const int factor3 = 1566083941;
        int hash1 = (seed << shift1) + seed;
        int hash2 = hash1;

        hash1 = MixHashCode(hash1, value1, shift2, shift3);
        hash2 = MixHashCode(hash2, value2, shift2, shift3);

        return hash1 + (hash2 * factor3);
    }

    /// <summary>
    /// Combines three integer hash codes without allocating the object-array params path.
    /// </summary>
    /// <param name="value1">The first already computed integer hash code.</param>
    /// <param name="value2">The second already computed integer hash code.</param>
    /// <param name="value3">The third already computed integer hash code.</param>
    /// <returns>The signed Int32 mixed hash, with wrapping arithmetic; it is not guaranteed non-negative or
    /// collision-free.</returns>
    /// <remarks>
    /// Results depend on input order and the input objects' GetHashCode implementations. Tuple mixing ends with
    /// XOR, while array and integer overloads end with addition, so their results need not agree for the same
    /// item hashes. Default object/string hashes can vary across processes; do not serialize this result as a
    /// stable identity.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CombineHashCodes(
        int value1,
        int value2,
        int value3)
    {
        const int seed = 5381;
        const int shift1 = 16;
        const int shift2 = 5;
        const int shift3 = 27;
        const int factor3 = 1566083941;
        int hash1 = (seed << shift1) + seed;
        int hash2 = hash1;

        hash1 = MixHashCode(hash1, value1, shift2, shift3);
        hash2 = MixHashCode(hash2, value2, shift2, shift3);
        hash1 = MixHashCode(hash1, value3, shift2, shift3);

        return hash1 + (hash2 * factor3);
    }

    /// <summary>
    /// Combines four integer hash codes without allocating the object-array params path.
    /// </summary>
    /// <param name="value1">The first already computed integer hash code.</param>
    /// <param name="value2">The second already computed integer hash code.</param>
    /// <param name="value3">The third already computed integer hash code.</param>
    /// <param name="value4">The fourth already computed integer hash code.</param>
    /// <returns>The signed Int32 mixed hash, with wrapping arithmetic; it is not guaranteed non-negative or
    /// collision-free.</returns>
    /// <remarks>
    /// Results depend on input order and the input objects' GetHashCode implementations. Tuple mixing ends with
    /// XOR, while array and integer overloads end with addition, so their results need not agree for the same
    /// item hashes. Default object/string hashes can vary across processes; do not serialize this result as a
    /// stable identity.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CombineHashCodes(
        int value1,
        int value2,
        int value3,
        int value4)
    {
        const int seed = 5381;
        const int shift1 = 16;
        const int shift2 = 5;
        const int shift3 = 27;
        const int factor3 = 1566083941;
        int hash1 = (seed << shift1) + seed;
        int hash2 = hash1;

        hash1 = MixHashCode(hash1, value1, shift2, shift3);
        hash2 = MixHashCode(hash2, value2, shift2, shift3);
        hash1 = MixHashCode(hash1, value3, shift2, shift3);
        hash2 = MixHashCode(hash2, value4, shift2, shift3);

        return hash1 + (hash2 * factor3);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int MixHashCode(int hash, int itemHash, int shift2, int shift3)
    {
        unchecked
        {
            return ((hash << shift2) + hash + (hash >> shift3)) ^ itemHash;
        }
    }

    /// <summary>
    /// Computes a seeded MurmurHash3-style hash of UTF-16 string storage.
    /// </summary>
    /// <param name="key">The non-null UTF-16 string to hash; read without mutation or retention.</param>
    /// <param name="seed">The explicit signed hash seed; no entropy is added by the deterministic comparer or
    /// MurmurHash3. For CombineHashCodes the default is 5381.</param>
    /// <returns>The computed hash with its sign bit cleared, from zero through Int32.MaxValue.</returns>
    /// <exception cref="T:System.ArgumentNullException">key is null.</exception>
    /// <remarks>
    /// Adds no random entropy beyond the supplied seed. Pairs of UTF-16 characters are loaded as native-endian
    /// UInt32 values, so cross-endian identity is not guaranteed. Empty strings are supported; the algorithm uses
    /// character count rather than UTF-16 byte count.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe int MurmurHash3(string key, int seed)
    {
        ChronicleGuard.ThrowIfNull(key, nameof(key));

        const uint c1 = 0xcc9e2d51;
        const uint c2 = 0x1b873593;
        uint h1 = (uint)seed;
        ReadOnlySpan<char> chars = key.AsSpan();

        fixed (char* ptr = chars)
        {
            int length = chars.Length;
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

    #endregion
}
