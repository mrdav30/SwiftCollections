using System;

namespace SwiftCollections.Query;

/// <summary>
/// Represents a numerics-backed spatial hash optimized for high-churn broad-phase spatial queries.
/// </summary>
public class SwiftSpatialHash<TKey> : SwiftSpatialHash<TKey, BoundVolume>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SwiftSpatialHash{TKey}"/> class with the specified capacity and cell size.
    /// </summary>
    public SwiftSpatialHash(int capacity, float cellSize)
        : this(capacity, cellSize, SwiftSpatialHashOptions.Default)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SwiftSpatialHash{TKey}"/> class with the specified capacity, cell size, and options.
    /// </summary>
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
