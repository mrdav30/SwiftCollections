using BenchmarkDotNet.Attributes;
using System.Collections.Generic;

namespace SwiftCollections.Benchmarks
{
    [MemoryDiagnoser]
    public class StackIntegerBenchmarks
    {
        [Params(100, 1000, 10000, 100000)]
        public int N;

        private int[] _data;

        private SwiftStack<int> _swiftStack;
        private Stack<int> _systemStack;

        [GlobalSetup]
        public void Setup()
        {
            _data = new int[N];
            for (int i = 0; i < N; i++)
                _data[i] = TestHelper.GenerateRandomInt(0, N);
        }

        [Benchmark(Baseline = true)]
        public Stack<int> SystemStack_Push()
        {
            var stack = new Stack<int>();
            for (int i = 0; i < N; i++)
                stack.Push(i);
            return stack;
        }

        [Benchmark]
        public SwiftStack<int> SwiftStack_Push()
        {
            var stack = new SwiftStack<int>();
            for (int i = 0; i < N; i++)
                stack.Push(i);
            return stack;
        }

        [Benchmark]
        public int SystemStack_Iteration()
        {
            var stack = new Stack<int>(_data);
            var count = 0;
            foreach (var item in stack)
                count += item;
            return count;
        }

        [Benchmark]
        public int SwiftStack_Iteration()
        {
            var stack = new SwiftStack<int>(_data);
            var count = 0;
            foreach (var item in stack)
                count += item;
            return count;
        }

        [IterationSetup(Targets = new[] {
            nameof(SystemStack_Pop)
        })]
        public void IterationSetup_SystemStack()
        {
            _systemStack = new Stack<int>(_data);
        }

        [IterationSetup(Targets = new[] {
            nameof(SwiftStack_Pop)
        })]
        public void IterationSetup_SwiftStack()
        {
            _swiftStack = new SwiftStack<int>(_data);
        }

        [Benchmark]
        public void SystemStack_Pop()
        {
            for (int i = 0; i < N; i++)
                _systemStack.Pop();
        }

        [Benchmark]
        public void SwiftStack_Pop()
        {
            for (int i = 0; i < N; i++)
                _swiftStack.Pop();
        }
    }
}
