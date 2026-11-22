//=======================================================================
// SwiftFixedSpatialHash2D.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using FixedMathSharp;

namespace SwiftCollections.Query;

/// <summary>Indexes fixed-point rectangles in native XY grid cells.</summary>
/// <typeparam name="TKey">The non-null key type; equality and hashing must remain stable while indexed.</typeparam>
/// <remarks>
/// Bounds are copied and canonical keys retained. Inserting an equal key updates its bounds and returns false.
/// Queries append into caller-owned storage without deduplicating against existing entries. Ordering depends
/// on cell traversal, insertion and mutation history; deterministic replay requires deterministic key behavior
/// and identical inputs/history. Synchronize access externally and do not reenter the index from callbacks.
/// Cell coordinates clamp to Int32; a broad cell range can still require impractical traversal work.
/// </remarks>
public class SwiftFixedSpatialHash2D<TKey> : SwiftSpatialHash2D<TKey, FixedBoundVolume2D>
    where TKey : notnull
{
    private readonly Fixed64 _cellSize;

    /// <summary>Creates an empty XY index with the default neighborhood padding of one cell.</summary>
    /// <param name="capacity">Initial entry slots; nonpositive requests normalize to one and growth rounds up.
    /// Keep capacities below 2^30 and within available array memory.</param>
    /// <param name="cellSize">The strictly positive cell edge length in the same units as indexed bounds.</param>
    /// <exception cref="ArgumentOutOfRangeException">cellSize is zero or negative.</exception>
    public SwiftFixedSpatialHash2D(int capacity, Fixed64 cellSize)
        : this(capacity, cellSize, SwiftSpatialHashOptions.Default) { }

    /// <summary>Creates an empty XY index with explicit neighborhood options.</summary>
    /// <param name="capacity">Initial entry slots; nonpositive requests normalize to one and growth rounds up.
    /// Keep capacities below 2^30 and within available array memory.</param>
    /// <param name="cellSize">The strictly positive cell edge length in the same units as indexed bounds.</param>
    /// <param name="options">Copied options. Default padding is one; default(SwiftSpatialHashOptions) uses zero.</param>
    /// <exception cref="ArgumentOutOfRangeException">cellSize is zero or negative.</exception>
    public SwiftFixedSpatialHash2D(int capacity, Fixed64 cellSize, SwiftSpatialHashOptions options)
        : base(capacity, new FixedBoundVolume2DCellMapper(cellSize), options)
    {
        _cellSize = cellSize;
    }

    /// <summary>Maps a point to its exact fixed-point XY cell, with signed-coordinate clamping.</summary>
    /// <param name="point">The point in the same caller-defined space and units as indexed bounds; copied by value.</param>
    /// <returns>The componentwise floor of raw point coordinates divided by raw cell size, clamped to the Int32 domain.
    /// Negative fractional positions floor toward negative infinity.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SwiftSpatialHashCellIndex2D GetCellIndex(Vector2d point) => new(
        FixedSpatialHashCellMapping.ToCell(point.X, _cellSize),
        FixedSpatialHashCellMapping.ToCell(point.Y, _cellSize));

    /// <summary>Appends every candidate registered in the cell containing a point.</summary>
    /// <param name="point">The point in the indexed coordinate space and units; copied without retention.</param>
    /// <param name="results">Caller-owned storage to append to without clearing or removing existing duplicates.</param>
    /// <remarks>
    /// Does not filter candidates against the point or bounds. Each indexed entry occupies a cell at most once;
    /// use Query for exact intersections. Destination Add failures can leave partially appended output.
    /// </remarks>
    /// <exception cref="ArgumentNullException">results is null.</exception>
    public void CollectPointCandidates(Vector2d point, ICollection<TKey> results) =>
        CollectCellCandidates(GetCellIndex(point), results);

    private sealed class FixedBoundVolume2DCellMapper : ISpatialHashCellMapper2D<FixedBoundVolume2D>
    {
        private readonly Fixed64 _cellSize;

        public FixedBoundVolume2DCellMapper(Fixed64 cellSize)
        {
            FixedSpatialHashCellMapping.ValidateCellSize(cellSize);
            _cellSize = cellSize;
        }

        public void GetCellRange(FixedBoundVolume2D bounds,
            out SwiftSpatialHashCellIndex2D minCell, out SwiftSpatialHashCellIndex2D maxCell)
        {
            minCell = new SwiftSpatialHashCellIndex2D(
                FixedSpatialHashCellMapping.ToCell(bounds.Min.X, _cellSize),
                FixedSpatialHashCellMapping.ToCell(bounds.Min.Y, _cellSize));
            maxCell = new SwiftSpatialHashCellIndex2D(
                FixedSpatialHashCellMapping.ToCell(bounds.Max.X, _cellSize),
                FixedSpatialHashCellMapping.ToCell(bounds.Max.Y, _cellSize));
        }
    }
}
