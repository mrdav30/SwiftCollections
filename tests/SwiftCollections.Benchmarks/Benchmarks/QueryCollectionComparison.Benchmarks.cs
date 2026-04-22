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
public class QueryCollectionComparisonBenchmarks
{
    private const int DynamicWorldExtent = 2048;
    private const int MixedWorldExtent = 4096;
    private const int RegionalWorldExtent = 8192;
    private const int ExtremeVarianceWorldExtent = 16384;
    private const int LargeObjectChurnWorldExtent = 16384;
    private const int SparseHugeWorldExtent = 131072;
    private const int ClusteredGiantsWorldExtent = 16384;
    private const int QueryCount = 64;
    private const int QueryPasses = 32;

    [Params(2048, 8192)]
    public int N;

    private int[] _dynamicKeys;
    private BoundVolume[] _dynamicInitialBounds;
    private BoundVolume[] _dynamicUpdatedBounds;
    private int[] _dynamicUpdateKeys;
    private BoundVolume[] _dynamicQueryBounds;

    private int[] _mixedKeys;
    private BoundVolume[] _mixedBounds;
    private BoundVolume[] _mixedQueryBounds;

    private int[] _regionalKeys;
    private BoundVolume[] _regionalBounds;
    private BoundVolume[] _regionalQueryBounds;

    private int[] _extremeVarianceKeys;
    private BoundVolume[] _extremeVarianceBounds;
    private BoundVolume[] _extremeVarianceQueryBounds;

    private int[] _largeChurnKeys;
    private BoundVolume[] _largeChurnInitialBounds;
    private BoundVolume[] _largeChurnUpdatedBounds;
    private int[] _largeChurnUpdateKeys;
    private BoundVolume[] _largeChurnQueryBounds;

    private int[] _sparseHugeKeys;
    private BoundVolume[] _sparseHugeBounds;
    private BoundVolume[] _sparseHugeQueryBounds;

    private int[] _clusteredGiantsKeys;
    private BoundVolume[] _clusteredGiantsBounds;
    private BoundVolume[] _clusteredGiantsQueryBounds;

    private SwiftBVH<int> _dynamicBvh;
    private SwiftSpatialHash<int> _dynamicSpatialHash;
    private SwiftOctree<int> _dynamicOctree;

    private SwiftBVH<int> _mixedBvh;
    private SwiftSpatialHash<int> _mixedSpatialHash;
    private SwiftOctree<int> _mixedOctree;

    private SwiftBVH<int> _regionalBvh;
    private SwiftSpatialHash<int> _regionalSpatialHash;
    private SwiftOctree<int> _regionalOctree;

    private SwiftBVH<int> _extremeVarianceBvh;
    private SwiftSpatialHash<int> _extremeVarianceSpatialHash;
    private SwiftOctree<int> _extremeVarianceOctree;

    private SwiftBVH<int> _largeChurnBvh;
    private SwiftSpatialHash<int> _largeChurnSpatialHash;
    private SwiftOctree<int> _largeChurnOctree;

    private SwiftBVH<int> _sparseHugeBvh;
    private SwiftSpatialHash<int> _sparseHugeSpatialHash;
    private SwiftOctree<int> _sparseHugeOctree;

    private SwiftBVH<int> _clusteredGiantsBvh;
    private SwiftSpatialHash<int> _clusteredGiantsSpatialHash;
    private SwiftOctree<int> _clusteredGiantsOctree;

    private List<int> _results;

    [GlobalSetup]
    public void Setup()
    {
        _results = new List<int>(N);

        SetupDynamicScenario();
        SetupMixedScenario();
        SetupRegionalScenario();
        SetupExtremeVarianceScenario();
        SetupLargeObjectChurnScenario();
        SetupSparseHugeScenario();
        SetupClusteredGiantsScenario();
    }

    private void SetupDynamicScenario()
    {
        _dynamicKeys = TestHelper.GenerateShuffledRange(N, 1103);
        _dynamicInitialBounds = new BoundVolume[N];
        _dynamicUpdatedBounds = new BoundVolume[N];

        var random = new Random(20191);
        for (int i = 0; i < N; i++)
        {
            Vector3 size = new(1.5f, 1.5f, 1.5f);
            Vector3 min = ClampMin(RandomVector3(random, DynamicWorldExtent), DynamicWorldExtent, size);
            _dynamicInitialBounds[i] = new BoundVolume(min, min + size);

            Vector3 delta = new(2.5f, -1.5f, 2.0f);
            Vector3 movedMin = ClampMin(min + delta, DynamicWorldExtent, size);
            _dynamicUpdatedBounds[i] = new BoundVolume(movedMin, movedMin + size);
        }

        int updateCount = Math.Max(1, N / 4);
        _dynamicUpdateKeys = new int[updateCount];
        Array.Copy(_dynamicKeys, _dynamicUpdateKeys, updateCount);

        _dynamicQueryBounds = new BoundVolume[QueryCount];
        for (int i = 0; i < _dynamicQueryBounds.Length; i++)
        {
            Vector3 center = RandomVector3(random, DynamicWorldExtent);
            Vector3 halfExtents = new(12f, 12f, 12f);
            _dynamicQueryBounds[i] = new BoundVolume(center - halfExtents, center + halfExtents);
        }
    }

    private void SetupMixedScenario()
    {
        _mixedKeys = TestHelper.GenerateShuffledRange(N, 2207);
        _mixedBounds = new BoundVolume[N];
        _mixedQueryBounds = new BoundVolume[QueryCount];

        var random = new Random(31057);
        for (int i = 0; i < N; i++)
        {
            Vector3 min = RandomVector3(random, MixedWorldExtent);
            float sizeScalar = i % 3 == 0 ? 2f : i % 3 == 1 ? 14f : 72f;
            Vector3 size = new(sizeScalar, sizeScalar * 0.75f, sizeScalar * 1.25f);
            Vector3 clampedMin = ClampMin(min, MixedWorldExtent, size);
            _mixedBounds[i] = new BoundVolume(clampedMin, clampedMin + size);
        }

        for (int i = 0; i < _mixedQueryBounds.Length; i++)
        {
            Vector3 center = RandomVector3(random, MixedWorldExtent);
            Vector3 halfExtents = new(48f, 48f, 48f);
            _mixedQueryBounds[i] = new BoundVolume(center - halfExtents, center + halfExtents);
        }
    }

    private void SetupRegionalScenario()
    {
        _regionalKeys = TestHelper.GenerateShuffledRange(N, 3301);
        _regionalBounds = new BoundVolume[N];
        _regionalQueryBounds = new BoundVolume[QueryCount];

        var random = new Random(41041);
        Vector3[] clusterCenters =
        {
            new(512f, 512f, 512f),
            new(1536f, 1536f, 2048f),
            new(4096f, 1024f, 1024f),
            new(6144f, 6144f, 4096f)
        };

        for (int i = 0; i < N; i++)
        {
            Vector3 cluster = clusterCenters[i % clusterCenters.Length];
            Vector3 offset = new(
                NextCentered(random, 160f),
                NextCentered(random, 160f),
                NextCentered(random, 160f));

            Vector3 min = ClampMin(cluster + offset, RegionalWorldExtent, new Vector3(6f, 6f, 6f));
            _regionalBounds[i] = new BoundVolume(min, min + new Vector3(6f, 6f, 6f));
        }

        for (int i = 0; i < _regionalQueryBounds.Length; i++)
        {
            Vector3 cluster = clusterCenters[i % clusterCenters.Length];
            Vector3 halfExtents = new(128f, 128f, 128f);
            _regionalQueryBounds[i] = new BoundVolume(cluster - halfExtents, cluster + halfExtents);
        }
    }

    private void SetupExtremeVarianceScenario()
    {
        _extremeVarianceKeys = TestHelper.GenerateShuffledRange(N, 5107);
        _extremeVarianceBounds = new BoundVolume[N];
        _extremeVarianceQueryBounds = new BoundVolume[QueryCount];

        var random = new Random(52217);
        for (int i = 0; i < N; i++)
        {
            float sizeScalar = i % 40 == 0 ? 1024f : i % 8 == 0 ? 96f : 2f;
            Vector3 size = new(sizeScalar, sizeScalar * 0.65f, sizeScalar * 1.15f);
            Vector3 min = ClampMin(RandomVector3(random, ExtremeVarianceWorldExtent), ExtremeVarianceWorldExtent, size);
            _extremeVarianceBounds[i] = new BoundVolume(min, min + size);
        }

        for (int i = 0; i < _extremeVarianceQueryBounds.Length; i++)
        {
            Vector3 center = RandomVector3(random, ExtremeVarianceWorldExtent);
            Vector3 halfExtents = i % 2 == 0
                ? new Vector3(96f, 96f, 96f)
                : new Vector3(1024f, 768f, 896f);
            _extremeVarianceQueryBounds[i] = new BoundVolume(center - halfExtents, center + halfExtents);
        }
    }

    private void SetupLargeObjectChurnScenario()
    {
        _largeChurnKeys = TestHelper.GenerateShuffledRange(N, 6203);
        _largeChurnInitialBounds = new BoundVolume[N];
        _largeChurnUpdatedBounds = new BoundVolume[N];

        var random = new Random(63127);
        for (int i = 0; i < N; i++)
        {
            float sizeScalar = i % 3 == 0 ? 256f : i % 3 == 1 ? 384f : 640f;
            Vector3 size = new(sizeScalar, sizeScalar * 0.8f, sizeScalar * 0.9f);
            Vector3 min = ClampMin(RandomVector3(random, LargeObjectChurnWorldExtent), LargeObjectChurnWorldExtent, size);
            _largeChurnInitialBounds[i] = new BoundVolume(min, min + size);

            Vector3 delta = new(
                (i % 5 - 2) * 48f,
                (i % 7 - 3) * 32f,
                (i % 3 - 1) * 64f);
            Vector3 movedMin = ClampMin(min + delta, LargeObjectChurnWorldExtent, size);
            _largeChurnUpdatedBounds[i] = new BoundVolume(movedMin, movedMin + size);
        }

        int updateCount = Math.Max(1, N / 3);
        _largeChurnUpdateKeys = new int[updateCount];
        Array.Copy(_largeChurnKeys, _largeChurnUpdateKeys, updateCount);

        _largeChurnQueryBounds = new BoundVolume[QueryCount];
        for (int i = 0; i < _largeChurnQueryBounds.Length; i++)
        {
            Vector3 center = RandomVector3(random, LargeObjectChurnWorldExtent);
            Vector3 halfExtents = new(320f, 256f, 320f);
            _largeChurnQueryBounds[i] = new BoundVolume(center - halfExtents, center + halfExtents);
        }
    }

    private void SetupSparseHugeScenario()
    {
        _sparseHugeKeys = TestHelper.GenerateShuffledRange(N, 7307);
        _sparseHugeBounds = new BoundVolume[N];
        _sparseHugeQueryBounds = new BoundVolume[QueryCount];

        var random = new Random(74131);
        for (int i = 0; i < N; i++)
        {
            float sizeScalar = i % 16 == 0 ? 192f : i % 4 == 0 ? 24f : 4f;
            Vector3 size = new(sizeScalar, sizeScalar * 0.7f, sizeScalar * 1.1f);
            Vector3 min = ClampMin(RandomVector3(random, SparseHugeWorldExtent), SparseHugeWorldExtent, size);
            _sparseHugeBounds[i] = new BoundVolume(min, min + size);
        }

        for (int i = 0; i < _sparseHugeQueryBounds.Length; i++)
        {
            Vector3 center = RandomVector3(random, SparseHugeWorldExtent);
            Vector3 halfExtents = new(12f, 12f, 12f);
            _sparseHugeQueryBounds[i] = new BoundVolume(center - halfExtents, center + halfExtents);
        }
    }

    private void SetupClusteredGiantsScenario()
    {
        _clusteredGiantsKeys = TestHelper.GenerateShuffledRange(N, 8401);
        _clusteredGiantsBounds = new BoundVolume[N];
        _clusteredGiantsQueryBounds = new BoundVolume[QueryCount];

        var random = new Random(85213);
        Vector3[] clusterCenters =
        {
            new(2048f, 2048f, 2048f),
            new(4096f, 6144f, 3072f),
            new(8192f, 4096f, 8192f),
            new(12288f, 11264f, 6144f)
        };

        for (int i = 0; i < N; i++)
        {
            if (i % 48 == 0)
            {
                Vector3 giantCenter = clusterCenters[(i / 48) % clusterCenters.Length] +
                    new Vector3(1024f, 0f, 1024f);
                Vector3 halfExtents = new(1792f, 1280f, 1536f);
                Vector3 min = ClampMin(giantCenter - halfExtents, ClusteredGiantsWorldExtent, halfExtents * 2f);
                _clusteredGiantsBounds[i] = new BoundVolume(min, min + (halfExtents * 2f));
                continue;
            }

            Vector3 cluster = clusterCenters[i % clusterCenters.Length];
            Vector3 offset = new(
                NextCentered(random, 140f),
                NextCentered(random, 140f),
                NextCentered(random, 140f));
            Vector3 size = new(5f, 5f, 5f);
            Vector3 minSmall = ClampMin(cluster + offset, ClusteredGiantsWorldExtent, size);
            _clusteredGiantsBounds[i] = new BoundVolume(minSmall, minSmall + size);
        }

        for (int i = 0; i < _clusteredGiantsQueryBounds.Length; i++)
        {
            Vector3 cluster = clusterCenters[i % clusterCenters.Length];
            Vector3 halfExtents = i % 3 == 0
                ? new Vector3(160f, 160f, 160f)
                : new Vector3(1536f, 768f, 1536f);
            _clusteredGiantsQueryBounds[i] = new BoundVolume(cluster - halfExtents, cluster + halfExtents);
        }
    }

    [IterationSetup(Targets = new[]
    {
        nameof(DynamicNeighbor_SwiftBVH),
        nameof(DynamicNeighbor_SwiftSpatialHash),
        nameof(DynamicNeighbor_SwiftOctree)
    })]
    public void IterationSetup_DynamicNeighbor()
    {
        _dynamicBvh = BuildBvh(_dynamicKeys, _dynamicInitialBounds);
        _dynamicSpatialHash = BuildSpatialHash(_dynamicKeys, _dynamicInitialBounds, 8f, SwiftSpatialHashOptions.Default);
        _dynamicOctree = BuildOctree(
            _dynamicKeys,
            _dynamicInitialBounds,
            DynamicWorldExtent,
            new SwiftOctreeOptions(8, 8),
            4f);
    }

    [IterationSetup(Targets = new[]
    {
        nameof(MixedBroadPhase_SwiftBVH),
        nameof(MixedBroadPhase_SwiftSpatialHash),
        nameof(MixedBroadPhase_SwiftOctree)
    })]
    public void IterationSetup_MixedBroadPhase()
    {
        _mixedBvh = BuildBvh(_mixedKeys, _mixedBounds);
        _mixedSpatialHash = BuildSpatialHash(_mixedKeys, _mixedBounds, 32f, SwiftSpatialHashOptions.Default);
        _mixedOctree = BuildOctree(
            _mixedKeys,
            _mixedBounds,
            MixedWorldExtent,
            new SwiftOctreeOptions(8, 12),
            8f);
    }

    [IterationSetup(Targets = new[]
    {
        nameof(StaticRegionalQuery_SwiftBVH),
        nameof(StaticRegionalQuery_SwiftSpatialHash),
        nameof(StaticRegionalQuery_SwiftOctree)
    })]
    public void IterationSetup_RegionalQueries()
    {
        _regionalBvh = BuildBvh(_regionalKeys, _regionalBounds);
        _regionalSpatialHash = BuildSpatialHash(_regionalKeys, _regionalBounds, 64f, SwiftSpatialHashOptions.Default);
        _regionalOctree = BuildOctree(
            _regionalKeys,
            _regionalBounds,
            RegionalWorldExtent,
            new SwiftOctreeOptions(9, 10),
            16f);
    }

    [IterationSetup(Targets = new[]
    {
        nameof(ExtremeSizeVarianceBroadPhase_SwiftBVH),
        nameof(ExtremeSizeVarianceBroadPhase_SwiftSpatialHash),
        nameof(ExtremeSizeVarianceBroadPhase_SwiftOctree)
    })]
    public void IterationSetup_ExtremeVariance()
    {
        _extremeVarianceBvh = BuildBvh(_extremeVarianceKeys, _extremeVarianceBounds);
        _extremeVarianceSpatialHash = BuildSpatialHash(_extremeVarianceKeys, _extremeVarianceBounds, 192f, SwiftSpatialHashOptions.Default);
        _extremeVarianceOctree = BuildOctree(
            _extremeVarianceKeys,
            _extremeVarianceBounds,
            ExtremeVarianceWorldExtent,
            new SwiftOctreeOptions(9, 12),
            24f);
    }

    [IterationSetup(Targets = new[]
    {
        nameof(LargeObjectChurn_SwiftBVH),
        nameof(LargeObjectChurn_SwiftSpatialHash),
        nameof(LargeObjectChurn_SwiftOctree)
    })]
    public void IterationSetup_LargeObjectChurn()
    {
        _largeChurnBvh = BuildBvh(_largeChurnKeys, _largeChurnInitialBounds);
        _largeChurnSpatialHash = BuildSpatialHash(_largeChurnKeys, _largeChurnInitialBounds, 256f, SwiftSpatialHashOptions.Default);
        _largeChurnOctree = BuildOctree(
            _largeChurnKeys,
            _largeChurnInitialBounds,
            LargeObjectChurnWorldExtent,
            new SwiftOctreeOptions(8, 10),
            64f);
    }

    [IterationSetup(Targets = new[]
    {
        nameof(SparseHugeWorldNeedleQueries_SwiftBVH),
        nameof(SparseHugeWorldNeedleQueries_SwiftSpatialHash),
        nameof(SparseHugeWorldNeedleQueries_SwiftOctree)
    })]
    public void IterationSetup_SparseHuge()
    {
        _sparseHugeBvh = BuildBvh(_sparseHugeKeys, _sparseHugeBounds);
        _sparseHugeSpatialHash = BuildSpatialHash(_sparseHugeKeys, _sparseHugeBounds, 512f, SwiftSpatialHashOptions.Default);
        _sparseHugeOctree = BuildOctree(
            _sparseHugeKeys,
            _sparseHugeBounds,
            SparseHugeWorldExtent,
            new SwiftOctreeOptions(11, 10),
            32f);
    }

    [IterationSetup(Targets = new[]
    {
        nameof(ClusteredWithOverlappingGiants_SwiftBVH),
        nameof(ClusteredWithOverlappingGiants_SwiftSpatialHash),
        nameof(ClusteredWithOverlappingGiants_SwiftOctree)
    })]
    public void IterationSetup_ClusteredGiants()
    {
        _clusteredGiantsBvh = BuildBvh(_clusteredGiantsKeys, _clusteredGiantsBounds);
        _clusteredGiantsSpatialHash = BuildSpatialHash(_clusteredGiantsKeys, _clusteredGiantsBounds, 256f, SwiftSpatialHashOptions.Default);
        _clusteredGiantsOctree = BuildOctree(
            _clusteredGiantsKeys,
            _clusteredGiantsBounds,
            ClusteredGiantsWorldExtent,
            new SwiftOctreeOptions(9, 12),
            32f);
    }

    [Benchmark]
    [BenchmarkCategory("DynamicNeighbor")]
    public int DynamicNeighbor_SwiftBVH()
    {
        UpdateEntries(_dynamicUpdateKeys, _dynamicUpdatedBounds, _dynamicBvh);
        return ExecuteQueries(_dynamicBvh, _dynamicQueryBounds);
    }

    [Benchmark]
    [BenchmarkCategory("DynamicNeighbor")]
    public int DynamicNeighbor_SwiftSpatialHash()
    {
        UpdateEntries(_dynamicUpdateKeys, _dynamicUpdatedBounds, _dynamicSpatialHash);
        return ExecuteQueries(_dynamicSpatialHash, _dynamicQueryBounds);
    }

    [Benchmark]
    [BenchmarkCategory("DynamicNeighbor")]
    public int DynamicNeighbor_SwiftOctree()
    {
        UpdateEntries(_dynamicUpdateKeys, _dynamicUpdatedBounds, _dynamicOctree);
        return ExecuteQueries(_dynamicOctree, _dynamicQueryBounds);
    }

    [Benchmark]
    [BenchmarkCategory("MixedBroadPhase")]
    public int MixedBroadPhase_SwiftBVH() => ExecuteQueries(_mixedBvh, _mixedQueryBounds);

    [Benchmark]
    [BenchmarkCategory("MixedBroadPhase")]
    public int MixedBroadPhase_SwiftSpatialHash() => ExecuteQueries(_mixedSpatialHash, _mixedQueryBounds);

    [Benchmark]
    [BenchmarkCategory("MixedBroadPhase")]
    public int MixedBroadPhase_SwiftOctree() => ExecuteQueries(_mixedOctree, _mixedQueryBounds);

    [Benchmark]
    [BenchmarkCategory("StaticRegional")]
    public int StaticRegionalQuery_SwiftBVH() => ExecuteQueries(_regionalBvh, _regionalQueryBounds);

    [Benchmark]
    [BenchmarkCategory("StaticRegional")]
    public int StaticRegionalQuery_SwiftSpatialHash() => ExecuteQueries(_regionalSpatialHash, _regionalQueryBounds);

    [Benchmark]
    [BenchmarkCategory("StaticRegional")]
    public int StaticRegionalQuery_SwiftOctree() => ExecuteQueries(_regionalOctree, _regionalQueryBounds);

    [Benchmark]
    [BenchmarkCategory("ExtremeSizeVariance")]
    public int ExtremeSizeVarianceBroadPhase_SwiftBVH() => ExecuteQueries(_extremeVarianceBvh, _extremeVarianceQueryBounds);

    [Benchmark]
    [BenchmarkCategory("ExtremeSizeVariance")]
    public int ExtremeSizeVarianceBroadPhase_SwiftSpatialHash() => ExecuteQueries(_extremeVarianceSpatialHash, _extremeVarianceQueryBounds);

    [Benchmark]
    [BenchmarkCategory("ExtremeSizeVariance")]
    public int ExtremeSizeVarianceBroadPhase_SwiftOctree() => ExecuteQueries(_extremeVarianceOctree, _extremeVarianceQueryBounds);

    [Benchmark]
    [BenchmarkCategory("LargeObjectChurn")]
    public int LargeObjectChurn_SwiftBVH()
    {
        UpdateEntries(_largeChurnUpdateKeys, _largeChurnUpdatedBounds, _largeChurnBvh);
        return ExecuteQueries(_largeChurnBvh, _largeChurnQueryBounds);
    }

    [Benchmark]
    [BenchmarkCategory("LargeObjectChurn")]
    public int LargeObjectChurn_SwiftSpatialHash()
    {
        UpdateEntries(_largeChurnUpdateKeys, _largeChurnUpdatedBounds, _largeChurnSpatialHash);
        return ExecuteQueries(_largeChurnSpatialHash, _largeChurnQueryBounds);
    }

    [Benchmark]
    [BenchmarkCategory("LargeObjectChurn")]
    public int LargeObjectChurn_SwiftOctree()
    {
        UpdateEntries(_largeChurnUpdateKeys, _largeChurnUpdatedBounds, _largeChurnOctree);
        return ExecuteQueries(_largeChurnOctree, _largeChurnQueryBounds);
    }

    [Benchmark]
    [BenchmarkCategory("SparseHugeWorld")]
    public int SparseHugeWorldNeedleQueries_SwiftBVH() => ExecuteQueries(_sparseHugeBvh, _sparseHugeQueryBounds);

    [Benchmark]
    [BenchmarkCategory("SparseHugeWorld")]
    public int SparseHugeWorldNeedleQueries_SwiftSpatialHash() => ExecuteQueries(_sparseHugeSpatialHash, _sparseHugeQueryBounds);

    [Benchmark]
    [BenchmarkCategory("SparseHugeWorld")]
    public int SparseHugeWorldNeedleQueries_SwiftOctree() => ExecuteQueries(_sparseHugeOctree, _sparseHugeQueryBounds);

    [Benchmark]
    [BenchmarkCategory("ClusteredGiants")]
    public int ClusteredWithOverlappingGiants_SwiftBVH() => ExecuteQueries(_clusteredGiantsBvh, _clusteredGiantsQueryBounds);

    [Benchmark]
    [BenchmarkCategory("ClusteredGiants")]
    public int ClusteredWithOverlappingGiants_SwiftSpatialHash() => ExecuteQueries(_clusteredGiantsSpatialHash, _clusteredGiantsQueryBounds);

    [Benchmark]
    [BenchmarkCategory("ClusteredGiants")]
    public int ClusteredWithOverlappingGiants_SwiftOctree() => ExecuteQueries(_clusteredGiantsOctree, _clusteredGiantsQueryBounds);

    private SwiftBVH<int> BuildBvh(int[] keys, BoundVolume[] bounds)
    {
        var bvh = new SwiftBVH<int>(N);
        InsertEntries(keys, bounds, bvh);
        return bvh;
    }

    private SwiftSpatialHash<int> BuildSpatialHash(int[] keys, BoundVolume[] bounds, float cellSize, SwiftSpatialHashOptions options)
    {
        var spatialHash = new SwiftSpatialHash<int>(N, cellSize, options);
        InsertEntries(keys, bounds, spatialHash);
        return spatialHash;
    }

    private SwiftOctree<int> BuildOctree(
        int[] keys,
        BoundVolume[] bounds,
        float worldExtent,
        SwiftOctreeOptions options,
        float minNodeSize)
    {
        var octree = new SwiftOctree<int>(
            new BoundVolume(Vector3.Zero, new Vector3(worldExtent, worldExtent, worldExtent)),
            options,
            minNodeSize);
        InsertEntries(keys, bounds, octree);
        return octree;
    }

    private static void InsertEntries(int[] keys, BoundVolume[] bounds, SwiftBVH<int> bvh)
    {
        for (int i = 0; i < keys.Length; i++)
        {
            int key = keys[i];
            bvh.Insert(key, bounds[key]);
        }
    }

    private static void InsertEntries(int[] keys, BoundVolume[] bounds, SwiftSpatialHash<int> spatialHash)
    {
        for (int i = 0; i < keys.Length; i++)
        {
            int key = keys[i];
            spatialHash.Insert(key, bounds[key]);
        }
    }

    private static void InsertEntries(int[] keys, BoundVolume[] bounds, SwiftOctree<int> octree)
    {
        for (int i = 0; i < keys.Length; i++)
        {
            int key = keys[i];
            octree.Insert(key, bounds[key]);
        }
    }

    private static void UpdateEntries(int[] keys, BoundVolume[] updatedBounds, SwiftBVH<int> bvh)
    {
        for (int i = 0; i < keys.Length; i++)
        {
            int key = keys[i];
            bvh.UpdateEntryBounds(key, updatedBounds[key]);
        }
    }

    private static void UpdateEntries(int[] keys, BoundVolume[] updatedBounds, SwiftSpatialHash<int> spatialHash)
    {
        for (int i = 0; i < keys.Length; i++)
        {
            int key = keys[i];
            spatialHash.UpdateEntryBounds(key, updatedBounds[key]);
        }
    }

    private static void UpdateEntries(int[] keys, BoundVolume[] updatedBounds, SwiftOctree<int> octree)
    {
        for (int i = 0; i < keys.Length; i++)
        {
            int key = keys[i];
            octree.UpdateEntryBounds(key, updatedBounds[key]);
        }
    }

    private int ExecuteQueries(SwiftBVH<int> bvh, BoundVolume[] queryBounds)
    {
        int totalHits = 0;
        for (int pass = 0; pass < QueryPasses; pass++)
        {
            for (int i = 0; i < queryBounds.Length; i++)
            {
                _results.Clear();
                bvh.Query(queryBounds[i], _results);
                totalHits += _results.Count;
            }
        }

        return totalHits;
    }

    private int ExecuteQueries(SwiftSpatialHash<int> spatialHash, BoundVolume[] queryBounds)
    {
        int totalHits = 0;
        for (int pass = 0; pass < QueryPasses; pass++)
        {
            for (int i = 0; i < queryBounds.Length; i++)
            {
                _results.Clear();
                spatialHash.Query(queryBounds[i], _results);
                totalHits += _results.Count;
            }
        }

        return totalHits;
    }

    private int ExecuteQueries(SwiftOctree<int> octree, BoundVolume[] queryBounds)
    {
        int totalHits = 0;
        for (int pass = 0; pass < QueryPasses; pass++)
        {
            for (int i = 0; i < queryBounds.Length; i++)
            {
                _results.Clear();
                octree.Query(queryBounds[i], _results);
                totalHits += _results.Count;
            }
        }

        return totalHits;
    }

    private static Vector3 RandomVector3(Random random, float extent)
    {
        return new Vector3(
            (float)(random.NextDouble() * extent),
            (float)(random.NextDouble() * extent),
            (float)(random.NextDouble() * extent));
    }

    private static Vector3 ClampMin(Vector3 min, float extent, Vector3 size)
    {
        return new Vector3(
            MathF.Min(MathF.Max(min.X, 0f), extent - size.X),
            MathF.Min(MathF.Max(min.Y, 0f), extent - size.Y),
            MathF.Min(MathF.Max(min.Z, 0f), extent - size.Z));
    }

    private static float NextCentered(Random random, float radius)
    {
        return (float)((random.NextDouble() * 2.0 - 1.0) * radius);
    }
}
