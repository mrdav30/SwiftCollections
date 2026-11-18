//=======================================================================
// FixedBoundVolume.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Runtime.CompilerServices;
using FixedMathSharp;
using FixedMathSharp.Geometry;

namespace SwiftCollections.Query;

/// <summary>
/// Represents an axis-aligned bounding box (AABB) in 3D space using fixed-point math.
/// </summary>
/// <remarks>
/// Uses FixedMathSharp Q32.32 coordinates. Swapped endpoints normalize componentwise. Default is a zero-size box
/// at the origin; touching faces/edges/points intersect. Center/size/volume results are value copies. Size can be
/// unrepresentable even when endpoint-based intersection/union/cost remain valid.
/// </remarks>
public struct FixedBoundVolume : IBoundVolume<FixedBoundVolume>, IEquatable<FixedBoundVolume>
{
    private FixedBoundBox _bounds;

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.Query.FixedBoundVolume" /> struct.
    /// </summary>
    /// <param name="min">One coordinate endpoint in caller units; endpoints are normalized componentwise.</param>
    /// <param name="max">The opposite coordinate endpoint in caller units; endpoints are normalized
    /// componentwise.</param>
    public FixedBoundVolume(Vector3d min, Vector3d max)
    {
        _bounds = FixedBoundBox.FromMinMax(min, max);
    }

    /// <summary>
    /// Gets the minimum point of the bounding volume.
    /// </summary>
    /// <value>The componentwise minimum coordinate in caller units.</value>
    public Vector3d Min => _bounds.Min;

    /// <summary>
    /// Gets the maximum point of the bounding volume.
    /// </summary>
    /// <value>The componentwise maximum coordinate in caller units.</value>
    public Vector3d Max => _bounds.Max;

    /// <summary>
    /// Gets the nearest-even Q32.32 center point of the bounding volume.
    /// </summary>
    /// <value>The nearest-even Q32.32 midpoint in caller units.</value>
    public Vector3d Center => _bounds.Center;

    /// <summary>
    /// Gets the exact axis-aligned size of the bounding volume.
    /// </summary>
    /// <value>The exact component spans in caller units; throws if a positive span is not representable.</value>
    /// <exception cref="T:System.OverflowException"> A positive component span is outside the representable
    /// scalar domain. </exception>
    public Vector3d Size => _bounds.Proportions;

    /// <summary>
    /// Gets the volume of the bounding box.
    /// </summary>
    /// <value>The cubic-unit result of sequential rounded, saturating Fixed64 multiplication of Size
    /// components.</value>
    /// <exception cref="T:System.OverflowException"> A positive component span is outside the representable
    /// scalar domain. </exception>
    /// <remarks>
    /// Size throws for unrepresentable positive spans. Representable spans can still produce a saturated product
    /// without throwing; intermediate saturation can lose information even if a later small factor would bring
    /// the exact volume back into range. Use GetCost for its separately widened insertion heuristic.
    /// </remarks>
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
    /// <param name="other">The value to compare or combine, without mutating either input.</param>
    /// <returns>A new value with componentwise minimum/maximum enclosing both boxes; neither input
    /// changes.</returns>
    /// <remarks>
    /// The resulting volume is the smallest axis-aligned bounding box that fully contains both input volumes.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedBoundVolume Union(FixedBoundVolume other)
    {
        return new FixedBoundVolume(Vector3d.Min(Min, other.Min), Vector3d.Max(Max, other.Max));
    }

    /// <summary>
    /// Determines whether this volume intersects with the specified volume.
    /// </summary>
    /// <param name="other">The value to compare or combine, without mutating either input.</param>
    /// <returns>true unless at least one axis strictly separates the boxes; touching boundaries count as
    /// intersection.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Intersects(FixedBoundVolume other)
    {
        return !(Min.X > other.Max.X || Max.X < other.Min.X ||
                 Min.Y > other.Max.Y || Max.Y < other.Min.Y ||
                 Min.Z > other.Max.Z || Max.Z < other.Min.Z);
    }

    /// <summary>
    /// Calculates the additional volume required to expand the current volume to fully contain the specified
    /// volume.
    /// </summary>
    /// <param name="other">The value to compare or combine, without mutating either input.</param>
    /// <returns>The additional union volume floored to integer cubic caller units and clamped to Int64.MaxValue;
    /// zero if already enclosed.</returns>
    /// <remarks>
    /// Uses exact widened endpoint arithmetic and can handle unrepresentable Size components without
    /// materializing them.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long GetCost(FixedBoundVolume other)
    {
        return _bounds.GetVolumeExpansionCost(other._bounds);
    }

    /// <summary>
    /// Determines whether the bounds of this volume are equal to those of the specified volume.
    /// </summary>
    /// <param name="other">The value to compare or combine, without mutating either input.</param>
    /// <returns>true if both endpoint vectors compare equal; cached derived metadata does not
    /// participate.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool BoundsEquals(FixedBoundVolume other)
    {
        return Min == other.Min && Max == other.Max;
    }

    /// <summary>
    /// Compares the stored value components for equality.
    /// </summary>
    /// <param name="other">The value to compare or combine, without mutating either input.</param>
    /// <returns>true if the compared stored components are equal; false for null, other runtime types, or
    /// different components.</returns>
    public bool Equals(FixedBoundVolume other) => BoundsEquals(other);

    /// <summary>
    /// Compares the stored value components for equality.
    /// </summary>
    /// <param name="obj">The object to compare; null and other runtime types compare unequal.</param>
    /// <returns>true if the compared stored components are equal; false for null, other runtime types, or
    /// different components.</returns>
    public override bool Equals(object obj) => obj is FixedBoundVolume other && BoundsEquals(other);

    /// <summary>
    /// Determines whether two BoundVolume instances are equal.
    /// </summary>
    /// <param name="left">The left value to compare.</param>
    /// <param name="right">The right value to compare.</param>
    /// <returns>true if the compared values are equal by their stored coordinate/option/endpoint components;
    /// otherwise false.</returns>
    public static bool operator ==(FixedBoundVolume left, FixedBoundVolume right) => left.Equals(right);

    /// <summary>
    /// Determines whether two BoundVolume instances are not equal.
    /// </summary>
    /// <param name="left">The left value to compare.</param>
    /// <param name="right">The right value to compare.</param>
    /// <returns>true if the compared values are unequal by their stored coordinate/option/endpoint components;
    /// otherwise false.</returns>
    public static bool operator !=(FixedBoundVolume left, FixedBoundVolume right) => !(left == right);

    /// <summary>
    /// Returns a hash code for the stored value components.
    /// </summary>
    /// <returns>An Int32 hash suitable for lookup; equal values produce equal hashes within the running
    /// process.</returns>
    /// <remarks>
    /// HashCode.Combine is suitable for in-process lookup, not a persistent or cross-process deterministic
    /// identifier.
    /// </remarks>
    public override int GetHashCode() => HashCode.Combine(Min, Max);

    /// <summary>
    /// Returns a string that represents the current object, including the minimum and maximum values.
    /// </summary>
    /// <returns>A string in the format "Min: {Min}, Max: {Max}" that displays the minimum and maximum values of
    /// the object.</returns>
    public override string ToString() => $"Min: {Min}, Max: {Max}";
}
