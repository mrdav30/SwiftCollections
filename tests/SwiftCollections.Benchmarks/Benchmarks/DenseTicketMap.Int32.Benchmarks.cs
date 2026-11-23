using System.Collections.Generic;

namespace SwiftCollections.Benchmarks;

public class DenseTicketMapInt32Benchmarks : DenseTicketMapWorkloadBenchmarks<DenseTicketBenchmarkInt32>
{
    // Exactly four bytes, with curated occupancy/high-water controls.
    public override IEnumerable<string> Cases => new[] { "128x1", "4096x4", "16384x10" };
}
