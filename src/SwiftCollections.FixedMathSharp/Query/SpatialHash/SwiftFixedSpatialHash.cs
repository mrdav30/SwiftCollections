//=======================================================================
// SwiftFixedSpatialHash.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System.Runtime.CompilerServices;
using FixedMathSharp;

namespace SwiftCollections.Query;

/// <summary>
/// Indexes fixed-point bounds in integer grid cells.
/// </summary>
/// <typeparam name="T">The stable key type; equality and hashing must remain unchanged while indexed.</typeparam>
/// <remarks>
/// Mutable runtime index: synchronize all access externally, including queries that use shared scratch/stamps.
/// Key and volume callbacks must not reenter or mutate the index. Bounds are copied and keys are retained;
/// changing a key's equality/hash breaks lookup. Query ordering depends on current topology and mutation history;
/// it is not sorted or a serialized order guarantee. Determinism requires deterministic keys, input order and
/// volume/mapping arithmetic. A key is indexed once; duplicate Insert updates its bounds and returns false. A
/// query adds each matching stored key once, independently of keys already in the destination.
/// </remarks>
public class SwiftFixedSpatialHash<T> : SwiftSpatialHash<T, FixedBoundVolume>
{
    private readonly Fixed64 _cellSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.Query.SwiftFixedSpatialHash`1" /> class
    /// with the specified capacity and cell size.
    /// </summary>
    /// <param name="capacity">The requested initial storage length in entries. Non-positive values normalize to
    /// one; growth rounds up. Keep capacities below 2^30 and within available array memory.</param>
    /// <param name="cellSize">The strictly positive cell edge length in the same coordinate units as bounds. Uses
    /// fixed-point raw floor and Int32 cell-coordinate clamping.</param>
    /// <exception cref="T:System.ArgumentOutOfRangeException">The cell/node size is not positive.</exception>
    public SwiftFixedSpatialHash(int capacity, Fixed64 cellSize)
        : this(capacity, cellSize, SwiftSpatialHashOptions.Default) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.Query.SwiftFixedSpatialHash`1" /> class
    /// with the specified capacity, cell size, and options.
    /// </summary>
    /// <param name="capacity">The requested initial storage length in entries. Non-positive values normalize to
    /// one; growth rounds up. Keep capacities below 2^30 and within available array memory.</param>
    /// <param name="cellSize">The strictly positive cell edge length in the same coordinate units as bounds. Uses
    /// fixed-point raw floor and Int32 cell-coordinate clamping.</param>
    /// <param name="options">The options copied by value. Default property padding is one;
    /// default(SwiftSpatialHashOptions) padding is zero.</param>
    /// <exception cref="T:System.ArgumentOutOfRangeException">The cell/node size is not positive.</exception>
    public SwiftFixedSpatialHash(int capacity, Fixed64 cellSize, SwiftSpatialHashOptions options)
        : base(capacity, new FixedBoundVolumeCellMapper(cellSize), options)
    {
        _cellSize = cellSize;
    }

    /// <summary>
    /// Collects broad-phase candidates registered in the spatial-hash cell containing a point. Callers remain
    /// responsible for exact filtering.
    /// </summary>
    /// <param name="point">The point in the same coordinate space and units as indexed bounds; copied by
    /// value.</param>
    /// <param name="results">The caller-owned mutable collection to append matching keys to. Existing entries are
    /// preserved; no deduplication against pre-existing entries is performed.</param>
    /// <exception cref="T:System.ArgumentNullException">results is null.</exception>
    /// <remarks>
    /// Appends all stored candidates for one cell, without testing point/volume intersection and without clearing
    /// results. A stored entry occupies a cell at most once; existing destination duplicates are not removed.
    /// </remarks>
    public void CollectPointCandidates(Vector3d point, System.Collections.Generic.ICollection<T> results) =>
        CollectCellCandidates(GetCellIndex(point), results);

    /// <summary>
    /// Maps a fixed-point position to its exact spatial-hash cell.
    /// </summary>
    /// <param name="point">The point in the same coordinate space and units as indexed bounds; copied by
    /// value.</param>
    /// <returns>The componentwise floor of point divided by cell size, clamped to the Int32 cell domain. Negative
    /// fractional positions map toward negative infinity.</returns>
    /// <remarks>
    /// Uses exact raw fixed-point division; the input point is copied and no storage is retained.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SwiftSpatialHashCellIndex GetCellIndex(Vector3d point) =>
        new(
            FixedSpatialHashCellMapping.ToCell(point.X, _cellSize),
            FixedSpatialHashCellMapping.ToCell(point.Y, _cellSize),
            FixedSpatialHashCellMapping.ToCell(point.Z, _cellSize));

    private sealed class FixedBoundVolumeCellMapper : ISpatialHashCellMapper<FixedBoundVolume>
    {
        private readonly Fixed64 _cellSize;

        public FixedBoundVolumeCellMapper(Fixed64 cellSize)
        {
            FixedSpatialHashCellMapping.ValidateCellSize(cellSize);

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
            return FixedSpatialHashCellMapping.ToCell(value, _cellSize);
        }
    }
}
