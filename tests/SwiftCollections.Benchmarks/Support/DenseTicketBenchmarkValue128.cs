namespace SwiftCollections.Benchmarks;

public struct DenseTicketBenchmarkValue128 : IDenseTicketBenchmarkValue<DenseTicketBenchmarkValue128>
{
    public DenseTicketBenchmarkValue32 A, B, C, D;
    public static DenseTicketBenchmarkValue128 Create(int id) => new() { A = DenseTicketBenchmarkValue32.Create(id), D = DenseTicketBenchmarkValue32.Create(id + 24) };
    public static int Read(DenseTicketBenchmarkValue128 value) => DenseTicketBenchmarkValue32.Read(value.A) + DenseTicketBenchmarkValue32.Read(value.D);
    public static void Increment(ref DenseTicketBenchmarkValue128 value) => value.A.Value++;
}
