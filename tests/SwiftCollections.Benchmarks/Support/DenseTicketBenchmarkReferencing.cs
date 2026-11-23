namespace SwiftCollections.Benchmarks;

public struct DenseTicketBenchmarkReferencing : IDenseTicketBenchmarkValue<DenseTicketBenchmarkReferencing>
{
    public int Value;
    public DenseTicketBenchmarkReference Detail;
    public static DenseTicketBenchmarkReferencing Create(int id) => new() { Value = id, Detail = DenseTicketBenchmarkReference.Create(id + 1) };
    public static int Read(DenseTicketBenchmarkReferencing value) => value.Value + value.Detail.Value;
    public static void Increment(ref DenseTicketBenchmarkReferencing value) => value.Value++;
}
