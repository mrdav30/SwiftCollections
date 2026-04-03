using System;
using System.Collections.Generic;

namespace SwiftCollections.Tests;

internal static class CollisionStringFactory
{
    public static string[] CreateMaskedCollisions(IEqualityComparer<string> comparer, int mask, int count)
    {
        ArgumentNullException.ThrowIfNull(comparer);

        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        var buckets = new Dictionary<int, List<string>>();

        for (int i = 0; ; i++)
        {
            string candidate = $"collision-{i}";
            int bucket = comparer.GetHashCode(candidate) & mask;

            if (!buckets.TryGetValue(bucket, out List<string> values))
            {
                values = new List<string>(count);
                buckets.Add(bucket, values);
            }

            values.Add(candidate);
            if (values.Count == count)
                return values.ToArray();
        }
    }
}
