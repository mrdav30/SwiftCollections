using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using System.Collections.Generic;

namespace SwiftCollections.Benchmarks;

[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class QueueWorkloadBenchmarks
{
    [Params(100, 1000, 10000, 100000)]
    public int N;

    private int[] _data;
    private int[] _accessIndices;
    private int[] _replacementValues;
    private int _accessCount;
    private int _mutationCount;

    private Queue<int> _queue;
    private SwiftQueue<int> _swiftQueue;

    [GlobalSetup]
    public void Setup()
    {
        _data = TestHelper.GenerateShuffledRange(N, 42);

        _accessCount = N >> 5;
        if (_accessCount == 0)
            _accessCount = 1;
        if (_accessCount > 1024)
            _accessCount = 1024;

        _mutationCount = _accessCount;
        _accessIndices = new int[_accessCount];
        _replacementValues = new int[_mutationCount];

        int[] accessOrder = TestHelper.GenerateShuffledRange(N, 123);
        for (int i = 0; i < _accessCount; i++)
            _accessIndices[i] = accessOrder[i];

        for (int i = 0; i < _mutationCount; i++)
            _replacementValues[i] = N + i;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("RangeEnqueue")]
    public int Queue_EnqueueLoopRange()
    {
        var queue = new Queue<int>(N);
        for (int i = 0; i < N; i++)
            queue.Enqueue(_data[i]);
        return queue.Count;
    }

    [Benchmark]
    [BenchmarkCategory("RangeEnqueue")]
    public int SwiftQueue_EnqueueRange()
    {
        var queue = new SwiftQueue<int>(N);
        queue.EnqueueRange(_data);
        return queue.Count;
    }

    [IterationSetup(Targets = new[] {
        nameof(Queue_WrapAroundChurn),
        nameof(Queue_TailAccessViaSnapshot),
        nameof(Queue_IndexAccessViaSnapshot),
        nameof(Queue_ClearAndRefill)
    })]
    public void IterationSetup_Queue()
    {
        _queue = new Queue<int>(_data);
    }

    [IterationSetup(Targets = new[] {
        nameof(SwiftQueue_WrapAroundChurn),
        nameof(SwiftQueue_PeekTail),
        nameof(SwiftQueue_IndexAccess),
        nameof(SwiftQueue_FastClearAndRefill)
    })]
    public void IterationSetup_SwiftQueue()
    {
        _swiftQueue = new SwiftQueue<int>(_data);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("WrapAroundChurn")]
    public int Queue_WrapAroundChurn()
    {
        int sum = 0;
        for (int i = 0; i < _mutationCount; i++)
        {
            sum += _queue.Dequeue();
            _queue.Enqueue(_replacementValues[i]);
        }

        return sum + _queue.Count;
    }

    [Benchmark]
    [BenchmarkCategory("WrapAroundChurn")]
    public int SwiftQueue_WrapAroundChurn()
    {
        int sum = 0;
        for (int i = 0; i < _mutationCount; i++)
        {
            sum += _swiftQueue.Dequeue();
            _swiftQueue.Enqueue(_replacementValues[i]);
        }

        return sum + _swiftQueue.Count;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("TailAccess")]
    public int Queue_TailAccessViaSnapshot()
    {
        int[] snapshot = _queue.ToArray();
        return snapshot[snapshot.Length - 1];
    }

    [Benchmark]
    [BenchmarkCategory("TailAccess")]
    public int SwiftQueue_PeekTail()
    {
        return _swiftQueue.PeekTail();
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("IndexAccess")]
    public int Queue_IndexAccessViaSnapshot()
    {
        int[] snapshot = _queue.ToArray();
        int sum = 0;
        for (int i = 0; i < _accessCount; i++)
            sum += snapshot[_accessIndices[i]];
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("IndexAccess")]
    public int SwiftQueue_IndexAccess()
    {
        int sum = 0;
        for (int i = 0; i < _accessCount; i++)
            sum += _swiftQueue[_accessIndices[i]];
        return sum;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ResetAndRefill")]
    public int Queue_ClearAndRefill()
    {
        _queue.Clear();
        for (int i = 0; i < N; i++)
            _queue.Enqueue(_data[i]);
        return _queue.Count;
    }

    [Benchmark]
    [BenchmarkCategory("ResetAndRefill")]
    public int SwiftQueue_FastClearAndRefill()
    {
        _swiftQueue.FastClear();
        for (int i = 0; i < N; i++)
            _swiftQueue.Enqueue(_data[i]);
        return _swiftQueue.Count;
    }
}
