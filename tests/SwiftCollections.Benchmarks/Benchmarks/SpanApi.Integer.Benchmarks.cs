using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using System;
using System.Collections.Generic;

namespace SwiftCollections.Benchmarks;

[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class SpanApiIntegerBenchmarks
{
    [Params(100, 1000, 10000, 100000)]
    public int N;

    private int[] _data;
    private int _wrapCount;

    private SwiftList<int> _swiftList;
    private SwiftStack<int> _swiftStack;
    private SwiftQueue<int> _swiftQueue;

    private int[] _listCopyDestination;
    private int[] _queueCopyDestination;

    [GlobalSetup]
    public void Setup()
    {
        _data = TestHelper.GenerateShuffledRange(N, 42);

        _wrapCount = N >> 2;
        if (_wrapCount == 0)
            _wrapCount = 1;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ListRangeAdd")]
    public int SwiftList_AddRange_EnumerableArray()
    {
        var list = new SwiftList<int>();
        IEnumerable<int> items = _data;
        list.AddRange(items);
        return list.Count;
    }

    [Benchmark]
    [BenchmarkCategory("ListRangeAdd")]
    public int SwiftList_AddRange_Span()
    {
        var list = new SwiftList<int>();
        list.AddRange(_data.AsSpan());
        return list.Count;
    }

    [IterationSetup(Targets = new[]
    {
        nameof(SwiftList_CopyTo_Array),
        nameof(SwiftList_CopyTo_Span),
        nameof(SwiftList_SequentialRead_Indexer),
        nameof(SwiftList_SequentialRead_Span)
    })]
    public void IterationSetup_List()
    {
        _swiftList = new SwiftList<int>(_data);
        _listCopyDestination = new int[N];
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ListCopyTo")]
    public int SwiftList_CopyTo_Array()
    {
        _swiftList.CopyTo(_listCopyDestination, 0);
        return _listCopyDestination[0] + _listCopyDestination[N - 1];
    }

    [Benchmark]
    [BenchmarkCategory("ListCopyTo")]
    public int SwiftList_CopyTo_Span()
    {
        _swiftList.CopyTo(_listCopyDestination.AsSpan());
        return _listCopyDestination[0] + _listCopyDestination[N - 1];
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ListSequentialRead")]
    public int SwiftList_SequentialRead_Indexer()
    {
        int sum = 0;
        for (int i = 0; i < _swiftList.Count; i++)
            sum += _swiftList[i];
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("ListSequentialRead")]
    public int SwiftList_SequentialRead_Span()
    {
        int sum = 0;
        ReadOnlySpan<int> span = _swiftList.AsReadOnlySpan();
        for (int i = 0; i < span.Length; i++)
            sum += span[i];
        return sum;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("StackRangePush")]
    public int SwiftStack_PushLoop()
    {
        var stack = new SwiftStack<int>(N);
        for (int i = 0; i < N; i++)
            stack.Push(_data[i]);
        return stack.Count;
    }

    [Benchmark]
    [BenchmarkCategory("StackRangePush")]
    public int SwiftStack_PushRange_Span()
    {
        var stack = new SwiftStack<int>(N);
        stack.PushRange(_data.AsSpan());
        return stack.Count;
    }

    [IterationSetup(Targets = new[]
    {
        nameof(SwiftStack_SequentialRead_Indexer),
        nameof(SwiftStack_SequentialRead_Span)
    })]
    public void IterationSetup_Stack()
    {
        _swiftStack = new SwiftStack<int>(_data);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("StackSequentialRead")]
    public int SwiftStack_SequentialRead_Indexer()
    {
        int sum = 0;
        for (int i = 0; i < _swiftStack.Count; i++)
            sum += _swiftStack[i];
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("StackSequentialRead")]
    public int SwiftStack_SequentialRead_Span()
    {
        int sum = 0;
        ReadOnlySpan<int> span = _swiftStack.AsReadOnlySpan();
        for (int i = 0; i < span.Length; i++)
            sum += span[i];
        return sum;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("QueueRangeEnqueue")]
    public int SwiftQueue_EnqueueRange_EnumerableArray()
    {
        var queue = new SwiftQueue<int>(N);
        IEnumerable<int> items = _data;
        queue.EnqueueRange(items);
        return queue.Count;
    }

    [Benchmark]
    [BenchmarkCategory("QueueRangeEnqueue")]
    public int SwiftQueue_EnqueueRange_Span()
    {
        var queue = new SwiftQueue<int>(N);
        queue.EnqueueRange(_data.AsSpan());
        return queue.Count;
    }

    [IterationSetup(Targets = new[]
    {
        nameof(SwiftQueue_CopyTo_ArrayWrapped),
        nameof(SwiftQueue_CopyTo_SpanWrapped),
        nameof(SwiftQueue_SequentialRead_IndexerWrapped),
        nameof(SwiftQueue_SequentialRead_SegmentsWrapped)
    })]
    public void IterationSetup_QueueWrapped()
    {
        _swiftQueue = new SwiftQueue<int>(N);
        _swiftQueue.EnqueueRange(_data.AsSpan());

        for (int i = 0; i < _wrapCount; i++)
            _swiftQueue.Dequeue();

        for (int i = 0; i < _wrapCount; i++)
            _swiftQueue.Enqueue(N + i);

        _queueCopyDestination = new int[_swiftQueue.Count];
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("QueueCopyToWrapped")]
    public int SwiftQueue_CopyTo_ArrayWrapped()
    {
        _swiftQueue.CopyTo(_queueCopyDestination, 0);
        return _queueCopyDestination[0] + _queueCopyDestination[_queueCopyDestination.Length - 1];
    }

    [Benchmark]
    [BenchmarkCategory("QueueCopyToWrapped")]
    public int SwiftQueue_CopyTo_SpanWrapped()
    {
        _swiftQueue.CopyTo(_queueCopyDestination.AsSpan());
        return _queueCopyDestination[0] + _queueCopyDestination[_queueCopyDestination.Length - 1];
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("QueueSequentialReadWrapped")]
    public int SwiftQueue_SequentialRead_IndexerWrapped()
    {
        int sum = 0;
        for (int i = 0; i < _swiftQueue.Count; i++)
            sum += _swiftQueue[i];
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("QueueSequentialReadWrapped")]
    public int SwiftQueue_SequentialRead_SegmentsWrapped()
    {
        int sum = 0;

        _swiftQueue.GetSegments(out ReadOnlySpan<int> first, out ReadOnlySpan<int> second);

        for (int i = 0; i < first.Length; i++)
            sum += first[i];

        for (int i = 0; i < second.Length; i++)
            sum += second[i];

        return sum;
    }
}
