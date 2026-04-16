using System;
using System.Collections.Generic;
using System.Numerics;

namespace SwiftCollections.Query.Tests;

internal static class DeterministicBoundVolumeDataset
{
    public static IReadOnlyList<BoundVolume> Create(int count, int seed = 12345)
    {
        var random = new Random(seed);
        var volumes = new List<BoundVolume>(count);

        for (int i = 0; i < count; i++)
        {
            float minX = (float)(random.NextDouble() * 100.0);
            float minY = (float)(random.NextDouble() * 100.0);
            float minZ = (float)(random.NextDouble() * 100.0);

            float maxX = minX + 1f + (float)(random.NextDouble() * 5.0);
            float maxY = minY + 1f + (float)(random.NextDouble() * 5.0);
            float maxZ = minZ + 1f + (float)(random.NextDouble() * 5.0);

            volumes.Add(
                new BoundVolume(
                    new Vector3(minX, minY, minZ),
                    new Vector3(maxX, maxY, maxZ)));
        }

        return volumes;
    }
}
