using FixedMathSharp;
using System;

namespace SwiftCollections.Query;

/// <summary>
/// Represents a fixed-point spatial hash optimized for deterministic broad-phase spatial queries.
/// </summary>
public class SwiftFixedSpatialHash<T> : SwiftSpatialHash<T, FixedBoundVolume>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SwiftFixedSpatialHash{T}"/> class with the specified capacity and cell size.
    /// </summary>
    public SwiftFixedSpatialHash(int capacity, Fixed64 cellSize)
        : this(capacity, cellSize, SwiftSpatialHashOptions.Default)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SwiftFixedSpatialHash{T}"/> class with the specified capacity, cell size, and options.
    /// </summary>
    public SwiftFixedSpatialHash(int capacity, Fixed64 cellSize, SwiftSpatialHashOptions options)
        : base(capacity, new FixedBoundVolumeCellMapper(cellSize), options)
    {
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
                ToCell(bounds.Min.x),
                ToCell(bounds.Min.y),
                ToCell(bounds.Min.z));

            maxCell = new SwiftSpatialHashCellIndex(
                ToCell(bounds.Max.x),
                ToCell(bounds.Max.y),
                ToCell(bounds.Max.z));
        }

        private int ToCell(Fixed64 value)
        {
            return (value / _cellSize).FloorToInt();
        }
    }
}
