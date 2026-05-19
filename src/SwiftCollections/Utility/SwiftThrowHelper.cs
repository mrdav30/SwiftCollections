using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace SwiftCollections;

/// <summary>
/// Provides helper methods for throwing common exceptions in a consistent and efficient manner. 
/// These methods are intended to simplify argument and state validation throughout the codebase.
/// </summary>
/// <remarks>
/// This class centralizes exception throwing logic to improve code clarity and reduce repetitive validation code. 
/// Methods are typically inlined by the compiler to minimize performance overhead.
/// </remarks>
public static class SwiftThrowHelper
{
    #region Null Argument Validation

    /// <summary>
    /// Throws an ArgumentNullException if the provided argument is null.
    /// </summary>
    /// <param name="argument">The argument to check for null.</param>
    /// <param name="paramName">The name of the parameter that caused the exception.</param>
    /// <param name="message">An optional message to include in the exception.</param>
    /// <exception cref="ArgumentNullException">Thrown when the argument is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNull([NotNull] object? argument, string? paramName = null, string? message = null)
    {
        if (argument is null)
            ThrowArgumentNullException(paramName, message);
    }

    /// <summary>
    /// Throws an ArgumentNullException if the provided generic argument is null. 
    /// This method is useful for validating reference types in a generic context where the type parameter may not be constrained to non-nullable types.
    /// </summary>
    /// <typeparam name="T">The type of the argument to check.</typeparam>
    /// <param name="argument">The argument to check for null.</param>
    /// <param name="paramName">The name of the parameter that caused the exception.</param>
    /// <param name="message">An optional message to include in the exception.</param>
    /// <exception cref="ArgumentNullException">Thrown when the argument is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNullGeneric<T>([NotNull] T argument, string? paramName = null, string? message = null)
    {
        if (argument is null)
            ThrowArgumentNullException(paramName, message);
    }

    /// <summary>
    /// Throws an exception if the specified value is null and nulls are not allowed for TValue.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <param name="defaultValue">A default value of type TValue used to determine if nulls are illegal.</param>
    /// <param name="message">An optional message to include in the exception.</param>
    /// <exception cref="ArgumentNullException">The value is null and TValue is a value type.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNullAndNullsAreIllegal<TValue>(object? value, TValue? defaultValue, string? message = null)
    {
        if (value == null && !(defaultValue == null))
            ThrowArgumentNullException(nameof(value), message);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowArgumentNullException(string? paramName, string? message = null) =>
        throw new ArgumentNullException(paramName, message);

    #endregion

    #region Out of Range Validation

    /// <summary>
    /// Throws an ArgumentOutOfRangeException if the specified value is negative, indicating that the argument must be a non-negative integer.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <param name="paramName">The name of the parameter that caused the exception.</param>
    /// <param name="message">An optional message to include in the exception.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is negative.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNegative(int value, string? paramName = null, string? message = null)
    {
        if (value < 0)
            ThrowArgumentOutOfRangeException(paramName, value, message ?? $"{paramName} must be non-negative.");
    }

    /// <summary>
    /// Throws an ArgumentOutOfRangeException if the specified value is negative or zero, indicating that the argument must be a positive integer.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <param name="paramName">The name of the parameter that caused the exception.</param>
    /// <param name="message">An optional message to include in the exception.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is negative or zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNegativeOrZero(int value, string? paramName = null, string? message = null)
    {
        if (value < 0 || value == 0)
            ThrowArgumentOutOfRangeException(paramName, value, message ?? $"{paramName} must be greater than zero.");
    }

    /// <summary>
    /// Throws an ArgumentOutOfRangeException if the specified condition is true.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="actualValue">The value that caused the exception.</param>
    /// <param name="paramName">The name of the parameter that caused the exception.</param>
    /// <param name="message">An optional message to include in the exception.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the condition is true.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfArgumentOutOfRange([DoesNotReturnIf(true)] bool condition, int actualValue, string? paramName = null, string? message = null)
    {
        if (condition)
            ThrowArgumentOutOfRangeException(paramName, actualValue, message);
    }

    /// <summary>
    /// Throws an ArgumentOutOfRangeException if a copy destination index is outside [0, length].
    /// </summary>
    /// <param name="index">The destination index to check.</param>
    /// <param name="length">The destination length.</param>
    /// <param name="paramName">The name of the parameter that caused the exception.</param>
    /// <param name="message">An optional message to include in the exception.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is outside [0, length].</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfArrayIndexInvalid(int index, int length, string? paramName = null, string? message = null)
    {
        if ((uint)index > (uint)length)
            ThrowArgumentOutOfRangeException(paramName ?? nameof(index), index, message ?? "Array index is out of range.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowArgumentOutOfRangeException(string? paramName, object? actualValue, string? message = null) =>
        throw new ArgumentOutOfRangeException(paramName, actualValue, message);

    #endregion

    #region Invalid State Validation

    /// <summary>
    /// Throws an ArgumentException if the specified condition is true.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="paramName">The name of the parameter that caused the exception.</param>
    /// <param name="message">An optional message to include in the exception.</param>
    /// <exception cref="ArgumentException">Thrown when the condition is true.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfArgument([DoesNotReturnIf(true)] bool condition, string? paramName = null, string? message = null)
    {
        if (condition)
            ThrowArgumentException(paramName, message);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowArgumentException(string? paramName, string? message = null) =>
        throw new ArgumentException(message, paramName);

    /// <summary>
    /// Throws an InvalidOperationException if the specified condition is true, indicating that the object is in an invalid state for the attempted operation.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="objectName">The name of the object in an invalid state.</param>
    /// <param name="message">An optional message to include in the exception.</param>
    /// <exception cref="InvalidOperationException">Thrown when the condition is true, indicating an invalid state.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfTrue([DoesNotReturnIf(true)] bool condition, string? objectName = null, string? message = null)
    {
        if (condition)
            ThrowInvalidOperationException(objectName, message);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowInvalidOperationException(string? objectName, string? message = null) =>
        throw new InvalidOperationException(message ?? $"Object '{objectName}' is in an invalid state.");

    /// <summary>
    /// Throws an ObjectDisposedException if the specified condition is true, indicating that the object has been disposed and can no longer be used for the attempted operation.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="objectName">The name of the object that has been disposed.</param>
    /// <param name="message">An optional message to include in the exception.</param>
    /// <exception cref="ObjectDisposedException">Thrown when the condition is true, indicating that the object has been disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfDisposed([DoesNotReturnIf(true)] bool condition, string? objectName = null, string? message = null)
    {
        if (condition)
            ThrowObjectDisposedException(objectName, message);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowObjectDisposedException(string? objectName, string? message = null) =>
        throw new ObjectDisposedException(objectName, message ?? $"Object '{objectName}' has been disposed.");

    /// <summary>
    /// Throws a KeyNotFoundException if the specified index is negative, indicating that the key is invalid for the current collection or context.
    /// </summary>
    /// <param name="index">The index to check.</param>
    /// <param name="key">The key associated with the index.</param>
    /// <param name="message">An optional message to include in the exception.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the index is negative, indicating an invalid key.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfKeyInvalid(int index, object? key = null, string? message = null)
    {
        if (index < 0)
            ThrowKeyNotFoundException(index, key, message);
    }

    /// <summary>
    /// Throws a KeyNotFoundException if the specified condition is true.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="key">The key associated with the lookup.</param>
    /// <param name="message">An optional message to include in the exception.</param>
    /// <exception cref="KeyNotFoundException">Thrown when the condition is true.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfKeyNotFound([DoesNotReturnIf(true)] bool condition, object? key = null, string? message = null)
    {
        if (condition)
            ThrowKeyNotFoundException(-1, key, message);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowKeyNotFoundException(int index, object? key, string? message = null) =>
        throw new KeyNotFoundException(message ?? $"Key not found: {key}");

    /// <summary>
    /// Throws an IndexOutOfRangeException if the specified index is outside the valid range defined by count, indicating that the index is invalid for the current collection or context.
    /// </summary>
    /// <param name="index">The index to check.</param>
    /// <param name="count">The total number of elements in the collection.</param>
    /// <param name="message">An optional message to include in the exception.</param>
    /// <exception cref="IndexOutOfRangeException">Thrown when the index is outside the valid range.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfListIndexInvalid(int index, int count, string? message = null)
    {
        if ((uint)index >= (uint)count)
            ThrowIndexOutOfRangeException(index, message);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowIndexOutOfRangeException(int value, string? message = null) =>
        throw new IndexOutOfRangeException(message ?? $"Index out of range: {value}");

    #endregion
}
