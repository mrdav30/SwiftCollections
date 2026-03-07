using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using System.Collections.Generic;

namespace SwiftCollections.Benchmarks;

internal sealed class SetParityObject
{
    public string Id { get; set; }
    public string Name { get; set; }

    public override int GetHashCode() => Id.GetHashCode();

    public override bool Equals(object obj)
    {
        return obj is SetParityObject other && Id == other.Id;
    }
}

[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class SetParityObjectBenchmarks
{
    [Params(100, 1000, 10000, 100000)]
    public int N;

    private SetParityObject[] _data;

    private HashSet<SetParityObject> _hashSet;
    private SwiftHashSet<SetParityObject> _swiftHashSet;

    [GlobalSetup]
    public void Setup()
    {
        int[] order = TestHelper.GenerateShuffledRange(N, 42);
        _data = new SetParityObject[N];

        for (int i = 0; i < N; i++)
        {
            _data[i] = new SetParityObject
            {
                Id = $"Id_{order[i]}",
                Name = $"Name_{i}"
            };
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Add")]
    public int HashSet_Add()
    {
        var set = new HashSet<SetParityObject>(N);
        for (int i = 0; i < N; i++)
            set.Add(_data[i]);
        return set.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Add")]
    public int SwiftHashSet_Add()
    {
        var set = new SwiftHashSet<SetParityObject>(N);
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
        _hashSet = new HashSet<SetParityObject>(_data);
    }

    [IterationSetup(Targets = new[] {
        nameof(SwiftHashSet_Enumerate),
        nameof(SwiftHashSet_Contains),
        nameof(SwiftHashSet_Remove),
        nameof(SwiftHashSet_Clear)
    })]
    public void IterationSetup_SwiftHashSet()
    {
        _swiftHashSet = new SwiftHashSet<SetParityObject>(_data);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Enumerate")]
    public int HashSet_Enumerate()
    {
        int sum = 0;
        foreach (var item in _hashSet)
            sum += item.Name.Length;
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("Enumerate")]
    public int SwiftHashSet_Enumerate()
    {
        int sum = 0;
        foreach (var item in _swiftHashSet)
            sum += item.Name.Length;
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
}
