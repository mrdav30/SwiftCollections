using System.Collections.Generic;
using System.Xml.Linq;
using BenchmarkDotNet.Attributes;

namespace SwiftCollections.Benchmarks
{
    [MemoryDiagnoser]
    public class ListIntegerBenchmarks
    {
        [Params(100, 1000, 10000, 100000)]
        public int N;

        private int[] _data;

        private List<int> _list;
        private SwiftList<int> _swiftList;

        [GlobalSetup]
        public void Setup()
        {
            _data = new int[N];
            for (int i = 0; i < N; i++)
                _data[i] = TestHelper.GenerateRandomInt(0, N);
        }

        [Benchmark(Baseline = true)]
        public List<int> SystemList_AddTest()
        {
            var test = new List<int>();
            for (int i = 0; i < N; i++)
                test.Add(_data[i]);
            return test;
        }

        [Benchmark]
        public SwiftList<int> SwiftList_AddTest()
        {
            var test = new SwiftList<int>();
            for (int i = 0; i < N; i++)
                test.Add(_data[i]);
            return test;
        }

        [Benchmark]
        public List<int> SystemList_IterationTest()
        {
            var test = new List<int>(_data);
            int count = 0;
            foreach (var item in test)
                count++;
            return test;
        }

        // Benchmark iteration performance to measure enumeration efficiency
        [Benchmark]
        public SwiftList<int> SwiftList_IterationTest()
        {
            var test = new SwiftList<int>(_data);
            int count = 0;
            foreach (var item in test)
                count++;
            return test;
        }

        [Benchmark]
        public List<int> SystemList_IndexerIterationTest()
        {
            var test = new List<int>(_data);
            int count = 0;
            for (int i = 0; i < test.Count; i++)
                count += test[i]; // Direct indexer access
            return test;
        }

        [Benchmark]
        public SwiftList<int> SwiftList_IndexerIterationTest()
        {
            var test = new SwiftList<int>(_data);
            int count = 0;
            for (int i = 0; i < test.Count; i++)
                count += test[i]; // Direct indexer access
            return test;
        }

        [IterationSetup(Targets = new[] {
            nameof(SystemList_ReverseTest),
            nameof(SystemList_RemoveAllTest),
            nameof(SystemList_SortTest),
            nameof(SystemList_CustomSortTest),
            nameof(SystemList_InsertTest),
            nameof(SystemList_BinarySearchTest),
            nameof(SystemList_CopyToTest),
            nameof(SystemList_AddRangeTest),
            nameof(SystemList_ClearTest)
        })]
        public void IterationSetup_List()
        {
            _list = new List<int>(_data);
        }

        [IterationSetup(Targets = new[] {
            nameof(SwiftList_ReverseTest),
            nameof(SwiftList_RemoveAllTest),
            nameof(SwiftList_SortTest),
            nameof(SwiftList_CustomSortTest),
            nameof(SwiftList_InsertTest),
            nameof(SwiftList_BinarySearchTest),
            nameof(SwiftList_CopyToTest),
            nameof(SwiftList_AddRangeTest),
            nameof(SwiftList_ClearTest)
        })]
        public void IterationSetup_SwiftList()
        {
            _swiftList = new SwiftList<int>(_data);
        }

        [Benchmark]
        public List<int> SystemList_ReverseTest()
        {
            _list.Reverse();
            return _list;
        }

        [Benchmark]
        public SwiftList<int> SwiftList_ReverseTest()
        {
            _swiftList.Reverse();
            return _swiftList;
        }

        [Benchmark]
        public List<int> SystemList_RemoveAllTest()
        {
            _list.RemoveAll(i => i % 2 == 0);
            return _list;
        }

        [Benchmark]
        public SwiftList<int> SwiftList_RemoveAllTest()
        {
            _swiftList.RemoveAll(i => i % 2 == 0);
            return _swiftList;
        }

        [Benchmark]
        public List<int> SystemList_SortTest()
        {
            _list.Sort();
            return _list;
        }

        [Benchmark]
        public SwiftList<int> SwiftList_SortTest()
        {
            _swiftList.Sort();
            return _swiftList;
        }

        [Benchmark]
        public List<int> SystemList_CustomSortTest()
        {
            _list.Sort(Comparer<int>.Create((x, y) => y - x));
            return _list;
        }

        [Benchmark]
        public SwiftList<int> SwiftList_CustomSortTest()
        {
            _swiftList.Sort(Comparer<int>.Create((x, y) => y - x));
            return _swiftList;
        }

        [Benchmark]
        public List<int> SystemList_InsertTest()
        {
            _list.Insert(42, N - 5);
            return _list;
        }

        [Benchmark]
        public SwiftList<int> SwiftList_InsertTest()
        {
            _swiftList.Insert(42, N - 5);
            return _swiftList;
        }

        [Benchmark]
        public bool SystemList_BinarySearchTest()
        {
            _list.Sort();
            return _list.Contains(N - 5);
        }

        [Benchmark]
        public bool SwiftList_BinarySearchTest()
        {
            _swiftList.Sort();
            return _swiftList.Contains(N - 5);
        }

        [Benchmark]
        public int[] SystemList_CopyToTest()
        {
            var newArray = new int[N];
            _list.CopyTo(newArray, 0);
            return newArray;
        }

        [Benchmark]
        public int[] SwiftList_CopyToTest()
        {
            var newArray = new int[N];
            _swiftList.CopyTo(newArray, 0);
            return newArray;
        }

        [Benchmark]
        public List<int> SystemList_AddRangeTest()
        {
            var newlist = new List<int>();
            newlist.AddRange(_list);
            return newlist;
        }

        [Benchmark]
        public SwiftList<int> SwiftList_AddRangeTest()
        {
            var newlist = new SwiftList<int>();
            newlist.AddRange(_swiftList);
            return newlist;
        }

        [Benchmark]
        public List<int> SystemList_ClearTest()
        {
            _list.Clear();
            return _list;
        }

        [Benchmark]
        public SwiftList<int> SwiftList_ClearTest()
        {
            _swiftList.Clear();
            return _swiftList;
        }
    }
}
