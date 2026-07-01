//=======================================================================
// SwiftFixedOctree.cs
//=======================================================================
// MIT License, Copyright (c) 2024–present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using FixedMathSharp;

namespace SwiftCollections.Query;

/// <summary>
/// Represents a fixed-point octree optimized for deterministic hierarchical spatial queries.
/// </summary>
public sealed class SwiftFixedOctree<T> : SwiftOctree<T, FixedBoundVolume>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SwiftFixedOctree{T}"/> class.
    /// </summary>
    /// <param name="worldBounds">The immutable world bounds covered by the octree.</param>
    /// <param name="options">Backend-neutral octree options.</param>
    /// <param name="minNodeSize">The minimum child-node axis length allowed for fixed-point subdivision.</param>
    public SwiftFixedOctree(FixedBoundVolume worldBounds, SwiftOctreeOptions options, Fixed64 minNodeSize)
        : base(worldBounds, options, new FixedBoundVolumeOctreePartitioner(minNodeSize))
    { }

    private sealed class FixedBoundVolumeOctreePartitioner : IOctreeBoundsPartitioner<FixedBoundVolume>
    {
        private readonly Fixed64 _minNodeSize;

        public FixedBoundVolumeOctreePartitioner(Fixed64 minNodeSize)
        {
            if (minNodeSize <= Fixed64.Zero)
                throw new System.ArgumentOutOfRangeException(nameof(minNodeSize), minNodeSize, "Minimum node size must be greater than zero.");

            _minNodeSize = minNodeSize;
        }

        public bool ContainsBounds(FixedBoundVolume outer, FixedBoundVolume inner)
        {
            return inner.Min.X >= outer.Min.X &&
                   inner.Min.Y >= outer.Min.Y &&
                   inner.Min.Z >= outer.Min.Z &&
                   inner.Max.X <= outer.Max.X &&
                   inner.Max.Y <= outer.Max.Y &&
                   inner.Max.Z <= outer.Max.Z;
        }

        public bool CanSubdivide(FixedBoundVolume bounds)
        {
            Vector3d childSize = bounds.Size * Fixed64.Half;
            return childSize.X >= _minNodeSize &&
                   childSize.Y >= _minNodeSize &&
                   childSize.Z >= _minNodeSize;
        }

        public bool TryGetContainingChildIndex(FixedBoundVolume nodeBounds, FixedBoundVolume entryBounds, out int childIndex)
        {
            Vector3d midpoint = (nodeBounds.Min + nodeBounds.Max) * Fixed64.Half;

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

        public FixedBoundVolume CreateChildBounds(FixedBoundVolume parentBounds, int childIndex)
        {
            Vector3d midpoint = (parentBounds.Min + parentBounds.Max) * Fixed64.Half;
            bool upperX = (childIndex & 1) != 0;
            bool upperY = (childIndex & 2) != 0;
            bool upperZ = (childIndex & 4) != 0;

            return new FixedBoundVolume(
                new Vector3d(
                    upperX ? midpoint.X : parentBounds.Min.X,
                    upperY ? midpoint.Y : parentBounds.Min.Y,
                    upperZ ? midpoint.Z : parentBounds.Min.Z),
                new Vector3d(
                    upperX ? parentBounds.Max.X : midpoint.X,
                    upperY ? parentBounds.Max.Y : midpoint.Y,
                    upperZ ? parentBounds.Max.Z : midpoint.Z));
        }
    }
}
