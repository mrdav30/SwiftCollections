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
