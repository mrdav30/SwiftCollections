using System;
using System.Numerics;

namespace SwiftCollections.Query
{
    /// <summary>
    /// Represents an axis-aligned bounding box (AABB) in 3D space.
    /// </summary>
    public struct BoundingVolume : IBoundingVolume
    {
        public Vector3 Min { get; private set; }
        public Vector3 Max { get; private set; }

        public BoundingVolume(Vector3 min, Vector3 max)
        {
            Min = min;
            Max = max;
        }

        public Vector3 Center => (Min + Max) / 2;

        public Vector3 Size => Max - Min;

        public double Volume => (Size.X * Size.X) + (Size.Y * Size.Y) + (Size.Z * Size.Z);

        /// <summary>
        /// Creates the smallest bounding volume that contains both this and the other volume.
        /// </summary>
        public IBoundingVolume Union(IBoundingVolume other)
        {
            return new BoundingVolume(
                new Vector3(Math.Min(Min.X, other.Min.X), Math.Min(Min.Y, other.Min.Y), Math.Min(Min.Z, other.Min.Z)),
                new Vector3(Math.Max(Max.X, other.Max.X), Math.Max(Max.Y, other.Max.Y), Math.Max(Max.Z, other.Max.Z))
            );

        }

        /// <summary>
        /// Determines whether this volume intersects with another volume.
        /// </summary>
        public bool Intersects(IBoundingVolume other)
        {
            return !(Min.X > other.Max.X || Max.X < other.Min.X ||
                 Min.Y > other.Max.Y || Max.Y < other.Min.Y ||
                 Min.Z > other.Max.Z || Max.Z < other.Min.Z);
        }

        public override string ToString() => $"Min: {Min}, Max: {Max}";
    }
}
