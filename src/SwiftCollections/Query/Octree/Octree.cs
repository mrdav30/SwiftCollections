using FastCollections;
using FixedMathSharp;

namespace SwiftCollections.Query
{
    public class Octree<T> where T : IOctreeItem
    {
        public OctreeNode<T> Root { get; private set; }
        public Octree(Vector3d center, Fixed64 size)
        {
            Root = new OctreeNode<T>(center, size);
        }

        public void Insert(T child)
        {
            // Recursively insert the child into the octree
            Root.Insert(child);
        }

        public FastList<T> Query(Vector3d position, Fixed64 radius, ref FastList<T> output)
        {
            Root.Query(position, radius, ref output);
            return output;
        }
    }
}
