
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SwiftCollections.Query
{
    /// <summary>
    /// Represents an axis-aligned bounding box (AABB) in 3D space.
    /// </summary>
    public struct BoundVolume : IBoundVolume
    {
        /// <summary>
        /// The minimum point of the bounding volume.
        /// </summary>
        private Vector3 _min;

        /// <summary>
        /// The maximum point of the bounding volume.
        /// </summary>
        private Vector3 _max;

        /// <summary>
        /// The center of the bounding volume as the midpoint of the minimum and maximum points.
        /// </summary>
        private Vector3 _center;

        /// <summary>
        /// The size of the bounding volume as the difference between the maximum and minimum points.
        /// </summary>
        private Vector3 _size;

        /// <summary>
        /// The volume of the bounding box, calculated as the product of its dimensions.
        /// </summary>
        private double _volume;

        /// <summary>
        /// Marks the bounding volume as dirty, indicating its properties need recalculation.
        /// </summary>
        private bool _isDirty;

        public BoundVolume(Vector3 min, Vector3 max)
        {
            _min = min;
            _max = max;

            _isDirty = true;
            _center = default;
            _size = default;
            _volume = default;
        }

        /// <inheritdoc cref="_min"/>
        public Vector3 Min
        {
            get => _min;
            private set
            {
                _isDirty = true;
                _min = value;
            }
        }

        /// <inheritdoc cref="_max"/>
        public Vector3 Max
        {
            get => _max;
            private set
            {
                _isDirty = true;
                _max = value;
            }
        }

        /// <inheritdoc cref="_center"/>
        public Vector3 Center
        {
            get
            {
                if (_isDirty)
                    RecalculateMeta();
                return _center;
            }
        }

        /// <inheritdoc cref="_size"/>
        public Vector3 Size
        {
            get
            {
                if (_isDirty)
                    RecalculateMeta();
                return _size;
            }
        }

        /// <inheritdoc cref="_volume"/>
        public double Volume
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
            _center = (_min + _max) / 2;
            _size = _max - _min;
            _volume = _size.X * _size.Y * _size.Z;
            _isDirty = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IBoundVolume Union(IBoundVolume other)
        {
            if(other is BoundVolume otherBV)
                return Union(otherBV);

            return ThrowHelper.ThrowArgumentException<IBoundVolume>($"Mismatched bounding volume type detected!: {nameof(other)}");
        }

        /// <inheritdoc cref="Union(IBoundVolume)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BoundVolume Union(BoundVolume other)
        {
                return new BoundVolume(
                    new Vector3(Math.Min(Min.X, other.Min.X), Math.Min(Min.Y, other.Min.Y), Math.Min(Min.Z, other.Min.Z)),
                    new Vector3(Math.Max(Max.X, other.Max.X), Math.Max(Max.Y, other.Max.Y), Math.Max(Max.Z, other.Max.Z))
                );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Intersects(IBoundVolume other)
        {
            if (other is BoundVolume otherBVH)
            {
                return !(Min.X > otherBVH.Max.X || Max.X < otherBVH.Min.X ||
                 Min.Y > otherBVH.Max.Y || Max.Y < otherBVH.Min.Y ||
                 Min.Z > otherBVH.Max.Z || Max.Z < otherBVH.Min.Z);
            }

            return ThrowHelper.ThrowArgumentException<bool>($"Mismatched bounding volume type detected!: {nameof(other)}");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetCost(IBoundVolume other)
        {
            if (other is BoundVolume otherBVH)
                return (int)Math.Floor(Union(otherBVH).Volume - otherBVH.Volume);

            return ThrowHelper.ThrowArgumentException<int>($"Mismatched bounding volume type detected!: {nameof(other)}");
        }

        public override string ToString() => $"Min: {Min}, Max: {Max}";
    }
}
