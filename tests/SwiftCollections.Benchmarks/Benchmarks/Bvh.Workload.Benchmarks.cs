using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using SwiftCollections.Query;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace SwiftCollections.Benchmarks;

[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class BvhWorkloadBenchmarks
{
    private const int QueryCount = 64;

    [Params(1024, 4096, 16384)]
    public int N;

    private int[] _keys;
    private BoundVolume[] _initialBounds;
    private BoundVolume[] _updatedBounds;
    private int[] _updateKeys;
    private BoundVolume[] _queryBounds;

    private SwiftBVH<int> _bvh;
    private List<int> _queryResults;

    [GlobalSetup]
    public void Setup()
    {
        _keys = TestHelper.GenerateShuffledRange(N, 42);
        _initialBounds = new BoundVolume[N];
        _updatedBounds = new BoundVolume[N];

        var random = new Random(12345);
        for (int i = 0; i < N; i++)
        {
            float x = (float)(random.NextDouble() * 5000.0);
            float y = (float)(random.NextDouble() * 5000.0);
            float z = (float)(random.NextDouble() * 5000.0);

            var min = new Vector3(x, y, z);
            var max = min + new Vector3(1f, 1f, 1f);
            _initialBounds[i] = new BoundVolume(min, max);

            var movedMin = min + new Vector3(0.5f, 0.5f, 0.5f);
            _updatedBounds[i] = new BoundVolume(movedMin, movedMin + new Vector3(1f, 1f, 1f));
        }

        int updateCount = N / 4;
        if (updateCount == 0)
            updateCount = 1;

        _updateKeys = new int[updateCount];
        for (int i = 0; i < updateCount; i++)
            _updateKeys[i] = _keys[i];

        _queryBounds = new BoundVolume[QueryCount];
        for (int i = 0; i < QueryCount; i++)
        {
            float centerX = (float)(random.NextDouble() * 5000.0);
            float centerY = (float)(random.NextDouble() * 5000.0);
            float centerZ = (float)(random.NextDouble() * 5000.0);

            var halfExtents = new Vector3(25f, 25f, 25f);
            _queryBounds[i] = new BoundVolume(
                new Vector3(centerX, centerY, centerZ) - halfExtents,
                new Vector3(centerX, centerY, centerZ) + halfExtents);
        }

        _queryResults = new List<int>(N);
    }

    [Benchmark]
    [BenchmarkCategory("Insert")]
    public int SwiftBVH_Insert()
    {
        var bvh = new SwiftBVH<int>(N);
        for (int i = 0; i < N; i++)
            bvh.Insert(_keys[i], _initialBounds[i]);

        return bvh.Count;
    }

    [IterationSetup(Targets = new[] { nameof(SwiftBVH_Query), nameof(SwiftBVH_UpdateBounds) })]
    public void IterationSetup_Bvh()
    {
        _bvh = new SwiftBVH<int>(N);
        for (int i = 0; i < N; i++)
            _bvh.Insert(_keys[i], _initialBounds[i]);
    }

    [Benchmark]
    [BenchmarkCategory("Query")]
    public int SwiftBVH_Query()
    {
        int totalHits = 0;
        for (int i = 0; i < _queryBounds.Length; i++)
        {
            _queryResults.Clear();
            _bvh.Query(_queryBounds[i], _queryResults);
            totalHits += _queryResults.Count;
        }

        return totalHits;
    }

    [Benchmark]
    [BenchmarkCategory("Update")]
    public int SwiftBVH_UpdateBounds()
    {
        for (int i = 0; i < _updateKeys.Length; i++)
        {
            int key = _updateKeys[i];
            _bvh.UpdateEntryBounds(key, _updatedBounds[key]);
        }

        return _updateKeys.Length;
    }
}
