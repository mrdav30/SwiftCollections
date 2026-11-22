//=======================================================================
// SwiftQuadtree.BoundVolume.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Numerics;

namespace SwiftCollections.Query;

/// <summary>Indexes Numerics XY bounds in a four-child spatial tree within fixed world bounds.</summary>
/// <typeparam name="TKey">The non-null key type; equality and hashing must remain stable while indexed.</typeparam>
/// <remarks>
/// Bounds are copied and canonical keys retained. Queries append once per matching indexed key into caller-owned
/// storage. Parent entries precede children in increasing index order, with xBit | (yBit &lt;&lt; 1) numbering;
/// exact split-plane points prefer the upper half. Ordering depends on mutation history. Synchronize all access
/// externally and do not reenter or mutate the tree from callbacks. No cross-platform floating-point
/// determinism is promised. Use the fixed-point companion for authoritative deterministic simulation.
/// </remarks>
public sealed class SwiftQuadtree<TKey> : SwiftQuadtree<TKey, BoundVolume2D>
    where TKey : notnull
{
    /// <summary>Creates an empty XY tree with immutable world bounds and subdivision settings.</summary>
    /// <param name="worldBounds">The root rectangle in caller coordinates and units, copied by value. Inserted
    /// bounds must remain wholly inside it, including boundaries; the tree does not expand it.</param>
    /// <param name="options">Copied settings. Default uses depth/capacity zero and merging false, so it never splits.</param>
    /// <param name="minNodeSize">The finite positive minimum child edge length in the same coordinate units.
    /// Both actual rounded child spans on each axis must meet this length before subdivision.</param>
    /// <exception cref="ArgumentOutOfRangeException">minNodeSize is nonpositive or nonfinite.</exception>
    /// <remarks>
    /// Uses the same widened-then-float midpoint for admission, child bounds and placement. Positive minimum-size
    /// checks also require the midpoint to be strictly inside both parent axes. Degenerate or numerically
    /// indivisible nodes remain leaves. No bounds Size or Area value is materialized during subdivision.
    /// </remarks>
    public SwiftQuadtree(BoundVolume2D worldBounds, SwiftQuadtreeOptions options, float minNodeSize)
        : base(worldBounds, options, new BoundVolume2DQuadtreePartitioner(minNodeSize)) { }

    private sealed class BoundVolume2DQuadtreePartitioner : IQuadtreeBoundsPartitioner<BoundVolume2D>
    {
        private readonly float _minNodeSize;

        public BoundVolume2DQuadtreePartitioner(float minNodeSize)
        {
            if (!float.IsFinite(minNodeSize) || minNodeSize <= 0f)
                throw new ArgumentOutOfRangeException(nameof(minNodeSize), minNodeSize, "Minimum node size must be a finite positive value.");
            _minNodeSize = minNodeSize;
        }

        public bool ContainsBounds(BoundVolume2D outer, BoundVolume2D inner) =>
            inner.Min.X >= outer.Min.X && inner.Min.Y >= outer.Min.Y &&
            inner.Max.X <= outer.Max.X && inner.Max.Y <= outer.Max.Y;

        public bool CanSubdivide(BoundVolume2D bounds)
        {
            Vector2 midpoint = bounds.Center;
            return HasMinimumChildSpan(bounds.Min.X, midpoint.X, bounds.Max.X)
                 & HasMinimumChildSpan(bounds.Min.Y, midpoint.Y, bounds.Max.Y);
        }

        private bool HasMinimumChildSpan(float min, float midpoint, float max) =>
            (double)midpoint - min >= _minNodeSize & (double)max - midpoint >= _minNodeSize;

        public bool TryGetContainingChildIndex(BoundVolume2D nodeBounds, BoundVolume2D entryBounds, out int childIndex)
        {
            Vector2 midpoint = nodeBounds.Center;
            int xBit;
            if (entryBounds.Min.X >= midpoint.X)
                xBit = 1;
            else if (entryBounds.Max.X <= midpoint.X)
                xBit = 0;
            else
            {
                childIndex = -1;
                return false;
            }

            int yBit;
            if (entryBounds.Min.Y >= midpoint.Y)
                yBit = 1;
            else if (entryBounds.Max.Y <= midpoint.Y)
                yBit = 0;
            else
            {
                childIndex = -1;
                return false;
            }

            childIndex = xBit | (yBit << 1);
            return true;
        }

        public BoundVolume2D CreateChildBounds(BoundVolume2D parentBounds, int childIndex)
        {
            Vector2 midpoint = parentBounds.Center;
            bool upperX = (childIndex & 1) != 0;
            bool upperY = (childIndex & 2) != 0;
            return new BoundVolume2D(
                new Vector2(upperX ? midpoint.X : parentBounds.Min.X, upperY ? midpoint.Y : parentBounds.Min.Y),
                new Vector2(upperX ? parentBounds.Max.X : midpoint.X, upperY ? parentBounds.Max.Y : midpoint.Y));
        }
    }
}
