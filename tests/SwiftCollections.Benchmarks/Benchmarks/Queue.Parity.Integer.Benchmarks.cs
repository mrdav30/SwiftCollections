using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using System.Collections.Generic;

namespace SwiftCollections.Benchmarks;

[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class QueueParityIntegerBenchmarks
{
    [Params(100, 1000, 10000, 100000)]
    public int N;

    private int[] _data;
    private int _peekRepeats;

    private Queue<int> _queue;
    private SwiftQueue<int> _swiftQueue;

    [GlobalSetup]
    public void Setup()
    {
        _data = TestHelper.GenerateShuffledRange(N, 42);
        _peekRepeats = N >> 5;
        if (_peekRepeats == 0)
            _peekRepeats = 1;
        if (_peekRepeats > 1024)
            _peekRepeats = 1024;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Enqueue")]
    public int Queue_Enqueue()
    {
        var queue = new Queue<int>(N);
        for (int i = 0; i < N; i++)
            queue.Enqueue(_data[i]);
        return queue.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Enqueue")]
    public int SwiftQueue_Enqueue()
    {
        var queue = new SwiftQueue<int>(N);
        for (int i = 0; i < N; i++)
            queue.Enqueue(_data[i]);
        return queue.Count;
    }

    [IterationSetup(Targets = new[] {
        nameof(Queue_Enumerate),
        nameof(Queue_PeekRepeated),
        nameof(Queue_DequeueDrain),
        nameof(Queue_CopyTo),
        nameof(Queue_Clear)
    })]
    public void IterationSetup_Queue()
    {
        _queue = new Queue<int>(_data);
    }

    [IterationSetup(Targets = new[] {
        nameof(SwiftQueue_Enumerate),
        nameof(SwiftQueue_PeekRepeated),
        nameof(SwiftQueue_DequeueDrain),
        nameof(SwiftQueue_CopyTo),
        nameof(SwiftQueue_Clear)
    })]
    public void IterationSetup_SwiftQueue()
    {
        _swiftQueue = new SwiftQueue<int>(_data);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Enumerate")]
    public int Queue_Enumerate()
    {
        int sum = 0;
        foreach (int item in _queue)
            sum += item;
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("Enumerate")]
    public int SwiftQueue_Enumerate()
    {
        int sum = 0;
        foreach (int item in _swiftQueue)
            sum += item;
        return sum;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Peek")]
    public int Queue_PeekRepeated()
    {
        int sum = 0;
        for (int i = 0; i < _peekRepeats; i++)
            sum += _queue.Peek();
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("Peek")]
    public int SwiftQueue_PeekRepeated()
    {
        int sum = 0;
        for (int i = 0; i < _peekRepeats; i++)
            sum += _swiftQueue.Peek();
        return sum;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Drain")]
    public int Queue_DequeueDrain()
    {
        int sum = 0;
        while (_queue.Count > 0)
            sum += _queue.Dequeue();
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("Drain")]
    public int SwiftQueue_DequeueDrain()
    {
        int sum = 0;
        while (_swiftQueue.Count > 0)
            sum += _swiftQueue.Dequeue();
        return sum;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("CopyTo")]
    public int Queue_CopyTo()
    {
        var snapshot = new int[N];
        _queue.CopyTo(snapshot, 0);
        return snapshot[0] + snapshot[snapshot.Length - 1];
    }

    [Benchmark]
    [BenchmarkCategory("CopyTo")]
    public int SwiftQueue_CopyTo()
    {
        var snapshot = new int[N];
        _swiftQueue.CopyTo(snapshot, 0);
        return snapshot[0] + snapshot[snapshot.Length - 1];
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Clear")]
    public int Queue_Clear()
    {
        int count = _queue.Count;
        _queue.Clear();
        return count;
    }

    [Benchmark]
    [BenchmarkCategory("Clear")]
    public int SwiftQueue_Clear()
    {
        int count = _swiftQueue.Count;
        _swiftQueue.Clear();
        return count;
    }
}
