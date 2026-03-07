using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using System.Collections.Generic;

namespace SwiftCollections.Benchmarks;

[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class StackWorkloadBenchmarks
{
    [Params(100, 1000, 10000, 100000)]
    public int N;

    private int[] _data;
    private int[] _accessDepths;
    private int[] _replacementValues;
    private int _accessCount;
    private int _mutationCount;

    private Stack<int> _stack;
    private SwiftStack<int> _swiftStack;

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
        _accessDepths = new int[_accessCount];
        _replacementValues = new int[_mutationCount];

        int[] accessOrder = TestHelper.GenerateShuffledRange(N, 123);
        for (int i = 0; i < _accessCount; i++)
            _accessDepths[i] = accessOrder[i];

        for (int i = 0; i < _mutationCount; i++)
            _replacementValues[i] = N + i;
    }

    [IterationSetup(Targets = new[] {
        nameof(Stack_PushPopChurn),
        nameof(Stack_TopWindowAccessViaSnapshot),
        nameof(Stack_ClearAndRefill)
    })]
    public void IterationSetup_Stack()
    {
        _stack = new Stack<int>(_data);
    }

    [IterationSetup(Targets = new[] {
        nameof(SwiftStack_PushPopChurn),
        nameof(SwiftStack_TopWindowAccess),
        nameof(SwiftStack_FastClearAndRefill)
    })]
    public void IterationSetup_SwiftStack()
    {
        _swiftStack = new SwiftStack<int>(_data);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("PushPopChurn")]
    public int Stack_PushPopChurn()
    {
        int sum = 0;
        for (int i = 0; i < _mutationCount; i++)
        {
            sum += _stack.Pop();
            _stack.Push(_replacementValues[i]);
        }

        return sum + _stack.Count;
    }

    [Benchmark]
    [BenchmarkCategory("PushPopChurn")]
    public int SwiftStack_PushPopChurn()
    {
        int sum = 0;
        for (int i = 0; i < _mutationCount; i++)
        {
            sum += _swiftStack.Pop();
            _swiftStack.Push(_replacementValues[i]);
        }

        return sum + _swiftStack.Count;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("TopWindowAccess")]
    public int Stack_TopWindowAccessViaSnapshot()
    {
        int[] snapshot = _stack.ToArray();
        int sum = 0;
        for (int i = 0; i < _accessCount; i++)
            sum += snapshot[_accessDepths[i]];
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("TopWindowAccess")]
    public int SwiftStack_TopWindowAccess()
    {
        int sum = 0;
        int count = _swiftStack.Count;
        for (int i = 0; i < _accessCount; i++)
            sum += _swiftStack[count - 1 - _accessDepths[i]];
        return sum;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ResetAndRefill")]
    public int Stack_ClearAndRefill()
    {
        _stack.Clear();
        for (int i = 0; i < N; i++)
            _stack.Push(_data[i]);
        return _stack.Count;
    }

    [Benchmark]
    [BenchmarkCategory("ResetAndRefill")]
    public int SwiftStack_FastClearAndRefill()
    {
        _swiftStack.FastClear();
        for (int i = 0; i < N; i++)
            _swiftStack.Push(_data[i]);
        return _swiftStack.Count;
    }
}
