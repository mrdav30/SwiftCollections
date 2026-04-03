namespace SwiftCollections.Diagnostics;

/// <summary>
/// Represents a single diagnostic event emitted by a <see cref="DiagnosticChannel"/>.
/// </summary>
public readonly struct DiagnosticEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DiagnosticEvent"/> struct.
    /// </summary>
    /// <param name="channel">The channel that emitted the event.</param>
    /// <param name="level">The severity level of the event.</param>
    /// <param name="message">The event message.</param>
    /// <param name="source">An optional source identifier.</param>
    public DiagnosticEvent(
        string channel,
        DiagnosticLevel level,
        string message,
        string source = "")
    {
        Channel = channel ?? string.Empty;
        Level = level;
        Message = message ?? string.Empty;
        Source = source ?? string.Empty;
    }

    /// <summary>
    /// Gets the channel that emitted the event.
    /// </summary>
    public string Channel { get; }

    /// <summary>
    /// Gets the severity level of the event.
    /// </summary>
    public DiagnosticLevel Level { get; }

    /// <summary>
    /// Gets the diagnostic message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the optional source identifier.
    /// </summary>
    public string Source { get; }
}
