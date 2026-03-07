using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using System.Collections.Generic;

namespace SwiftCollections.Benchmarks;

[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class SetParityIntegerBenchmarks
{
    [Params(100, 1000, 10000, 100000)]
    public int N;

    private int[] _data;

    private HashSet<int> _hashSet;
    private SwiftHashSet<int> _swiftHashSet;
    private SwiftPackedSet<int> _packedSet;

    [GlobalSetup]
    public void Setup()
    {
        _data = TestHelper.GenerateShuffledRange(N);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Add")]
    public int HashSet_Add()
    {
        var set = new HashSet<int>(N);
        for (int i = 0; i < N; i++)
            set.Add(_data[i]);
        return set.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Add")]
    public int SwiftHashSet_Add()
    {
        var set = new SwiftHashSet<int>(N);
        for (int i = 0; i < N; i++)
            set.Add(_data[i]);
        return set.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Add")]
    public int PackedSet_Add()
    {
        var set = new SwiftPackedSet<int>(N);
        for (int i = 0; i < N; i++)
            set.Add(_data[i]);
        return set.Count;
    }

    [IterationSetup(Targets = new[] {
        nameof(HashSet_Enumerate),
        nameof(HashSet_Contains),
        nameof(HashSet_Remove),
        nameof(HashSet_Clear)
    })]
    public void IterationSetup_HashSet()
    {
        _hashSet = new HashSet<int>(_data);
    }

    [IterationSetup(Targets = new[] {
        nameof(SwiftHashSet_Enumerate),
        nameof(SwiftHashSet_Contains),
        nameof(SwiftHashSet_Remove),
        nameof(SwiftHashSet_Clear)
    })]
    public void IterationSetup_SwiftHashSet()
    {
        _swiftHashSet = new SwiftHashSet<int>(_data);
    }

    [IterationSetup(Targets = new[] {
        nameof(PackedSet_Enumerate),
        nameof(PackedSet_Contains),
        nameof(PackedSet_Remove),
        nameof(PackedSet_Clear)
    })]
    public void IterationSetup_PackedSet()
    {
        _packedSet = new SwiftPackedSet<int>(N);
        for (int i = 0; i < N; i++)
            _packedSet.Add(_data[i]);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Enumerate")]
    public int HashSet_Enumerate()
    {
        int sum = 0;
        foreach (var item in _hashSet)
            sum += item;
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("Enumerate")]
    public int SwiftHashSet_Enumerate()
    {
        int sum = 0;
        foreach (var item in _swiftHashSet)
            sum += item;
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("Enumerate")]
    public int PackedSet_Enumerate()
    {
        int sum = 0;
        foreach (var item in _packedSet)
            sum += item;
        return sum;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Contains")]
    public int HashSet_Contains()
    {
        int found = 0;
        for (int i = 0; i < N; i++)
        {
            if (_hashSet.Contains(_data[i]))
                found++;
        }

        return found;
    }

    [Benchmark]
    [BenchmarkCategory("Contains")]
    public int SwiftHashSet_Contains()
    {
        int found = 0;
        for (int i = 0; i < N; i++)
        {
            if (_swiftHashSet.Contains(_data[i]))
                found++;
        }

        return found;
    }

    [Benchmark]
    [BenchmarkCategory("Contains")]
    public int PackedSet_Contains()
    {
        int found = 0;
        for (int i = 0; i < N; i++)
        {
            if (_packedSet.Contains(_data[i]))
                found++;
        }

        return found;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Remove")]
    public int HashSet_Remove()
    {
        int removed = 0;
        for (int i = 0; i < N; i++)
        {
            if (_hashSet.Remove(_data[i]))
                removed++;
        }

        return removed;
    }

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public int SwiftHashSet_Remove()
    {
        int removed = 0;
        for (int i = 0; i < N; i++)
        {
            if (_swiftHashSet.Remove(_data[i]))
                removed++;
        }

        return removed;
    }

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public int PackedSet_Remove()
    {
        int removed = 0;
        for (int i = 0; i < N; i++)
        {
            if (_packedSet.Remove(_data[i]))
                removed++;
        }

        return removed;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Clear")]
    public int HashSet_Clear()
    {
        int count = _hashSet.Count;
        _hashSet.Clear();
        return count;
    }

    [Benchmark]
    [BenchmarkCategory("Clear")]
    public int SwiftHashSet_Clear()
    {
        int count = _swiftHashSet.Count;
        _swiftHashSet.Clear();
        return count;
    }

    [Benchmark]
    [BenchmarkCategory("Clear")]
    public int PackedSet_Clear()
    {
        int count = _packedSet.Count;
        _packedSet.Clear();
        return count;
    }
}
