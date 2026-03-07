using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using System.Collections.Generic;

namespace SwiftCollections.Benchmarks;

[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class ListParityIntegerBenchmarks
{
    [Params(100, 1000, 10000, 100000)]
    public int N;

    private int[] _data;
    private int _insertValue;
    private int _removeValue;

    private List<int> _list;
    private SwiftList<int> _swiftList;

    [GlobalSetup]
    public void Setup()
    {
        _data = TestHelper.GenerateShuffledRange(N, 42);
        _insertValue = N;
        _removeValue = _data[N >> 1];
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
    public int SwiftList_Add()
    {
        var list = new SwiftList<int>(N);
        for (int i = 0; i < N; i++)
            list.Add(_data[i]);
        return list.Count;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("AddRange")]
    public int List_AddRange()
    {
        var list = new List<int>();
        list.AddRange(_data);
        return list.Count;
    }

    [Benchmark]
    [BenchmarkCategory("AddRange")]
    public int SwiftList_AddRange()
    {
        var list = new SwiftList<int>();
        list.AddRange(_data);
        return list.Count;
    }

    [IterationSetup(Targets = new[] {
        nameof(List_Enumerate),
        nameof(List_IndexerEnumerate),
        nameof(List_InsertMiddle),
        nameof(List_RemoveByValue),
        nameof(List_Sort),
        nameof(List_CopyTo),
        nameof(List_Clear)
    })]
    public void IterationSetup_List()
    {
        _list = new List<int>(_data);
    }

    [IterationSetup(Targets = new[] {
        nameof(SwiftList_Enumerate),
        nameof(SwiftList_IndexerEnumerate),
        nameof(SwiftList_InsertMiddle),
        nameof(SwiftList_RemoveByValue),
        nameof(SwiftList_Sort),
        nameof(SwiftList_CopyTo),
        nameof(SwiftList_Clear)
    })]
    public void IterationSetup_SwiftList()
    {
        _swiftList = new SwiftList<int>(_data);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Enumerate")]
    public int List_Enumerate()
    {
        int sum = 0;
        foreach (int item in _list)
            sum += item;
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("Enumerate")]
    public int SwiftList_Enumerate()
    {
        int sum = 0;
        foreach (int item in _swiftList)
            sum += item;
        return sum;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("IndexerEnumerate")]
    public int List_IndexerEnumerate()
    {
        int sum = 0;
        for (int i = 0; i < _list.Count; i++)
            sum += _list[i];
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("IndexerEnumerate")]
    public int SwiftList_IndexerEnumerate()
    {
        int sum = 0;
        for (int i = 0; i < _swiftList.Count; i++)
            sum += _swiftList[i];
        return sum;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("InsertMiddle")]
    public int List_InsertMiddle()
    {
        _list.Insert(N >> 1, _insertValue);
        return _list.Count;
    }

    [Benchmark]
    [BenchmarkCategory("InsertMiddle")]
    public int SwiftList_InsertMiddle()
    {
        _swiftList.Insert(N >> 1, _insertValue);
        return _swiftList.Count;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("RemoveByValue")]
    public int List_RemoveByValue()
    {
        _list.Remove(_removeValue);
        return _list.Count;
    }

    [Benchmark]
    [BenchmarkCategory("RemoveByValue")]
    public int SwiftList_RemoveByValue()
    {
        _swiftList.Remove(_removeValue);
        return _swiftList.Count;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Sort")]
    public int List_Sort()
    {
        _list.Sort();
        return _list[0] + _list[_list.Count - 1];
    }

    [Benchmark]
    [BenchmarkCategory("Sort")]
    public int SwiftList_Sort()
    {
        _swiftList.Sort();
        return _swiftList[0] + _swiftList[_swiftList.Count - 1];
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("CopyTo")]
    public int List_CopyTo()
    {
        var copy = new int[N];
        _list.CopyTo(copy, 0);
        return copy[0] + copy[copy.Length - 1];
    }

    [Benchmark]
    [BenchmarkCategory("CopyTo")]
    public int SwiftList_CopyTo()
    {
        var copy = new int[N];
        _swiftList.CopyTo(copy, 0);
        return copy[0] + copy[copy.Length - 1];
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Clear")]
    public int List_Clear()
    {
        int count = _list.Count;
        _list.Clear();
        return count;
    }

    [Benchmark]
    [BenchmarkCategory("Clear")]
    public int SwiftList_Clear()
    {
        int count = _swiftList.Count;
        _swiftList.Clear();
        return count;
    }
}
