//=======================================================================
// SwiftSpatialHash2D.BoundVolume.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Runtime.CompilerServices;

namespace SwiftCollections.Query;

/// <summary>Indexes keyed Numerics rectangles in native XY cells.</summary>
/// <typeparam name="TKey">The non-null key type; equality and hashing must stay stable while indexed.</typeparam>
/// <remarks>
/// Bounds are copied and canonical keys retained. Queries append to caller-owned results in cell/history order.
/// Synchronize all access externally and do not reenter the index from callbacks. Computed floating-point mapping
/// does not promise cross-platform lockstep determinism. Large mapped ranges require visiting every cell.
/// </remarks>
public class SwiftSpatialHash2D<TKey> : SwiftSpatialHash2D<TKey, BoundVolume2D>
    where TKey : notnull
{
    /// <summary>Creates an empty XY index using default neighborhood padding.</summary>
    /// <param name="capacity">Initial entry storage; nonpositive values normalize to one and growth rounds up.
    /// Keep requests below 2^30 and within available array memory.</param>
    /// <param name="cellSize">The finite positive cell edge length in the bounds' caller coordinate units.</param>
    /// <exception cref="ArgumentOutOfRangeException">cellSize is nonpositive or nonfinite.</exception>
    public SwiftSpatialHash2D(int capacity, float cellSize)
        : this(capacity, cellSize, SwiftSpatialHashOptions.Default)
    {
    }

    /// <summary>Creates an empty XY index with copied query options.</summary>
    /// <param name="capacity">Initial entry storage; nonpositive values normalize to one and growth rounds up.
    /// Keep requests below 2^30 and within available array memory.</param>
    /// <param name="cellSize">The finite positive cell edge length in the bounds' caller coordinate units.</param>
    /// <param name="options">Copied options. Default padding is one; default(SwiftSpatialHashOptions) padding is zero.</param>
    /// <exception cref="ArgumentOutOfRangeException">cellSize is nonpositive or nonfinite.</exception>
    /// <remarks>Coordinates are divided in double precision, floored, then clamped to Int32 cells. Query ranges
    /// include both mapped endpoints; neighborhood expansion is clipped to Int32 limits.</remarks>
    public SwiftSpatialHash2D(int capacity, float cellSize, SwiftSpatialHashOptions options)
        : base(capacity, new BoundVolume2DCellMapper(cellSize), options)
    {
    }

    private sealed class BoundVolume2DCellMapper : ISpatialHashCellMapper2D<BoundVolume2D>
    {
        private readonly float _cellSize;

        internal BoundVolume2DCellMapper(float cellSize)
        {
            if (!float.IsFinite(cellSize) || cellSize <= 0f)
                throw new ArgumentOutOfRangeException(nameof(cellSize), cellSize, "Cell size must be a finite positive value.");
            _cellSize = cellSize;
        }

        public void GetCellRange(BoundVolume2D bounds, out SwiftSpatialHashCellIndex2D minCell, out SwiftSpatialHashCellIndex2D maxCell)
        {
            minCell = new SwiftSpatialHashCellIndex2D(ToCell(bounds.Min.X), ToCell(bounds.Min.Y));
            maxCell = new SwiftSpatialHashCellIndex2D(ToCell(bounds.Max.X), ToCell(bounds.Max.Y));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ToCell(float value)
        {
            double cell = (double)value / _cellSize;
            if (cell <= int.MinValue) return int.MinValue;
            if (cell >= int.MaxValue) return int.MaxValue;
            return (int)Math.Floor(cell);
        }
    }
}
