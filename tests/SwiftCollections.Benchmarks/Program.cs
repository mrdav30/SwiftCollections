using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using SwiftHashSetBenchmark;

namespace SwiftCollections.Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
            //var summary = BenchmarkRunner.Run<SwiftListBenchmarks>();

            //var summary = BenchmarkRunner.Run<SwiftQueueBenchmarks>();

            //var summary = BenchmarkRunner.Run<SwiftStackBenchmarks>();

            //var summary = BenchmarkRunner.Run<SwiftBucketBenchmarks>();
            //var summary = BenchmarkRunner.Run<RemovalInsertionBenchmarks>();

            // var summary = BenchmarkRunner.Run<SwiftDictionaryBenchmarks>();
            //var summary = BenchmarkRunner.Run<SwiftDictionaryStringBenchmarks>();

            //var summary = BenchmarkRunner.Run<SwiftSortedListBenchmarks>();

            //var summary = BenchmarkRunner.Run<IntegerHashSetBenchmark>();
            var summary = BenchmarkRunner.Run<StringHashSetBenchmark>();
        }
    }
}
