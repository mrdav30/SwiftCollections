using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace SwiftCollections.Benchmarks;

[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class ScratchSortingWorkloadBenchmarks
{
    [Params(64, 1024, 16384)]
    public int N;

    private int[] _keys;
    private SwiftSparseSet _set;
    private SwiftSparseMap<int> _map;
    private SwiftList<int> _scratch;
    private SwiftSortedList<int> _sortedList;

    [GlobalSetup]
    public void Setup()
    {
        _keys = TestHelper.GenerateShuffledRange(N, 42);
        _set = new SwiftSparseSet(N, N);
        _map = new SwiftSparseMap<int>(N, N);
        _scratch = new SwiftList<int>(N);
        _sortedList = new SwiftSortedList<int>(N);

        for (int i = 0; i < _keys.Length; i++)
        {
            int key = _keys[i];
            _set.Add(key);
            _map.Add(key, i);
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("SparseSetSortedKeys")]
    public int ManualSparseSet_CopyKeysThenSortInPlace()
    {
        _scratch.FastClear();
        _scratch.AddRange(_set.AsReadOnlySpan());
        _scratch.SortInPlace();

        return ConsumeScratch();
    }

    [Benchmark]
    [BenchmarkCategory("SparseSetSortedKeys")]
    public int SwiftSparseSet_CopySortedKeysTo()
    {
        _set.CopySortedKeysTo(_scratch);

        return ConsumeScratch();
    }

    [Benchmark]
    [BenchmarkCategory("SparseSetSortedKeys")]
    public int SwiftSortedList_AddRangeFromSparseSet()
    {
        _sortedList.FastClear();
        _sortedList.AddRange(_set);

        return _sortedList.PeekMin() + _sortedList.PeekMax() + _sortedList.Count;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("SparseMapSortedKeys")]
    public int ManualSparseMap_CopyKeysThenSortInPlace()
    {
        _scratch.FastClear();
        _scratch.AddRange(_map.Keys);
        _scratch.SortInPlace();

        return ConsumeScratch();
    }

    [Benchmark]
    [BenchmarkCategory("SparseMapSortedKeys")]
    public int SwiftSparseMap_CopySortedKeysTo()
    {
        _map.CopySortedKeysTo(_scratch);

        return ConsumeScratch();
    }

    private int ConsumeScratch() => _scratch[0] + _scratch[_scratch.Count - 1] + _scratch.Count;
}
