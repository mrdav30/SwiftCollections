namespace SwiftCollections.Query;

/// <summary>
/// Maps a query volume to the inclusive cell range occupied by that volume.
/// </summary>
/// <typeparam name="TVolume">The volume type used by the spatial hash.</typeparam>
public interface ISpatialHashCellMapper<TVolume>
    where TVolume : struct, IBoundVolume<TVolume>
{
    /// <summary>
    /// Calculates the inclusive minimum and maximum occupied cell coordinates for the supplied volume.
    /// </summary>
    /// <param name="bounds">The volume to map into cell space.</param>
    /// <param name="minCell">The minimum occupied cell coordinate.</param>
    /// <param name="maxCell">The maximum occupied cell coordinate.</param>
    void GetCellRange(TVolume bounds, out SwiftSpatialHashCellIndex minCell, out SwiftSpatialHashCellIndex maxCell);
}
