//=======================================================================
// IOctreeBoundsPartitioner.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

namespace SwiftCollections.Query;

/// <summary>
/// Maps octree subdivision and containment behavior for a specific volume backend.
/// </summary>
/// <typeparam name="TVolume">The value-type volume backend providing consistent intersection, union, equality and
/// cost operations.</typeparam>
/// <remarks>
/// The octree retains this backend by reference. Keep callback behavior stable for the tree's lifetime,
/// including when Count is zero: an empty Clear can retain subdivided topology. All methods must agree on
/// coordinate space and child numbering. A containing child must enclose the entire entry and stay within the
/// parent. Return false for straddlers; callbacks must not mutate/reenter the owning tree.
/// </remarks>
public interface IOctreeBoundsPartitioner<TVolume>
    where TVolume : struct, IBoundVolume<TVolume>
{
    /// <summary>
    /// Determines whether <paramref name="inner" /> is fully contained within <paramref name="outer" />.
    /// </summary>
    /// <param name="outer">The proposed containing volume in the shared coordinate space.</param>
    /// <param name="inner">The proposed contained volume in the same coordinate space.</param>
    /// <returns>true when inner is wholly contained by outer according to the backend, including the backend's
    /// boundary policy; otherwise false.</returns>
    bool ContainsBounds(TVolume outer, TVolume inner);

    /// <summary>
    /// Determines whether the supplied node bounds can be subdivided further.
    /// </summary>
    /// <param name="bounds">The volume in the same caller-defined coordinate space and units as other indexed
    /// bounds; copied by value. No transform is applied.</param>
    /// <returns>true when the backend can construct eight valid child regions satisfying its numeric/minimum-size
    /// limits; otherwise false.</returns>
    bool CanSubdivide(TVolume bounds);

    /// <summary>
    /// Attempts to resolve the child octant that fully contains the supplied entry bounds.
    /// </summary>
    /// <param name="nodeBounds">The parent node region in the shared coordinate space.</param>
    /// <param name="entryBounds">The entry region to place wholly in one child; straddling regions must remain at
    /// the parent.</param>
    /// <param name="childIndex">Receives an index from 0 through 7 only when true is returned; use no out index
    /// on false.</param>
    /// <returns>true when one child wholly contains entryBounds and childIndex is valid; false for straddlers
    /// that must stay in the parent. Built-in backends set childIndex to -1 on failure.</returns>
    bool TryGetContainingChildIndex(TVolume nodeBounds, TVolume entryBounds, out int childIndex);

    /// <summary>
    /// Creates the child-octant bounds for the supplied parent bounds and child index.
    /// </summary>
    /// <param name="parentBounds">The parent node region to subdivide.</param>
    /// <param name="childIndex">The octant index from 0 through 7, using the same mapping as
    /// TryGetContainingChildIndex.</param>
    /// <returns>A value copy of the selected child region, using the same child numbering as
    /// TryGetContainingChildIndex.</returns>
    TVolume CreateChildBounds(TVolume parentBounds, int childIndex);
}
