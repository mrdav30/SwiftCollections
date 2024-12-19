using BenchmarkDotNet.Running;

namespace SwiftCollections.Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
           // var ListIntegerBenchmarksSummary = BenchmarkRunner.Run<ListIntegerBenchmarks>();

            //var QueueIntegerBenchmarksSummary = BenchmarkRunner.Run<QueueIntegerBenchmarks>();

            var StackIntegerBenchmarksSummary = BenchmarkRunner.Run<StackIntegerBenchmarks>();

            //var BucketIntegerBenchmarksSummary = BenchmarkRunner.Run<BucketIntegerBenchmarks>();
            //var BucketRemovalInsertionBenchmarksSummary = BenchmarkRunner.Run<BucketRemovalInsertionBenchmarks>();

            // var summary = BenchmarkRunner.Run<DictionaryIntegerBenchmarks>();
            //var DictionaryStringBenchmarksSummary = BenchmarkRunner.Run<DictionaryStringBenchmarks>();

           // var SortedListIntegerBenchmarksSummary = BenchmarkRunner.Run<SortedListIntegerBenchmarks>();

            //var HashSetIntegerBenchmarkSumary = BenchmarkRunner.Run<HashSetIntegerBenchmark>();
            // var HashSetStringBenchmarkSummary = BenchmarkRunner.Run<HashSetStringBenchmark>();
            // var HashSetObjectBenchmarkSummary = BenchmarkRunner.Run<HashSetObjectBenchmark>();
        }
    }
}
