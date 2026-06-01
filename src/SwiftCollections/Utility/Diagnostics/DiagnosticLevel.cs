//=======================================================================
// DiagnosticLevel.cs
//=======================================================================
// MIT License, Copyright (c) 2024–present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

namespace SwiftCollections.Diagnostics;

/// <summary>
/// Defines the severity level for a diagnostic event.
/// </summary>
public enum DiagnosticLevel : byte
{
    /// <summary>
    /// Diagnostics are disabled.
    /// </summary>
    None = 0,

    /// <summary>
    /// Informational diagnostic output.
    /// </summary>
    Info = 1,

    /// <summary>
    /// Warning diagnostic output.
    /// </summary>
    Warning = 2,

    /// <summary>
    /// Error diagnostic output.
    /// </summary>
    Error = 3
}
