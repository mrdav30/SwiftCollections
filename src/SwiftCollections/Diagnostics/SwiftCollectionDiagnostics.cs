//=======================================================================
// SwiftCollectionDiagnostics.cs
//=======================================================================
// MIT License, Copyright (c) 2024–present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

namespace SwiftCollections.Diagnostics;

/// <summary>
/// Provides access to shared diagnostics for SwiftCollections.
/// </summary>
public static class SwiftCollectionDiagnostics
{
    /// <summary>
    /// Gets the shared SwiftCollections diagnostics channel.
    /// </summary>
    public static DiagnosticChannel Shared { get; } = new DiagnosticChannel("SwiftCollections");
}
