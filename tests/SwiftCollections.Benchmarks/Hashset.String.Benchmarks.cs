using System.Collections.Generic;
using BenchmarkDotNet.Attributes;

namespace SwiftCollections.Benchmarks
{
    [MemoryDiagnoser]
    public class HashSetStringBenchmark
    {
        [Params(100, 1000, 10000, 100000)]
        public int N;

        private string[] _data;

        private HashSet<string> _hashSet;
        private SwiftHashSet<string> _swiftHashset;

        [GlobalSetup]
        public void Setup()
        {
            _data = new string[N];
            for (int i = 0; i < N; i++)
                _data[i] = TestHelper.GenerateRandomString(10);
        }

        [Benchmark(Baseline = true)]
        public HashSet<string> HashSet_Add()
        {
            var set = new HashSet<string>();
            for (int i = 0; i < N; i++)
                set.Add(_data[i]);
            return set;
        }

        [Benchmark]
        public SwiftHashSet<string> SwiftHashSet_Add()
        {
            var set = new SwiftHashSet<string>();
            for (int i = 0; i < N; i++)
                set.Add(_data[i]);
            return set;
        }

        [Benchmark]
        public HashSet<string> HashSet_Enumeration()
        {
            var set = new HashSet<string>(_data);
            int count = 0;
            foreach (var item in set)
                count++;
            return set;
        }

        [Benchmark]
        public SwiftHashSet<string> SwiftHashSet_Enumeration()
        {
            var set = new SwiftHashSet<string>(_data);
            int count = 0;
            foreach (var item in set)
                count++;
            return set;
        }

        [IterationSetup(Targets = new[] { nameof(HashSet_Contains), nameof(HashSet_Remove) })]
        public void IterationSetup_Hashset()
        {
            _hashSet = new HashSet<string>(_data);
        }

        [IterationSetup(Targets = new[] { nameof(SwiftHashSet_Contains), nameof(SwiftHashSet_Remove) })]
        public void IterationSetup_SwiftHashset()
        {
            _swiftHashset = new SwiftHashSet<string>(_data);
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
