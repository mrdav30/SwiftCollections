using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SwiftCollections.Benchmarks
{
    [MemoryDiagnoser]
    public class SortedListIntegerBenchmarks
    {
        [Params(100, 1000, 10000, 100000)]
        public int N;

        private int[] _data;

        private SwiftSortedList<int> _swiftSorter;
        private List<int> _list;
        private SortedSet<int> _sortedSet;

        [GlobalSetup]
        public void Setup()
        {
            _data = new int[N];
            for (int i = 0; i < N; i++)
                _data[i] = TestHelper.GenerateRandomInt(0, N);
        }

        [Benchmark(Baseline = true)]
        public void List_Add()
        {
            var list = new List<int>();
            for (int i = 0; i < N; i++)
                list.Add(_data[i]);
            list.Sort();  // simulate manually sorted array
        }

        [Benchmark]
        public void SortedSet_Add()
        {
            var set = new SortedSet<int>();
            for (int i = 0; i < N; i++)
                set.Add(_data[i]);
        }

        [Benchmark]
        public void SwiftSorter_Add()
        {
            var swiftSorter = new SwiftSortedList<int>();
            for (int i = 0; i < N; i++)
                swiftSorter.Add(_data[i]);
        }

        [Benchmark]
        public void List_Empty_AddRange()
        {
            var list = new List<int>();
            list.AddRange(_data);
            list.Sort();  // simulate manually sorted array
            list.Add(42);
            list.Sort();
        }

        [Benchmark]
        public void SortedSet_Empty_AddRange()
        {
            var set = new SortedSet<int>();
            foreach (var item in _data)
                set.Add(item);
            set.Add(42);
        }


        [Benchmark]
        public void SwiftSorter_Empty_AddRange()
        {
            var swiftSorter = new SwiftSortedList<int>();
            swiftSorter.AddRange(_data);
            swiftSorter.Add(42);
        }

        [Benchmark]
        public void List_AddRange()
        {
            var list = new List<int>()
            {
                3, 4, 10, 15, 12
            };
            list.AddRange(_data);
            list.Sort();  // simulate manually sorted array
            list.Add(42);
            list.Sort();
        }

        [Benchmark]
        public void SortedSet_AddRange()
        {
            var set = new SortedSet<int>()
            {
                3, 4, 10, 15, 12
            };
            foreach (var item in _data)
                set.Add(item);
            set.Add(42);
        }

        [Benchmark]
        public void SwiftSorter_AddRange()
        {
            var swiftSorter = new SwiftSortedList<int>()
            {
                3, 4, 10, 15, 12
            };
            swiftSorter.AddRange(_data);
            swiftSorter.Add(42);
        }

        [Benchmark]
        public int List_BuildAndContains()
        {
            var list = new List<int>();
            list.AddRange(_data);

            list.Sort();  // simulate manually sorted array
            int count = 0;
            for (int i = 0; i < N; i++)
            {
                int index = list.BinarySearch(_data[i]);
                bool contains = index >= 0;
                if (contains) count++;
            }
            return count;
        }

        [Benchmark]
        public int SortedSet_BuildAndContains()
        {
            var sortedSet = new SortedSet<int>();
            for (int i = 0; i < _data.Length; i++)
                sortedSet.Add(_data[i]);

            int count = 0;
            for (int i = 0; i < N; i++)
            {
                bool contains = sortedSet.Contains(_data[i]);
                if (contains) count++;
            }
            return count;
        }

        [Benchmark]
        public int SwiftSortedList_BuildAndContains()
        {
            var swiftSortedList = new SwiftSortedList<int>();
            swiftSortedList.AddRange(_data);

            int count = 0;
            for (int i = 0; i < N; i++)
            {
                bool contains = swiftSortedList.Contains(_data[i]);
                if (contains) count++;
            }
            return count;
        }

        [IterationSetup(Targets = new[] { nameof(SwiftSorter_Enumerate), nameof(SwiftSorter_Remove) })]
        public void IterationSetup_SwiftSorter()
        {
            _swiftSorter = new SwiftSortedList<int>();
            _swiftSorter.AddRange(_data);
        }

        [IterationSetup(Targets = new[] { nameof(List_Enumerate), nameof(List_Remove) })]
        public void IterationSetup_List()
        {
            _list = new List<int>();
            _list.AddRange(_data);

        }

        [IterationSetup(Targets = new[] { nameof(SortedSet_Enumerate), nameof(SortedSet_Remove) })]
        public void IterationSetup_SortedSet()
        {
            _sortedSet = new SortedSet<int>();
            foreach (var item in _data)
                _sortedSet.Add(item);
        }

        [Benchmark]
        public void List_Remove()
        {
            for (int i = 0; i < N - 50; i += 4)
                _list.Remove(_data[i]);
            _list.Sort();  // simulate manually sorted array
        }

        [Benchmark]
        public void SortedSet_Remove()
        {
            for (int i = 0; i < N - 50; i += 4)
                _sortedSet.Remove(_data[i]);
        }

        [Benchmark]
        public void SwiftSorter_Remove()
        {
            for (int i = 0; i < N - 50; i += 4)
                _swiftSorter.Remove(_data[i]);
        }

        [Benchmark]
        public int List_Enumerate()
        {
            var count = 0;
            _list.Sort();
            foreach (var item in _list)
                count += item;
            return count;
        }

        [Benchmark]
        public int SortedSet_Enumerate()
        {
            var count = 0;
            foreach (var item in _sortedSet)
                count += item;
            return count;
        }

        [Benchmark]
        public int SwiftSorter_Enumerate()
        {
            var count = 0;
            foreach (var item in _swiftSorter)
                count += item;
            return count;
        }

    }
}