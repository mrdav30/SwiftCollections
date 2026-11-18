//=======================================================================
// BoundVolume.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SwiftCollections.Query;

/// <summary>
/// Represents an axis-aligned bounding box (AABB) in 3D space.
/// </summary>
/// <remarks>
/// Uses System.Numerics single-precision coordinates in a caller-defined space. Endpoints are stored verbatim
/// without normalization or finite checks. Supply finite componentwise min &lt;= max. Default is a zero-size box
/// at the origin; touching faces/edges/points intersect. Derived-property reads update a cache in this struct. No
/// cross-platform floating-point determinism is promised.
/// </remarks>
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
    /// Initializes a new instance of the <see cref="T:SwiftCollections.Query.BoundVolume" /> struct with the
    /// specified minimum and maximum points.
    /// </summary>
    /// <param name="min">One coordinate endpoint in caller units; must be componentwise no greater than max for
    /// meaningful geometric results.</param>
    /// <param name="max">The opposite coordinate endpoint in caller units; must be finite and ordered relative to
    /// min for meaningful geometric results.</param>
    public BoundVolume(Vector3 min, Vector3 max)
    {
        _min = min;
        _max = max;

        _isDirty = true;
        _center = default;
        _size = default;
        _volume = default;
    }

    /// <summary>
    /// The minimum point of the bounding volume.
    /// </summary>
    /// <value>The componentwise minimum coordinate in caller units.</value>
    public readonly Vector3 Min => _min;

    /// <summary>
    /// The maximum point of the bounding volume.
    /// </summary>
    /// <value>The componentwise maximum coordinate in caller units.</value>
    public readonly Vector3 Max => _max;

    /// <summary>
    /// The center of the bounding volume as the midpoint of the minimum and maximum points.
    /// </summary>
    /// <value>The single-precision midpoint (Min + Max) / 2; finite endpoints can still overflow intermediate
    /// addition.</value>
    public Vector3 Center
    {
        get
        {
            if (_isDirty)
                RecalculateMeta();
            return _center;
        }
    }

    /// <summary>
    /// The size of the bounding volume as the difference between the maximum and minimum points.
    /// </summary>
    /// <value>Max - Min in single precision; invalid ordering can produce negative components.</value>
    public Vector3 Size
    {
        get
        {
            if (_isDirty)
                RecalculateMeta();
            return _size;
        }
    }

    /// <summary>
    /// The volume of the bounding box, calculated as the product of its dimensions.
    /// </summary>
    /// <value>The single-precision product of Size components, widened to double afterward; intermediate overflow
    /// can produce infinity.</value>
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

    /// <summary>
    /// Determines whether the current bounding volume intersects with another.
    /// </summary>
    /// <param name="other">The value to compare or combine, without mutating either input.</param>
    /// <returns>true unless at least one axis strictly separates the boxes; touching boundaries count as
    /// intersection.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Intersects(BoundVolume other)
    {
        return !(Min.X > other.Max.X || Max.X < other.Min.X ||
                 Min.Y > other.Max.Y || Max.Y < other.Min.Y ||
                 Min.Z > other.Max.Z || Max.Z < other.Min.Z);
    }

    /// <summary>
    /// Calculates the backend-defined increase metric used to choose BVH insertion branches.
    /// </summary>
    /// <param name="other">The value to compare or combine, without mutating either input.</param>
    /// <returns>The union-volume increase truncated toward zero to Int64; positive values at/above Int64.MaxValue
    /// clamp to that limit. This is a heuristic, not an exact geometric measure.</returns>
    /// <remarks>
    /// Volume products are computed in float before widening. Keep coordinates and products finite/ordered; NaN,
    /// infinity and invalid extents do not have a useful cost contract.
    /// </remarks>
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

    /// <summary>
    /// Compares the stored value components for equality.
    /// </summary>
    /// <param name="other">The value to compare or combine, without mutating either input.</param>
    /// <returns>true if the compared stored components are equal; false for null, other runtime types, or
    /// different components.</returns>
    public readonly bool Equals(BoundVolume other) => BoundsEquals(other);

    /// <summary>
    /// Compares the stored value components for equality.
    /// </summary>
    /// <param name="obj">The object to compare; null and other runtime types compare unequal.</param>
    /// <returns>true if the compared stored components are equal; false for null, other runtime types, or
    /// different components.</returns>
    public override readonly bool Equals(object? obj) => obj is BoundVolume other && BoundsEquals(other);

    /// <summary>
    /// Determines whether two BoundVolume instances are equal.
    /// </summary>
    /// <param name="left">The left value to compare.</param>
    /// <param name="right">The right value to compare.</param>
    /// <returns>true if the compared values are equal by their stored coordinate/option/endpoint components;
    /// otherwise false.</returns>
    public static bool operator ==(BoundVolume left, BoundVolume right) => left.Equals(right);

    /// <summary>
    /// Determines whether two BoundVolume instances are not equal.
    /// </summary>
    /// <param name="left">The left value to compare.</param>
    /// <param name="right">The right value to compare.</param>
    /// <returns>true if the compared values are unequal by their stored coordinate/option/endpoint components;
    /// otherwise false.</returns>
    public static bool operator !=(BoundVolume left, BoundVolume right) => !(left == right);

    /// <summary>
    /// Returns a hash code for the stored value components.
    /// </summary>
    /// <returns>An Int32 hash suitable for lookup; equal values produce equal hashes within the running
    /// process.</returns>
    /// <remarks>
    /// HashCode.Combine is suitable for in-process lookup, not a persistent or cross-process deterministic
    /// identifier.
    /// </remarks>
    public override readonly int GetHashCode() => HashCode.Combine(Min, Max);

    /// <summary>
    /// Returns a string that represents the current object, including the minimum and maximum values.
    /// </summary>
    /// <returns>A string in the format "Min: {Min}, Max: {Max}" that displays the minimum and maximum values of
    /// the object.</returns>
    public override readonly string ToString() => $"Min: {Min}, Max: {Max}";
}
