using System.Collections.Generic;
using BenchmarkDotNet.Attributes;

namespace SwiftCollections.Benchmarks
{
    [MemoryDiagnoser]
    public class QueueIntegerBenchmarks
    {
        [Params(100, 1000, 10000, 100000)]
        public int N;

        private int[] _data;

        private Queue<int> _systemQueue;
        private SwiftQueue<int> _swiftQueue;

        [GlobalSetup]
        public void Setup()
        {
            _data = new int[N];
            for (int i = 0; i < N; i++)
                _data[i] = TestHelper.GenerateRandomInt(0, N);
        }

        [Benchmark(Baseline = true)]
        public Queue<int> SystemQueue_EnqueueTest()
        {
            var testQueue = new Queue<int>();
            for (int i = 0; i < N; i++) 
                testQueue.Enqueue(_data[i]);
            return testQueue;
        }

        [Benchmark]
        public SwiftQueue<int> SwiftQueue_EnqueueTest()
        {
            var testQueue = new SwiftQueue<int>();
            for (int i = 0; i < N; i++)
                testQueue.Enqueue(_data[i]);
            return testQueue;
        }

        [Benchmark]
        public Queue<int> SystemQueue_IterationTest()
        {
            var queue = new Queue<int>(_data);
            int count = 0;
            foreach (var item in queue)
                count += item;
            return queue;
        }

        [Benchmark]
        public SwiftQueue<int> SwiftQueue_IterationTest()
        {
            var queue = new SwiftQueue<int>(_data);
            int count = 0;
            foreach (var item in queue)
                count += item;
            return queue;
        }

        [Benchmark]
        public Queue<int> SystemQueue_TrimExcessCapacityTest()
        {
            var queue = new Queue<int>(N);
            for (int i = 0; i < N * 0.5f; i++) queue.Enqueue(i);
            queue.TrimExcess();
            return queue;
        }

        [Benchmark]
        public SwiftQueue<int> SwiftQueue_TrimExcessCapacityTest()
        {
            var queue = new SwiftQueue<int>(N);
            for (int i = 0; i < N * 0.5f; i++) queue.Enqueue(i);
            queue.TrimExcessCapacity();
            return queue;
        }

        [IterationSetup(Targets = new[] { 
            nameof(SystemQueue_DequeueTest), 
            nameof(SystemQueue_PeekTest), 
            nameof(SystemQueue_LastTest),
            nameof(SystemQueue_ClearTest),
            nameof(SystemQueue_ToArrayTest)
        })]
        public void IterationSetup_SystemQueue()
        {
            _systemQueue = new Queue<int>(_data);
        }

        [IterationSetup(Targets = new[] { 
            nameof(SwiftQueue_DequeueTest), 
            nameof(SwiftQueue_PeekTest), 
            nameof(SwiftQueue_PeekTailTest),
            nameof(SwiftQueue_ClearTest),
            nameof(SwiftQueue_ToArrayTest)
        })]
        public void IterationSetup_SwiftQueue()
        {
            _swiftQueue = new SwiftQueue<int>(_data);
        }

        [Benchmark]
        public Queue<int> SystemQueue_DequeueTest()
        {
            while (_systemQueue.Count > 0)
            {
                _systemQueue.Dequeue();
            }
            return _systemQueue;
        }

        [Benchmark]
        public SwiftQueue<int> SwiftQueue_DequeueTest()
        {
            while (_swiftQueue.Count > 0)
            {
                _swiftQueue.Dequeue();
            }
            return _swiftQueue;
        }

        [Benchmark]
        public int SystemQueue_PeekTest()
        {
            return _systemQueue.Peek();
        }

        [Benchmark]
        public int SwiftQueue_PeekTest()
        {
            return _swiftQueue.Peek();
        }

        [Benchmark]
        public int SystemQueue_LastTest()
        {
            var queueArray = _systemQueue.ToArray();
            var item = queueArray[queueArray.Length - 1];
            return item;
        }

        [Benchmark]
        public int SwiftQueue_PeekTailTest()
        {
            return _swiftQueue.PeekTail();
        }

        [Benchmark]
        public Queue<int> SystemQueue_ClearTest()
        {
            _systemQueue.Clear();
            return _systemQueue;
        }

        [Benchmark]
        public SwiftQueue<int> SwiftQueue_ClearTest()
        {
            _swiftQueue.Clear();
            return _swiftQueue;
        }

        [Benchmark]
        public int[] SystemQueue_ToArrayTest()
        {
            int[] newArray = _systemQueue.ToArray();
            return newArray;
        }

        [Benchmark]
        public int[] SwiftQueue_ToArrayTest()
        {
            int[] newArray = _swiftQueue.ToArray();
            return newArray;
        }
    }
}
