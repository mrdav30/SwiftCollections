using FastCollections;
using FixedMathSharp;
using System.Collections.Generic;

namespace SwiftCollections.Query
{
    public class SpatialHash<T> where T : IPosition
    {
        private Dictionary<int, FastList<T>> _buckets;
        private Fixed64 _size;

        public SpatialHash(Fixed64 size)
        {
            _buckets = new Dictionary<int, FastList<T>>();
            _size = size;
        }

        public void Insert(T item)
        {
            int hash = Hash(item.Position);
            if (!_buckets.TryGetValue(hash, out FastList<T> bucket))
            {
                bucket = new FastList<T>();
                _buckets[hash] = bucket;
            }
            bucket.Add(item);
        }

        public void Query(Vector3d position, ref FastList<T> result)
        {
            int originalHash = Hash(position);
            List<int> hashesToCheck = new List<int>() { originalHash };

            // Calculate the hashes of the neighboring cells:
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (dx != 0 || dy != 0 || dz != 0)  // Exclude the original cell
                        {
                            Vector3d offset = new Vector3d(dx, dy, dz) * _size;
                            int hash = Hash(position + offset);
                            hashesToCheck.Add(hash);
                        }
                    }
                }
            }

            // Look in all of the cells:
            foreach (int hash in hashesToCheck)
            {
                if (_buckets.TryGetValue(hash, out FastList<T> bucket))
                {
                    for (int i = 0; i < bucket.Count; i++)
                    {
                        result.Add(bucket[i]);
                    }
                }
            }
        }

        private int Hash(Vector3d position)
        {
            Fixed64 x = position.x / _size;
            Fixed64 y = position.y / _size;
            Fixed64 z = position.z / _size;
            return x.GetHashCode() ^ y.GetHashCode() ^ z.GetHashCode();
        }

        public void Clear()
        {
            _buckets.Clear();
        }   
    }
}
