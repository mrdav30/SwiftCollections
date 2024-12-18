using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using SwiftCollections;

namespace SwiftHashSetBenchmark
{
    public class CustomObject
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public override int GetHashCode() => Id.GetHashCode();
        public override bool Equals(object obj)
        {
            return obj is CustomObject other && Id == other.Id;
        }
    }

    [MemoryDiagnoser]
    public class HashSetObjectBenchmark
    {
        [Params(100, 1000, 10000, 100000)]
        public int N;

        private CustomObject[] _data;

        private HashSet<CustomObject> _hashSet;
        private SwiftHashSet<CustomObject> _swiftHashset;

        [GlobalSetup]
        public void Setup()
        {
            _data = new CustomObject[N];
            for (int i = 0; i < N; i++)
            {
                _data[i] = new CustomObject
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = $"Name_{i}"
                };
            }
        }

        [Benchmark(Baseline = true)]
        public void HashSet_Add()
        {
            var set = new HashSet<CustomObject>();
            for (int i = 0; i < N; i++)
                set.Add(_data[i]);
        }

        [Benchmark]
        public void SwiftHashSet_Add()
        {
            var set = new SwiftHashSet<CustomObject>();
            for (int i = 0; i < N; i++)
                set.Add(_data[i]);
        }

        [Benchmark]
        public HashSet<CustomObject> HashSet_Enumeration()
        {
            var set = new HashSet<CustomObject>(_data);
            int count = 0;
            foreach (var item in set)
                count++;
            return set;
        }

        [Benchmark]
        public SwiftHashSet<CustomObject> SwiftHashSet_Enumeration()
        {
            var set = new SwiftHashSet<CustomObject>(_data);
            int count = 0;
            foreach (var item in set)
                count++;
            return set;
        }

        [IterationSetup(Targets = new[] { nameof(HashSet_Contains), nameof(HashSet_Remove) })]
        public void IterationSetup_Hashset()
        {
            _hashSet = new HashSet<CustomObject>(_data);
        }

        [IterationSetup(Targets = new[] { nameof(SwiftHashSet_Contains), nameof(SwiftHashSet_Remove) })]
        public void IterationSetup_SwiftHashset()
        {
            _swiftHashset = new SwiftHashSet<CustomObject>(_data);
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
