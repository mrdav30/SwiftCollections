//=======================================================================
// ISpatialHashCellMapper.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

namespace SwiftCollections.Query;

/// <summary>
/// Maps a query volume to the inclusive cell range occupied by that volume.
/// </summary>
/// <typeparam name="TVolume">The value-type volume backend providing consistent intersection, union, equality and
/// cost operations.</typeparam>
/// <remarks>
/// The spatial hash retains the mapper. Return ordered inclusive cell bounds covering every possible
/// intersection; min must not exceed max on any axis. Cell mapping must remain stable across
/// insert/update/remove/query. Very large ranges require iteration over every cell and can be impractical; no
/// range-size budget is enforced.
/// </remarks>
public interface ISpatialHashCellMapper<TVolume>
    where TVolume : struct, IBoundVolume<TVolume>
{
    /// <summary>
    /// Calculates the inclusive minimum and maximum occupied cell coordinates for the supplied volume.
    /// </summary>
    /// <param name="bounds">The volume in the same caller-defined coordinate space and units as other indexed
    /// bounds; copied by value. No transform is applied.</param>
    /// <param name="minCell">Receives the inclusive minimum occupied cell coordinate on successful
    /// return.</param>
    /// <param name="maxCell">Receives the inclusive maximum occupied cell coordinate on successful
    /// return.</param>
    /// <remarks>
    /// Both out values are valid only after successful return. Callers iterate every cell in this inclusive
    /// range; no source storage is retained or changed.
    /// </remarks>
    void GetCellRange(TVolume bounds, out SwiftSpatialHashCellIndex minCell, out SwiftSpatialHashCellIndex maxCell);
}
