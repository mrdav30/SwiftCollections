//=======================================================================
// SpatialHashGrid2D.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Runtime.CompilerServices;

namespace SwiftCollections.Query;

internal readonly struct SpatialHashGrid2D<TVolume> : ISpatialHashGrid<TVolume, SwiftSpatialHashCellIndex2D>
    where TVolume : struct, IBoundVolume<TVolume>
{
    private readonly ISpatialHashCellMapper2D<TVolume> _mapper;

    internal SpatialHashGrid2D(ISpatialHashCellMapper2D<TVolume> mapper) => _mapper = mapper;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetRange(TVolume bounds, int padding, out SwiftSpatialHashCellIndex2D min, out SwiftSpatialHashCellIndex2D max)
    {
        _mapper.GetCellRange(bounds, out min, out max);
        min = new SwiftSpatialHashCellIndex2D(
            (int)Math.Max(int.MinValue, (long)min.X - padding),
            (int)Math.Max(int.MinValue, (long)min.Y - padding));
        max = new SwiftSpatialHashCellIndex2D(
            (int)Math.Min(int.MaxValue, (long)max.X + padding),
            (int)Math.Min(int.MaxValue, (long)max.Y + padding));
        return min.X <= max.X && min.Y <= max.Y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext(ref SwiftSpatialHashCellIndex2D cell, SwiftSpatialHashCellIndex2D min, SwiftSpatialHashCellIndex2D max)
    {
        if (cell.Y < max.Y)
            cell = new SwiftSpatialHashCellIndex2D(cell.X, cell.Y + 1);
        else if (cell.X < max.X)
            cell = new SwiftSpatialHashCellIndex2D(cell.X + 1, min.Y);
        else
            return false;
        return true;
    }
}
