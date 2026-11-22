//=======================================================================
// FixedTreeSubdivision.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Runtime.CompilerServices;
using FixedMathSharp;

namespace SwiftCollections.Query;

/// <summary>
/// Shares minimum-size validation and full-range fixed-point child-span checks between tree backends.
/// </summary>
internal static class FixedTreeSubdivision
{
    internal static void ValidateMinimumNodeSize(Fixed64 minNodeSize)
    {
        if (minNodeSize <= Fixed64.Zero)
            throw new ArgumentOutOfRangeException(nameof(minNodeSize), minNodeSize, "Minimum node size must be greater than zero.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool HasMinimumChildSpan(Fixed64 min, Fixed64 midpoint, Fixed64 max, ulong requiredRawSpan)
    {
        // Unsigned differences preserve ordered distances across the full signed scalar domain.
        ulong lowerRawSpan = unchecked((ulong)midpoint.m_rawValue - (ulong)min.m_rawValue);
        ulong upperRawSpan = unchecked((ulong)max.m_rawValue - (ulong)midpoint.m_rawValue);
        return lowerRawSpan >= requiredRawSpan & upperRawSpan >= requiredRawSpan;
    }
}
