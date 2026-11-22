//=======================================================================
// ISpatialHashGrid.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

namespace SwiftCollections.Query;

// Only dimensional mapping and coordinate stepping differ between the two grid owners.
/// <summary>
/// Defines dimensional cell-range mapping and coordinate stepping for the shared spatial hash engine.
/// </summary>
internal interface ISpatialHashGrid<TVolume, TCell>
    where TVolume : struct, IBoundVolume<TVolume>
    where TCell : struct
{
    bool TryGetRange(TVolume bounds, int padding, out TCell min, out TCell max);
    bool MoveNext(ref TCell cell, TCell min, TCell max);
}
