using FixedMathSharp;
using System.Runtime.CompilerServices;

namespace SwiftCollections.Query
{
    /// <summary>
    /// Represents an axis-aligned bounding box (AABB) in 3D space using Fixed-Point Math.
    /// </summary>
    public struct FixedBoundVolume : IBoundVolume
    {
        /// <summary>
        /// The minimum point of the bounding volume.
        /// </summary>
        private Vector3d _min;

        /// <summary>
        /// The maximum point of the bounding volume.
        /// </summary>
        private Vector3d _max;

        /// <summary>
        /// The center of the bounding volume as the midpoint of the minimum and maximum points.
        /// </summary>
        private Vector3d _center;

        /// <summary>
        /// The size of the bounding volume as the difference between the maximum and minimum points.
        /// </summary>
        private Vector3d _size;

        /// <summary>
        /// The volume of the bounding box, calculated as the product of its dimensions.
        /// </summary>
        private Fixed64 _volume;

        /// <summary>
        /// Marks the bounding volume as dirty, indicating its properties need recalculation.
        /// </summary>
        private bool _isDirty;

        public FixedBoundVolume(Vector3d min, Vector3d max)
        {
            _min = min;
            _max = max;

            _isDirty = true;
            _center = default;
            _size = default;
            _volume = default;
        }

        /// <inheritdoc cref="_min"/>
        public Vector3d Min
        {
            get => _min;
            private set
            {
                _isDirty = true;
                _min = value;
            }
        }

        /// <inheritdoc cref="_max"/>
        public Vector3d Max
        {
            get => _max;
            private set
            {
                _isDirty = true;
                _max = value;
            }
        }

        /// <inheritdoc cref="_center"/>
        public Vector3d Center
        {
            get
            {
                if (_isDirty)
                    RecalculateMeta();
                return _center;
            }
        }

        /// <inheritdoc cref="_size"/>
        public Vector3d Size
        {
            get
            {
                if (_isDirty)
                    RecalculateMeta();
                return _size;
            }
        }

        /// <inheritdoc cref="_volume"/>
        public Fixed64 Volume
        {
            get
            {
                if (_isDirty)
                    RecalculateMeta();
                return _volume;
            }
        }

        /// <summary>
        /// Forces recalculation of the bounding volume's metadata, such as center, size, and volume.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RecalculateMeta()
        {
            _center = (_min + _max) * Fixed64.Half;
            _size = _max - _min;
            _volume = _size.x * _size.y * _size.z;
            _isDirty = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IBoundVolume Union(IBoundVolume other)
        {
            if (other is FixedBoundVolume otherBV)
                return Union(otherBV);

            return ThrowHelper.ThrowArgumentException<IBoundVolume>($"Mismatched bounding volume type detected!: {nameof(other)}");
        }

        /// <inheritdoc cref="Union(IBoundVolume)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FixedBoundVolume Union(FixedBoundVolume other)
        {
            return new FixedBoundVolume(Vector3d.Min(Min, other.Min), Vector3d.Max(Max, other.Max));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Intersects(IBoundVolume other)
        {
            if (other is FixedBoundVolume otherBV)
            {
                return !(Min.x > otherBV.Max.x || Max.x < otherBV.Min.x ||
                         Min.y > otherBV.Max.y || Max.y < otherBV.Min.y ||
                         Min.z > otherBV.Max.z || Max.z < otherBV.Min.z);
            }

            return ThrowHelper.ThrowArgumentException<bool>($"Mismatched bounding volume type detected!: {nameof(other)}");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetCost(IBoundVolume other)
        {
            if (other is FixedBoundVolume otherBV)
                return (Union(otherBV).Volume - otherBV.Volume).FloorToInt();

            return ThrowHelper.ThrowArgumentException<int>($"Mismatched bounding volume type detected!: {nameof(other)}");
        }

        public override string ToString() => $"Min: {Min}, Max: {Max}";
    }
}
