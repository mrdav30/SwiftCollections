//=======================================================================
// SwiftFixedQuadtree.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using FixedMathSharp;

namespace SwiftCollections.Query;

/// <summary>Indexes fixed-point rectangles in a bounded world with four-child subdivision.</summary>
/// <typeparam name="TKey">The non-null stable key type; equality and hashing must remain unchanged while indexed.</typeparam>
/// <remarks>
/// Copies bounds and options and retains canonical keys. Queries append to caller-owned storage without
/// deduplicating its existing entries. Subdivision uses the nearest-even fixed midpoint and both actual child
/// spans, without reading potentially overflowing Size or Area. Split-plane points prefer the upper half;
/// straddlers remain at the parent. Query order follows node entries then children in index order and depends
/// on mutation history. Deterministic replay requires deterministic keys and identical inputs and history.
/// Synchronize all access externally and do not reenter or mutate the index from callbacks. This runtime
/// index has no persistence contract.
/// </remarks>
public sealed class SwiftFixedQuadtree<TKey> : SwiftQuadtree<TKey, FixedBoundVolume2D>
    where TKey : notnull
{
    /// <summary>Creates an empty quadtree within copied, immutable world bounds.</summary>
    /// <param name="worldBounds">The root rectangle in caller-defined coordinate space and Q32.32 units.
    /// Every inserted rectangle must be wholly contained, including its boundaries.</param>
    /// <param name="options">Copied subdivision options. default(SwiftQuadtreeOptions) uses zero depth and
    /// capacity with merging disabled, leaving an unsplit tree.</param>
    /// <param name="minNodeSize">The strictly positive minimum child edge length in the bounds' units.
    /// Both actual halves on both axes must meet this length before subdivision.</param>
    /// <exception cref="System.ArgumentOutOfRangeException">minNodeSize is zero or negative.</exception>
    public SwiftFixedQuadtree(FixedBoundVolume2D worldBounds, SwiftQuadtreeOptions options, Fixed64 minNodeSize)
        : base(worldBounds, options, new FixedBoundVolumeQuadtreePartitioner(minNodeSize))
    {
    }

    private sealed class FixedBoundVolumeQuadtreePartitioner : IQuadtreeBoundsPartitioner<FixedBoundVolume2D>
    {
        private readonly Fixed64 _minNodeSize;

        public FixedBoundVolumeQuadtreePartitioner(Fixed64 minNodeSize)
        {
            FixedTreeSubdivision.ValidateMinimumNodeSize(minNodeSize);
            _minNodeSize = minNodeSize;
        }

        public bool ContainsBounds(FixedBoundVolume2D outer, FixedBoundVolume2D inner)
        {
            return inner.Min.X >= outer.Min.X &&
                   inner.Min.Y >= outer.Min.Y &&
                   inner.Max.X <= outer.Max.X &&
                   inner.Max.Y <= outer.Max.Y;
        }

        public bool CanSubdivide(FixedBoundVolume2D bounds)
        {
            Vector2d min = bounds.Min;
            Vector2d midpoint = bounds.Center;
            Vector2d max = bounds.Max;
            ulong requiredRawSpan = (ulong)_minNodeSize.m_rawValue;
            return FixedTreeSubdivision.HasMinimumChildSpan(min.X, midpoint.X, max.X, requiredRawSpan)
                 & FixedTreeSubdivision.HasMinimumChildSpan(min.Y, midpoint.Y, max.Y, requiredRawSpan);
        }

        public bool TryGetContainingChildIndex(FixedBoundVolume2D nodeBounds, FixedBoundVolume2D entryBounds, out int childIndex)
        {
            Vector2d midpoint = nodeBounds.Center;
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

        public FixedBoundVolume2D CreateChildBounds(FixedBoundVolume2D parentBounds, int childIndex)
        {
            Vector2d midpoint = parentBounds.Center;
            bool upperX = (childIndex & 1) != 0;
            bool upperY = (childIndex & 2) != 0;
            return new FixedBoundVolume2D(
                new Vector2d(
                    upperX ? midpoint.X : parentBounds.Min.X,
                    upperY ? midpoint.Y : parentBounds.Min.Y),
                new Vector2d(
                    upperX ? parentBounds.Max.X : midpoint.X,
                    upperY ? parentBounds.Max.Y : midpoint.Y));
        }
    }
}
