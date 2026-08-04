using System;
using SwiftCollections.Diagnostics;
using Xunit;

namespace SwiftCollections.Tests;

public class SwiftThrowHelperTests
{
    [Fact]
    public void ThrowIfNull_WithoutExplicitParamName_UsesCallerArgumentExpression()
    {
        object value = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            SwiftThrowHelper.ThrowIfNull(value));

        Assert.Equal(nameof(value), exception.ParamName);
    }

    [Fact]
    public void ThrowIfNullGeneric_PreservesReferenceAndNullableValueContracts()
    {
        string reference = null;
        int? nullableValue = null;

        Assert.Throws<ArgumentNullException>(() => SwiftThrowHelper.ThrowIfNullGeneric(reference));
        Assert.Throws<ArgumentNullException>(() => SwiftThrowHelper.ThrowIfNullGeneric(nullableValue));
        SwiftThrowHelper.ThrowIfNullGeneric(0);
    }

    [Fact]
    public void ThrowIfNullAndNullsAreIllegal_OnlyThrowsForNonNullableDefaults()
    {
        SwiftThrowHelper.ThrowIfNullAndNullsAreIllegal("value", default(int));
        SwiftThrowHelper.ThrowIfNullAndNullsAreIllegal(null, default(string));
        SwiftThrowHelper.ThrowIfNullAndNullsAreIllegal(null, default(int?));

        object value = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
            SwiftThrowHelper.ThrowIfNullAndNullsAreIllegal(value, default(int)));

        Assert.Equal(nameof(value), exception.ParamName);
    }

    [Fact]
    public void ThrowIfNegative_WithoutParamName_UsesGenericDefaultMessage()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            SwiftThrowHelper.ThrowIfNegative(-1));

        Assert.Contains("Value must be non-negative.", exception.Message);
        Assert.False(exception.Message.StartsWith(" must be non-negative.", StringComparison.Ordinal));
    }

    [Fact]
    public void ThrowIfTrue_WithoutObjectName_UsesGenericDefaultMessage()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            SwiftThrowHelper.ThrowIfTrue(true));

        Assert.Equal("Operation is not valid in the current state.", exception.Message);
    }

    [Fact]
    public void ThrowIfTrue_WithoutMessage_UsesObjectNameInInvalidOperationMessage()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            SwiftThrowHelper.ThrowIfTrue(true, "Pool"));

        Assert.Contains("Pool", exception.Message);
    }

    [Fact]
    public void ThrowIfArgument_WithInterpolatedMessage_DoesNotEvaluateMessageWhenConditionIsFalse()
    {
        int evaluations = 0;

        string SideEffect()
        {
            evaluations++;
            return "evaluated";
        }

        SwiftThrowHelper.ThrowIfArgument(false, nameof(evaluations), $"Hidden {SideEffect()}.");

        Assert.Equal(0, evaluations);
    }

    [Fact]
    public void ThrowIfArgument_WithInterpolatedMessage_EvaluatesMessageWhenConditionIsTrue()
    {
        int value = 7;

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            SwiftThrowHelper.ThrowIfArgument(true, nameof(value), $"Value {value} is invalid."));

        Assert.Contains("Value 7 is invalid.", exception.Message);
        Assert.Equal(nameof(value), exception.ParamName);
    }

    [Fact]
    public void ThrowIfArgumentOutOfRange_WithInterpolatedMessage_DoesNotEvaluateMessageWhenConditionIsFalse()
    {
        int evaluations = 0;

        string SideEffect()
        {
            evaluations++;
            return "evaluated";
        }

        SwiftThrowHelper.ThrowIfArgumentOutOfRange(false, 5, nameof(evaluations), $"Hidden {SideEffect()}.");

        Assert.Equal(0, evaluations);
    }
}
