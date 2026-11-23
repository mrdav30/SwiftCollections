namespace SwiftCollections.Benchmarks;

public struct DenseTicketBenchmarkValue32 : IDenseTicketBenchmarkValue<DenseTicketBenchmarkValue32>
{
    public int Value, B, C, D, E, F, G, H;
    public static DenseTicketBenchmarkValue32 Create(int id) => new() { Value = id, B = id + 1, H = id + 7 };
    public static int Read(DenseTicketBenchmarkValue32 value) => value.Value + value.B + value.H;
    public static void Increment(ref DenseTicketBenchmarkValue32 value) => value.Value++;
}
