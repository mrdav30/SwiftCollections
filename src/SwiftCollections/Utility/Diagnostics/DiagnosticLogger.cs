namespace SwiftCollections.Diagnostics;

using System;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Provides reusable logger state and diagnostic channels for libraries built on SwiftCollections diagnostics.
/// </summary>
/// <remarks>
/// Static logger facades can delegate to a derived instance of this type while keeping their existing public API.
/// </remarks>
public abstract class DiagnosticLogger
{
    private readonly DiagnosticChannel _channel;
    private readonly DiagnosticChannel _debugChannel;
    private Action<DiagnosticLevel, string, string> _logHandler;
    private Func<DiagnosticLevel, string, string, string> _customFormatter;
    private bool _enableDebugLogging;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiagnosticLogger"/> class.
    /// </summary>
    /// <param name="channelName">The diagnostic channel name.</param>
    protected DiagnosticLogger(string channelName)
    {
        _channel = CreateChannel(channelName);
        _debugChannel = CreateChannel(channelName);
        Name = _channel.Name;
        _logHandler = DefaultLogHandler;
        _customFormatter = DefaultLogFormatter;
        MinimumLevel = DiagnosticLevel.Warning;
        RefreshDebugMinimumLevel();
    }

    /// <summary>
    /// Gets the logger name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the diagnostic channel used for normal diagnostics.
    /// </summary>
    public DiagnosticChannel Channel => _channel;

    /// <summary>
    /// Gets the diagnostic channel used for verbose debug diagnostics.
    /// </summary>
    public DiagnosticChannel DebugChannel => _debugChannel;

    /// <summary>
    /// Gets or sets a value indicating whether verbose debug diagnostics should be emitted.
    /// </summary>
    public bool EnableDebugLogging
    {
        get => _enableDebugLogging;
        set
        {
            _enableDebugLogging = value;
            RefreshDebugMinimumLevel();
        }
    }

    /// <summary>
    /// Gets or sets the minimum severity required for normal diagnostics to be emitted.
    /// </summary>
    public DiagnosticLevel MinimumLevel
    {
        get => _channel.MinimumLevel;
        set
        {
            _channel.MinimumLevel = value;
            RefreshDebugMinimumLevel();
        }
    }

    /// <summary>
    /// Gets or sets the delegate used to write formatted log messages.
    /// Assigning <see langword="null"/> restores <see cref="DefaultLogHandler"/>.
    /// </summary>
    [AllowNull]
    public Action<DiagnosticLevel, string, string> LogHandler
    {
        get => _logHandler;
        set => _logHandler = value ?? DefaultLogHandler;
    }

    /// <summary>
    /// Gets or sets the formatter used to transform log arguments into a final log entry.
    /// Assigning <see langword="null"/> restores <see cref="DefaultLogFormatter"/>.
    /// </summary>
    [AllowNull]
    public Func<DiagnosticLevel, string, string, string> CustomFormatter
    {
        get => _customFormatter;
        set => _customFormatter = value ?? DefaultLogFormatter;
    }

    /// <summary>
    /// Determines whether normal diagnostics at the specified level are currently enabled.
    /// </summary>
    /// <param name="level">The diagnostic level to evaluate.</param>
    /// <returns><see langword="true"/> when messages at <paramref name="level"/> will be emitted; otherwise, <see langword="false"/>.</returns>
    public bool IsEnabled(DiagnosticLevel level)
    {
        return _channel.IsEnabled(level);
    }

    /// <summary>
    /// Writes a log message using the current <see cref="CustomFormatter"/>.
    /// </summary>
    /// <param name="level">The severity level of the log message.</param>
    /// <param name="message">The log message.</param>
    /// <param name="source">The source of the log message.</param>
    public virtual void DefaultLogHandler(DiagnosticLevel level, string message, string source)
    {
        string entry = CustomFormatter(level, message, source);
        if (level == DiagnosticLevel.Error)
            Console.Error.WriteLine(entry);
        else
            Console.WriteLine(entry);
    }

    /// <summary>
    /// Formats a log entry using a deterministic, source-first layout.
    /// </summary>
    /// <param name="level">The severity level of the log message.</param>
    /// <param name="message">The log message.</param>
    /// <param name="source">The source of the log message.</param>
    /// <returns>A formatted log entry.</returns>
    public virtual string DefaultLogFormatter(DiagnosticLevel level, string message, string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return $"[{level}] {Name}: {message}";

        return $"[{level}] {Name}.{source}: {message}";
    }

    /// <summary>
    /// Receives diagnostics emitted by either channel.
    /// </summary>
    /// <param name="diagnostic">The emitted diagnostic event.</param>
    protected virtual void HandleDiagnosticEvent(in DiagnosticEvent diagnostic)
    {
        _logHandler(diagnostic.Level, diagnostic.Message, ResolveSource(diagnostic));
    }

    /// <summary>
    /// Resolves the source value passed to <see cref="LogHandler"/>.
    /// </summary>
    /// <param name="diagnostic">The emitted diagnostic event.</param>
    /// <returns>The resolved source.</returns>
    protected virtual string ResolveSource(in DiagnosticEvent diagnostic)
    {
        return string.IsNullOrWhiteSpace(diagnostic.Source)
            ? diagnostic.Channel
            : diagnostic.Source;
    }

    private DiagnosticChannel CreateChannel(string channelName)
    {
        return new DiagnosticChannel(channelName)
        {
            MinimumLevel = DiagnosticLevel.Warning,
            Sink = HandleDiagnosticEvent
        };
    }

    private void RefreshDebugMinimumLevel()
    {
        _debugChannel.MinimumLevel = _enableDebugLogging
            ? _channel.MinimumLevel
            : DiagnosticLevel.None;
    }
}
