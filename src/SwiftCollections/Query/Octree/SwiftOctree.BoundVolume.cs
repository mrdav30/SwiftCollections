using System;
using System.Numerics;

namespace SwiftCollections.Query;

/// <summary>
/// Represents a numerics-backed octree optimized for hierarchical spatial queries.
/// </summary>
public sealed class SwiftOctree<TKey> : SwiftOctree<TKey, BoundVolume>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SwiftOctree{TKey}"/> class.
    /// </summary>
    /// <param name="worldBounds">The immutable world bounds covered by the octree.</param>
    /// <param name="options">Backend-neutral octree options.</param>
    /// <param name="minNodeSize">The minimum child-node axis length allowed for numerics subdivision.</param>
    public SwiftOctree(BoundVolume worldBounds, SwiftOctreeOptions options, float minNodeSize)
        : base(worldBounds, options, new BoundVolumeOctreePartitioner(minNodeSize))
    {
    }

    private sealed class BoundVolumeOctreePartitioner : IOctreeBoundsPartitioner<BoundVolume>
    {
        private readonly float _minNodeSize;

        public BoundVolumeOctreePartitioner(float minNodeSize)
        {
            if (float.IsNaN(minNodeSize) || float.IsInfinity(minNodeSize) || minNodeSize <= 0f)
                throw new ArgumentOutOfRangeException(nameof(minNodeSize), minNodeSize, "Minimum node size must be a finite positive value.");

            _minNodeSize = minNodeSize;
        }

        public bool ContainsBounds(BoundVolume outer, BoundVolume inner)
        {
            return inner.Min.X >= outer.Min.X &&
                   inner.Min.Y >= outer.Min.Y &&
                   inner.Min.Z >= outer.Min.Z &&
                   inner.Max.X <= outer.Max.X &&
                   inner.Max.Y <= outer.Max.Y &&
                   inner.Max.Z <= outer.Max.Z;
        }

        public bool CanSubdivide(BoundVolume bounds)
        {
            Vector3 childSize = bounds.Size * 0.5f;
            return childSize.X >= _minNodeSize &&
                   childSize.Y >= _minNodeSize &&
                   childSize.Z >= _minNodeSize;
        }

        public bool TryGetContainingChildIndex(BoundVolume nodeBounds, BoundVolume entryBounds, out int childIndex)
        {
            Vector3 midpoint = (nodeBounds.Min + nodeBounds.Max) * 0.5f;

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

            int zBit;
            if (entryBounds.Min.Z >= midpoint.Z)
                zBit = 1;
            else if (entryBounds.Max.Z <= midpoint.Z)
                zBit = 0;
            else
            {
                childIndex = -1;
                return false;
            }

            childIndex = xBit | (yBit << 1) | (zBit << 2);
            return true;
        }

        public BoundVolume CreateChildBounds(BoundVolume parentBounds, int childIndex)
        {
            Vector3 midpoint = (parentBounds.Min + parentBounds.Max) * 0.5f;
            bool upperX = (childIndex & 1) != 0;
            bool upperY = (childIndex & 2) != 0;
            bool upperZ = (childIndex & 4) != 0;

            return new BoundVolume(
                new Vector3(
                    upperX ? midpoint.X : parentBounds.Min.X,
                    upperY ? midpoint.Y : parentBounds.Min.Y,
                    upperZ ? midpoint.Z : parentBounds.Min.Z),
                new Vector3(
                    upperX ? parentBounds.Max.X : midpoint.X,
                    upperY ? parentBounds.Max.Y : midpoint.Y,
                    upperZ ? parentBounds.Max.Z : midpoint.Z));
        }
    }
}
