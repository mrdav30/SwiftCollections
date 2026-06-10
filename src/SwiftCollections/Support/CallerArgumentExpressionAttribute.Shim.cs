#if NETSTANDARD2_1
namespace System.Runtime.CompilerServices;

using System;

/// <summary>
/// Provides the compiler-recognized caller argument expression marker for target frameworks that do not define it publicly.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class CallerArgumentExpressionAttribute : Attribute
{
    /// <summary>
    /// Initializes the attribute with the source parameter name.
    /// </summary>
    /// <param name="parameterName">The method parameter whose call-site expression should be captured.</param>
    public CallerArgumentExpressionAttribute(string parameterName)
    {
        ParameterName = parameterName;
    }

    /// <summary>
    /// Gets the method parameter whose call-site expression should be captured.
    /// </summary>
    public string ParameterName { get; }
}
#endif
