namespace SwiftCollections.Benchmarks;

public struct DenseTicketBenchmarkInt32 : IDenseTicketBenchmarkValue<DenseTicketBenchmarkInt32>
{
    public int Value;
    public static DenseTicketBenchmarkInt32 Create(int id) => new() { Value = id };
    public static int Read(DenseTicketBenchmarkInt32 value) => value.Value;
    public static void Increment(ref DenseTicketBenchmarkInt32 value) => value.Value++;
}
