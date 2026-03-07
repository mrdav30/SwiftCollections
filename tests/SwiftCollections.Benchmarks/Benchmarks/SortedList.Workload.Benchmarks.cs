using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using System.Collections.Generic;

namespace SwiftCollections.Benchmarks;

[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class SortedListWorkloadBenchmarks
{
    [Params(100, 1000, 5000, 10000)]
    public int N;

    private int[] _data;
    private int[] _lookupOrder;
    private int[] _mutationKeys;
    private int[] _replacementValues;
    private int _mutationCount;
    private int _popCount;

    private List<int> _manualSortedList;
    private SwiftSortedList<int> _swiftSortedList;

    [GlobalSetup]
    public void Setup()
    {
        _data = TestHelper.GenerateShuffledRange(N, 42);
        _lookupOrder = TestHelper.GenerateShuffledRange(N, 123);

        _mutationCount = N >> 5;
        if (_mutationCount == 0)
            _mutationCount = 1;
        if (_mutationCount > 1024)
            _mutationCount = 1024;

        _popCount = _mutationCount;
        _mutationKeys = new int[_mutationCount];
        _replacementValues = new int[_mutationCount];

        for (int i = 0; i < _mutationCount; i++)
        {
            _mutationKeys[i] = _lookupOrder[i];
            _replacementValues[i] = N + i;
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("IncrementalAdd")]
    public int ManualSortedList_IncrementalAdd()
    {
        var list = new List<int>(N);
        for (int i = 0; i < N; i++)
            InsertSorted(list, _data[i]);
        return list.Count;
    }

    [Benchmark]
    [BenchmarkCategory("IncrementalAdd")]
    public int SwiftSortedList_IncrementalAdd()
    {
        var list = new SwiftSortedList<int>(N);
        for (int i = 0; i < N; i++)
            list.Add(_data[i]);
        return list.Count;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("BulkLoad")]
    public int ManualSortedList_BulkLoad()
    {
        var list = new List<int>(N);
        list.AddRange(_data);
        list.Sort();
        return list.Count;
    }

    [Benchmark]
    [BenchmarkCategory("BulkLoad")]
    public int SwiftSortedList_BulkLoad()
    {
        var list = new SwiftSortedList<int>(N);
        list.AddRange(_data);
        return list.Count;
    }

    [IterationSetup(Targets = new[] {
        nameof(ManualSortedList_Search),
        nameof(ManualSortedList_Enumerate),
        nameof(ManualSortedList_RemoveByValue),
        nameof(ManualSortedList_ChurnSubset),
        nameof(ManualSortedList_PopMin)
    })]
    public void IterationSetup_ManualSortedList()
    {
        _manualSortedList = new List<int>(N);
        _manualSortedList.AddRange(_data);
        _manualSortedList.Sort();
    }

    [IterationSetup(Targets = new[] {
        nameof(SwiftSortedList_Search),
        nameof(SwiftSortedList_Enumerate),
        nameof(SwiftSortedList_RemoveByValue),
        nameof(SwiftSortedList_ChurnSubset),
        nameof(SwiftSortedList_PopMin)
    })]
    public void IterationSetup_SwiftSortedList()
    {
        _swiftSortedList = new SwiftSortedList<int>(N);
        _swiftSortedList.AddRange(_data);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Search")]
    public int ManualSortedList_Search()
    {
        int sum = 0;
        for (int i = 0; i < N; i++)
        {
            int index = _manualSortedList.BinarySearch(_lookupOrder[i]);
            if (index >= 0)
                sum += index;
        }

        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("Search")]
    public int SwiftSortedList_Search()
    {
        int sum = 0;
        for (int i = 0; i < N; i++)
        {
            int index = _swiftSortedList.Search(_lookupOrder[i]);
            if (index >= 0)
                sum += index;
        }

        return sum;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Enumerate")]
    public int ManualSortedList_Enumerate()
    {
        int sum = 0;
        foreach (int item in _manualSortedList)
            sum += item;
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("Enumerate")]
    public int SwiftSortedList_Enumerate()
    {
        int sum = 0;
        foreach (int item in _swiftSortedList)
            sum += item;
        return sum;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("RemoveByValue")]
    public int ManualSortedList_RemoveByValue()
    {
        for (int i = 0; i < _mutationCount; i++)
        {
            int index = _manualSortedList.BinarySearch(_mutationKeys[i]);
            if (index >= 0)
                _manualSortedList.RemoveAt(index);
        }

        return _manualSortedList.Count;
    }

    [Benchmark]
    [BenchmarkCategory("RemoveByValue")]
    public int SwiftSortedList_RemoveByValue()
    {
        for (int i = 0; i < _mutationCount; i++)
            _swiftSortedList.Remove(_mutationKeys[i]);
        return _swiftSortedList.Count;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Churn")]
    public int ManualSortedList_ChurnSubset()
    {
        for (int i = 0; i < _mutationCount; i++)
        {
            int index = _manualSortedList.BinarySearch(_mutationKeys[i]);
            if (index >= 0)
                _manualSortedList.RemoveAt(index);

            InsertSorted(_manualSortedList, _replacementValues[i]);
        }

        return _manualSortedList.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Churn")]
    public int SwiftSortedList_ChurnSubset()
    {
        for (int i = 0; i < _mutationCount; i++)
        {
            _swiftSortedList.Remove(_mutationKeys[i]);
            _swiftSortedList.Add(_replacementValues[i]);
        }

        return _swiftSortedList.Count;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("PopMin")]
    public int ManualSortedList_PopMin()
    {
        int sum = 0;
        for (int i = 0; i < _popCount; i++)
        {
            sum += _manualSortedList[0];
            _manualSortedList.RemoveAt(0);
        }

        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("PopMin")]
    public int SwiftSortedList_PopMin()
    {
        int sum = 0;
        for (int i = 0; i < _popCount; i++)
            sum += _swiftSortedList.PopMin();
        return sum;
    }

    private static void InsertSorted(List<int> list, int value)
    {
        int index = list.BinarySearch(value);
        if (index < 0)
            index = ~index;
        list.Insert(index, value);
    }
}
