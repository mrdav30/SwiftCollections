//=======================================================================
// QueryKeyIndexMap.cs
//=======================================================================
// MIT License, Copyright (c) 2024–present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using SwiftCollections.Utility;

namespace SwiftCollections.Query;

internal sealed class QueryKeyIndexMap<TKey> where TKey : notnull
{
    private readonly IEqualityComparer<TKey> _comparer;
    private readonly Func<int, TKey, bool> _isMatch;
    private readonly Func<int, bool> _canRehash;
    private readonly Func<int, TKey> _getKey;
    private int[] _buckets;
    private int _bucketMask;

    public QueryKeyIndexMap(
        int capacity,
        Func<int, TKey, bool> isMatch,
        Func<int, bool> canRehash,
        Func<int, TKey> getKey)
    {
        _comparer = SwiftHashTools.GetDeterministicEqualityComparer<TKey>();
        _isMatch = isMatch;
        _canRehash = canRehash;
        _getKey = getKey;
        capacity = NormalizeBucketCapacity(capacity);
        _buckets = new int[capacity].Populate(() => -1);
        _bucketMask = capacity - 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Insert(TKey key, int index)
    {
        int bucketIndex = GetStartBucket(key);

        while (_buckets[bucketIndex] != -1)
            bucketIndex = (bucketIndex + 1) & _bucketMask;

        _buckets[bucketIndex] = index;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Find(TKey key)
    {
        int bucketIndex = GetStartBucket(key);

        while (_buckets[bucketIndex] != -1)
        {
            int candidate = _buckets[bucketIndex];
            if (_isMatch(candidate, key))
                return candidate;

            bucketIndex = (bucketIndex + 1) & _bucketMask;
        }

        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove(TKey key)
    {
        int bucketIndex = GetStartBucket(key);

        while (_buckets[bucketIndex] != -1)
        {
            int candidate = _buckets[bucketIndex];
            if (_isMatch(candidate, key))
            {
                _buckets[bucketIndex] = -1;
                RehashBucketCluster((bucketIndex + 1) & _bucketMask);
                return true;
            }

            bucketIndex = (bucketIndex + 1) & _bucketMask;
        }

        return false;
    }

    public void ResizeAndRehash(int capacity, int entryCount)
    {
        capacity = NormalizeBucketCapacity(capacity);
        _buckets = new int[capacity].Populate(() => -1);
        _bucketMask = capacity - 1;

        for (int i = 0; i < entryCount; i++)
        {
            if (!_canRehash(i))
                continue;

            Insert(_getKey(i), i);
        }
    }

    public void Clear()
    {
        for (int i = 0; i < _buckets.Length; i++)
            _buckets[i] = -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetStartBucket(TKey key)
    {
        int hash = _comparer.GetHashCode(key) & 0x7FFFFFFF;
        return hash & _bucketMask;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int NormalizeBucketCapacity(int capacity)
    {
        capacity = SwiftHashTools.NextPowerOfTwo(capacity);
        return capacity <= 1 ? 2 : capacity * 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RehashBucketCluster(int startIndex)
    {
        int bucketIndex = startIndex;

        while (_buckets[bucketIndex] != -1)
        {
            int candidate = _buckets[bucketIndex];
            _buckets[bucketIndex] = -1;

            Insert(_getKey(candidate), candidate);

            bucketIndex = (bucketIndex + 1) & _bucketMask;
        }
    }
}
