using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using System.Collections.Generic;

namespace SwiftCollections.Benchmarks;

[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class BucketWorkloadBenchmarks
{
    [Params(100, 1000, 10000, 100000)]
    public int N;

    private int[] _values;
    private int[] _lookupOrder;
    private int[] _churnPositions;
    private int[] _replacementValues;

    private SwiftBucket<int> _swiftBucket;
    private int[] _bucketIndices;
    private Dictionary<int, int> _dictionary;

    [GlobalSetup]
    public void Setup()
    {
        _values = TestHelper.GenerateShuffledRange(N, 42);
        _lookupOrder = TestHelper.GenerateShuffledRange(N, 123);

        int churnCount = N / 4;
        if (churnCount == 0)
            churnCount = 1;

        int[] churnOrder = TestHelper.GenerateShuffledRange(N, 777);
        _churnPositions = new int[churnCount];
        _replacementValues = new int[churnCount];

        for (int i = 0; i < churnCount; i++)
        {
            _churnPositions[i] = churnOrder[i];
            _replacementValues[i] = N + i;
        }
    }

    [IterationSetup(Targets = new[] {
        nameof(Dictionary_LookupByKey),
        nameof(Dictionary_RemoveByKey),
        nameof(Dictionary_EnumerateValues),
        nameof(Dictionary_KeyedChurn25)
    })]
    public void IterationSetup_Dictionary()
    {
        _dictionary = new Dictionary<int, int>(N);
        for (int i = 0; i < N; i++)
            _dictionary.Add(i, _values[i]);
    }

    [IterationSetup(Targets = new[] {
        nameof(Bucket_LookupByStoredIndex),
        nameof(Bucket_RemoveByStoredIndex),
        nameof(Bucket_EnumerateActiveValues),
        nameof(Bucket_StableSlotChurn25)
    })]
    public void IterationSetup_Bucket()
    {
        _swiftBucket = new SwiftBucket<int>(N);
        _bucketIndices = new int[N];

        for (int i = 0; i < N; i++)
            _bucketIndices[i] = _swiftBucket.Add(_values[i]);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Lookup")]
    public int Dictionary_LookupByKey()
    {
        int sum = 0;
        for (int i = 0; i < N; i++)
        {
            int key = _lookupOrder[i];
            if (_dictionary.TryGetValue(key, out int value))
                sum += value;
        }

        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("Lookup")]
    public int Bucket_LookupByStoredIndex()
    {
        int sum = 0;
        for (int i = 0; i < N; i++)
        {
            int position = _lookupOrder[i];
            sum += _swiftBucket[_bucketIndices[position]];
        }

        return sum;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Remove")]
    public int Dictionary_RemoveByKey()
    {
        int removed = 0;
        for (int i = 0; i < N; i++)
        {
            int key = _lookupOrder[i];
            if (_dictionary.Remove(key))
                removed++;
        }

        return removed;
    }

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public int Bucket_RemoveByStoredIndex()
    {
        int removed = 0;
        for (int i = 0; i < N; i++)
        {
            int position = _lookupOrder[i];
            if (_swiftBucket.TryRemoveAt(_bucketIndices[position]))
                removed++;
        }

        return removed;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Enumerate")]
    public int Dictionary_EnumerateValues()
    {
        int sum = 0;
        foreach (var item in _dictionary.Values)
            sum += item;
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("Enumerate")]
    public int Bucket_EnumerateActiveValues()
    {
        int sum = 0;
        foreach (var item in _swiftBucket)
            sum += item;
        return sum;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Churn")]
    public int Dictionary_KeyedChurn25()
    {
        for (int i = 0; i < _churnPositions.Length; i++)
        {
            int key = _churnPositions[i];
            _dictionary.Remove(key);
            _dictionary.Add(key, _replacementValues[i]);
        }

        return _dictionary.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Churn")]
    public int Bucket_StableSlotChurn25()
    {
        for (int i = 0; i < _churnPositions.Length; i++)
        {
            int position = _churnPositions[i];
            int index = _bucketIndices[position];

            _swiftBucket.TryRemoveAt(index);
            _swiftBucket.InsertAt(index, _replacementValues[i]);
        }

        return _swiftBucket.Count;
    }
}
