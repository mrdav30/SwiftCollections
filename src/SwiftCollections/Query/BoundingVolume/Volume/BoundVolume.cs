using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SwiftCollections.Query;

/// <summary>
/// Represents an axis-aligned bounding box (AABB) in 3D space.
/// </summary>
public struct BoundVolume : IBoundVolume<BoundVolume>, IEquatable<BoundVolume>
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

    /// <summary>
    /// Initializes a new instance of the <see cref="BoundVolume"/> struct with the specified minimum and maximum points.
    /// </summary>
    /// <param name="min">The minimum point of the bounding volume.</param>
    /// <param name="max">The maximum point of the bounding volume.</param>
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
        readonly get => _min;
        private set
        {
            _isDirty = true;
            _min = value;
        }
    }

    /// <inheritdoc cref="_max"/>
    public Vector3 Max
    {
        readonly get => _max;
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

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly BoundVolume Union(BoundVolume other)
    {
        return new BoundVolume(
            new Vector3(Math.Min(Min.X, other.Min.X), Math.Min(Min.Y, other.Min.Y), Math.Min(Min.Z, other.Min.Z)),
            new Vector3(Math.Max(Max.X, other.Max.X), Math.Max(Max.Y, other.Max.Y), Math.Max(Max.Z, other.Max.Z))
        );
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Intersects(BoundVolume other)
    {
        return !(Min.X > other.Max.X || Max.X < other.Min.X ||
                 Min.Y > other.Max.Y || Max.Y < other.Min.Y ||
                 Min.Z > other.Max.Z || Max.Z < other.Min.Z);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly long GetCost(BoundVolume other)
    {
        BoundVolume union = Union(other);
        Vector3 size = Max - Min;
        double volume = size.X * size.Y * size.Z;
        double delta = union.Volume - volume;
        return delta >= (double)long.MaxValue ? long.MaxValue : (long)delta;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool BoundsEquals(BoundVolume other)
    {
        return Min.Equals(other.Min) && Max.Equals(other.Max);
    }

    public readonly bool Equals(BoundVolume other) => BoundsEquals(other);

    public override readonly bool Equals(object obj) => obj is BoundVolume other && BoundsEquals(other);

    public override readonly int GetHashCode() => HashCode.Combine(Min, Max);

    public override readonly string ToString() => $"Min: {Min}, Max: {Max}";
}
