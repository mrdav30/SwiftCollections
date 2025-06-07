using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using System;
using System.Collections.Generic;

namespace SwiftCollections.Benchmarks
{
    [MemoryDiagnoser]
    public class BucketRemovalInsertionBenchmarks
    {
        [Params(100, 1000, 10000, 100000)]
        public int N;

        private int[] _data;

        private SwiftBucket<int> _swiftBucket;
        private List<int> _list;

        [IterationSetup]
        public void Setup()
        {
            _swiftBucket = new SwiftBucket<int>();
            _list = new List<int>();

            // Populate collections
            for (int i = 0; i < N; i++)
            {
                _swiftBucket.Add(i);
                _list.Add(i);
            }

            // Generate random indices to remove
            Random rand = new Random(42); // Seed for reproducibility
            _data = new int[(int)(N * 0.25f)]; // Remove 25% of elements
            for (int i = 0; i < _data.Length; i++)
                _data[i] = rand.Next(0, N);
        }

        [Benchmark(Baseline = true)]
        public void List_RandomRemovalsAndInsertions()
        {
            foreach (var index in _data)
            {
                if (index < _list.Count)
                    _list.RemoveAt(index);

                // InsertAt at random index
                int newIndex = index % (_list.Count + 1);
                _list.Insert(newIndex, index);
            }
        }

        [Benchmark]
        public void SwiftBucket_RandomRemovalsAndInsertions()
        {
            foreach (var index in _data)
            {
                // Remove if allocated
                _swiftBucket.TryRemoveAt(index);

                // InsertAt at random index
                int newIndex = index % (_swiftBucket.PeakCount + 1);
                _swiftBucket.InsertAt(newIndex, index);
            }
        }
    }
}
