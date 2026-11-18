//=======================================================================
// SwiftSpatialHash.BoundVolume.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;

namespace SwiftCollections.Query;

/// <summary>
/// Indexes keyed bounds in grid cells using a supplied or built-in volume mapper.
/// </summary>
/// <typeparam name="TKey">The stable key type; equality and hashing must remain unchanged while
/// indexed.</typeparam>
/// <remarks>
/// Mutable runtime index: synchronize all access externally, including queries that use shared scratch/stamps.
/// Key and volume callbacks must not reenter or mutate the index. Bounds are copied and keys are retained;
/// changing a key's equality/hash breaks lookup. Query ordering depends on current topology and mutation history;
/// it is not sorted or a serialized order guarantee. Determinism requires deterministic keys, input order and
/// volume/mapping arithmetic. A key is indexed once; duplicate Insert updates its bounds and returns false. A
/// query adds each matching stored key once, independently of keys already in the destination.
/// </remarks>
public class SwiftSpatialHash<TKey> : SwiftSpatialHash<TKey, BoundVolume>
    where TKey : notnull
{
    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.Query.SwiftSpatialHash`1" /> class with
    /// the specified capacity and cell size.
    /// </summary>
    /// <param name="capacity">The requested initial storage length in entries. Non-positive values normalize to
    /// one; growth rounds up. Keep capacities below 2^30 and within available array memory.</param>
    /// <param name="cellSize">The strictly positive cell edge length in the same coordinate units as bounds. Must
    /// also be finite. Floating coordinate division is floored; keep mapped cell coordinates representable as
    /// Int32.</param>
    /// <exception cref="T:System.ArgumentOutOfRangeException">The cell/node size is not positive or is
    /// non-finite.</exception>
    public SwiftSpatialHash(int capacity, float cellSize)
        : this(capacity, cellSize, SwiftSpatialHashOptions.Default)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.Query.SwiftSpatialHash`1" /> class with
    /// the specified capacity, cell size, and options.
    /// </summary>
    /// <param name="capacity">The requested initial storage length in entries. Non-positive values normalize to
    /// one; growth rounds up. Keep capacities below 2^30 and within available array memory.</param>
    /// <param name="cellSize">The strictly positive cell edge length in the same coordinate units as bounds. Must
    /// also be finite. Floating coordinate division is floored; keep mapped cell coordinates representable as
    /// Int32.</param>
    /// <param name="options">The options copied by value. Default property padding is one;
    /// default(SwiftSpatialHashOptions) padding is zero.</param>
    /// <exception cref="T:System.ArgumentOutOfRangeException">The cell/node size is not positive or is
    /// non-finite.</exception>
    public SwiftSpatialHash(int capacity, float cellSize, SwiftSpatialHashOptions options)
        : base(capacity, new BoundVolumeCellMapper(cellSize), options)
    {
    }

    private sealed class BoundVolumeCellMapper : ISpatialHashCellMapper<BoundVolume>
    {
        private readonly float _cellSize;

        public BoundVolumeCellMapper(float cellSize)
        {
            if (float.IsNaN(cellSize) || float.IsInfinity(cellSize) || cellSize <= 0f)
                throw new ArgumentOutOfRangeException(nameof(cellSize), cellSize, "Cell size must be a finite positive value.");

            _cellSize = cellSize;
        }

        public void GetCellRange(BoundVolume bounds, out SwiftSpatialHashCellIndex minCell, out SwiftSpatialHashCellIndex maxCell)
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

        private int ToCell(float value)
        {
            return (int)MathF.Floor(value / _cellSize);
        }
    }
}
