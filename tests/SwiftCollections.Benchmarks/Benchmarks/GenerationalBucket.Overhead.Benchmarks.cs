using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace SwiftCollections.Benchmarks;

[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class GenerationalBucketOverheadBenchmarks
{
    [Params(100, 1000, 10000, 100000)]
    public int N;

    private int[] _data;
    private int[] _bucketIndices;
    private SwiftGenerationalBucket<int>.Handle[] _handles;

    private SwiftBucket<int> _bucket;
    private SwiftGenerationalBucket<int> _generationalBucket;

    [GlobalSetup]
    public void Setup()
    {
        _data = TestHelper.GenerateShuffledRange(N);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Add")]
    public int Bucket_Add()
    {
        var bucket = new SwiftBucket<int>(N);
        for (int i = 0; i < N; i++)
            bucket.Add(_data[i]);
        return bucket.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Add")]
    public int GenerationalBucket_Add()
    {
        var bucket = new SwiftGenerationalBucket<int>(N);
        for (int i = 0; i < N; i++)
            bucket.Add(_data[i]);
        return bucket.Count;
    }

    [IterationSetup(Targets = new[] {
        nameof(Bucket_LookupByIndex),
        nameof(Bucket_Enumerate),
        nameof(Bucket_RemoveByIndex),
        nameof(Bucket_IndexValidityCheck)
    })]
    public void IterationSetup_Bucket()
    {
        _bucket = new SwiftBucket<int>(N);
        _bucketIndices = new int[N];

        for (int i = 0; i < N; i++)
            _bucketIndices[i] = _bucket.Add(_data[i]);
    }

    [IterationSetup(Targets = new[] {
        nameof(GenerationalBucket_LookupByHandle),
        nameof(GenerationalBucket_Enumerate),
        nameof(GenerationalBucket_RemoveByHandle),
        nameof(GenerationalBucket_HandleValidityCheck)
    })]
    public void IterationSetup_GenerationalBucket()
    {
        _generationalBucket = new SwiftGenerationalBucket<int>(N);
        _handles = new SwiftGenerationalBucket<int>.Handle[N];

        for (int i = 0; i < N; i++)
            _handles[i] = _generationalBucket.Add(_data[i]);
    }

    [BenchmarkCategory("Lookup")]
    [Benchmark(Baseline = true)]
    public int Bucket_LookupByIndex()
    {
        int sum = 0;
        for (int i = 0; i < N; i++)
            sum += _bucket[_bucketIndices[i]];
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("Lookup")]
    public int GenerationalBucket_LookupByHandle()
    {
        int sum = 0;
        for (int i = 0; i < N; i++)
        {
            if (_generationalBucket.TryGet(_handles[i], out int value))
                sum += value;
        }

        return sum;
    }

    [BenchmarkCategory("Enumerate")]
    [Benchmark(Baseline = true)]
    public int Bucket_Enumerate()
    {
        int sum = 0;
        foreach (var item in _bucket)
            sum += item;
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("Enumerate")]
    public int GenerationalBucket_Enumerate()
    {
        int sum = 0;
        foreach (var item in _generationalBucket)
            sum += item;
        return sum;
    }

    [BenchmarkCategory("Remove")]
    [Benchmark(Baseline = true)]
    public int Bucket_RemoveByIndex()
    {
        int removed = 0;
        for (int i = 0; i < N; i++)
        {
            if (_bucket.TryRemoveAt(_bucketIndices[i]))
                removed++;
        }

        return removed;
    }

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public int GenerationalBucket_RemoveByHandle()
    {
        int removed = 0;
        for (int i = 0; i < N; i++)
        {
            if (_generationalBucket.Remove(_handles[i]))
                removed++;
        }

        return removed;
    }

    [BenchmarkCategory("Validity")]
    [Benchmark(Baseline = true)]
    public int Bucket_IndexValidityCheck()
    {
        int allocated = 0;
        for (int i = 0; i < N; i++)
        {
            if (_bucket.IsAllocated(_bucketIndices[i]))
                allocated++;
        }

        return allocated;
    }

    [Benchmark]
    [BenchmarkCategory("Validity")]
    public int GenerationalBucket_HandleValidityCheck()
    {
        int valid = 0;
        for (int i = 0; i < N; i++)
        {
            if (_generationalBucket.IsValid(_handles[i]))
                valid++;
        }

        return valid;
    }
}
