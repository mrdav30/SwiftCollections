using System.Numerics;

namespace SwiftCollections.Query
{
    public interface IBoundingVolume
    {
        Vector3 Min { get; }
        Vector3 Max { get; }
        double Volume { get; }
        IBoundingVolume Union(IBoundingVolume other);
        bool Intersects(IBoundingVolume other);
    }
}
