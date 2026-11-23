namespace SwiftCollections.Benchmarks;

public interface IDenseTicketBenchmarkValue<T> where T : IDenseTicketBenchmarkValue<T>
{
    static abstract T Create(int id);
    static abstract int Read(T value);
    static abstract void Increment(ref T value);
}
