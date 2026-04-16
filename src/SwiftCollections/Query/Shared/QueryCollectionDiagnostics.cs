using SwiftCollections.Diagnostics;

namespace SwiftCollections.Query;

internal static class QueryCollectionDiagnostics
{
    public static void WriteInfo(string source, string message)
    {
        SwiftCollectionDiagnostics.Shared.Write(DiagnosticLevel.Info, message, source);
    }

    public static void WriteWarning(string source, string message)
    {
        SwiftCollectionDiagnostics.Shared.Write(DiagnosticLevel.Warning, message, source);
    }

    public static void WriteError(string source, string message)
    {
        SwiftCollectionDiagnostics.Shared.Write(DiagnosticLevel.Error, message, source);
    }
}
