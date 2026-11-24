//=======================================================================
// BoundVolume2D.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using SwiftCollections.Utility;

namespace SwiftCollections.Query;

/// <summary>
/// Represents immutable axis-aligned bounds in two dimensions.
/// </summary>
/// <remarks>
/// Endpoints use single-precision coordinates in one caller-defined space. Construction requires finite,
/// ordered endpoints; default is a valid zero-size box at the origin. Boundary contact intersects.
/// Operations copy values and never mutate inputs. No cross-platform floating-point determinism is promised.
/// </remarks>
public readonly struct BoundVolume2D : IBoundVolume<BoundVolume2D>, IEquatable<BoundVolume2D>
{
    /// <summary>Creates bounds from finite, componentwise ordered endpoints.</summary>
    /// <param name="min">The minimum coordinate in caller units, copied by value.</param>
    /// <param name="max">The maximum coordinate in the same space, copied by value.</param>
    /// <exception cref="ArgumentOutOfRangeException">min or max contains a nonfinite component; min is checked first.</exception>
    /// <exception cref="ArgumentException">max is less than min on either axis. Endpoints are not normalized.</exception>
    public BoundVolume2D(Vector2 min, Vector2 max)
    {
        if (!float.IsFinite(min.X) || !float.IsFinite(min.Y))
            throw new ArgumentOutOfRangeException(nameof(min), "Minimum coordinates must be finite.");
        if (!float.IsFinite(max.X) || !float.IsFinite(max.Y))
            throw new ArgumentOutOfRangeException(nameof(max), "Maximum coordinates must be finite.");
        if (min.X > max.X || min.Y > max.Y)
            throw new ArgumentException("Maximum coordinates must be at least the minimum coordinates.", nameof(max));

        Min = min;
        Max = max;
    }

    /// <summary>Gets the minimum endpoint.</summary>
    /// <value>The stored minimum coordinate in caller units.</value>
    public Vector2 Min { get; }

    /// <summary>Gets the maximum endpoint.</summary>
    /// <value>The stored maximum coordinate in caller units.</value>
    public Vector2 Max { get; }

    /// <summary>Gets the midpoint without overflowing single-precision endpoint addition.</summary>
    /// <value>The midpoint computed in double precision and narrowed to Vector2 in caller units.</value>
    public Vector2 Center => new((float)(((double)Min.X + Max.X) * 0.5d), (float)(((double)Min.Y + Max.Y) * 0.5d));

    /// <summary>Gets the full span along each axis.</summary>
    /// <value>Max minus Min in caller units, narrowed to Vector2 after widened subtraction.</value>
    /// <exception cref="OverflowException">Either positive span exceeds float.MaxValue.</exception>
    public Vector2 Size
    {
        get
        {
            double width = (double)Max.X - Min.X;
            double height = (double)Max.Y - Min.Y;
            if (SpanExceedsMaximum(Min.X, Max.X) || SpanExceedsMaximum(Min.Y, Max.Y))
                throw new OverflowException("The bounds span cannot be represented by Vector2.");
            return new Vector2((float)width, (float)height);
        }
    }

    private static bool SpanExceedsMaximum(float min, float max)
    {
        // Subtract the larger magnitude first: near the limit this is exact even for a tiny cross-zero excess.
        double first = -(double)min;
        double second = max;
        return Math.Min(first, second) > (double)float.MaxValue - Math.Max(first, second);
    }

    /// <summary>Gets the area independently of whether Size is representable.</summary>
    /// <value>The product of widened endpoint spans in square caller units; zero for a degenerate axis.</value>
    public double Area => ((double)Max.X - Min.X) * ((double)Max.Y - Min.Y);

    /// <summary>Creates the smallest axis-aligned bounds enclosing both inputs.</summary>
    /// <param name="other">Bounds in the same coordinate space; neither input changes.</param>
    /// <returns>A new value enclosing this value and other.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BoundVolume2D Union(BoundVolume2D other) => new(Vector2.Min(Min, other.Min), Vector2.Max(Max, other.Max));

    /// <summary>Tests inclusive overlap in both axes.</summary>
    /// <param name="other">Bounds in the same coordinate space; neither input changes.</param>
    /// <returns>true when the bounds overlap or touch; false when an axis separates them.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Intersects(BoundVolume2D other) =>
        !(Min.X > other.Max.X || Max.X < other.Min.X || Min.Y > other.Max.Y || Max.Y < other.Min.Y);

    /// <summary>Calculates the area increase used by BVH insertion.</summary>
    /// <param name="other">Bounds in the same coordinate space; neither input changes.</param>
    /// <returns>The computed union-area increase floored to whole square caller units and clamped to long.MaxValue.</returns>
    /// <remarks>
    /// Uses widened endpoint spans without reading Size. Contained bounds and sub-unit growth can return zero.
    /// Double rounding can lose a small increase between large areas; this is a floating-point heuristic.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long GetCost(BoundVolume2D other)
    {
        double delta = Union(other).Area - Area;
        return delta >= (double)long.MaxValue ? long.MaxValue : (long)delta;
    }

    /// <summary>Compares both endpoints.</summary>
    /// <param name="other">The bounds to compare without mutating either input.</param>
    /// <returns>true when both endpoint vectors are equal; otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool BoundsEquals(BoundVolume2D other) => Min == other.Min && Max == other.Max;

    /// <summary>Compares both endpoints for equality.</summary>
    /// <param name="other">The bounds to compare.</param>
    /// <returns>true when both endpoint vectors are equal; otherwise false.</returns>
    public bool Equals(BoundVolume2D other) => BoundsEquals(other);

    /// <summary>Compares bounds with an object.</summary>
    /// <param name="obj">The object to compare; null and other types compare unequal.</param>
    /// <returns>true for equal BoundVolume2D endpoints; otherwise false.</returns>
    public override bool Equals(object? obj) => obj is BoundVolume2D other && Equals(other);

    /// <summary>Compares two bounds for equality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>true when both endpoints are equal; otherwise false.</returns>
    public static bool operator ==(BoundVolume2D left, BoundVolume2D right) => left.Equals(right);

    /// <summary>Compares two bounds for inequality.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>true when an endpoint differs; otherwise false.</returns>
    public static bool operator !=(BoundVolume2D left, BoundVolume2D right) => !left.Equals(right);

    /// <summary>Hashes the endpoint coordinates using deterministic integer mixing.</summary>
    /// <returns>An Int32 hash; equal bounds, including signed zero variants, have equal hashes.</returns>
    /// <remarks>The hash is not a unique or persistent identity.</remarks>
    public override int GetHashCode() => SwiftHashTools.CombineHashCodes(
        CoordinateHash(Min.X), CoordinateHash(Min.Y), CoordinateHash(Max.X), CoordinateHash(Max.Y));

    private static int CoordinateHash(float value) => value == 0f ? 0 : BitConverter.SingleToInt32Bits(value);

    /// <summary>Formats the minimum and maximum coordinates for diagnostics.</summary>
    /// <returns>A string in the format "Min: {Min}, Max: {Max}" using the current culture.</returns>
    public override string ToString() => $"Min: {Min}, Max: {Max}";
}
