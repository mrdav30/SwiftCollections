using System;
using System.Numerics;
using BenchmarkDotNet.Attributes;
using SwiftCollections.Query;

namespace SwiftCollections.Benchmarks;

[MemoryDiagnoser]
public class Query2DSpatialHashBenchmarks
{
    [Params(128, 2048, 8192)]
    public int N;

    private BoundVolume2D[] _bounds;
    private BoundVolume2D[] _queries;
    private BoundVolume[] _embeddedQueries;
    private SwiftSpatialHash2D<int> _index;
    private SwiftSpatialHash<int> _embedded;
    private SwiftList<int> _results;

    [GlobalSetup]
    public void Setup()
    {
        _bounds = new BoundVolume2D[N];
        _queries = new BoundVolume2D[32];
        _embeddedQueries = new BoundVolume[32];
        _index = new SwiftSpatialHash2D<int>(N, 8);
        _embedded = new SwiftSpatialHash<int>(N, 8);
        _results = new SwiftList<int>(N);
        uint seed = 0xC0FFEE;
        for (int i = 0; i < N; i++)
        {
            Vector2 min = new(Next(ref seed) % 1024, Next(ref seed) % 1024);
            _bounds[i] = new BoundVolume2D(min, min + Vector2.One);
            _index.Insert(i, _bounds[i]);
            _embedded.Insert(i, new BoundVolume(new Vector3(min, 0), new Vector3(min + Vector2.One, 1)));
        }
        for (int q = 0; q < _queries.Length; q++)
        {
            Vector2 anchor = _bounds[(q * 97 + 13) % N].Min;
            _queries[q] = new BoundVolume2D(anchor - new Vector2(2), anchor + new Vector2(4));
            _embeddedQueries[q] = new BoundVolume(new Vector3(_queries[q].Min, 0), new Vector3(_queries[q].Max, 1));
            int expected = 0;
            for (int i = 0; i < N; i++)
                if (_queries[q].Intersects(_bounds[i])) expected++;
            _results.Clear();
            _index.Query(_queries[q], _results);
            if (_results.Count != expected) throw new InvalidOperationException("XY count differs from brute force.");
            _results.Clear();
            _embedded.Query(_embeddedQueries[q], _results);
            if (_results.Count != expected) throw new InvalidOperationException("XYZ embedding count differs from brute force.");
        }
    }

    [Benchmark]
    public int Query2D()
    {
        int hits = 0;
        foreach (BoundVolume2D query in _queries)
        {
            _results.Clear();
            _index.Query(query, _results);
            hits += _results.Count;
        }
        return hits;
    }

    [Benchmark]
    public int Query3DEmbedding()
    {
        int hits = 0;
        foreach (BoundVolume query in _embeddedQueries)
        {
            _results.Clear();
            _embedded.Query(query, _results);
            hits += _results.Count;
        }
        return hits;
    }

    [Benchmark]
    public int BruteForce2D()
    {
        int hits = 0;
        foreach (BoundVolume2D query in _queries)
        {
            _results.Clear();
            for (int i = 0; i < N; i++)
                if (query.Intersects(_bounds[i])) _results.Add(i);
            hits += _results.Count;
        }
        return hits;
    }

    private static uint Next(ref uint seed)
    {
        seed ^= seed << 13;
        seed ^= seed >> 17;
        seed ^= seed << 5;
        return seed;
    }
}
