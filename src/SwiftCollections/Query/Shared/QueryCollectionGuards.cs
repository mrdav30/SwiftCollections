using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SwiftCollections.Query;

internal static class QueryCollectionGuards
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int NormalizeCapacity(int capacity)
    {
        return SwiftHashTools.NextPowerOfTwo(capacity);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfResultsCollectionNull<TKey>(ICollection<TKey> results, string paramName)
    {
        SwiftThrowHelper.ThrowIfNull(results, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfKeyNull<TKey>(TKey key, string paramName)
    {
        SwiftThrowHelper.ThrowIfNull(key, paramName);
    }
}
