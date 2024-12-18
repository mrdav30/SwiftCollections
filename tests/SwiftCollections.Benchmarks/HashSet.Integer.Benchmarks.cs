using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using SwiftCollections;

namespace SwiftHashSetBenchmark
{
    [MemoryDiagnoser]
    public class HashSetIntegerBenchmark
    {
        [Params(100, 1000, 10000, 100000)]
        public int N;

        private int[] _data;

        private HashSet<int> _hashSet;
        private SwiftHashSet<int> _swiftHashset;

        [GlobalSetup]
        public void Setup()
        {
            _data = new int[N];
            var rand = new Random(42);
            for (int i = 0; i < N; i++)
            {
                _data[i] = rand.Next();
            }
        }

        [Benchmark(Baseline = true)]
        public void HashSet_Add()
        {
            var set = new HashSet<int>();
            for (int i = 0; i < N; i++)
                set.Add(_data[i]);
        }

        [Benchmark]
        public void SwiftHashSet_Add()
        {
            var set = new SwiftHashSet<int>();
            for (int i = 0; i < N; i++)
                set.Add(_data[i]);
        }

        [Benchmark]
        public int HashSet_Enumeration()
        {
            var set = new HashSet<int>(_data);
            int count = 0;
            foreach (var item in set)
                count += item;
            return count;
        }

        [Benchmark]
        public int SwiftHashSet_Enumeration()
        {
            var set = new SwiftHashSet<int>(_data);
            int count = 0;
            foreach (var item in set)
                count += item;
            return count;
        }

        [IterationSetup(Targets = new[] { nameof(HashSet_Contains), nameof(HashSet_Remove) })]
        public void IterationSetup_Hashset()
        {
            _hashSet = new HashSet<int>(_data);
        }

        [IterationSetup(Targets = new[] { nameof(SwiftHashSet_Contains), nameof(SwiftHashSet_Remove) })]
        public void IterationSetup_SwiftHashset()
        {
            _swiftHashset = new SwiftHashSet<int>(_data);
        }

        [Benchmark]
        public int HashSet_Contains()
        {
            int found = 0;
            for (int i = 0; i < N; i++)
            {
                if (_hashSet.Contains(_data[i]))
                    found++;
            }
            return found;
        }

        [Benchmark]
        public int SwiftHashSet_Contains()
        {
            int found = 0;
            for (int i = 0; i < N; i++)
            {
                if (_swiftHashset.Contains(_data[i]))
                    found++;
            }
            return found;
        }

        [Benchmark]
        public void HashSet_Remove()
        {
            for (int i = 0; i < N; i++)
                _hashSet.Remove(_data[i]);
        }

        [Benchmark]
        public void SwiftHashSet_Remove()
        {
            for (int i = 0; i < N; i++)
                _swiftHashset.Remove(_data[i]);
        }
    }
}
