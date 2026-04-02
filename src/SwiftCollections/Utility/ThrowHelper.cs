using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace SwiftCollections;

public static class ThrowHelper
{
#nullable enable

    /// <inheritdoc cref="ArgumentNullException"/>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowIfNull([NotNull] object? argument, string? paramName = null)
    {
        if (argument is null)
            ThrowArgumentNullException(paramName);
    }

    /// <summary>
    /// Throws an exception if the specified value is null and nulls are not allowed for TValue.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <param name="defaultValue">A default value of type TValue used to determine if nulls are illegal.</param>
    /// <exception cref="ArgumentNullException">The value is null and TValue is a value type.</exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowIfNullAndNullsAreIllegal<TValue>(object value, TValue? defaultValue)
    {
        if (value == null && !(defaultValue == null))
            ThrowArgumentNullException(nameof(value));
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowArgumentNullException(string? paramName) =>
        throw new ArgumentNullException(paramName);

    /// <inheritdoc cref="ArgumentOutOfRangeException"/>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowIfNegative(int value, string? paramName = null)
    {
        if (value < 0)
            ThrowArgumentOutOfRangeException(value, paramName);
    }

    /// <inheritdoc cref="ArgumentOutOfRangeException"/>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowIfNegativeOrZero(int value, string? paramName = null)
    {
        if (value < 0 || value == 0)
            ThrowArgumentOutOfRangeException(value, paramName);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowArgumentOutOfRangeException(int value, string? paramName) =>
        throw new ArgumentOutOfRangeException(paramName, $"{paramName} must be greater than zero. Value: {value}");

    /// <inheritdoc cref="ObjectDisposedException"/>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowIfDisposed([DoesNotReturnIf(true)] bool condition, string? objectName = null)
    {
        if (condition)
            ThrowObjectDisposedException(objectName);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowObjectDisposedException(string? objectName) =>
        throw new ObjectDisposedException(objectName);
}
