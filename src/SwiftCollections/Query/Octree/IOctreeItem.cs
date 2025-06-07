using FixedMathSharp;

namespace SwiftCollections.Query
{
    public interface IOctreeItem
    {
        bool EdgeIntersectsBox(Vector3d nodeCenter, Fixed64 nodeSize);
    }
}
