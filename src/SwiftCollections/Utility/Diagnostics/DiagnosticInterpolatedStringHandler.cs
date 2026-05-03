namespace SwiftCollections.Diagnostics;

using System;
using System.Runtime.CompilerServices;
using System.Text;

/// <summary>
/// Builds diagnostic messages for enabled diagnostic levels while allowing disabled levels
/// to skip formatted interpolation expression evaluation.
/// </summary>
[InterpolatedStringHandler]
public ref struct DiagnosticInterpolatedStringHandler
{
    private readonly bool _isEnabled;
#if NET6_0_OR_GREATER
    private DefaultInterpolatedStringHandler _builder;
#else
    private StringBuilder? _builder;
#endif

    /// <summary>
    /// Initializes a new instance of the <see cref="DiagnosticInterpolatedStringHandler"/> struct.
    /// </summary>
    /// <param name="literalLength">The combined length of literal portions in the interpolated string.</param>
    /// <param name="formattedCount">The number of formatted expressions in the interpolated string.</param>
    /// <param name="channel">The diagnostic channel that will receive the message.</param>
    /// <param name="level">The severity level being evaluated.</param>
    /// <param name="isEnabled">
    /// Set to <c>true</c> when formatted expressions should be evaluated; otherwise, <c>false</c>.
    /// </param>
    public DiagnosticInterpolatedStringHandler(
        int literalLength,
        int formattedCount,
        DiagnosticChannel channel,
        DiagnosticLevel level,
        out bool isEnabled)
    {
        isEnabled = channel != null && channel.IsEnabled(level);
        _isEnabled = isEnabled;
#if NET6_0_OR_GREATER
        _builder = isEnabled ? new DefaultInterpolatedStringHandler(literalLength, formattedCount) : default;
#else
        _ = formattedCount;
        _builder = isEnabled ? new StringBuilder(literalLength) : null;
#endif
    }

    /// <summary>
    /// Gets a value indicating whether this handler is actively building a diagnostic message.
    /// </summary>
    public bool IsEnabled => _isEnabled;

    /// <summary>
    /// Appends a literal string segment.
    /// </summary>
    /// <param name="value">The literal string segment.</param>
    public void AppendLiteral(string value)
    {
        if (!_isEnabled)
            return;

#if NET6_0_OR_GREATER
        _builder.AppendLiteral(value);
#else
        _builder!.Append(value);
#endif
    }

    /// <summary>
    /// Appends a formatted value.
    /// </summary>
    /// <typeparam name="T">The type of value to append.</typeparam>
    /// <param name="value">The value to append.</param>
    public void AppendFormatted<T>(T value)
    {
        if (!_isEnabled)
            return;

#if NET6_0_OR_GREATER
        _builder.AppendFormatted(value);
#else
        _builder!.Append(value);
#endif
    }

    /// <summary>
    /// Appends a formatted value using the specified format string.
    /// </summary>
    /// <typeparam name="T">The type of value to append.</typeparam>
    /// <param name="value">The value to append.</param>
    /// <param name="format">The format string to apply.</param>
    public void AppendFormatted<T>(T value, string? format)
    {
        if (!_isEnabled)
            return;

#if NET6_0_OR_GREATER
        _builder.AppendFormatted(value, format);
#else
        AppendFormattedValue(value, 0, format);
#endif
    }

    /// <summary>
    /// Appends a formatted value with the specified alignment.
    /// </summary>
    /// <typeparam name="T">The type of value to append.</typeparam>
    /// <param name="value">The value to append.</param>
    /// <param name="alignment">The minimum width for the formatted value.</param>
    public void AppendFormatted<T>(T value, int alignment)
    {
        if (!_isEnabled)
            return;

#if NET6_0_OR_GREATER
        _builder.AppendFormatted(value, alignment);
#else
        AppendFormattedValue(value, alignment, null);
#endif
    }

    /// <summary>
    /// Appends a formatted value with the specified alignment and format string.
    /// </summary>
    /// <typeparam name="T">The type of value to append.</typeparam>
    /// <param name="value">The value to append.</param>
    /// <param name="alignment">The minimum width for the formatted value.</param>
    /// <param name="format">The format string to apply.</param>
    public void AppendFormatted<T>(T value, int alignment, string? format)
    {
        if (!_isEnabled)
            return;

#if NET6_0_OR_GREATER
        _builder.AppendFormatted(value, alignment, format);
#else
        AppendFormattedValue(value, alignment, format);
#endif
    }

    /// <summary>
    /// Appends a string value.
    /// </summary>
    /// <param name="value">The value to append.</param>
    public void AppendFormatted(string? value)
    {
        if (!_isEnabled)
            return;

#if NET6_0_OR_GREATER
        _builder.AppendFormatted(value);
#else
        _builder!.Append(value);
#endif
    }

    /// <summary>
    /// Appends a string value with the specified alignment.
    /// </summary>
    /// <param name="value">The value to append.</param>
    /// <param name="alignment">The minimum width for the formatted value.</param>
    public void AppendFormatted(string? value, int alignment)
    {
        if (!_isEnabled)
            return;

#if NET6_0_OR_GREATER
        _builder.AppendFormatted(value, alignment);
#else
        AppendAligned(value, alignment);
#endif
    }

    /// <summary>
    /// Appends a string value with the specified alignment and format string.
    /// </summary>
    /// <param name="value">The value to append.</param>
    /// <param name="alignment">The minimum width for the formatted value.</param>
    /// <param name="format">The format string to apply.</param>
    public void AppendFormatted(string? value, int alignment, string? format)
    {
        if (!_isEnabled)
            return;

#if NET6_0_OR_GREATER
        _builder.AppendFormatted(value, alignment, format);
#else
        AppendAligned(value, alignment);
#endif
    }

    /// <summary>
    /// Appends a character span.
    /// </summary>
    /// <param name="value">The span to append.</param>
    public void AppendFormatted(ReadOnlySpan<char> value)
    {
        if (!_isEnabled)
            return;

#if NET6_0_OR_GREATER
        _builder.AppendFormatted(value);
#else
        _builder!.Append(value);
#endif
    }

    /// <summary>
    /// Appends a character span with the specified alignment.
    /// </summary>
    /// <param name="value">The span to append.</param>
    /// <param name="alignment">The minimum width for the formatted value.</param>
    public void AppendFormatted(ReadOnlySpan<char> value, int alignment)
    {
        if (!_isEnabled)
            return;

#if NET6_0_OR_GREATER
        _builder.AppendFormatted(value, alignment);
#else
        AppendAligned(value, alignment);
#endif
    }

    /// <summary>
    /// Appends a character span with the specified alignment and format string.
    /// </summary>
    /// <param name="value">The span to append.</param>
    /// <param name="alignment">The minimum width for the formatted value.</param>
    /// <param name="format">The format string to apply.</param>
    public void AppendFormatted(ReadOnlySpan<char> value, int alignment, string? format)
    {
        if (!_isEnabled)
            return;

#if NET6_0_OR_GREATER
        _builder.AppendFormatted(value, alignment, format);
#else
        AppendAligned(value, alignment);
#endif
    }

    internal string GetFormattedText()
    {
#if NET6_0_OR_GREATER
        return _builder.ToStringAndClear();
#else
        return _builder?.ToString() ?? string.Empty;
#endif
    }

#if !NET6_0_OR_GREATER
    private void AppendFormattedValue<T>(T value, int alignment, string? format)
    {
        string? text = value is IFormattable formattable
            ? formattable.ToString(format, null)
            : value?.ToString();

        AppendAligned(text, alignment);
    }

    private void AppendAligned(string? value, int alignment)
    {
        value ??= string.Empty;

        if (alignment == 0)
        {
            _builder!.Append(value);
            return;
        }

        int width = alignment < 0 ? -alignment : alignment;
        int padding = width - value.Length;

        if (padding <= 0)
        {
            _builder!.Append(value);
            return;
        }

        if (alignment > 0)
            _builder!.Append(' ', padding);

        _builder!.Append(value);

        if (alignment < 0)
            _builder!.Append(' ', padding);
    }

    private void AppendAligned(ReadOnlySpan<char> value, int alignment)
    {
        if (alignment == 0)
        {
            _builder!.Append(value);
            return;
        }

        int width = alignment < 0 ? -alignment : alignment;
        int padding = width - value.Length;

        if (padding <= 0)
        {
            _builder!.Append(value);
            return;
        }

        if (alignment > 0)
            _builder!.Append(' ', padding);

        _builder!.Append(value);

        if (alignment < 0)
            _builder!.Append(' ', padding);
    }
#endif
}
