namespace SwiftCollections.Diagnostics;

using System.Runtime.CompilerServices;

/// <summary>
/// Receives diagnostic events emitted by a <see cref="DiagnosticChannel"/>.
/// </summary>
/// <param name="diagnostic">The emitted diagnostic event.</param>
public delegate void DiagnosticSink(in DiagnosticEvent diagnostic);

/// <summary>
/// Represents a named diagnostic channel with a configurable sink and minimum level.
/// </summary>
public sealed class DiagnosticChannel
{
    private static readonly DiagnosticSink NoopSink = static (in DiagnosticEvent _) => { };
    private DiagnosticSink _sink;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiagnosticChannel"/> class.
    /// </summary>
    /// <param name="name">The channel name. Blank names fall back to <c>Default</c>.</param>
    public DiagnosticChannel(string name)
    {
        Name = string.IsNullOrWhiteSpace(name) ? "Default" : name;
        _sink = NoopSink;
        MinimumLevel = DiagnosticLevel.None;
    }

    /// <summary>
    /// Gets the channel name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets or sets the minimum level required for diagnostics to be emitted.
    /// </summary>
    public DiagnosticLevel MinimumLevel { get; set; }

    /// <summary>
    /// Gets or sets the sink that receives emitted diagnostics.
    /// Assigning <c>null</c> restores the default no-op sink.
    /// </summary>
    public DiagnosticSink Sink
    {
        get => _sink;
        set => _sink = value ?? NoopSink;
    }

    /// <summary>
    /// Determines whether the specified level is enabled for this channel.
    /// </summary>
    /// <param name="level">The level to evaluate.</param>
    /// <returns><c>true</c> when the level is enabled; otherwise, <c>false</c>.</returns>
    public bool IsEnabled(DiagnosticLevel level)
    {
        return MinimumLevel != DiagnosticLevel.None
            && level >= MinimumLevel
            && level != DiagnosticLevel.None;
    }

    /// <summary>
    /// Emits a diagnostic event when the specified level is enabled.
    /// </summary>
    /// <param name="level">The severity level of the diagnostic event.</param>
    /// <param name="message">The diagnostic message.</param>
    /// <param name="source">An optional source identifier.</param>
    public void Write(DiagnosticLevel level, string message, string source = "")
    {
        if (!IsEnabled(level))
            return;

        _sink(new DiagnosticEvent(Name, level, message, source));
    }

    /// <summary>
    /// Emits a diagnostic event from an interpolated message when the specified level is enabled.
    /// Disabled levels do not evaluate formatted interpolation expressions.
    /// </summary>
    /// <param name="level">The severity level of the diagnostic event.</param>
    /// <param name="message">The interpolated diagnostic message.</param>
    /// <param name="source">An optional source identifier.</param>
    public void Write(
        DiagnosticLevel level,
        [InterpolatedStringHandlerArgument("", nameof(level))] DiagnosticInterpolatedStringHandler message,
        string source = "")
    {
        if (!message.IsEnabled)
            return;

        _sink(new DiagnosticEvent(Name, level, message.GetFormattedText(), source));
    }
}
