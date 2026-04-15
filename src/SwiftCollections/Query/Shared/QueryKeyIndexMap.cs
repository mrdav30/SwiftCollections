using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SwiftCollections.Query;

internal sealed class QueryKeyIndexMap<TKey>
{
    private readonly IEqualityComparer<TKey> _comparer;
    private int[] _buckets;
    private int _bucketMask;

    public QueryKeyIndexMap(int capacity, IEqualityComparer<TKey> comparer = null)
    {
        _comparer = comparer ?? SwiftHashTools.GetDeterministicEqualityComparer<TKey>();
        capacity = NormalizeBucketCapacity(capacity);
        _buckets = new int[capacity].Populate(() => -1);
        _bucketMask = capacity - 1;
    }

    public int Capacity => _buckets.Length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Insert(TKey key, int index)
    {
        int bucketIndex = GetStartBucket(key);

        while (_buckets[bucketIndex] != -1)
            bucketIndex = (bucketIndex + 1) & _bucketMask;

        _buckets[bucketIndex] = index;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Find(TKey key, Func<int, TKey, bool> isMatch)
    {
        int bucketIndex = GetStartBucket(key);

        while (_buckets[bucketIndex] != -1)
        {
            int candidate = _buckets[bucketIndex];
            if (isMatch(candidate, key))
                return candidate;

            bucketIndex = (bucketIndex + 1) & _bucketMask;
        }

        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove(
        TKey key,
        Func<int, TKey, bool> isMatch,
        Func<int, bool> canRehash,
        Func<int, TKey> getKey)
    {
        int bucketIndex = GetStartBucket(key);

        while (_buckets[bucketIndex] != -1)
        {
            int candidate = _buckets[bucketIndex];
            if (isMatch(candidate, key))
            {
                _buckets[bucketIndex] = -1;
                RehashBucketCluster((bucketIndex + 1) & _bucketMask, canRehash, getKey);
                return true;
            }

            bucketIndex = (bucketIndex + 1) & _bucketMask;
        }

        return false;
    }

    public void ResizeAndRehash(int capacity, int entryCount, Func<int, bool> shouldRehash, Func<int, TKey> getKey)
    {
        capacity = NormalizeBucketCapacity(capacity);
        _buckets = new int[capacity].Populate(() => -1);
        _bucketMask = capacity - 1;

        for (int i = 0; i < entryCount; i++)
        {
            if (!shouldRehash(i))
                continue;

            Insert(getKey(i), i);
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
        capacity = QueryCollectionGuards.NormalizeCapacity(capacity);
        return capacity <= 1 ? 2 : capacity * 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RehashBucketCluster(int startIndex, Func<int, bool> canRehash, Func<int, TKey> getKey)
    {
        int bucketIndex = startIndex;

        while (_buckets[bucketIndex] != -1)
        {
            int candidate = _buckets[bucketIndex];
            _buckets[bucketIndex] = -1;

            if (canRehash(candidate))
                Insert(getKey(candidate), candidate);

            bucketIndex = (bucketIndex + 1) & _bucketMask;
        }
    }
}
