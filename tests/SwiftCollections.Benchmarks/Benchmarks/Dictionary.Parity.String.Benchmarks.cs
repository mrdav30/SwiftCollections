using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using System.Collections.Generic;

namespace SwiftCollections.Benchmarks;

[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class DictionaryParityStringBenchmarks
{
    [Params(100, 1000, 10000, 100000)]
    public int N;

    private string[] _keys;
    private int[] _values;

    private Dictionary<string, int> _dictionary;
    private SwiftDictionary<string, int> _swiftDictionary;

    [GlobalSetup]
    public void Setup()
    {
        int[] keyOrder = TestHelper.GenerateShuffledRange(N, 42);
        _keys = new string[N];
        _values = TestHelper.GenerateShuffledRange(N, 123);

        for (int i = 0; i < N; i++)
            _keys[i] = $"Key_{keyOrder[i]}";
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Add")]
    public int Dictionary_Add()
    {
        var dictionary = new Dictionary<string, int>(N);
        for (int i = 0; i < N; i++)
            dictionary.Add(_keys[i], _values[i]);
        return dictionary.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Add")]
    public int SwiftDictionary_Add()
    {
        var dictionary = new SwiftDictionary<string, int>(N);
        for (int i = 0; i < N; i++)
            dictionary.Add(_keys[i], _values[i]);
        return dictionary.Count;
    }

    [IterationSetup(Targets = new[] {
        nameof(Dictionary_Enumerate),
        nameof(Dictionary_LookupByKey),
        nameof(Dictionary_RemoveByKey),
        nameof(Dictionary_Clear)
    })]
    public void IterationSetup_Dictionary()
    {
        _dictionary = new Dictionary<string, int>(N);
        for (int i = 0; i < N; i++)
            _dictionary.Add(_keys[i], _values[i]);
    }

    [IterationSetup(Targets = new[] {
        nameof(SwiftDictionary_Enumerate),
        nameof(SwiftDictionary_LookupByKey),
        nameof(SwiftDictionary_RemoveByKey),
        nameof(SwiftDictionary_Clear)
    })]
    public void IterationSetup_SwiftDictionary()
    {
        _swiftDictionary = new SwiftDictionary<string, int>(N);
        for (int i = 0; i < N; i++)
            _swiftDictionary.Add(_keys[i], _values[i]);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Enumerate")]
    public int Dictionary_Enumerate()
    {
        int sum = 0;
        foreach (var item in _dictionary)
            sum += item.Value;
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("Enumerate")]
    public int SwiftDictionary_Enumerate()
    {
        int sum = 0;
        foreach (var item in _swiftDictionary)
            sum += item.Value;
        return sum;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Lookup")]
    public int Dictionary_LookupByKey()
    {
        int sum = 0;
        for (int i = 0; i < N; i++)
        {
            if (_dictionary.TryGetValue(_keys[i], out int value))
                sum += value;
        }

        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("Lookup")]
    public int SwiftDictionary_LookupByKey()
    {
        int sum = 0;
        for (int i = 0; i < N; i++)
        {
            if (_swiftDictionary.TryGetValue(_keys[i], out int value))
                sum += value;
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
            if (_dictionary.Remove(_keys[i]))
                removed++;
        }

        return removed;
    }

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public int SwiftDictionary_RemoveByKey()
    {
        int removed = 0;
        for (int i = 0; i < N; i++)
        {
            if (_swiftDictionary.Remove(_keys[i]))
                removed++;
        }

        return removed;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Clear")]
    public int Dictionary_Clear()
    {
        int count = _dictionary.Count;
        _dictionary.Clear();
        return count;
    }

    [Benchmark]
    [BenchmarkCategory("Clear")]
    public int SwiftDictionary_Clear()
    {
        int count = _swiftDictionary.Count;
        _swiftDictionary.Clear();
        return count;
    }
}
