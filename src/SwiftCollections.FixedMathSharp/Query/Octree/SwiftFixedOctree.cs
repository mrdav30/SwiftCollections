//=======================================================================
// SwiftFixedOctree.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using FixedMathSharp;

namespace SwiftCollections.Query;

/// <summary>
/// Indexes fixed-point bounds in a subdivided bounded world region.
/// </summary>
/// <typeparam name="T">The stable key type; equality and hashing must remain unchanged while indexed.</typeparam>
/// <remarks>
/// Mutable runtime index: synchronize all access externally, including queries that use shared scratch/stamps.
/// Key and volume callbacks must not reenter or mutate the index. Bounds are copied and keys are retained;
/// changing a key's equality/hash breaks lookup. Query ordering depends on current topology and mutation history;
/// it is not sorted or a serialized order guarantee. Determinism requires deterministic keys, input order and
/// volume/mapping arithmetic. A key is indexed once; duplicate Insert updates its bounds and returns false. A
/// query adds each matching stored key once, independently of keys already in the destination.
/// </remarks>
public sealed class SwiftFixedOctree<T> : SwiftOctree<T, FixedBoundVolume>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.Query.SwiftFixedOctree`1" /> class.
    /// </summary>
    /// <param name="worldBounds">The fixed root region in caller coordinates, copied by value. All inserted entry
    /// bounds must be fully contained, including their boundaries.</param>
    /// <param name="options">The options copied by value. default(SwiftOctreeOptions) yields depth/capacity zero
    /// and merging false; the tree does not validate it.</param>
    /// <param name="minNodeSize">The strictly positive minimum child edge length in bounds coordinate units,
    /// required on every axis before subdivision. Both fixed-point halves must meet it.</param>
    /// <exception cref="T:System.ArgumentOutOfRangeException">The cell/node size is not positive.</exception>
    public SwiftFixedOctree(FixedBoundVolume worldBounds, SwiftOctreeOptions options, Fixed64 minNodeSize)
        : base(worldBounds, options, new FixedBoundVolumeOctreePartitioner(minNodeSize))
    { }

    private sealed class FixedBoundVolumeOctreePartitioner : IOctreeBoundsPartitioner<FixedBoundVolume>
    {
        private readonly Fixed64 _minNodeSize;

        public FixedBoundVolumeOctreePartitioner(Fixed64 minNodeSize)
        {
            FixedTreeSubdivision.ValidateMinimumNodeSize(minNodeSize);

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
            Vector3d min = bounds.Min;
            Vector3d midpoint = bounds.Center;
            Vector3d max = bounds.Max;
            ulong requiredRawSpan = (ulong)_minNodeSize.m_rawValue;
            return FixedTreeSubdivision.HasMinimumChildSpan(min.X, midpoint.X, max.X, requiredRawSpan)
                 & FixedTreeSubdivision.HasMinimumChildSpan(min.Y, midpoint.Y, max.Y, requiredRawSpan)
                 & FixedTreeSubdivision.HasMinimumChildSpan(min.Z, midpoint.Z, max.Z, requiredRawSpan);
        }

        public bool TryGetContainingChildIndex(FixedBoundVolume nodeBounds, FixedBoundVolume entryBounds, out int childIndex)
        {
            Vector3d midpoint = nodeBounds.Center;

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
            Vector3d midpoint = parentBounds.Center;
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
