using BenchmarkDotNet.Attributes;
using System.Collections.Generic;

namespace SwiftCollections.Benchmarks
{
    [MemoryDiagnoser]
    public class BucketIntegerBenchmarks
    {
        [Params(100, 1000, 10000, 100000)]
        public int N;

        private int[] _data;

        private SwiftBucket<int> _swiftBucket;
        private List<int> _list;
        private Dictionary<int, int> _dictionary;

        [GlobalSetup]
        public void Setup()
        {
            _data = new int[N];
            for (int i = 0; i < N; i++)
                _data[i] = TestHelper.GenerateRandomInt(0, N);
        }

        [Benchmark(Baseline = true)]
        public List<int> List_Add()
        {
            var list = new List<int>();
            for (int i = 0; i < N; i++)
                list.Add(i);
            return list;
        }

        [Benchmark]
        public Dictionary<int, int> Dictionary_Add()
        {
            var dictionary = new Dictionary<int, int>();
            for (int i = 0; i < N; i++)
                dictionary.Add(i, i);
            return dictionary;
        }

        [Benchmark]
        public SwiftBucket<int> SwiftBucket_Add()
        {
            var swiftBucket = new SwiftBucket<int>();
            for (int i = 0; i < N; i++)
                swiftBucket.Add(i);
            return swiftBucket;
        }

        [IterationSetup(Targets = new[] { 
            nameof(SwiftBucket_Remove), 
            nameof(SwiftBucket_Iteration),
            nameof(SwiftBucket_Contains) 
        })]
        public void IterationSetup_SwiftBucket()
        {
            _swiftBucket = new SwiftBucket<int>();
            for (int i = 0; i < N; i++)
                _swiftBucket.Add(_data[i]);
        }

        [IterationSetup(Targets = new[] { 
            nameof(List_Remove), 
            nameof(List_Iteration),
            nameof(List_Contains) 
        })]
        public void IterationSetup_List()
        {
            _list = new List<int>(_data);
        }

        [IterationSetup(Targets = new[] { 
            nameof(Dictionary_Remove), 
            nameof(Dictionary_Iteration), 
            nameof(Dictionary_Contains) 
        })]
        public void IterationSetup_Dictionary()
        {
            _dictionary = new Dictionary<int, int>();
            for (int i = 0; i < N; i++)
                _dictionary.Add(i, _data[i]);
        }

        [Benchmark]
        public List<int> List_Remove()
        {
            for (int i = N - 1; i >= 0; i -= 2)
                _list.RemoveAt(i);
            return _list;
        }

        [Benchmark]
        public Dictionary<int, int> Dictionary_Remove()
        {
            for (int i = N - 1; i >= 0; i -= 2)
                _dictionary.Remove(i);
            return _dictionary;
        }

        [Benchmark]
        public SwiftBucket<int> SwiftBucket_Remove()
        {
            for (int i = N - 1; i >= 0; i -= 2)
                _swiftBucket.TryRemoveAt(i);
            return _swiftBucket;
        }

        [Benchmark]
        public int List_Iteration()
        {
            int sum = 0;
            foreach (var item in _list)
                sum += item;
            return sum;
        }

        [Benchmark]
        public int Dictionary_Iteration()
        {
            int sum = 0;
            foreach (var item in _dictionary.Values)
                sum += item;
            return sum;
        }

        [Benchmark]
        public int SwiftBucket_Iteration()
        {
            int sum = 0;
            foreach (var item in _swiftBucket)
                sum += item;
            return sum;
        }

        [Benchmark]
        public List<int> List_Contains()
        {
            for (int i = 0; i < N; i++)
                _list.Contains(_data[i]);
            return _list;
        }

        [Benchmark]
        public Dictionary<int, int> Dictionary_Contains()
        {
            for (int i = 0; i < N; i++)
                _dictionary.ContainsValue(_data[i]);
            return _dictionary;
        }

        [Benchmark]
        public SwiftBucket<int> SwiftBucket_Contains()
        {
            for (int i = 0; i < N; i++)
                _swiftBucket.Contains(_data[i]);
            return _swiftBucket;
        }
    }
}
