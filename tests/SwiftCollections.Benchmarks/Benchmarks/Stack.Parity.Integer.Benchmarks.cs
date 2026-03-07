using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using System.Collections.Generic;

namespace SwiftCollections.Benchmarks;

[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class StackParityIntegerBenchmarks
{
    [Params(100, 1000, 10000, 100000)]
    public int N;

    private int[] _data;
    private int[] _probeValues;
    private int _probeCount;
    private int _peekRepeats;

    private Stack<int> _stack;
    private SwiftStack<int> _swiftStack;

    [GlobalSetup]
    public void Setup()
    {
        _data = TestHelper.GenerateShuffledRange(N, 42);

        _probeCount = N >> 5;
        if (_probeCount == 0)
            _probeCount = 1;
        if (_probeCount > 1024)
            _probeCount = 1024;

        _peekRepeats = _probeCount;
        _probeValues = new int[_probeCount];

        int[] probeOrder = TestHelper.GenerateShuffledRange(N, 123);
        for (int i = 0; i < _probeCount; i++)
            _probeValues[i] = probeOrder[i];
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Push")]
    public int Stack_Push()
    {
        var stack = new Stack<int>(N);
        for (int i = 0; i < N; i++)
            stack.Push(_data[i]);
        return stack.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Push")]
    public int SwiftStack_Push()
    {
        var stack = new SwiftStack<int>(N);
        for (int i = 0; i < N; i++)
            stack.Push(_data[i]);
        return stack.Count;
    }

    [IterationSetup(Targets = new[] {
        nameof(Stack_Enumerate),
        nameof(Stack_PeekRepeated),
        nameof(Stack_ContainsSubset),
        nameof(Stack_PopDrain),
        nameof(Stack_Clear)
    })]
    public void IterationSetup_Stack()
    {
        _stack = new Stack<int>(_data);
    }

    [IterationSetup(Targets = new[] {
        nameof(SwiftStack_Enumerate),
        nameof(SwiftStack_PeekRepeated),
        nameof(SwiftStack_ContainsSubset),
        nameof(SwiftStack_PopDrain),
        nameof(SwiftStack_Clear)
    })]
    public void IterationSetup_SwiftStack()
    {
        _swiftStack = new SwiftStack<int>(_data);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Enumerate")]
    public int Stack_Enumerate()
    {
        int sum = 0;
        foreach (int item in _stack)
            sum += item;
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("Enumerate")]
    public int SwiftStack_Enumerate()
    {
        int sum = 0;
        foreach (int item in _swiftStack)
            sum += item;
        return sum;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Peek")]
    public int Stack_PeekRepeated()
    {
        int sum = 0;
        for (int i = 0; i < _peekRepeats; i++)
            sum += _stack.Peek();
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("Peek")]
    public int SwiftStack_PeekRepeated()
    {
        int sum = 0;
        for (int i = 0; i < _peekRepeats; i++)
            sum += _swiftStack.Peek();
        return sum;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Contains")]
    public int Stack_ContainsSubset()
    {
        int found = 0;
        for (int i = 0; i < _probeCount; i++)
        {
            if (_stack.Contains(_probeValues[i]))
                found++;
        }

        return found;
    }

    [Benchmark]
    [BenchmarkCategory("Contains")]
    public int SwiftStack_ContainsSubset()
    {
        int found = 0;
        for (int i = 0; i < _probeCount; i++)
        {
            if (_swiftStack.Contains(_probeValues[i]))
                found++;
        }

        return found;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Drain")]
    public int Stack_PopDrain()
    {
        int sum = 0;
        while (_stack.Count > 0)
            sum += _stack.Pop();
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("Drain")]
    public int SwiftStack_PopDrain()
    {
        int sum = 0;
        while (_swiftStack.Count > 0)
            sum += _swiftStack.Pop();
        return sum;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Clear")]
    public int Stack_Clear()
    {
        int count = _stack.Count;
        _stack.Clear();
        return count;
    }

    [Benchmark]
    [BenchmarkCategory("Clear")]
    public int SwiftStack_Clear()
    {
        int count = _swiftStack.Count;
        _swiftStack.Clear();
        return count;
    }
}
