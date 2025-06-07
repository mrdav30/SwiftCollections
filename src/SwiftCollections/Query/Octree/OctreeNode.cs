using FastCollections;
using FixedMathSharp;

namespace SwiftCollections.Query
{
    public class OctreeNode<T> where T : IOctreeItem
    {
        private const int ChildNodeCount = 8;
        /// <summary>
        /// the threshold at which a node in your octree will split into smaller child nodes.
        /// If you set it too high, your octree might not be as effective at spatially partitioning your objects, meaning queries might be slower.
        /// On the other hand, setting it too low could result in an excessively deep octree, which could also hurt performance and consume more memory. 
        /// A common value is in the range of 1-10.
        /// </summary>
        private const int MaximumItemCount = 50;
        /// <summary>
        /// The smallest size a node can have. When your nodes reach this size, they will stop splitting, regardless of how many items they contain. 
        /// This prevents your octree from becoming infinitely deep if many items are very close to each other. 
        /// What this size should be depends on the scale of your world. 
        ///     - If your world is in the scale of thousands, a minimum size of 1 might be reasonable. 
        ///     - If your world is in the scale of ones, a minimum size of 0.01 might be more appropriate.
        /// </summary>
        private static readonly Fixed64 MinimumSize = (Fixed64)0.01f;
        public Vector3d Center { get; private set; }
        public Fixed64 Size { get; private set; }
        public FastList<T> MyObjects { get; private set; }
        public OctreeNode<T>[] Children { get; private set; }

        public OctreeNode(Vector3d center, Fixed64 size)
        {
            Center = center;
            Size = size;
            MyObjects = new FastList<T>();
            Children = new OctreeNode<T>[ChildNodeCount];
        }
        public void Insert(T child)
        {
            if (!ContainsChild(child)) return;

            if (Children[0] != null)
            {
                for (int i = 0; i < Children.Length; i++)
                {
                    if (Children[i].ContainsChild(child))
                        Children[i].Insert(child);
                }
            }
            else if (MyObjects.Count > MaximumItemCount && Size >= MinimumSize) 
            {
                Subdivide();
                Insert(child); // Re-insert the item now that the node has been subdivided
            }
            else
                MyObjects.Add(child);
        }

        private void Subdivide()
        {
            Fixed64 quarter = Size / 4;
            Fixed64 newSize = Size / 2;

            Children[0] = new OctreeNode<T>(Center + new Vector3d(-quarter, quarter, -quarter), newSize);
            Children[1] = new OctreeNode<T>(Center + new Vector3d(quarter, quarter, -quarter), newSize);
            Children[2] = new OctreeNode<T>(Center + new Vector3d(-quarter, quarter, quarter), newSize);
            Children[3] = new OctreeNode<T>(Center + new Vector3d(quarter, quarter, quarter), newSize);
            Children[4] = new OctreeNode<T>(Center + new Vector3d(-quarter, -quarter, -quarter), newSize);
            Children[5] = new OctreeNode<T>(Center + new Vector3d(quarter, -quarter, -quarter), newSize);
            Children[6] = new OctreeNode<T>(Center + new Vector3d(-quarter, -quarter, quarter), newSize);
            Children[7] = new OctreeNode<T>(Center + new Vector3d(quarter, -quarter, quarter), newSize);

            FastList<T> oldObjects = MyObjects;
            MyObjects.Clear();
            for (int i = 0; i < oldObjects.Count; i++)
                Insert(oldObjects[i]); // Re-insert the items into the subdivided node
        }

        public void Query(Vector3d position, Fixed64 radius, ref FastList<T> result)
        {
            if (!BoxIntersectsSphere(position, radius)) return;

            if (Children[0] == null)
                result.AddRange(MyObjects);
            else
            {
                for (int i = 0; i < ChildNodeCount; i++)
                    Children[i].Query(position, radius, ref result);
            }
        }

        public bool ContainsChild(T child)
        {
            return child.EdgeIntersectsBox(Center, Size);
        }

        private bool BoxIntersectsSphere(Vector3d sphereCenter, Fixed64 sphereRadius)
        {
            Fixed64 rSquared = sphereRadius * sphereRadius;
            Vector3d min = Center - Size / 2;
            Vector3d max = Center + Size / 2;

            Fixed64 dSquared = Fixed64.Zero;
            dSquared += GetAxisContribution(min.x, max.x, sphereCenter.x);
            dSquared += GetAxisContribution(min.y, max.y, sphereCenter.y);
            dSquared += GetAxisContribution(min.z, max.z, sphereCenter.z);

            return dSquared <= rSquared;
        }

        private Fixed64 GetAxisContribution(Fixed64 min, Fixed64 max, Fixed64 value)
        {
            if (value < min) return (value - min).Squared();
            if (value > max) return (value - max).Squared();
            return Fixed64.Zero;
        }

        public void MoveCenter(Vector3d delta)
        {
            Center += delta;
            for (int i = 0; i < Children.Length; i++)
                Children[i]?.MoveCenter(delta);
        }
    }
}
