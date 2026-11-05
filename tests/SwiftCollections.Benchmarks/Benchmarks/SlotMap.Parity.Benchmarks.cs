using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace SwiftCollections.Benchmarks;

[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class SlotMapParityBenchmarks
{
    [Params(100, 1000, 10000, 100000)]
    public int N;

    private int[] _data;

    private SwiftSlotMap<int> _swiftSlotMap;
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
    public int SlotMap_Add()
    {
        var slotMap = new SwiftSlotMap<int>(N);
        for (int i = 0; i < N; i++)
            slotMap.Add(_data[i]);
        return slotMap.Count;
    }

    [IterationSetup(Targets = new[] {
            nameof(SlotMap_RemoveByValue),
            nameof(SlotMap_Enumerate),
            nameof(SlotMap_ContainsValue)
        })]
    public void IterationSetup_SlotMap()
    {
        _swiftSlotMap = new SwiftSlotMap<int>(N);
        for (int i = 0; i < N; i++)
            _swiftSlotMap.Add(_data[i]);
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
    public int SlotMap_RemoveByValue()
    {
        int removed = 0;
        for (int i = 0; i < N; i++)
        {
            if (_swiftSlotMap.TryRemove(_data[i]))
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
    public int SlotMap_Enumerate()
    {
        int sum = 0;
        foreach (var item in _swiftSlotMap)
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
    public int SlotMap_ContainsValue()
    {
        int found = 0;
        for (int i = 0; i < N; i++)
        {
            if (_swiftSlotMap.Contains(_data[i]))
                found++;
        }

        return found;
    }
}
