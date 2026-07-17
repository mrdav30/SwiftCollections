//=======================================================================
// SwiftThrowHelper.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

namespace SwiftCollections.Diagnostics;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

/// <summary>
/// Provides allocation-conscious guard helpers for throwing common exceptions.
/// </summary>
/// <remarks>
/// Validation methods keep the success path small and route exception creation through no-inline throw helpers.
/// Interpolated custom messages are condition-gated so formatted expressions are not evaluated unless the guard throws.
/// </remarks>
public static class SwiftThrowHelper
{
    private static class GenericNullability<T>
    {
        internal static readonly bool CanBeNull = default(T) is null;
    }

    #region Null Argument Validation

    /// <summary>
    /// Throws an <see cref="ArgumentNullException"/> if the provided argument is null.
    /// </summary>
    /// <param name="argument">The argument to check for null.</param>
    /// <param name="paramName">The name of the parameter that caused the exception.</param>
    /// <exception cref="ArgumentNullException">Thrown when the argument is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNull(
        [NotNull] object? argument,
        [CallerArgumentExpression(nameof(argument))] string? paramName = null)
    {
        if (argument is null)
            ThrowArgumentNullException(paramName);
    }

    /// <summary>
    /// Throws an <see cref="ArgumentNullException"/> if the provided generic argument is null.
    /// </summary>
    /// <typeparam name="T">The type of the argument to check.</typeparam>
    /// <param name="argument">The argument to check for null.</param>
    /// <param name="paramName">The name of the parameter that caused the exception.</param>
    /// <exception cref="ArgumentNullException">Thrown when the argument is null.</exception>
#pragma warning disable CS8777 // Cached generic nullability avoids boxing non-nullable value types.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNullGeneric<T>(
        [NotNull] T argument,
        [CallerArgumentExpression(nameof(argument))] string? paramName = null)
    {
        if (GenericNullability<T>.CanBeNull && argument is null)
            ThrowArgumentNullException(paramName);
    }
#pragma warning restore CS8777

    /// <summary>
    /// Throws an <see cref="ArgumentNullException"/> if the specified value is null and nulls are not legal for <typeparamref name="TValue"/>.
    /// </summary>
    /// <typeparam name="TValue">The value type used to determine whether null is legal.</typeparam>
    /// <param name="value">The value to check.</param>
    /// <param name="defaultValue">A default value of type <typeparamref name="TValue"/> used to determine if nulls are illegal.</param>
    /// <param name="paramName">The name of the parameter that caused the exception.</param>
    /// <exception cref="ArgumentNullException">Thrown when the value is null and nulls are illegal.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNullAndNullsAreIllegal<TValue>(
        object? value,
        TValue? defaultValue,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value is null && defaultValue is not null)
            ThrowArgumentNullException(paramName);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowArgumentNullException(string? paramName)
    {
        paramName = NormalizeParamName(paramName);

        if (string.IsNullOrEmpty(paramName))
            throw new ArgumentNullException(null, "Value cannot be null.");

        throw new ArgumentNullException(paramName);
    }

    #endregion

    #region Out of Range Validation

    /// <summary>
    /// Throws an <see cref="ArgumentOutOfRangeException"/> if the specified value is negative.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <param name="paramName">The name of the parameter that caused the exception.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is negative.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNegative(
        int value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value < 0)
        {
            paramName = NormalizeParamName(paramName);
            ThrowArgumentOutOfRangeException(paramName, value, GetNonNegativeMessage(paramName));
        }
    }

    /// <summary>
    /// Throws an <see cref="ArgumentOutOfRangeException"/> if the specified value is negative or zero.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <param name="paramName">The name of the parameter that caused the exception.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is negative or zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNegativeOrZero(
        int value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value <= 0)
        {
            paramName = NormalizeParamName(paramName);
            ThrowArgumentOutOfRangeException(paramName, value, GetPositiveMessage(paramName));
        }
    }

    /// <summary>
    /// Throws an <see cref="ArgumentOutOfRangeException"/> if the specified condition is true.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="actualValue">The value that caused the exception.</param>
    /// <param name="paramName">The name of the parameter that caused the exception.</param>
    /// <param name="message">An optional message to include in the exception.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the condition is true.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfArgumentOutOfRange(
        [DoesNotReturnIf(true)] bool condition,
        int? actualValue,
        [CallerArgumentExpression(nameof(actualValue))] string? paramName = null,
        string? message = null)
    {
        if (condition)
        {
            paramName = NormalizeParamName(paramName);
            ThrowArgumentOutOfRangeException(paramName, actualValue, message ?? GetArgumentOutOfRangeMessage(paramName));
        }
    }

    /// <summary>
    /// Throws an <see cref="ArgumentOutOfRangeException"/> with a lazily formatted message if the specified condition is true.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="actualValue">The value that caused the exception.</param>
    /// <param name="message">The interpolated exception message.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the condition is true.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfArgumentOutOfRange(
        [DoesNotReturnIf(true)] bool condition,
        int? actualValue,
        [InterpolatedStringHandlerArgument(nameof(condition))] SwiftThrowInterpolatedStringHandler message)
    {
        if (condition)
            ThrowArgumentOutOfRangeException(null, actualValue, message.GetFormattedText());
    }

    /// <summary>
    /// Throws an <see cref="ArgumentOutOfRangeException"/> with a lazily formatted message if the specified condition is true.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="actualValue">The value that caused the exception.</param>
    /// <param name="paramName">The name of the parameter that caused the exception.</param>
    /// <param name="message">The interpolated exception message.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the condition is true.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfArgumentOutOfRange(
        [DoesNotReturnIf(true)] bool condition,
        int? actualValue,
        string? paramName,
        [InterpolatedStringHandlerArgument(nameof(condition))] SwiftThrowInterpolatedStringHandler message)
    {
        if (condition)
        {
            paramName = NormalizeParamName(paramName);
            ThrowArgumentOutOfRangeException(paramName, actualValue, message.GetFormattedText());
        }
    }

    /// <summary>
    /// Throws an <see cref="ArgumentOutOfRangeException"/> if a copy destination index is outside [0, length].
    /// </summary>
    /// <param name="index">The destination index to check.</param>
    /// <param name="length">The destination length.</param>
    /// <param name="paramName">The name of the parameter that caused the exception.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is outside [0, length].</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfArrayIndexInvalid(
        int index,
        int length,
        [CallerArgumentExpression(nameof(index))] string? paramName = null)
    {
        if ((uint)index > (uint)length)
        {
            paramName = NormalizeParamName(paramName);
            ThrowArgumentOutOfRangeException(paramName, index, "Array index is out of range.");
        }
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowArgumentOutOfRangeException(string? paramName, object? actualValue, string message) =>
        throw new ArgumentOutOfRangeException(paramName, actualValue, message);

    #endregion

    #region Invalid State Validation

    /// <summary>
    /// Throws an <see cref="ArgumentException"/> if the specified condition is true.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="paramName">The name of the parameter that caused the exception.</param>
    /// <param name="message">An optional message to include in the exception.</param>
    /// <exception cref="ArgumentException">Thrown when the condition is true.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfArgument(
        [DoesNotReturnIf(true)] bool condition,
        string? paramName = null,
        string? message = null)
    {
        if (condition)
        {
            paramName = NormalizeParamName(paramName);
            ThrowArgumentException(paramName, message ?? GetArgumentMessage(paramName));
        }
    }

    /// <summary>
    /// Throws an <see cref="ArgumentException"/> with a lazily formatted message if the specified condition is true.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="message">The interpolated exception message.</param>
    /// <exception cref="ArgumentException">Thrown when the condition is true.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfArgument(
        [DoesNotReturnIf(true)] bool condition,
        [InterpolatedStringHandlerArgument(nameof(condition))] SwiftThrowInterpolatedStringHandler message)
    {
        if (condition)
            ThrowArgumentException(null, message.GetFormattedText());
    }

    /// <summary>
    /// Throws an <see cref="ArgumentException"/> with a lazily formatted message if the specified condition is true.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="paramName">The name of the parameter that caused the exception.</param>
    /// <param name="message">The interpolated exception message.</param>
    /// <exception cref="ArgumentException">Thrown when the condition is true.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfArgument(
        [DoesNotReturnIf(true)] bool condition,
        string? paramName,
        [InterpolatedStringHandlerArgument(nameof(condition))] SwiftThrowInterpolatedStringHandler message)
    {
        if (condition)
        {
            paramName = NormalizeParamName(paramName);
            ThrowArgumentException(paramName, message.GetFormattedText());
        }
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowArgumentException(string? paramName, string message) =>
        throw new ArgumentException(message, paramName);

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> if the specified condition is true.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="objectName">The name of the object in an invalid state.</param>
    /// <param name="message">An optional message to include in the exception.</param>
    /// <exception cref="InvalidOperationException">Thrown when the condition is true.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfTrue(
        [DoesNotReturnIf(true)] bool condition,
        string? objectName = null,
        string? message = null)
    {
        if (condition)
            ThrowInvalidOperationException(message ?? GetInvalidOperationMessage(objectName));
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> with a lazily formatted message if the specified condition is true.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="message">The interpolated exception message.</param>
    /// <exception cref="InvalidOperationException">Thrown when the condition is true.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfTrue(
        [DoesNotReturnIf(true)] bool condition,
        [InterpolatedStringHandlerArgument(nameof(condition))] SwiftThrowInterpolatedStringHandler message)
    {
        if (condition)
            ThrowInvalidOperationException(message.GetFormattedText());
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> with a lazily formatted message if the specified condition is true.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="objectName">The name of the object in an invalid state.</param>
    /// <param name="message">The interpolated exception message.</param>
    /// <exception cref="InvalidOperationException">Thrown when the condition is true.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfTrue(
        [DoesNotReturnIf(true)] bool condition,
        string? objectName,
        [InterpolatedStringHandlerArgument(nameof(condition))] SwiftThrowInterpolatedStringHandler message)
    {
        if (condition)
            ThrowInvalidOperationException(message.GetFormattedText());
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowInvalidOperationException(string message) =>
        throw new InvalidOperationException(message);

    /// <summary>
    /// Throws an <see cref="ObjectDisposedException"/> if the specified condition is true.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="objectName">The name of the object that has been disposed.</param>
    /// <param name="message">An optional message to include in the exception.</param>
    /// <exception cref="ObjectDisposedException">Thrown when the condition is true.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfDisposed(
        [DoesNotReturnIf(true)] bool condition,
        string? objectName = null,
        string? message = null)
    {
        if (condition)
            ThrowObjectDisposedException(objectName, message ?? GetObjectDisposedMessage(objectName));
    }

    /// <summary>
    /// Throws an <see cref="ObjectDisposedException"/> with a lazily formatted message if the specified condition is true.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="message">The interpolated exception message.</param>
    /// <exception cref="ObjectDisposedException">Thrown when the condition is true.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfDisposed(
        [DoesNotReturnIf(true)] bool condition,
        [InterpolatedStringHandlerArgument(nameof(condition))] SwiftThrowInterpolatedStringHandler message)
    {
        if (condition)
            ThrowObjectDisposedException(null, message.GetFormattedText());
    }

    /// <summary>
    /// Throws an <see cref="ObjectDisposedException"/> with a lazily formatted message if the specified condition is true.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="objectName">The name of the object that has been disposed.</param>
    /// <param name="message">The interpolated exception message.</param>
    /// <exception cref="ObjectDisposedException">Thrown when the condition is true.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfDisposed(
        [DoesNotReturnIf(true)] bool condition,
        string? objectName,
        [InterpolatedStringHandlerArgument(nameof(condition))] SwiftThrowInterpolatedStringHandler message)
    {
        if (condition)
            ThrowObjectDisposedException(objectName, message.GetFormattedText());
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowObjectDisposedException(string? objectName, string message) =>
        throw new ObjectDisposedException(objectName, message);

    /// <summary>
    /// Throws a <see cref="KeyNotFoundException"/> if the specified index is negative.
    /// </summary>
    /// <param name="index">The index to check.</param>
    /// <param name="key">The key associated with the index.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the index is negative.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfKeyInvalid(int index, object? key = null)
    {
        if (index < 0)
            ThrowKeyNotFoundException(GetKeyNotFoundMessage(key));
    }

    /// <summary>
    /// Throws a <see cref="KeyNotFoundException"/> if the specified condition is true.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="key">The key associated with the lookup.</param>
    /// <param name="message">An optional message to include in the exception.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the condition is true.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfKeyNotFound(
        [DoesNotReturnIf(true)] bool condition,
        object? key = null,
        string? message = null)
    {
        if (condition)
            ThrowKeyNotFoundException(message ?? GetKeyNotFoundMessage(key));
    }

    /// <summary>
    /// Throws a <see cref="KeyNotFoundException"/> with a lazily formatted message if the specified condition is true.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="message">The interpolated exception message.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the condition is true.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfKeyNotFound(
        [DoesNotReturnIf(true)] bool condition,
        [InterpolatedStringHandlerArgument(nameof(condition))] SwiftThrowInterpolatedStringHandler message)
    {
        if (condition)
            ThrowKeyNotFoundException(message.GetFormattedText());
    }

    /// <summary>
    /// Throws a <see cref="KeyNotFoundException"/> with a lazily formatted message if the specified condition is true.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="key">The key associated with the lookup.</param>
    /// <param name="message">The interpolated exception message.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the condition is true.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfKeyNotFound(
        [DoesNotReturnIf(true)] bool condition,
        object? key,
        [InterpolatedStringHandlerArgument(nameof(condition))] SwiftThrowInterpolatedStringHandler message)
    {
        if (condition)
            ThrowKeyNotFoundException(message.GetFormattedText());
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowKeyNotFoundException(string message) =>
        throw new KeyNotFoundException(message);

    /// <summary>
    /// Throws an <see cref="IndexOutOfRangeException"/> if the specified index is outside the valid range defined by count.
    /// </summary>
    /// <param name="index">The index to check.</param>
    /// <param name="count">The total number of elements in the collection.</param>
    /// <exception cref="IndexOutOfRangeException">Thrown when the index is outside the valid range.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfListIndexInvalid(
        int index,
        int count)
    {
        if ((uint)index >= (uint)count)
            ThrowIndexOutOfRangeException(index);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowIndexOutOfRangeException(int value) =>
        throw new IndexOutOfRangeException($"Index out of range: {value}");

    #endregion

    #region Message Helpers

    private static string GetNonNegativeMessage(string? paramName) =>
        string.IsNullOrEmpty(paramName)
            ? "Value must be non-negative."
            : $"{paramName} must be non-negative.";

    private static string GetPositiveMessage(string? paramName) =>
        string.IsNullOrEmpty(paramName)
            ? "Value must be greater than zero."
            : $"{paramName} must be greater than zero.";

    private static string GetArgumentOutOfRangeMessage(string? paramName) =>
        string.IsNullOrEmpty(paramName)
            ? "Specified argument was out of range."
            : $"{paramName} is out of range.";

    private static string GetArgumentMessage(string? paramName) =>
        string.IsNullOrEmpty(paramName)
            ? "The argument is invalid."
            : $"{paramName} is invalid.";

    private static string GetInvalidOperationMessage(string? objectName) =>
        string.IsNullOrEmpty(objectName)
            ? "Operation is not valid in the current state."
            : $"Object '{objectName}' is in an invalid state.";

    private static string GetObjectDisposedMessage(string? objectName) =>
        string.IsNullOrEmpty(objectName)
            ? "Object has been disposed."
            : $"Object '{objectName}' has been disposed.";

    private static string GetKeyNotFoundMessage(object? key) =>
        key is null
            ? "Key was not found."
            : $"Key not found: {key}";

    private static string? NormalizeParamName(string? paramName)
    {
        if (string.IsNullOrEmpty(paramName) || paramName == "null")
            return null;

        char first = paramName[0];
        return char.IsLetter(first) || first == '_' || first == '@'
            ? paramName
            : null;
    }

    #endregion
}
