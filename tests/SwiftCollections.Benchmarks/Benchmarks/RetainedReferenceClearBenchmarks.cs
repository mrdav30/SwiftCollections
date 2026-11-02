using System;
using BenchmarkDotNet.Attributes;

namespace SwiftCollections.Benchmarks;

/// <summary>Full retained-reference cleanup compared with direct backing-array cleanup and ordinary clearing.</summary>
[MemoryDiagnoser]
public class RetainedReferenceClearBenchmarks
{
    [Params(128, 16384)]
    public int Capacity { get; set; }

    [Params(0, 16)]
    public int LiveCount { get; set; }

    private SwiftList<object> _list;
    private readonly object _value = new();

    [GlobalSetup]
    public void Setup()
    {
        _list = new SwiftList<object>(Capacity);
        for (int i = 0; i < Capacity; i++) _list.Add(_value);
        _list.FastClear();
    }

    private void Prepare()
    {
        for (int i = 0; i < LiveCount; i++) _list.Add(_value);
    }

    [Benchmark(Baseline = true)]
    public int ManualFullStorageClear()
    {
        Prepare();
        Array.Clear(_list.InnerArray, 0, _list.InnerArray.Length);
        _list.FastClear();
        return _list.Count;
    }

    [Benchmark]
    public int ExplicitFullStorageClear()
    {
        Prepare();
        _list.Clear(clearRetainedReferences: true);
        return _list.Count;
    }

    [Benchmark]
    public int OrdinaryClear()
    {
        Prepare();
        _list.Clear();
        return _list.Count;
    }

    [Benchmark]
    public int FastClear()
    {
        Prepare();
        _list.FastClear();
        return _list.Count;
    }
}
