//=======================================================================
// SwiftCollectionDiagnostics.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using Chronicler.Diagnostics;

namespace SwiftCollections.Diagnostics;

/// <summary>
/// Provides access to shared diagnostics for SwiftCollections.
/// </summary>
public static class SwiftCollectionDiagnostics
{
    /// <summary>
    /// Gets the shared SwiftCollections diagnostics channel.
    /// </summary>
    /// <value>The process-wide mutable diagnostic channel named SwiftCollections.</value>
    /// <remarks>
    /// Configuration affects all library consumers using this channel; synchronization and output behavior belong
    /// to Chronicler.Diagnostics.
    /// </remarks>
    public static DiagnosticChannel Shared { get; } = new DiagnosticChannel("SwiftCollections");
}
