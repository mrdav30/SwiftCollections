using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace SwiftCollections.Benchmarks;

[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public abstract class DenseTicketMapWorkloadBenchmarks<T> where T : IDenseTicketBenchmarkValue<T>
{
    [ParamsSource(nameof(Cases))]
    public string Case;
    public virtual IEnumerable<string> Cases => new[] { "4096x4" };

    private int _n;
    private SwiftDenseTicketMap<T> _dense;
    private SwiftTicketMap<T> _slot;
    private Dictionary<SwiftTicket, T> _dictionary;
    private SwiftTicket[] _denseTickets;
    private SwiftTicket[] _slotTickets;
    private SwiftTicket[] _dictionaryKeys;
    private SwiftTicket[] _missTickets;
    private SwiftTicket[] _halfMissTickets;
    private long _initialSum;

    [GlobalSetup]
    public void Setup()
    {
        string[] parts = Case.Split('x');
        _n = int.Parse(parts[0]);
        int peak = _n * int.Parse(parts[1]);
        _dense = new SwiftDenseTicketMap<T>(peak);
        _slot = new SwiftTicketMap<T>(peak);
        _dictionary = new Dictionary<SwiftTicket, T>(_n);
        _denseTickets = new SwiftTicket[_n];
        _slotTickets = new SwiftTicket[_n];
        _dictionaryKeys = new SwiftTicket[_n];
        var denseAll = new SwiftTicket[peak];
        var slotAll = new SwiftTicket[peak];
        for (int i = 0; i < peak; i++)
        {
            denseAll[i] = _dense.Add(T.Create(i));
            slotAll[i] = _slot.Add(T.Create(i));
        }
        for (int i = 0; i < peak - _n; i++)
        {
            _dense.Remove(denseAll[i]);
            _slot.Remove(slotAll[i]);
        }
        _initialSum = 0;
        for (int i = 0; i < _n; i++)
        {
            int id = peak - _n + i;
            _denseTickets[i] = denseAll[id];
            _slotTickets[i] = slotAll[id];
            _dictionaryKeys[i] = slotAll[id];
            _dictionary.Add(_dictionaryKeys[i], T.Create(id));
            _initialSum += T.Read(T.Create(id));
        }
        // Establish equal observable values before timing. Keys in the BCL control are externally issued;
        // dictionary churn deliberately reuses them and does not measure generation-checked issuance.
        if (Dense_Scan() != _initialSum || Ticket_Scan() != _initialSum || Dictionary_Scan() != _initialSum ||
            Dense_Lookup() != _initialSum || Ticket_Lookup() != _initialSum || Dictionary_Lookup() != _initialSum ||
            Dense_Span() != _initialSum)
            throw new InvalidOperationException("Persistence-independent benchmark inputs disagree.");
        Dense_Churn(); Ticket_Churn(); Dictionary_Churn();
        if (Dense_Scan() != _initialSum || Ticket_Scan() != _initialSum || Dictionary_Scan() != _initialSum)
            throw new InvalidOperationException("Reserved churn changed logical values.");
        long expectedMixed = _initialSum + 16;
        if (Dense_Mixed() != expectedMixed || Ticket_Mixed() != expectedMixed || Dictionary_Mixed() != expectedMixed)
            throw new InvalidOperationException("Mixed workloads disagree.");
        _missTickets = new SwiftTicket[_n];
        for (int i = 0; i < _n; i++)
        {
            _missTickets[i] = (i & 3) switch
            {
                0 => new SwiftTicket(-1, 1),
                1 => new SwiftTicket(peak, 1),
                2 => denseAll[i], // Removed at high-water occupancy, stale at full occupancy.
                _ => new SwiftTicket(_denseTickets[i].Index, _denseTickets[i].Generation ^ 0x80000000u)
            };
            if (_dense.TryGet(_missTickets[i], out T missing) ||
                !EqualityComparer<T>.Default.Equals(missing, default))
                throw new InvalidOperationException("Invalid lookup did not return false/default.");
        }
        int[] lookupOrder = TestHelper.GenerateShuffledRange(2 * _n, 42);
        _halfMissTickets = new SwiftTicket[lookupOrder.Length];
        for (int i = 0; i < lookupOrder.Length; i++)
        {
            int at = lookupOrder[i];
            _halfMissTickets[i] = (at & 1) == 0 ? _denseTickets[at / 2] : _missTickets[at / 2];
        }
        if (Dense_LookupMisses() != 0 || Dense_LookupHalfMisses() != expectedMixed)
            throw new InvalidOperationException("Miss lookup workloads disagree.");
        // Mutable reference payloads are independently constructed for each owner, so one control cannot
        // change another. The single verified mixed tick is the common start state for measured operations.
        Console.WriteLine($"DenseTicket workload {typeof(T).Name}: N={_n}; peak={peak}; dense={_dense.DenseCapacity}; slots={_dense.SlotCapacity}; checksum={expectedMixed}");
    }

    [Benchmark, BenchmarkCategory("Scan")]
    public long Dense_Scan()
    {
        long sum = 0;
        foreach (T value in _dense) sum += T.Read(value);
        return sum;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Scan")]
    public long Ticket_Scan()
    {
        long sum = 0;
        foreach (T value in _slot) sum += T.Read(value);
        return sum;
    }

    [Benchmark, BenchmarkCategory("Scan")]
    public long Dictionary_Scan()
    {
        long sum = 0;
        foreach (KeyValuePair<SwiftTicket, T> value in _dictionary) sum += T.Read(value.Value);
        return sum;
    }

    [Benchmark, BenchmarkCategory("Span")]
    public long Dense_Span()
    {
        long sum = 0;
        foreach (T value in _dense.Values) sum += T.Read(value);
        return sum;
    }

    [Benchmark, BenchmarkCategory("Lookup")]
    public long Dense_Lookup()
    {
        long sum = 0;
        for (int i = 0; i < _n; i++) { _dense.TryGet(_denseTickets[i], out T value); sum += T.Read(value); }
        return sum;
    }

    [Benchmark, BenchmarkCategory("LookupMisses")]
    public int Dense_LookupMisses()
    {
        int found = 0;
        for (int i = 0; i < _missTickets.Length; i++)
            if (_dense.TryGet(_missTickets[i], out _)) found++;
        return found;
    }

    [Benchmark, BenchmarkCategory("LookupHalfMisses")]
    public long Dense_LookupHalfMisses()
    {
        long sum = 0;
        for (int i = 0; i < _halfMissTickets.Length; i++)
            if (_dense.TryGet(_halfMissTickets[i], out T value)) sum += T.Read(value);
        return sum;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Lookup")]
    public long Ticket_Lookup()
    {
        long sum = 0;
        for (int i = 0; i < _n; i++) { _slot.TryGet(_slotTickets[i], out T value); sum += T.Read(value); }
        return sum;
    }

    [Benchmark, BenchmarkCategory("Lookup")]
    public long Dictionary_Lookup()
    {
        long sum = 0;
        for (int i = 0; i < _n; i++) { _dictionary.TryGetValue(_dictionaryKeys[i], out T value); sum += T.Read(value); }
        return sum;
    }

    [Benchmark, BenchmarkCategory("Churn")]
    public int Dense_Churn()
    {
        for (int i = 0; i < _n; i++)
        {
            _dense.TryGet(_denseTickets[i], out T value);
            _dense.Remove(_denseTickets[i]);
            _denseTickets[i] = _dense.Add(value);
        }
        return _dense.Count;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Churn")]
    public int Ticket_Churn()
    {
        for (int i = 0; i < _n; i++)
        {
            _slot.TryGet(_slotTickets[i], out T value);
            _slot.Remove(_slotTickets[i]);
            _slotTickets[i] = _slot.Add(value);
        }
        return _slot.Count;
    }

    [Benchmark, BenchmarkCategory("Churn")]
    public int Dictionary_Churn()
    {
        for (int i = 0; i < _n; i++)
        {
            SwiftTicket key = _dictionaryKeys[i];
            T value = _dictionary[key];
            _dictionary.Remove(key);
            _dictionary.Add(key, value);
        }
        return _dictionary.Count;
    }

    [Benchmark, BenchmarkCategory("Mixed")]
    public long Dense_Mixed()
    {
        for (int k = 0; k < 16; k++) T.Increment(ref _dense.GetRef(_denseTickets[(k * 29) & (_n - 1)]));
        _dense.TryGet(_denseTickets[0], out T value);
        _dense.Remove(_denseTickets[0]);
        _denseTickets[0] = _dense.Add(value);
        return Dense_Scan();
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Mixed")]
    public long Ticket_Mixed()
    {
        for (int k = 0; k < 16; k++) T.Increment(ref _slot.GetRef(_slotTickets[(k * 29) & (_n - 1)]));
        _slot.TryGet(_slotTickets[0], out T value);
        _slot.Remove(_slotTickets[0]);
        _slotTickets[0] = _slot.Add(value);
        return Ticket_Scan();
    }

    [Benchmark, BenchmarkCategory("Mixed")]
    public long Dictionary_Mixed()
    {
        for (int k = 0; k < 16; k++)
        {
            SwiftTicket key = _dictionaryKeys[(k * 29) & (_n - 1)];
            T updated = _dictionary[key];
            T.Increment(ref updated);
            _dictionary[key] = updated;
        }
        SwiftTicket removed = _dictionaryKeys[0];
        T value = _dictionary[removed];
        _dictionary.Remove(removed);
        _dictionary.Add(removed, value);
        return Dictionary_Scan();
    }
}
