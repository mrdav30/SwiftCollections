//=======================================================================
// SwiftOctree.BoundVolume.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Numerics;

namespace SwiftCollections.Query;

/// <summary>
/// Represents a numerics-backed octree optimized for hierarchical spatial queries.
/// </summary>
/// <typeparam name="TKey">The stable key type; equality and hashing must remain unchanged while
/// indexed.</typeparam>
/// <remarks>
/// Mutable runtime index: synchronize all access externally, including queries that use shared scratch/stamps.
/// Key and volume callbacks must not reenter or mutate the index. Bounds are copied and keys are retained;
/// changing a key's equality/hash breaks lookup. Query ordering depends on current topology and mutation history;
/// it is not sorted or a serialized order guarantee. Determinism requires deterministic keys, input order and
/// volume/mapping arithmetic. A key is indexed once; duplicate Insert updates its bounds and returns false. A
/// query adds each matching stored key once, independently of keys already in the destination. Supply finite
/// ordered bounds. Subdivision uses widened arithmetic and the same rounded midpoint for child construction and
/// selection. It stops when either resulting child span on any axis is below the minimum node size.
/// </remarks>
public sealed class SwiftOctree<TKey> : SwiftOctree<TKey, BoundVolume>
    where TKey : notnull, IEquatable<TKey>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.Query.SwiftOctree`1" /> class.
    /// </summary>
    /// <param name="worldBounds">The fixed root region in caller coordinates, copied by value. All inserted entry
    /// bounds must be fully contained, including their boundaries.</param>
    /// <param name="options">The options copied by value. default(SwiftOctreeOptions) yields depth/capacity zero
    /// and merging false; the tree does not validate it.</param>
    /// <param name="minNodeSize">The strictly positive minimum child edge length in bounds coordinate units,
    /// required for both rounded child spans on every axis before subdivision, compared in double precision.
    /// Must also be finite.</param>
    /// <exception cref="T:System.ArgumentOutOfRangeException">The cell/node size is not positive or is
    /// non-finite.</exception>
    public SwiftOctree(BoundVolume worldBounds, SwiftOctreeOptions options, float minNodeSize)
        : base(worldBounds, options, new BoundVolumeOctreePartitioner(minNodeSize)) { }

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
            Vector3 midpoint = GetMidpoint(bounds);
            return HasMinimumChildSpan(bounds.Min.X, midpoint.X, bounds.Max.X)
                 & HasMinimumChildSpan(bounds.Min.Y, midpoint.Y, bounds.Max.Y)
                 & HasMinimumChildSpan(bounds.Min.Z, midpoint.Z, bounds.Max.Z);
        }

        private bool HasMinimumChildSpan(float min, float midpoint, float max)
        {
            return (double)midpoint - min >= _minNodeSize
                 & (double)max - midpoint >= _minNodeSize;
        }

        private static Vector3 GetMidpoint(BoundVolume bounds)
        {
            // Widen before addition; narrow only the shared split plane, including for subnormal spans.
            return new Vector3(
                (float)(((double)bounds.Min.X + bounds.Max.X) * 0.5),
                (float)(((double)bounds.Min.Y + bounds.Max.Y) * 0.5),
                (float)(((double)bounds.Min.Z + bounds.Max.Z) * 0.5));
        }

        public bool TryGetContainingChildIndex(BoundVolume nodeBounds, BoundVolume entryBounds, out int childIndex)
        {
            Vector3 midpoint = GetMidpoint(nodeBounds);

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
            Vector3 midpoint = GetMidpoint(parentBounds);
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
