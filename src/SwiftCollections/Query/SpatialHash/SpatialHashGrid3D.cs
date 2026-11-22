//=======================================================================
// SpatialHashGrid3D.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Runtime.CompilerServices;

namespace SwiftCollections.Query;

internal readonly struct SpatialHashGrid3D<TVolume> : ISpatialHashGrid<TVolume, SwiftSpatialHashCellIndex>
    where TVolume : struct, IBoundVolume<TVolume>
{
    private readonly ISpatialHashCellMapper<TVolume> _mapper;

    internal SpatialHashGrid3D(ISpatialHashCellMapper<TVolume> mapper) => _mapper = mapper;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetRange(TVolume bounds, int padding, out SwiftSpatialHashCellIndex min, out SwiftSpatialHashCellIndex max)
    {
        _mapper.GetCellRange(bounds, out min, out max);
        min = new SwiftSpatialHashCellIndex(
            (int)Math.Max(int.MinValue, (long)min.X - padding),
            (int)Math.Max(int.MinValue, (long)min.Y - padding),
            (int)Math.Max(int.MinValue, (long)min.Z - padding));
        max = new SwiftSpatialHashCellIndex(
            (int)Math.Min(int.MaxValue, (long)max.X + padding),
            (int)Math.Min(int.MaxValue, (long)max.Y + padding),
            (int)Math.Min(int.MaxValue, (long)max.Z + padding));
        return min.X <= max.X && min.Y <= max.Y && min.Z <= max.Z;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext(ref SwiftSpatialHashCellIndex cell, SwiftSpatialHashCellIndex min, SwiftSpatialHashCellIndex max)
    {
        // Test before incrementing so Int32.MaxValue remains a valid inclusive endpoint.
        if (cell.Z < max.Z)
            cell = new SwiftSpatialHashCellIndex(cell.X, cell.Y, cell.Z + 1);
        else if (cell.Y < max.Y)
            cell = new SwiftSpatialHashCellIndex(cell.X, cell.Y + 1, min.Z);
        else if (cell.X < max.X)
            cell = new SwiftSpatialHashCellIndex(cell.X + 1, min.Y, min.Z);
        else
            return false;
        return true;
    }
}
