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
/// Creates <see cref="FixedBoundVolume"/> instances from FixedMathSharp bounds types.
/// </summary>
public static class FixedBoundVolumeFactory
{
    /// <summary>
    /// Creates a fixed query volume from a FixedMathSharp bounding box.
    /// </summary>
    /// <param name="bounds">The source bounding box.</param>
    /// <returns>A query volume using the source bounds minimum and maximum corners.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedBoundVolume Create(FixedBoundBox bounds) => new(bounds.Min, bounds.Max);

    /// <summary>
    /// Creates a fixed query volume from a FixedMathSharp bounding sphere.
    /// </summary>
    /// <param name="bounds">The source bounding sphere.</param>
    /// <returns>A query volume using the sphere's enclosing minimum and maximum corners.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedBoundVolume Create(FixedBoundSphere bounds) => new(bounds.Min, bounds.Max);

    /// <summary>
    /// Creates a fixed query volume from a FixedMathSharp bounding frustum.
    /// </summary>
    /// <param name="bounds">The source bounding frustum.</param>
    /// <returns>A query volume using the frustum's enclosing minimum and maximum corners.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedBoundVolume Create(FixedBoundFrustum bounds) => new(bounds.Min, bounds.Max);
}
