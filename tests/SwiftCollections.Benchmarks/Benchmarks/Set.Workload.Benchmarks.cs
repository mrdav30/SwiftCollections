using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using System.Collections.Generic;

namespace SwiftCollections.Benchmarks;

[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class SetWorkloadBenchmarks
{
    [Params(100, 1000, 10000, 100000)]
    public int N;

    private int[] _values;
    private int[] _removeValues;
    private int[] _replacementValues;
    private int[] _postChurnProbeValues;

    private HashSet<int> _hashSet;
    private SwiftHashSet<int> _swiftHashSet;
    private SwiftPackedSet<int> _packedSet;

    [GlobalSetup]
    public void Setup()
    {
        _values = TestHelper.GenerateShuffledRange(N, 42);

        int churnCount = N / 4;
        if (churnCount == 0)
            churnCount = 1;

        int[] removalOrder = TestHelper.GenerateShuffledRange(N, 77);
        _removeValues = new int[churnCount];
        _replacementValues = new int[churnCount];

        var activeValues = new int[N];
        for (int i = 0; i < N; i++)
            activeValues[i] = _values[i];

        for (int i = 0; i < churnCount; i++)
        {
            int position = removalOrder[i];
            _removeValues[i] = _values[position];
            _replacementValues[i] = N + i;
            activeValues[position] = _replacementValues[i];
        }

        int[] probeOrder = TestHelper.GenerateShuffledRange(N, 123);
        _postChurnProbeValues = new int[N];
        for (int i = 0; i < N; i++)
            _postChurnProbeValues[i] = activeValues[probeOrder[i]];
    }

    [IterationSetup(Targets = new[] { nameof(HashSet_Churn25) })]
    public void IterationSetup_HashSet_Churn()
    {
        _hashSet = new HashSet<int>(_values);
    }

    [IterationSetup(Targets = new[] {
        nameof(HashSet_EnumerateAfterChurn25),
        nameof(HashSet_ContainsAfterChurn25)
    })]
    public void IterationSetup_HashSet_PostChurn()
    {
        _hashSet = new HashSet<int>(_values);
        ApplyChurn(_hashSet);
    }

    [IterationSetup(Targets = new[] { nameof(SwiftHashSet_Churn25) })]
    public void IterationSetup_SwiftHashSet_Churn()
    {
        _swiftHashSet = new SwiftHashSet<int>(_values);
    }

    [IterationSetup(Targets = new[] {
        nameof(SwiftHashSet_EnumerateAfterChurn25),
        nameof(SwiftHashSet_ContainsAfterChurn25)
    })]
    public void IterationSetup_SwiftHashSet_PostChurn()
    {
        _swiftHashSet = new SwiftHashSet<int>(_values);
        ApplyChurn(_swiftHashSet);
    }

    [IterationSetup(Targets = new[] { nameof(PackedSet_Churn25) })]
    public void IterationSetup_PackedSet_Churn()
    {
        _packedSet = new SwiftPackedSet<int>(N);
        for (int i = 0; i < N; i++)
            _packedSet.Add(_values[i]);
    }

    [IterationSetup(Targets = new[] {
        nameof(PackedSet_EnumerateAfterChurn25),
        nameof(PackedSet_ContainsAfterChurn25)
    })]
    public void IterationSetup_PackedSet_PostChurn()
    {
        _packedSet = new SwiftPackedSet<int>(N);
        for (int i = 0; i < N; i++)
            _packedSet.Add(_values[i]);
        ApplyChurn(_packedSet);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Churn")]
    public int HashSet_Churn25()
    {
        ApplyChurn(_hashSet);
        return _hashSet.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Churn")]
    public int SwiftHashSet_Churn25()
    {
        ApplyChurn(_swiftHashSet);
        return _swiftHashSet.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Churn")]
    public int PackedSet_Churn25()
    {
        ApplyChurn(_packedSet);
        return _packedSet.Count;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("EnumerateAfterChurn")]
    public int HashSet_EnumerateAfterChurn25()
    {
        int sum = 0;
        foreach (var item in _hashSet)
            sum += item;
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("EnumerateAfterChurn")]
    public int SwiftHashSet_EnumerateAfterChurn25()
    {
        int sum = 0;
        foreach (var item in _swiftHashSet)
            sum += item;
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("EnumerateAfterChurn")]
    public int PackedSet_EnumerateAfterChurn25()
    {
        int sum = 0;
        foreach (var item in _packedSet)
            sum += item;
        return sum;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ContainsAfterChurn")]
    public int HashSet_ContainsAfterChurn25()
    {
        int found = 0;
        for (int i = 0; i < _postChurnProbeValues.Length; i++)
        {
            if (_hashSet.Contains(_postChurnProbeValues[i]))
                found++;
        }

        return found;
    }

    [Benchmark]
    [BenchmarkCategory("ContainsAfterChurn")]
    public int SwiftHashSet_ContainsAfterChurn25()
    {
        int found = 0;
        for (int i = 0; i < _postChurnProbeValues.Length; i++)
        {
            if (_swiftHashSet.Contains(_postChurnProbeValues[i]))
                found++;
        }

        return found;
    }

    [Benchmark]
    [BenchmarkCategory("ContainsAfterChurn")]
    public int PackedSet_ContainsAfterChurn25()
    {
        int found = 0;
        for (int i = 0; i < _postChurnProbeValues.Length; i++)
        {
            if (_packedSet.Contains(_postChurnProbeValues[i]))
                found++;
        }

        return found;
    }

    private void ApplyChurn(HashSet<int> set)
    {
        for (int i = 0; i < _removeValues.Length; i++)
        {
            set.Remove(_removeValues[i]);
            set.Add(_replacementValues[i]);
        }
    }

    private void ApplyChurn(SwiftHashSet<int> set)
    {
        for (int i = 0; i < _removeValues.Length; i++)
        {
            set.Remove(_removeValues[i]);
            set.Add(_replacementValues[i]);
        }
    }

    private void ApplyChurn(SwiftPackedSet<int> set)
    {
        for (int i = 0; i < _removeValues.Length; i++)
        {
            set.Remove(_removeValues[i]);
            set.Add(_replacementValues[i]);
        }
    }
}
