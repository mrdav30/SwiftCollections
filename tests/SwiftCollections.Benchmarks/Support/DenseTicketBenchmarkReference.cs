namespace SwiftCollections.Benchmarks;

public sealed class DenseTicketBenchmarkReference : IDenseTicketBenchmarkValue<DenseTicketBenchmarkReference>
{
    public int Value;
    public static DenseTicketBenchmarkReference Create(int id) => new() { Value = id };
    public static int Read(DenseTicketBenchmarkReference value) => value.Value;
    public static void Increment(ref DenseTicketBenchmarkReference value) => value.Value++;
}
