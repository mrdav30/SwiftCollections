using System;

namespace SwiftCollections.Query.Tests
{
    public struct TestBoundVolume : IBoundVolume<TestBoundVolume>, IEquatable<TestBoundVolume>
    {
        public TestBoundVolume(float minX, float minY, float minZ, float maxX, float maxY, float maxZ)
        {
            MinX = minX;
            MinY = minY;
            MinZ = minZ;
            MaxX = maxX;
            MaxY = maxY;
            MaxZ = maxZ;
        }

        public float MinX { get; }
        public float MinY { get; }
        public float MinZ { get; }
        public float MaxX { get; }
        public float MaxY { get; }
        public float MaxZ { get; }

        public TestBoundVolume Union(TestBoundVolume other)
        {
            return new TestBoundVolume(
                Math.Min(MinX, other.MinX),
                Math.Min(MinY, other.MinY),
                Math.Min(MinZ, other.MinZ),
                Math.Max(MaxX, other.MaxX),
                Math.Max(MaxY, other.MaxY),
                Math.Max(MaxZ, other.MaxZ));
        }

        public bool Intersects(TestBoundVolume other)
        {
            return !(MinX > other.MaxX || MaxX < other.MinX ||
                     MinY > other.MaxY || MaxY < other.MinY ||
                     MinZ > other.MaxZ || MaxZ < other.MinZ);
        }

        public int GetCost(TestBoundVolume other)
        {
            TestBoundVolume union = Union(other);
            float sizeX = union.MaxX - union.MinX;
            float sizeY = union.MaxY - union.MinY;
            float sizeZ = union.MaxZ - union.MinZ;

            float currentSizeX = MaxX - MinX;
            float currentSizeY = MaxY - MinY;
            float currentSizeZ = MaxZ - MinZ;

            float unionVolume = sizeX * sizeY * sizeZ;
            float currentVolume = currentSizeX * currentSizeY * currentSizeZ;
            return (int)Math.Floor(unionVolume - currentVolume);
        }

        public bool BoundsEquals(TestBoundVolume other)
        {
            return MinX.Equals(other.MinX) &&
                   MinY.Equals(other.MinY) &&
                   MinZ.Equals(other.MinZ) &&
                   MaxX.Equals(other.MaxX) &&
                   MaxY.Equals(other.MaxY) &&
                   MaxZ.Equals(other.MaxZ);
        }

        public bool Equals(TestBoundVolume other) => BoundsEquals(other);

        public override bool Equals(object obj) => obj is TestBoundVolume other && BoundsEquals(other);

        public override int GetHashCode() => HashCode.Combine(MinX, MinY, MinZ, MaxX, MaxY, MaxZ);
    }
}
