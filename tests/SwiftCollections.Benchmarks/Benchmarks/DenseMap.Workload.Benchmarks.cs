using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using System.Collections.Generic;

namespace SwiftCollections.Benchmarks;

[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class DenseMapWorkloadBenchmarks
{
    [Params(100, 1000, 10000, 100000)]
    public int N;

    private int[] _values;
    private int[] _lookupOrder;
    private int[] _updatedValues;
    private int[] _churnKeys;
    private int[] _churnReplacementValues;

    private Dictionary<int, int> _dictionary;
    private SwiftDictionary<int, int> _swiftDictionary;
    private SwiftSparseMap<int> _sparseMap;

    [GlobalSetup]
    public void Setup()
    {
        _values = TestHelper.GenerateShuffledRange(N, 42);
        _lookupOrder = TestHelper.GenerateShuffledRange(N, 123);
        _updatedValues = new int[N];

        for (int i = 0; i < N; i++)
            _updatedValues[i] = N + _values[i];

        int churnCount = N / 4;
        if (churnCount == 0)
            churnCount = 1;

        int[] churnOrder = TestHelper.GenerateShuffledRange(N, 777);
        _churnKeys = new int[churnCount];
        _churnReplacementValues = new int[churnCount];

        for (int i = 0; i < churnCount; i++)
        {
            _churnKeys[i] = churnOrder[i];
            _churnReplacementValues[i] = (N * 2) + i;
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("AddDenseKeys")]
    public int Dictionary_AddDenseKeys()
    {
        var dictionary = new Dictionary<int, int>(N);
        for (int i = 0; i < N; i++)
            dictionary.Add(i, _values[i]);
        return dictionary.Count;
    }

    [Benchmark]
    [BenchmarkCategory("AddDenseKeys")]
    public int SwiftDictionary_AddDenseKeys()
    {
        var dictionary = new SwiftDictionary<int, int>(N);
        for (int i = 0; i < N; i++)
            dictionary.Add(i, _values[i]);
        return dictionary.Count;
    }

    [Benchmark]
    [BenchmarkCategory("AddDenseKeys")]
    public int SparseMap_AddDenseKeys()
    {
        var sparseMap = new SwiftSparseMap<int>(N, N);
        for (int i = 0; i < N; i++)
            sparseMap.Add(i, _values[i]);
        return sparseMap.Count;
    }

    [IterationSetup(Targets = new[] {
        nameof(Dictionary_LookupDenseKeys),
        nameof(Dictionary_EnumerateValues)
    })]
    public void IterationSetup_Dictionary_Read()
    {
        _dictionary = new Dictionary<int, int>(N);
        for (int i = 0; i < N; i++)
            _dictionary.Add(i, _values[i]);
    }

    [IterationSetup(Targets = new[] {
        nameof(SwiftDictionary_LookupDenseKeys),
        nameof(SwiftDictionary_EnumerateValues)
    })]
    public void IterationSetup_SwiftDictionary_Read()
    {
        _swiftDictionary = new SwiftDictionary<int, int>(N);
        for (int i = 0; i < N; i++)
            _swiftDictionary.Add(i, _values[i]);
    }

    [IterationSetup(Targets = new[] {
        nameof(SparseMap_LookupDenseKeys),
        nameof(SparseMap_EnumerateValues)
    })]
    public void IterationSetup_SparseMap_Read()
    {
        _sparseMap = new SwiftSparseMap<int>(N, N);
        for (int i = 0; i < N; i++)
            _sparseMap.Add(i, _values[i]);
    }

    [IterationSetup(Targets = new[] { nameof(Dictionary_OverwriteExistingValues) })]
    public void IterationSetup_Dictionary_Overwrite()
    {
        _dictionary = new Dictionary<int, int>(N);
        for (int i = 0; i < N; i++)
            _dictionary.Add(i, _values[i]);
    }

    [IterationSetup(Targets = new[] { nameof(SwiftDictionary_OverwriteExistingValues) })]
    public void IterationSetup_SwiftDictionary_Overwrite()
    {
        _swiftDictionary = new SwiftDictionary<int, int>(N);
        for (int i = 0; i < N; i++)
            _swiftDictionary.Add(i, _values[i]);
    }

    [IterationSetup(Targets = new[] { nameof(SparseMap_OverwriteExistingValues) })]
    public void IterationSetup_SparseMap_Overwrite()
    {
        _sparseMap = new SwiftSparseMap<int>(N, N);
        for (int i = 0; i < N; i++)
            _sparseMap.Add(i, _values[i]);
    }

    [IterationSetup(Targets = new[] { nameof(Dictionary_KeyedChurn25) })]
    public void IterationSetup_Dictionary_Churn()
    {
        _dictionary = new Dictionary<int, int>(N);
        for (int i = 0; i < N; i++)
            _dictionary.Add(i, _values[i]);
    }

    [IterationSetup(Targets = new[] { nameof(SwiftDictionary_KeyedChurn25) })]
    public void IterationSetup_SwiftDictionary_Churn()
    {
        _swiftDictionary = new SwiftDictionary<int, int>(N);
        for (int i = 0; i < N; i++)
            _swiftDictionary.Add(i, _values[i]);
    }

    [IterationSetup(Targets = new[] { nameof(SparseMap_KeyedChurn25) })]
    public void IterationSetup_SparseMap_Churn()
    {
        _sparseMap = new SwiftSparseMap<int>(N, N);
        for (int i = 0; i < N; i++)
            _sparseMap.Add(i, _values[i]);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("LookupDenseKeys")]
    public int Dictionary_LookupDenseKeys()
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
    [BenchmarkCategory("LookupDenseKeys")]
    public int SwiftDictionary_LookupDenseKeys()
    {
        int sum = 0;
        for (int i = 0; i < N; i++)
        {
            int key = _lookupOrder[i];
            if (_swiftDictionary.TryGetValue(key, out int value))
                sum += value;
        }

        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("LookupDenseKeys")]
    public int SparseMap_LookupDenseKeys()
    {
        int sum = 0;
        for (int i = 0; i < N; i++)
        {
            int key = _lookupOrder[i];
            if (_sparseMap.TryGetValue(key, out int value))
                sum += value;
        }

        return sum;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("EnumerateValues")]
    public int Dictionary_EnumerateValues()
    {
        int sum = 0;
        foreach (var item in _dictionary.Values)
            sum += item;
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("EnumerateValues")]
    public int SwiftDictionary_EnumerateValues()
    {
        int sum = 0;
        foreach (var item in _swiftDictionary.Values)
            sum += item;
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("EnumerateValues")]
    public int SparseMap_EnumerateValues()
    {
        int sum = 0;
        foreach (var item in _sparseMap)
            sum += item.Value;
        return sum;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("OverwriteExisting")]
    public int Dictionary_OverwriteExistingValues()
    {
        int sum = 0;
        for (int i = 0; i < N; i++)
        {
            int key = _lookupOrder[i];
            _dictionary[key] = _updatedValues[key];
            sum += _dictionary[key];
        }

        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("OverwriteExisting")]
    public int SwiftDictionary_OverwriteExistingValues()
    {
        int sum = 0;
        for (int i = 0; i < N; i++)
        {
            int key = _lookupOrder[i];
            _swiftDictionary[key] = _updatedValues[key];
            sum += _swiftDictionary[key];
        }

        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("OverwriteExisting")]
    public int SparseMap_OverwriteExistingValues()
    {
        int sum = 0;
        for (int i = 0; i < N; i++)
        {
            int key = _lookupOrder[i];
            _sparseMap[key] = _updatedValues[key];
            sum += _sparseMap[key];
        }

        return sum;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("KeyedChurn")]
    public int Dictionary_KeyedChurn25()
    {
        for (int i = 0; i < _churnKeys.Length; i++)
        {
            int key = _churnKeys[i];
            _dictionary.Remove(key);
            _dictionary.Add(key, _churnReplacementValues[i]);
        }

        return _dictionary.Count;
    }

    [Benchmark]
    [BenchmarkCategory("KeyedChurn")]
    public int SwiftDictionary_KeyedChurn25()
    {
        for (int i = 0; i < _churnKeys.Length; i++)
        {
            int key = _churnKeys[i];
            _swiftDictionary.Remove(key);
            _swiftDictionary.Add(key, _churnReplacementValues[i]);
        }

        return _swiftDictionary.Count;
    }

    [Benchmark]
    [BenchmarkCategory("KeyedChurn")]
    public int SparseMap_KeyedChurn25()
    {
        for (int i = 0; i < _churnKeys.Length; i++)
        {
            int key = _churnKeys[i];
            _sparseMap.Remove(key);
            _sparseMap.Add(key, _churnReplacementValues[i]);
        }

        return _sparseMap.Count;
    }
}
