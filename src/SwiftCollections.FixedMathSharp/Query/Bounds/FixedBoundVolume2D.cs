//=======================================================================
// FixedBoundVolume2D.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Runtime.CompilerServices;
using FixedMathSharp;
using FixedMathSharp.Geometry;
using SwiftCollections.Utility;

namespace SwiftCollections.Query;

/// <summary>Represents an immutable axis-aligned rectangle using fixed-point coordinates.</summary>
/// <remarks>
/// Coordinates use caller-defined space and Q32.32 units. Endpoints normalize componentwise; default is a
/// zero-size origin rectangle. Touching edges and corners intersect. Endpoint-based operations and insertion
/// cost remain valid even when Size or Area is not representable. No supplied value is retained by reference.
/// </remarks>
public readonly struct FixedBoundVolume2D : IBoundVolume<FixedBoundVolume2D>, IEquatable<FixedBoundVolume2D>
{
    private readonly FixedBoundArea _bounds;

    /// <summary>Copies and normalizes the supplied rectangle endpoints.</summary>
    /// <param name="min">One endpoint in caller coordinate units.</param>
    /// <param name="max">The opposite endpoint; swapped components are normalized.</param>
    public FixedBoundVolume2D(Vector2d min, Vector2d max) => _bounds = FixedBoundArea.FromMinMax(min, max);

    private FixedBoundVolume2D(FixedBoundArea bounds) => _bounds = bounds;

    /// <summary>Gets the minimum corner.</summary>
    /// <value>A copy of the componentwise minimum in caller coordinate units.</value>
    public Vector2d Min => _bounds.Min;

    /// <summary>Gets the maximum corner.</summary>
    /// <value>A copy of the componentwise maximum in caller coordinate units.</value>
    public Vector2d Max => _bounds.Max;

    /// <summary>Gets the nearest-even Q32.32 midpoint.</summary>
    /// <value>The componentwise midpoint in caller coordinate units, valid across the full scalar domain.</value>
    public Vector2d Center => _bounds.Center;

    /// <summary>Gets the exact width and height.</summary>
    /// <value>The positive endpoint spans in caller coordinate units.</value>
    /// <exception cref="OverflowException">A component span is not representable by Fixed64.</exception>
    public Vector2d Size => _bounds.Size;

    /// <summary>Gets the area rounded to the nearest-even Q32.32 value.</summary>
    /// <value>The area in square caller units, computed independently of Size.</value>
    /// <exception cref="OverflowException">The rounded area is not representable by Fixed64.</exception>
    public Fixed64 Area => _bounds.Area;

    /// <summary>Creates the smallest rectangle enclosing both operands.</summary>
    /// <param name="other">The rectangle to enclose without mutating either operand.</param>
    /// <returns>A value enclosing both rectangles.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FixedBoundVolume2D Union(FixedBoundVolume2D other) => new(FixedBoundArea.Union(_bounds, other._bounds));

    /// <summary>Tests inclusive rectangle intersection.</summary>
    /// <param name="other">The rectangle to test without mutating it.</param>
    /// <returns>True for overlap or boundary contact; false when either axis separates the rectangles.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Intersects(FixedBoundVolume2D other) => _bounds.Intersects(other._bounds);

    /// <summary>Computes the additional area needed to enclose another rectangle.</summary>
    /// <param name="other">The rectangle to enclose without mutating either operand.</param>
    /// <returns>The exact union-area increase floored to integer square caller units and clamped to
    /// Int64.MaxValue. Containment or sub-unit growth can return zero.</returns>
    /// <remarks>Computes the difference before rounding, independently of checked Size and Area.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long GetCost(FixedBoundVolume2D other) => _bounds.GetAreaExpansionCost(other._bounds);

    /// <summary>Compares the stored endpoints.</summary>
    /// <param name="other">The rectangle to compare without mutating it.</param>
    /// <returns>True when both normalized endpoints are equal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool BoundsEquals(FixedBoundVolume2D other) => _bounds.Equals(other._bounds);

    /// <summary>Compares the stored endpoints.</summary>
    /// <param name="other">The rectangle to compare.</param>
    /// <returns>True when both normalized endpoints are equal.</returns>
    public bool Equals(FixedBoundVolume2D other) => BoundsEquals(other);

    /// <summary>Compares this rectangle with an object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns>True for a FixedBoundVolume2D with equal endpoints; false for null or another type.</returns>
    public override bool Equals(object obj) => obj is FixedBoundVolume2D other && Equals(other);

    /// <summary>Tests endpoint equality.</summary>
    /// <param name="left">The first rectangle.</param>
    /// <param name="right">The second rectangle.</param>
    /// <returns>True when both normalized endpoints are equal.</returns>
    public static bool operator ==(FixedBoundVolume2D left, FixedBoundVolume2D right) => left.Equals(right);

    /// <summary>Tests endpoint inequality.</summary>
    /// <param name="left">The first rectangle.</param>
    /// <param name="right">The second rectangle.</param>
    /// <returns>True when at least one normalized endpoint differs.</returns>
    public static bool operator !=(FixedBoundVolume2D left, FixedBoundVolume2D right) => !left.Equals(right);

    /// <summary>Computes a deterministic endpoint hash.</summary>
    /// <returns>An Int32 lookup hash; equal rectangles hash equally. It is not a unique persistent identity.</returns>
    public override int GetHashCode() => SwiftHashTools.CombineHashCodes(
        Min.X.GetHashCode(), Min.Y.GetHashCode(), Max.X.GetHashCode(), Max.Y.GetHashCode());

    /// <summary>Formats the endpoints for diagnostics.</summary>
    /// <returns>A string containing Min and Max using the vectors' diagnostic formatting.</returns>
    public override string ToString() => $"Min: {Min}, Max: {Max}";
}
