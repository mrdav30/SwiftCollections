using System;
using System.Collections;
using System.Runtime.Serialization;
using Xunit;

namespace SwiftCollections.Tests;

public class SwiftEqualityComparerTests
{
    [Fact]
    public void DeterministicStringComparer_UsesSeededBehaviorAndSerializationState()
    {
        var comparer = new SwiftDeterministicStringEqualityComparer(17);
        var sameSeed = new SwiftDeterministicStringEqualityComparer(17);
        var differentSeed = new SwiftDeterministicStringEqualityComparer(18);
        IEqualityComparer nonGeneric = comparer;

        Assert.True(comparer.Equals((string)null, (string)null));
        Assert.True(comparer.Equals("alpha", "alpha"));
        Assert.False(comparer.Equals("alpha", "beta"));
        Assert.False(comparer.Equals((string)null, "alpha"));
        Assert.True(nonGeneric.Equals(null, null));
        Assert.True(nonGeneric.Equals("alpha", "alpha"));
        Assert.True(nonGeneric.Equals(5, 5));
        Assert.False(nonGeneric.Equals(null, "alpha"));
        Assert.False(nonGeneric.Equals("alpha", 5));

        Assert.Equal(0, comparer.GetHashCode((string)null));
        Assert.Equal(0, nonGeneric.GetHashCode(null));
        Assert.Equal(comparer.GetHashCode("lockstep"), sameSeed.GetHashCode("lockstep"));
        Assert.Equal(nonGeneric.GetHashCode(5), nonGeneric.GetHashCode(5));
        Assert.True(comparer.Equals((object)sameSeed));
        Assert.False(comparer.Equals((object)differentSeed));
        Assert.False(comparer.Equals(new object()));
        Assert.NotEqual(comparer.GetHashCode(), differentSeed.GetHashCode());

        var info = CreateSerializationInfo(typeof(SwiftDeterministicStringEqualityComparer));
        comparer.GetObjectData(info, default);

        Assert.Equal(17, info.GetInt32("Seed"));
    }

    [Fact]
    public void DeterministicObjectComparer_UsesSeededBehaviorAndSerializationState()
    {
        var comparer = new SwiftDeterministicObjectEqualityComparer(31);
        var sameSeed = new SwiftDeterministicObjectEqualityComparer(31);
        var differentSeed = new SwiftDeterministicObjectEqualityComparer(41);
        IEqualityComparer nonGeneric = comparer;

        Assert.True(nonGeneric.Equals(null, null));
        Assert.True(nonGeneric.Equals("alpha", "alpha"));
        Assert.True(nonGeneric.Equals(5, 5));
        Assert.False(nonGeneric.Equals(null, "alpha"));
        Assert.False(nonGeneric.Equals("alpha", 5));

        Assert.Equal(0, comparer.GetHashCode(null));
        Assert.Equal(comparer.GetHashCode("lockstep"), sameSeed.GetHashCode("lockstep"));
        Assert.Equal(nonGeneric.GetHashCode(5), nonGeneric.GetHashCode(5));
        Assert.True(comparer.Equals((object)sameSeed));
        Assert.False(comparer.Equals((object)differentSeed));
        Assert.False(comparer.Equals(new object()));
        Assert.NotEqual(comparer.GetHashCode(), differentSeed.GetHashCode());

        var info = CreateSerializationInfo(typeof(SwiftDeterministicObjectEqualityComparer));
        comparer.GetObjectData(info, default);

        Assert.Equal(31, info.GetInt32("Seed"));
    }

    [Fact]
    public void RandomizedStringComparer_ComparesValuesAndSerializesEntropy()
    {
        var comparer = new SwiftStringEqualityComparer();
        IEqualityComparer nonGeneric = comparer;

        Assert.True(comparer.Equals((string)null, (string)null));
        Assert.True(comparer.Equals("alpha", "alpha"));
        Assert.False(comparer.Equals("alpha", "beta"));
        Assert.False(comparer.Equals((string)null, "alpha"));
        Assert.True(nonGeneric.Equals(null, null));
        Assert.True(nonGeneric.Equals("alpha", "alpha"));
        Assert.True(nonGeneric.Equals(5, 5));
        Assert.False(nonGeneric.Equals(null, "alpha"));
        Assert.False(nonGeneric.Equals("alpha", 5));

        Assert.Equal(0, comparer.GetHashCode((string)null));
        Assert.Equal(0, nonGeneric.GetHashCode(null));
        Assert.Equal(comparer.GetHashCode("alpha"), nonGeneric.GetHashCode("alpha"));
        Assert.Equal(nonGeneric.GetHashCode(5), nonGeneric.GetHashCode(5));
        Assert.Equal(comparer.GetHashCode(), comparer.GetHashCode());
        Assert.True(comparer.Equals((object)comparer));
        Assert.False(comparer.Equals(new object()));

        var info = CreateSerializationInfo(typeof(SwiftStringEqualityComparer));
        comparer.GetObjectData(info, default);

        _ = info.GetInt64("Entropy");
    }

    [Fact]
    public void RandomizedObjectComparer_ComparesValuesAndSerializesEntropy()
    {
        var comparer = new SwiftObjectEqualityComparer();
        IEqualityComparer nonGeneric = comparer;

        Assert.True(nonGeneric.Equals(null, null));
        Assert.True(nonGeneric.Equals("alpha", "alpha"));
        Assert.True(nonGeneric.Equals(5, 5));
        Assert.False(nonGeneric.Equals(null, "alpha"));
        Assert.False(nonGeneric.Equals("alpha", 5));

        Assert.Equal(0, comparer.GetHashCode(null));
        Assert.Equal(comparer.GetHashCode("alpha"), nonGeneric.GetHashCode("alpha"));
        Assert.Equal(nonGeneric.GetHashCode(5), nonGeneric.GetHashCode(5));
        Assert.Equal(comparer.GetHashCode(), comparer.GetHashCode());
        Assert.True(comparer.Equals((object)comparer));
        Assert.False(comparer.Equals(new object()));

        var info = CreateSerializationInfo(typeof(SwiftObjectEqualityComparer));
        comparer.GetObjectData(info, default);

        _ = info.GetInt64("Entropy");
    }

    private static SerializationInfo CreateSerializationInfo(Type type)
    {
#pragma warning disable SYSLIB0050
        return new SerializationInfo(type, new FormatterConverter());
#pragma warning restore SYSLIB0050
    }
}
