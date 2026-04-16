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
    {
    }

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
            return inner.Min.x >= outer.Min.x &&
                   inner.Min.y >= outer.Min.y &&
                   inner.Min.z >= outer.Min.z &&
                   inner.Max.x <= outer.Max.x &&
                   inner.Max.y <= outer.Max.y &&
                   inner.Max.z <= outer.Max.z;
        }

        public bool CanSubdivide(FixedBoundVolume bounds)
        {
            Vector3d childSize = bounds.Size * Fixed64.Half;
            return childSize.x >= _minNodeSize &&
                   childSize.y >= _minNodeSize &&
                   childSize.z >= _minNodeSize;
        }

        public bool TryGetContainingChildIndex(FixedBoundVolume nodeBounds, FixedBoundVolume entryBounds, out int childIndex)
        {
            Vector3d midpoint = (nodeBounds.Min + nodeBounds.Max) * Fixed64.Half;

            int xBit;
            if (entryBounds.Min.x >= midpoint.x)
                xBit = 1;
            else if (entryBounds.Max.x <= midpoint.x)
                xBit = 0;
            else
            {
                childIndex = -1;
                return false;
            }

            int yBit;
            if (entryBounds.Min.y >= midpoint.y)
                yBit = 1;
            else if (entryBounds.Max.y <= midpoint.y)
                yBit = 0;
            else
            {
                childIndex = -1;
                return false;
            }

            int zBit;
            if (entryBounds.Min.z >= midpoint.z)
                zBit = 1;
            else if (entryBounds.Max.z <= midpoint.z)
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
                    upperX ? midpoint.x : parentBounds.Min.x,
                    upperY ? midpoint.y : parentBounds.Min.y,
                    upperZ ? midpoint.z : parentBounds.Min.z),
                new Vector3d(
                    upperX ? parentBounds.Max.x : midpoint.x,
                    upperY ? parentBounds.Max.y : midpoint.y,
                    upperZ ? parentBounds.Max.z : midpoint.z));
        }
    }
}
