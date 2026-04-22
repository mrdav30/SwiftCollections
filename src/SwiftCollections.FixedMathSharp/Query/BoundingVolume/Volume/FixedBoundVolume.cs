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

    /// <summary>
    /// Creates a new volume that represents the union of this volume and the specified volume.
    /// </summary>
    /// <remarks>
    /// The resulting volume is the smallest axis-aligned bounding box that fully contains both input volumes.
    /// </remarks>
    /// <param name="other">
    /// The volume to combine with this volume. 
    /// The resulting volume will encompass both this volume and the specified volume.
    /// </param>
    /// <returns>A new FixedBoundVolume that contains both this volume and the specified volume.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly FixedBoundVolume Union(FixedBoundVolume other)
    {
        return new FixedBoundVolume(Vector3d.Min(Min, other.Min), Vector3d.Max(Max, other.Max));
    }

    /// <summary>
    /// Determines whether this volume intersects with the specified volume.
    /// </summary>
    /// <param name="other">The volume to test for intersection with this volume.</param>
    /// <returns>true if the volumes intersect; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Intersects(FixedBoundVolume other)
    {
        return !(Min.x > other.Max.x || Max.x < other.Min.x ||
                 Min.y > other.Max.y || Max.y < other.Min.y ||
                 Min.z > other.Max.z || Max.z < other.Min.z);
    }

    /// <summary>
    /// Calculates the additional volume required to expand the current volume to fully contain the specified volume.
    /// </summary>
    /// <param name="other">The volume to be encompassed by the current volume.</param>
    /// <returns>
    /// The minimum additional volume needed to contain the specified volume. 
    /// Returns 0 if the current volume already contains the specified volume.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long GetCost(FixedBoundVolume other)
    {
        Fixed64 delta = Union(other).Volume - Volume;
        return delta <= Fixed64.Zero ? 0L : (long)delta.FloorToInt();
    }

    /// <summary>
    /// Determines whether the bounds of this volume are equal to those of the specified volume.
    /// </summary>
    /// <param name="other">A <see cref="FixedBoundVolume"/> to compare with the current volume.</param>
    /// <returns>true if both the minimum and maximum bounds of the volumes are equal; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool BoundsEquals(FixedBoundVolume other)
    {
        return Min == other.Min && Max == other.Max;
    }

    /// <inheritdoc/>
    public readonly bool Equals(FixedBoundVolume other) => BoundsEquals(other);

    /// <inheritdoc/>
    public override readonly bool Equals(object obj) => obj is FixedBoundVolume other && BoundsEquals(other);

    /// <summary>
    /// Determines whether two BoundVolume instances are equal.
    /// </summary>
    public static bool operator ==(FixedBoundVolume left, FixedBoundVolume right) => left.Equals(right);

    /// <summary>
    /// Determines whether two BoundVolume instances are not equal.
    /// </summary>
    public static bool operator !=(FixedBoundVolume left, FixedBoundVolume right) => !(left == right);

    /// <inheritdoc/>
    public override readonly int GetHashCode() => HashCode.Combine(Min, Max);

    /// <summary>
    /// Returns a string that represents the current object, including the minimum and maximum values.
    /// </summary>
    /// <returns>A string in the format "Min: {Min}, Max: {Max}" that displays the minimum and maximum values of the object.</returns>
    public override readonly string ToString() => $"Min: {Min}, Max: {Max}";
}
