//=======================================================================
// SwiftThrowInterpolatedStringHandler.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

namespace SwiftCollections.Diagnostics;

using System;
using System.Runtime.CompilerServices;

/// <summary>
/// Builds exception messages only when the guarded throw condition is true.
/// </summary>
[InterpolatedStringHandler]
public ref struct SwiftThrowInterpolatedStringHandler
{
    private DiagnosticInterpolatedStringHandler _message;

    /// <summary>
    /// Initializes a new instance of the <see cref="SwiftThrowInterpolatedStringHandler"/> struct.
    /// </summary>
    /// <param name="literalLength">The combined length of literal portions in the interpolated string.</param>
    /// <param name="formattedCount">The number of formatted expressions in the interpolated string.</param>
    /// <param name="condition">The throw condition that controls whether formatting occurs.</param>
    /// <param name="isEnabled">Set to <see langword="true"/> when formatted expressions should be evaluated.</param>
    public SwiftThrowInterpolatedStringHandler(
        int literalLength,
        int formattedCount,
        bool condition,
        out bool isEnabled)
    {
        _message = new DiagnosticInterpolatedStringHandler(literalLength, formattedCount, condition, out isEnabled);
    }

    /// <summary>
    /// Gets whether this handler is actively building an exception message.
    /// </summary>
    public bool IsEnabled => _message.IsEnabled;

    internal string GetFormattedText() => _message.GetFormattedText();

    /// <summary>
    /// Appends a literal string segment.
    /// </summary>
    /// <param name="value">The literal string segment.</param>
    public void AppendLiteral(string value)
    {
        _message.AppendLiteral(value);
    }

    /// <summary>
    /// Appends a formatted value.
    /// </summary>
    /// <typeparam name="T">The type of value to append.</typeparam>
    /// <param name="value">The value to append.</param>
    public void AppendFormatted<T>(T value)
    {
        _message.AppendFormatted(value);
    }

    /// <summary>
    /// Appends a formatted value using the specified format string.
    /// </summary>
    /// <typeparam name="T">The type of value to append.</typeparam>
    /// <param name="value">The value to append.</param>
    /// <param name="format">The format string to apply.</param>
    public void AppendFormatted<T>(T value, string? format)
    {
        _message.AppendFormatted(value, format);
    }

    /// <summary>
    /// Appends a formatted value with the specified alignment.
    /// </summary>
    /// <typeparam name="T">The type of value to append.</typeparam>
    /// <param name="value">The value to append.</param>
    /// <param name="alignment">The minimum width for the formatted value.</param>
    public void AppendFormatted<T>(T value, int alignment)
    {
        _message.AppendFormatted(value, alignment);
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
        _message.AppendFormatted(value, alignment, format);
    }

    /// <summary>
    /// Appends a string value.
    /// </summary>
    /// <param name="value">The value to append.</param>
    public void AppendFormatted(string? value)
    {
        _message.AppendFormatted(value);
    }

    /// <summary>
    /// Appends a string value with the specified alignment.
    /// </summary>
    /// <param name="value">The value to append.</param>
    /// <param name="alignment">The minimum width for the formatted value.</param>
    public void AppendFormatted(string? value, int alignment)
    {
        _message.AppendFormatted(value, alignment);
    }

    /// <summary>
    /// Appends a string value with the specified alignment and format string.
    /// </summary>
    /// <param name="value">The value to append.</param>
    /// <param name="alignment">The minimum width for the formatted value.</param>
    /// <param name="format">The format string to apply.</param>
    public void AppendFormatted(string? value, int alignment, string? format)
    {
        _message.AppendFormatted(value, alignment, format);
    }

    /// <summary>
    /// Appends a character span.
    /// </summary>
    /// <param name="value">The span to append.</param>
    public void AppendFormatted(ReadOnlySpan<char> value)
    {
        _message.AppendFormatted(value);
    }

    /// <summary>
    /// Appends a character span with the specified alignment.
    /// </summary>
    /// <param name="value">The span to append.</param>
    /// <param name="alignment">The minimum width for the formatted value.</param>
    public void AppendFormatted(ReadOnlySpan<char> value, int alignment)
    {
        _message.AppendFormatted(value, alignment);
    }

    /// <summary>
    /// Appends a character span with the specified alignment and format string.
    /// </summary>
    /// <param name="value">The span to append.</param>
    /// <param name="alignment">The minimum width for the formatted value.</param>
    /// <param name="format">The format string to apply.</param>
    public void AppendFormatted(ReadOnlySpan<char> value, int alignment, string? format)
    {
        _message.AppendFormatted(value, alignment, format);
    }
}
