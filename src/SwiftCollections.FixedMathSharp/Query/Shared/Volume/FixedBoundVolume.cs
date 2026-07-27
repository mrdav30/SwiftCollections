//=======================================================================
// FixedBoundVolume.cs
//=======================================================================
// MIT License, Copyright (c) 2024–present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using FixedMathSharp;
using FixedMathSharp.Geometry;
using System;
using System.Runtime.CompilerServices;

namespace SwiftCollections.Query;

/// <summary>
/// Represents an axis-aligned bounding box (AABB) in 3D space using fixed-point math.
/// </summary>
/// <remarks>
/// Construction normalizes swapped endpoints. Derived center and size semantics
/// are inherited from <see cref="FixedBoundBox"/>.
/// </remarks>
public struct FixedBoundVolume : IBoundVolume<FixedBoundVolume>, IEquatable<FixedBoundVolume>
{
    private FixedBoundBox _bounds;

    /// <summary>
    /// Initializes a new instance of the <see cref="FixedBoundVolume"/> struct.
    /// </summary>
    /// <param name="min">One endpoint of the volume.</param>
    /// <param name="max">The opposite endpoint of the volume.</param>
    public FixedBoundVolume(Vector3d min, Vector3d max)
    {
        _bounds = FixedBoundBox.FromMinMax(min, max);
    }

    /// <summary>
    /// Gets the minimum point of the bounding volume.
    /// </summary>
    public Vector3d Min => _bounds.Min;

    /// <summary>
    /// Gets the maximum point of the bounding volume.
    /// </summary>
    public Vector3d Max => _bounds.Max;

    /// <summary>
    /// Gets the nearest-even Q32.32 center point of the bounding volume.
    /// </summary>
    public Vector3d Center => _bounds.Center;

    /// <summary>
    /// Gets the exact axis-aligned size of the bounding volume.
    /// </summary>
    /// <exception cref="OverflowException">
    /// A positive component span is outside the representable scalar domain.
    /// </exception>
    public Vector3d Size => _bounds.Proportions;

    /// <summary>
    /// Gets the volume of the bounding box.
    /// </summary>
    /// <exception cref="OverflowException">
    /// A positive component span is outside the representable scalar domain.
    /// </exception>
    public Fixed64 Volume
    {
        get
        {
            Vector3d size = Size;
            return size.X * size.Y * size.Z;
        }
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
    public FixedBoundVolume Union(FixedBoundVolume other)
    {
        return new FixedBoundVolume(Vector3d.Min(Min, other.Min), Vector3d.Max(Max, other.Max));
    }

    /// <summary>
    /// Determines whether this volume intersects with the specified volume.
    /// </summary>
    /// <param name="other">The volume to test for intersection with this volume.</param>
    /// <returns>true if the volumes intersect; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Intersects(FixedBoundVolume other)
    {
        return !(Min.X > other.Max.X || Max.X < other.Min.X ||
                 Min.Y > other.Max.Y || Max.Y < other.Min.Y ||
                 Min.Z > other.Max.Z || Max.Z < other.Min.Z);
    }

    /// <summary>
    /// Calculates the additional volume required to expand the current volume to fully contain the specified volume.
    /// </summary>
    /// <param name="other">The volume to be encompassed by the current volume.</param>
    /// <returns>
    /// The floored additional volume needed to contain the specified volume,
    /// clamped to <see cref="long.MaxValue"/>. Returns 0 when the current volume
    /// already contains the specified volume.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long GetCost(FixedBoundVolume other)
    {
        return _bounds.GetVolumeExpansionCost(other._bounds);
    }

    /// <summary>
    /// Determines whether the bounds of this volume are equal to those of the specified volume.
    /// </summary>
    /// <param name="other">A <see cref="FixedBoundVolume"/> to compare with the current volume.</param>
    /// <returns>true if both the minimum and maximum bounds of the volumes are equal; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool BoundsEquals(FixedBoundVolume other)
    {
        return Min == other.Min && Max == other.Max;
    }

    /// <inheritdoc/>
    public bool Equals(FixedBoundVolume other) => BoundsEquals(other);

    /// <inheritdoc/>
    public override bool Equals(object obj) => obj is FixedBoundVolume other && BoundsEquals(other);

    /// <summary>
    /// Determines whether two BoundVolume instances are equal.
    /// </summary>
    public static bool operator ==(FixedBoundVolume left, FixedBoundVolume right) => left.Equals(right);

    /// <summary>
    /// Determines whether two BoundVolume instances are not equal.
    /// </summary>
    public static bool operator !=(FixedBoundVolume left, FixedBoundVolume right) => !(left == right);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Min, Max);

    /// <summary>
    /// Returns a string that represents the current object, including the minimum and maximum values.
    /// </summary>
    /// <returns>A string in the format "Min: {Min}, Max: {Max}" that displays the minimum and maximum values of the object.</returns>
    public override string ToString() => $"Min: {Min}, Max: {Max}";
}
