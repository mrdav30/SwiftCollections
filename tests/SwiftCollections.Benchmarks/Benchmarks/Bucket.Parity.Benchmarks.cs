using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using System.Collections.Generic;

namespace SwiftCollections.Benchmarks;

[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class BucketParityBenchmarks
{
    [Params(100, 1000, 10000, 100000)]
    public int N;

    private int[] _data;

    private SwiftBucket<int> _swiftBucket;
    private List<int> _list;

    [GlobalSetup]
    public void Setup()
    {
        _data = TestHelper.GenerateShuffledRange(N);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Add")]
    public int List_Add()
    {
        var list = new List<int>(N);
        for (int i = 0; i < N; i++)
            list.Add(_data[i]);
        return list.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Add")]
    public int Bucket_Add()
    {
        var bucket = new SwiftBucket<int>(N);
        for (int i = 0; i < N; i++)
            bucket.Add(_data[i]);
        return bucket.Count;
    }

    [IterationSetup(Targets = new[] {
            nameof(Bucket_RemoveByValue),
            nameof(Bucket_Enumerate),
            nameof(Bucket_ContainsValue)
        })]
    public void IterationSetup_Bucket()
    {
        _swiftBucket = new SwiftBucket<int>(N);
        for (int i = 0; i < N; i++)
            _swiftBucket.Add(_data[i]);
    }

    [IterationSetup(Targets = new[] {
            nameof(List_RemoveByValue),
            nameof(List_Enumerate),
            nameof(List_ContainsValue)
        })]
    public void IterationSetup_List()
    {
        _list = new List<int>(_data);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Remove")]
    public int List_RemoveByValue()
    {
        int removed = 0;
        for (int i = 0; i < N; i++)
        {
            if (_list.Remove(_data[i]))
                removed++;
        }

        return removed;
    }

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public int Bucket_RemoveByValue()
    {
        int removed = 0;
        for (int i = 0; i < N; i++)
        {
            if (_swiftBucket.TryRemove(_data[i]))
                removed++;
        }

        return removed;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Enumerate")]
    public int List_Enumerate()
    {
        int sum = 0;
        foreach (var item in _list)
            sum += item;
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("Enumerate")]
    public int Bucket_Enumerate()
    {
        int sum = 0;
        foreach (var item in _swiftBucket)
            sum += item;
        return sum;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Contains")]
    public int List_ContainsValue()
    {
        int found = 0;
        for (int i = 0; i < N; i++)
        {
            if (_list.Contains(_data[i]))
                found++;
        }

        return found;
    }

    [Benchmark]
    [BenchmarkCategory("Contains")]
    public int Bucket_ContainsValue()
    {
        int found = 0;
        for (int i = 0; i < N; i++)
        {
            if (_swiftBucket.Contains(_data[i]))
                found++;
        }

        return found;
    }
}
