//=======================================================================
// SwiftFixedSpatialHash.cs
//=======================================================================
// MIT License, Copyright (c) 2024–present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Runtime.CompilerServices;
using FixedMathSharp;

namespace SwiftCollections.Query;

/// <summary>
/// Represents a fixed-point spatial hash optimized for deterministic broad-phase spatial queries.
/// </summary>
public class SwiftFixedSpatialHash<T> : SwiftSpatialHash<T, FixedBoundVolume>
{
    private readonly Fixed64 _cellSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="SwiftFixedSpatialHash{T}"/> class with the specified capacity and cell size.
    /// </summary>
    public SwiftFixedSpatialHash(int capacity, Fixed64 cellSize)
        : this(capacity, cellSize, SwiftSpatialHashOptions.Default) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SwiftFixedSpatialHash{T}"/> class with the specified capacity, cell size, and options.
    /// </summary>
    public SwiftFixedSpatialHash(int capacity, Fixed64 cellSize, SwiftSpatialHashOptions options)
        : base(capacity, new FixedBoundVolumeCellMapper(cellSize), options)
    {
        _cellSize = cellSize;
    }

    /// <summary>
    /// Collects broad-phase candidates registered in the spatial-hash cell
    /// containing a point. Callers remain responsible for exact filtering.
    /// </summary>
    /// <param name="point">The fixed-point position whose cell should be queried.</param>
    /// <param name="results">The caller-owned result collection.</param>
    public void CollectPointCandidates(Vector3d point, System.Collections.Generic.ICollection<T> results) =>
        CollectCellCandidates(GetCellIndex(point), results);

    /// <summary>
    /// Maps a fixed-point position to its exact spatial-hash cell.
    /// </summary>
    /// <param name="point">The fixed-point position to map.</param>
    /// <returns>The containing spatial-hash cell.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SwiftSpatialHashCellIndex GetCellIndex(Vector3d point) =>
        new(
            ToCell(point.X, _cellSize),
            ToCell(point.Y, _cellSize),
            ToCell(point.Z, _cellSize));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ToCell(Fixed64 value, Fixed64 cellSize)
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

    private sealed class FixedBoundVolumeCellMapper : ISpatialHashCellMapper<FixedBoundVolume>
    {
        private readonly Fixed64 _cellSize;

        public FixedBoundVolumeCellMapper(Fixed64 cellSize)
        {
            if (cellSize <= Fixed64.Zero)
                throw new ArgumentOutOfRangeException(nameof(cellSize), cellSize, "Cell size must be greater than zero.");

            _cellSize = cellSize;
        }

        public void GetCellRange(FixedBoundVolume bounds, out SwiftSpatialHashCellIndex minCell, out SwiftSpatialHashCellIndex maxCell)
        {
            minCell = new SwiftSpatialHashCellIndex(
                ToCell(bounds.Min.X),
                ToCell(bounds.Min.Y),
                ToCell(bounds.Min.Z));

            maxCell = new SwiftSpatialHashCellIndex(
                ToCell(bounds.Max.X),
                ToCell(bounds.Max.Y),
                ToCell(bounds.Max.Z));
        }

        private int ToCell(Fixed64 value)
        {
            return SwiftFixedSpatialHash<T>.ToCell(value, _cellSize);
        }
    }
}
