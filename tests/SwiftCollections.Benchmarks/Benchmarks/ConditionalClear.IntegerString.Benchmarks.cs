using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace SwiftCollections.Benchmarks;

[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class ConditionalClearBenchmarks
{
    [Params(100, 1000, 10000, 100000)]
    public int N;

    private int[] _intData;
    private string[] _stringData;

    private SwiftList<int> _intList;
    private SwiftList<string> _stringList;

    private SwiftStack<int> _intStack;
    private SwiftStack<string> _stringStack;

    private SwiftQueue<int> _intQueue;
    private SwiftQueue<string> _stringQueue;

    [GlobalSetup]
    public void Setup()
    {
        _intData = TestHelper.GenerateShuffledRange(N, 42);
        _stringData = new string[N];

        for (int i = 0; i < N; i++)
            _stringData[i] = "value-" + _intData[i];
    }

    [IterationSetup(Targets = new[]
    {
        nameof(SwiftList_RemoveAtMiddle_Int),
        nameof(SwiftList_Clear_Int),
        nameof(SwiftList_FastClear_Int)
    })]
    public void IterationSetup_IntList()
    {
        _intList = new SwiftList<int>(_intData);
    }

    [IterationSetup(Targets = new[]
    {
        nameof(SwiftList_RemoveAtMiddle_String),
        nameof(SwiftList_Clear_String)
    })]
    public void IterationSetup_StringList()
    {
        _stringList = new SwiftList<string>(_stringData);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ListRemoveAt")]
    public int SwiftList_RemoveAtMiddle_Int()
    {
        int index = _intList.Count >> 1;
        _intList.RemoveAt(index);
        return _intList.Count;
    }

    [Benchmark]
    [BenchmarkCategory("ListRemoveAt")]
    public int SwiftList_RemoveAtMiddle_String()
    {
        int index = _stringList.Count >> 1;
        _stringList.RemoveAt(index);
        return _stringList.Count;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ListClear")]
    public int SwiftList_Clear_Int()
    {
        int count = _intList.Count;
        _intList.Clear();
        return count;
    }

    [Benchmark]
    [BenchmarkCategory("ListClear")]
    public int SwiftList_Clear_String()
    {
        int count = _stringList.Count;
        _stringList.Clear();
        return count;
    }

    [Benchmark]
    [BenchmarkCategory("ListClear")]
    public int SwiftList_FastClear_Int()
    {
        int count = _intList.Count;
        _intList.FastClear();
        return count;
    }

    [IterationSetup(Targets = new[]
    {
        nameof(SwiftStack_Pop_Int),
        nameof(SwiftStack_Clear_Int),
        nameof(SwiftStack_FastClear_Int)
    })]
    public void IterationSetup_IntStack()
    {
        _intStack = new SwiftStack<int>(_intData);
    }

    [IterationSetup(Targets = new[]
    {
        nameof(SwiftStack_Pop_String),
        nameof(SwiftStack_Clear_String)
    })]
    public void IterationSetup_StringStack()
    {
        _stringStack = new SwiftStack<string>(_stringData);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("StackPop")]
    public int SwiftStack_Pop_Int() => _intStack.Pop();

    [Benchmark]
    [BenchmarkCategory("StackPop")]
    public int SwiftStack_Pop_String() => _stringStack.Pop().Length;

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("StackClear")]
    public int SwiftStack_Clear_Int()
    {
        int count = _intStack.Count;
        _intStack.Clear();
        return count;
    }

    [Benchmark]
    [BenchmarkCategory("StackClear")]
    public int SwiftStack_Clear_String()
    {
        int count = _stringStack.Count;
        _stringStack.Clear();
        return count;
    }

    [Benchmark]
    [BenchmarkCategory("StackClear")]
    public int SwiftStack_FastClear_Int()
    {
        int count = _intStack.Count;
        _intStack.FastClear();
        return count;
    }

    [IterationSetup(Targets = new[]
    {
        nameof(SwiftQueue_Dequeue_Int),
        nameof(SwiftQueue_Clear_Int),
        nameof(SwiftQueue_FastClear_Int)
    })]
    public void IterationSetup_IntQueue()
    {
        _intQueue = new SwiftQueue<int>(_intData);
    }

    [IterationSetup(Targets = new[]
    {
        nameof(SwiftQueue_Dequeue_String),
        nameof(SwiftQueue_Clear_String)
    })]
    public void IterationSetup_StringQueue()
    {
        _stringQueue = new SwiftQueue<string>(_stringData);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("QueueDequeue")]
    public int SwiftQueue_Dequeue_Int() => _intQueue.Dequeue();

    [Benchmark]
    [BenchmarkCategory("QueueDequeue")]
    public int SwiftQueue_Dequeue_String() => _stringQueue.Dequeue().Length;

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("QueueClear")]
    public int SwiftQueue_Clear_Int()
    {
        int count = _intQueue.Count;
        _intQueue.Clear();
        return count;
    }

    [Benchmark]
    [BenchmarkCategory("QueueClear")]
    public int SwiftQueue_Clear_String()
    {
        int count = _stringQueue.Count;
        _stringQueue.Clear();
        return count;
    }

    [Benchmark]
    [BenchmarkCategory("QueueClear")]
    public int SwiftQueue_FastClear_Int()
    {
        int count = _intQueue.Count;
        _intQueue.FastClear();
        return count;
    }
}
