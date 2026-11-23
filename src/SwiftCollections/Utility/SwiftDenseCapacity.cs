//=======================================================================
// SwiftDenseCapacity.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;

namespace SwiftCollections.Utility;

/// <summary>Shares bounded, power-of-two growth for dense ticket owners and stores.</summary>
internal static class SwiftDenseCapacity
{
    internal const int Minimum = 8;
    internal const int Maximum = 1 << 30;

    // Owners validate requests and decide when allocation can publish.
    internal static int Grow(int current, int required)
    {
        if (required <= current) return current;
        long target = Math.Min(Maximum, Math.Max(required, Math.Max(Minimum, (long)current * 2)));
        // NextPowerOfTwo returns a sentinel even at the exact supported limit.
        return Math.Min(Maximum, SwiftHashTools.NextPowerOfTwo((int)target));
    }
}
