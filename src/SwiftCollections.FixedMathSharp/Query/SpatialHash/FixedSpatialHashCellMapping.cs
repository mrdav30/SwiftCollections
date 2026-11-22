//=======================================================================
// FixedSpatialHashCellMapping.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Runtime.CompilerServices;
using FixedMathSharp;

namespace SwiftCollections.Query;

/// <summary>
/// Validates fixed-point cell sizes and maps coordinates to floored, Int32-clamped cell indices.
/// </summary>
internal static class FixedSpatialHashCellMapping
{
    internal static void ValidateCellSize(Fixed64 cellSize)
    {
        if (cellSize <= Fixed64.Zero)
            throw new ArgumentOutOfRangeException(nameof(cellSize), cellSize, "Cell size must be greater than zero.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int ToCell(Fixed64 value, Fixed64 cellSize)
    {
        if ((uint)cellSize.m_rawValue == 0U)
        {
            int wholeValue = (int)(value.m_rawValue >> 32);
            int wholeCellSize = (int)(cellSize.m_rawValue >> 32);
            int wholeQuotient = wholeValue / wholeCellSize;
            return wholeValue % wholeCellSize < 0
                ? wholeQuotient - 1
                : wholeQuotient;
        }

        long quotient = value.m_rawValue / cellSize.m_rawValue;
        if (value.m_rawValue % cellSize.m_rawValue < 0L)
            quotient--;

        if (quotient < int.MinValue)
            return int.MinValue;
        if (quotient > int.MaxValue)
            return int.MaxValue;
        return (int)quotient;
    }
}
