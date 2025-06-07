using BenchmarkDotNet.Attributes;
using System.Collections.Generic;

namespace SwiftCollections.Benchmarks
{
    [MemoryDiagnoser]
    public class DictionaryIntegerBenchmarks
    {
        [Params(1000, 5000, 10000, 50000, 100000)]
        public int N;

        private SwiftDictionary<int, int> _swiftDictionary;
        private Dictionary<int, int> _dictionary;

        [Benchmark]
        public SwiftDictionary<int, int> SwiftDictionary_Add()
        {
            var swiftDictionary = new SwiftDictionary<int, int>();
            for (int i = 0; i < N; i++)
                swiftDictionary.Add(i, i);
            return swiftDictionary;
        }

        [Benchmark]
        public Dictionary<int, int> Dictionary_Add()
        {
            var dictionary = new Dictionary<int, int>();
            for (int i = 0; i < N; i++)
                dictionary.Add(i, i);
            return dictionary;
        }

        [IterationSetup(Targets = new[] { nameof(SwiftDictionary_TryGetValue), nameof(SwiftDictionary_Remove) })]
        public void IterationSetup_SwiftDictionary()
        {
            _swiftDictionary = new SwiftDictionary<int, int>(N);
            for (int i = 0; i < N; i++)
                _swiftDictionary.Add(i, i);
        }

        [IterationSetup(Targets = new[] { nameof(Dictionary_TryGetValue), nameof(Dictionary_Remove) })]
        public void IterationSetup_Dictionary()
        {
            _dictionary = new Dictionary<int, int>(N);
            for (int i = 0; i < N; i++)
                _dictionary.Add(i, i);
        }

        [Benchmark]
        public SwiftDictionary<int, int> SwiftDictionary_TryGetValue()
        {
            for (int i = 0; i < N; i++)
                _swiftDictionary.TryGetValue(i, out int _);
            return _swiftDictionary;
        }

        [Benchmark]
        public Dictionary<int, int> Dictionary_TryGetValue()
        {
            for (int i = 0; i < N; i++)
                _dictionary.TryGetValue(i, out int _);
            return _dictionary;
        }

        [Benchmark]
        public SwiftDictionary<int, int> SwiftDictionary_Remove()
        {
            for (int i = 0; i < N; i++)
                _swiftDictionary.Remove(i);
            return _swiftDictionary;
        }

        [Benchmark]
        public Dictionary<int, int> Dictionary_Remove()
        {
            for (int i = 0; i < N; i++)
                _dictionary.Remove(i);
            return _dictionary;
        }
    }
}
