//=======================================================================
// ISubdivisionPartitioner.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

namespace SwiftCollections.Query;

/// <summary>
/// Defines the dimensional subdivision and diagnostic operations used by the shared tree engine.
/// </summary>
internal interface ISubdivisionPartitioner<TVolume>
    where TVolume : struct, IBoundVolume<TVolume>
{
    int ChildCount { get; }
    string DiagnosticSource { get; }
    string WorldBoundsError { get; }
    bool ContainsBounds(TVolume outer, TVolume inner);
    bool CanSubdivide(TVolume bounds);
    bool TryGetContainingChildIndex(TVolume nodeBounds, TVolume entryBounds, out int childIndex);
    TVolume CreateChildBounds(TVolume parentBounds, int childIndex);
}
