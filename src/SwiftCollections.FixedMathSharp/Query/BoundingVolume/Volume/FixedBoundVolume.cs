using FixedMathSharp;
using System;
using System.Runtime.CompilerServices;

namespace SwiftCollections.Query;

/// <summary>
/// Represents an axis-aligned bounding box (AABB) in 3D space using fixed-point math.
/// </summary>
public struct FixedBoundVolume : IBoundVolume<FixedBoundVolume>, IEquatable<FixedBoundVolume>
{
    private Vector3d _min;
    private Vector3d _max;
    private Vector3d _center;
    private Vector3d _size;
    private Fixed64 _volume;
    private bool _isDirty;

    /// <summary>
    /// Initializes a new instance of the <see cref="FixedBoundVolume"/> struct.
    /// </summary>
    /// <param name="min">The minimum point of the volume.</param>
    /// <param name="max">The maximum point of the volume.</param>
    public FixedBoundVolume(Vector3d min, Vector3d max)
    {
        _min = min;
        _max = max;
        _center = default;
        _size = default;
        _volume = default;
        _isDirty = true;
    }

    /// <summary>
    /// Gets the minimum point of the bounding volume.
    /// </summary>
    public readonly Vector3d Min => _min;

    /// <summary>
    /// Gets the maximum point of the bounding volume.
    /// </summary>
    public readonly Vector3d Max => _max;

    /// <summary>
    /// Gets the center point of the bounding volume.
    /// </summary>
    public Vector3d Center
    {
        get
        {
            if (_isDirty)
                RecalculateMeta();
            return _center;
        }
    }

    /// <summary>
    /// Gets the axis-aligned size of the bounding volume.
    /// </summary>
    public Vector3d Size
    {
        get
        {
            if (_isDirty)
                RecalculateMeta();
            return _size;
        }
    }

    /// <summary>
    /// Gets the volume of the bounding box.
    /// </summary>
    public Fixed64 Volume
    {
        get
        {
            if (_isDirty)
                RecalculateMeta();
            return _volume;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecalculateMeta()
    {
        _center = (_min + _max) * Fixed64.Half;
        _size = _max - _min;
        _volume = _size.x * _size.y * _size.z;
        _isDirty = false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly FixedBoundVolume Union(FixedBoundVolume other)
    {
        return new FixedBoundVolume(Vector3d.Min(Min, other.Min), Vector3d.Max(Max, other.Max));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Intersects(FixedBoundVolume other)
    {
        return !(Min.x > other.Max.x || Max.x < other.Min.x ||
                 Min.y > other.Max.y || Max.y < other.Min.y ||
                 Min.z > other.Max.z || Max.z < other.Min.z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly int GetCost(FixedBoundVolume other)
    {
        return (Union(other).Volume - other.Volume).FloorToInt();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool BoundsEquals(FixedBoundVolume other)
    {
        return Min == other.Min && Max == other.Max;
    }

    public readonly bool Equals(FixedBoundVolume other) => BoundsEquals(other);

    public override readonly bool Equals(object obj) => obj is FixedBoundVolume other && BoundsEquals(other);

    public override readonly int GetHashCode() => HashCode.Combine(Min, Max);

    public override readonly string ToString() => $"Min: {Min}, Max: {Max}";
}
