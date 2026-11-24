//=======================================================================
// FixedBoundVolumeFactory.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System.Runtime.CompilerServices;
using FixedMathSharp.Geometry;

namespace SwiftCollections.Query;

/// <summary>
/// Creates <see cref="T:SwiftCollections.Query.FixedBoundVolume" /> instances from FixedMathSharp bounds types.
/// </summary>
public static class FixedBoundVolumeFactory
{
    /// <summary>
    /// Creates a fixed query volume from a FixedMathSharp bounding box.
    /// </summary>
    /// <param name="bounds">The volume in the same caller-defined coordinate space and units as other indexed
    /// bounds; copied by value. No transform is applied.</param>
    /// <returns>A query volume using the source bounds minimum and maximum corners.</returns>
    /// <remarks>
    /// Copies the source's axis-aligned Min/Max envelope in the same coordinate space. Sphere/frustum results are
    /// broad-phase boxes, not exact curved/frustum intersection predicates. Source state is not retained.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedBoundVolume Create(FixedBoundBox bounds) => new(bounds.Min, bounds.Max);

    /// <summary>
    /// Creates a fixed query volume from a FixedMathSharp bounding sphere.
    /// </summary>
    /// <param name="bounds">The volume in the same caller-defined coordinate space and units as other indexed
    /// bounds; copied by value. No transform is applied.</param>
    /// <returns>A query volume using the sphere's enclosing minimum and maximum corners.</returns>
    /// <remarks>
    /// Copies the source's axis-aligned Min/Max envelope in the same coordinate space. Sphere/frustum results are
    /// broad-phase boxes, not exact curved/frustum intersection predicates. Source state is not retained.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedBoundVolume Create(FixedBoundSphere bounds) => new(bounds.Min, bounds.Max);

    /// <summary>
    /// Creates a fixed query volume from a FixedMathSharp bounding frustum.
    /// </summary>
    /// <param name="bounds">The volume in the same caller-defined coordinate space and units as other indexed
    /// bounds; copied by value. No transform is applied.</param>
    /// <returns>A query volume using the frustum's enclosing minimum and maximum corners.</returns>
    /// <remarks>
    /// Copies the source's axis-aligned Min/Max envelope in the same coordinate space. Sphere/frustum results are
    /// broad-phase boxes, not exact curved/frustum intersection predicates. Source state is not retained.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedBoundVolume Create(FixedBoundFrustum bounds) => new(bounds.Min, bounds.Max);
}
