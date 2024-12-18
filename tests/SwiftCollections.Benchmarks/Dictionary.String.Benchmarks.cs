using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System.Xml.Linq;

namespace SwiftCollections.Benchmarks
{
    [MemoryDiagnoser]
    public class DictionaryStringBenchmarks
    {
        [Params(100, 1000, 10000, 100000)]
        public int N;

        private string[] _data;

        private SwiftDictionary<string, int> _swiftDictionary;
        private Dictionary<string, int> _dictionary;

        [GlobalSetup] 
        public void Setup() {
            _data = new string[100000];
            for (int i = 0; i < _data.Length; i++)
                _data[i] = TestHelper.GenerateRandomString(10);
        }

        [Benchmark(Baseline = true)]
        public Dictionary<string, int> Dictionary_Add()
        {
            var dictionary = new Dictionary<string, int>();
            for (int i = 0; i < N; i++)
                dictionary.Add(_data[i], i);
            return dictionary;
        }

        [Benchmark]
        public SwiftDictionary<string, int> SwiftDictionary_Add()
        {
            var swiftDictionary = new SwiftDictionary<string, int>();
            for (int i = 0; i < N; i++)
                swiftDictionary.Add(_data[i], i);
            return swiftDictionary;
        }

        [IterationSetup(Targets = new[] { nameof(Dictionary_Enumeration), nameof(Dictionary_TryGetValue), nameof(Dictionary_Remove) })]
        public void IterationSetup_Dictionary()
        {
            _dictionary = new Dictionary<string, int>(N);
            for (int i = 0; i < N; i++)
                _dictionary.Add(_data[i], i);
        }

        [IterationSetup(Targets = new[] { nameof(SwiftDictionary_Enumeration), nameof(SwiftDictionary_TryGetValue), nameof(SwiftDictionary_Remove) })]
        public void IterationSetup_SwiftDictionary()
        {
            _swiftDictionary = new SwiftDictionary<string, int>(N);
            for (int i = 0; i < N; i++)
                _swiftDictionary.Add(_data[i], i);
        }

        [Benchmark]
        public Dictionary<string, int> Dictionary_Enumeration()
        {
            int count = 0;
            foreach (var item in _dictionary)
                count++;
            return _dictionary;

        }

        [Benchmark]
        public SwiftDictionary<string, int> SwiftDictionary_Enumeration()
        {
            int count = 0;
            foreach (var item in _swiftDictionary)
                count++;
            return _swiftDictionary;
        }

        [Benchmark]
        public Dictionary<string, int> Dictionary_TryGetValue()
        {
            for (int i = 0; i < N; i++)
                _dictionary.TryGetValue(_data[i], out int _);
            return _dictionary;
        }

        [Benchmark]
        public SwiftDictionary<string, int> SwiftDictionary_TryGetValue()
        {
            for (int i = 0; i < N; i++)
                _swiftDictionary.TryGetValue(_data[i], out int _);
            return _swiftDictionary;
        }

        [Benchmark]
        public Dictionary<string, int> Dictionary_Remove()
        {
            for (int i = 0; i < N; i++)
                _dictionary.Remove(_data[i]);
            return _dictionary;
        }

        [Benchmark]
        public SwiftDictionary<string, int> SwiftDictionary_Remove()
        {
            for (int i = 0; i < N; i++)
                _swiftDictionary.Remove(_data[i]);
            return _swiftDictionary;
        }
    }
}
