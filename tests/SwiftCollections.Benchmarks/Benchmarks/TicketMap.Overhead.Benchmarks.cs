using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace SwiftCollections.Benchmarks;

[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class TicketMapOverheadBenchmarks
{
    [Params(100, 1000, 10000, 100000)]
    public int N;

    private int[] _data;
    private int[] _slotMapIndices;
    private SwiftTicket[] _tickets;

    private SwiftSlotMap<int> _slotMap;
    private SwiftTicketMap<int> _ticketMap;

    [GlobalSetup]
    public void Setup()
    {
        _data = TestHelper.GenerateShuffledRange(N);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Add")]
    public int SlotMap_Add()
    {
        var slotMap = new SwiftSlotMap<int>(N);
        for (int i = 0; i < N; i++)
            slotMap.Add(_data[i]);
        return slotMap.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Add")]
    public int TicketMap_Add()
    {
        var slotMap = new SwiftTicketMap<int>(N);
        for (int i = 0; i < N; i++)
            slotMap.Add(_data[i]);
        return slotMap.Count;
    }

    [IterationSetup(Targets = new[] {
        nameof(SlotMap_LookupByIndex),
        nameof(SlotMap_Enumerate),
        nameof(SlotMap_RemoveByIndex),
        nameof(SlotMap_IndexValidityCheck)
    })]
    public void IterationSetup_SlotMap()
    {
        _slotMap = new SwiftSlotMap<int>(N);
        _slotMapIndices = new int[N];

        for (int i = 0; i < N; i++)
            _slotMapIndices[i] = _slotMap.Add(_data[i]);
    }

    [IterationSetup(Targets = new[] {
        nameof(TicketMap_LookupByTicket),
        nameof(TicketMap_Enumerate),
        nameof(TicketMap_RemoveByTicket),
        nameof(TicketMap_TicketValidityCheck)
    })]
    public void IterationSetup_TicketMap()
    {
        _ticketMap = new SwiftTicketMap<int>(N);
        _tickets = new SwiftTicket[N];

        for (int i = 0; i < N; i++)
            _tickets[i] = _ticketMap.Add(_data[i]);
    }

    [BenchmarkCategory("Lookup")]
    [Benchmark(Baseline = true)]
    public int SlotMap_LookupByIndex()
    {
        int sum = 0;
        for (int i = 0; i < N; i++)
            sum += _slotMap[_slotMapIndices[i]];
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("Lookup")]
    public int TicketMap_LookupByTicket()
    {
        int sum = 0;
        for (int i = 0; i < N; i++)
        {
            if (_ticketMap.TryGet(_tickets[i], out int value))
                sum += value;
        }

        return sum;
    }

    [BenchmarkCategory("Enumerate")]
    [Benchmark(Baseline = true)]
    public int SlotMap_Enumerate()
    {
        int sum = 0;
        foreach (var item in _slotMap)
            sum += item;
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("Enumerate")]
    public int TicketMap_Enumerate()
    {
        int sum = 0;
        foreach (var item in _ticketMap)
            sum += item;
        return sum;
    }

    [BenchmarkCategory("Remove")]
    [Benchmark(Baseline = true)]
    public int SlotMap_RemoveByIndex()
    {
        int removed = 0;
        for (int i = 0; i < N; i++)
        {
            if (_slotMap.TryRemoveAt(_slotMapIndices[i]))
                removed++;
        }

        return removed;
    }

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public int TicketMap_RemoveByTicket()
    {
        int removed = 0;
        for (int i = 0; i < N; i++)
        {
            if (_ticketMap.Remove(_tickets[i]))
                removed++;
        }

        return removed;
    }

    [BenchmarkCategory("Validity")]
    [Benchmark(Baseline = true)]
    public int SlotMap_IndexValidityCheck()
    {
        int allocated = 0;
        for (int i = 0; i < N; i++)
        {
            if (_slotMap.IsAllocated(_slotMapIndices[i]))
                allocated++;
        }

        return allocated;
    }

    [Benchmark]
    [BenchmarkCategory("Validity")]
    public int TicketMap_TicketValidityCheck()
    {
        int valid = 0;
        for (int i = 0; i < N; i++)
        {
            if (_ticketMap.IsValid(_tickets[i]))
                valid++;
        }

        return valid;
    }
}
