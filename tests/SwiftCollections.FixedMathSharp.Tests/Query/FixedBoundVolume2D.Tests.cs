using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using FixedMathSharp;
using SwiftCollections.Utility;
using Xunit;

namespace SwiftCollections.Query.Tests;

public class FixedBoundVolume2DTests
{
    [Fact]
    public void Constructor_NormalizesAndStoresOnlyEndpoints()
    {
        var bounds = new FixedBoundVolume2D(new Vector2d(5, -2), new Vector2d(1, 8));

        Assert.Equal(new Vector2d(1, -2), bounds.Min);
        Assert.Equal(new Vector2d(5, 8), bounds.Max);
        Assert.Equal(new Vector2d(3, 3), bounds.Center);
        Assert.Equal(new Vector2d(4, 10), bounds.Size);
        Assert.Equal((Fixed64)40, bounds.Area);
        Assert.Equal(32, Unsafe.SizeOf<FixedBoundVolume2D>());
        Assert.Equal(Vector2d.Zero, default(FixedBoundVolume2D).Min);
        Assert.Equal(Vector2d.Zero, default(FixedBoundVolume2D).Max);
        Assert.Equal(Fixed64.Zero, default(FixedBoundVolume2D).Area);
    }

    [Fact]
    public void FullDomainMetadata_UsesWidenedEndpointsAndIndependentCheckedArea()
    {
        var line = new FixedBoundVolume2D(
            new Vector2d(Fixed64.MinValue, Fixed64.Zero),
            new Vector2d(Fixed64.MaxValue, Fixed64.Zero));
        Assert.Equal(Vector2d.Zero, line.Center);
        Assert.Throws<OverflowException>(() => line.Size);
        Assert.Equal(Fixed64.Zero, line.Area);

        var tall = new FixedBoundVolume2D(
            new Vector2d(Fixed64.Zero, Fixed64.MinValue),
            new Vector2d(Fixed64.Zero, Fixed64.MaxValue));
        Assert.Throws<OverflowException>(() => tall.Size);
        Assert.Equal(Fixed64.Zero, tall.Area);

        var huge = new FixedBoundVolume2D(
            new Vector2d(Fixed64.MinValue, Fixed64.MinValue),
            new Vector2d(Fixed64.MaxValue, Fixed64.MaxValue));
        Assert.Throws<OverflowException>(() => huge.Area);
        Assert.Equal(long.MaxValue, default(FixedBoundVolume2D).GetCost(huge));

        var odd = new FixedBoundVolume2D(
            new Vector2d(Fixed64.FromRaw(3), Fixed64.FromRaw(-4)),
            new Vector2d(Fixed64.FromRaw(4), Fixed64.FromRaw(-3)));
        Assert.Equal(new Vector2d(Fixed64.FromRaw(4), Fixed64.FromRaw(-4)), odd.Center);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(3, 2)]
    [InlineData(5, 2)]
    public void Area_RoundsOnceToNearestEven(long widthRaw, long expectedRaw)
    {
        var bounds = new FixedBoundVolume2D(Vector2d.Zero,
            new Vector2d(Fixed64.FromRaw(widthRaw), Fixed64.Half));

        Assert.Equal(Fixed64.FromRaw(expectedRaw), bounds.Area);
    }

    [Fact]
    public void Intersection_IncludesTouchingAndRejectsEverySeparatingDirection()
    {
        var bounds = Box(0, 0, 1, 1);

        Assert.True(bounds.Intersects(Box(1, 1, 2, 2)));
        Assert.True(bounds.Intersects(default));
        Assert.True(default(FixedBoundVolume2D).Intersects(default));
        Assert.False(bounds.Intersects(Box(-2, 0, -1, 1)));
        Assert.False(bounds.Intersects(Box(2, 0, 3, 1)));
        Assert.False(bounds.Intersects(Box(0, -2, 1, -1)));
        Assert.False(bounds.Intersects(Box(0, 2, 1, 3)));
    }

    [Fact]
    public void UnionAndCost_DoNotMutateOrMaterializeSize()
    {
        var bounds = Box(0, 0, 2, 2);
        var other = Box(-1, 1, 3, 3);
        FixedBoundVolume2D union = bounds.Union(other);

        Assert.Equal(new Vector2d(-1, 0), union.Min);
        Assert.Equal(new Vector2d(3, 3), union.Max);
        Assert.Equal(Box(0, 0, 2, 2), bounds);
        Assert.Equal(Box(-1, 1, 3, 3), other);
        Assert.Equal(8, bounds.GetCost(other));
        Assert.Equal(0, union.GetCost(bounds));

        var thin = new FixedBoundVolume2D(Vector2d.Zero,
            new Vector2d(Fixed64.FromRaw(1), Fixed64.One));
        Assert.Equal(0, default(FixedBoundVolume2D).GetCost(thin));
    }

    [Fact]
    public void Cost_AgreesWithIndependentBigIntegerOracleAcrossEndpointDomain()
    {
        long[] raw = { long.MinValue, long.MinValue + 1, -4294967296L, -1, 0, 1,
            4294967296L, long.MaxValue - 1, long.MaxValue };
        var random = new Random(20191);
        for (int i = 0; i < 256; i++)
        {
            var left = new FixedBoundVolume2D(
                new Vector2d(Fixed64.FromRaw(raw[random.Next(raw.Length)]), Fixed64.FromRaw(raw[random.Next(raw.Length)])),
                new Vector2d(Fixed64.FromRaw(raw[random.Next(raw.Length)]), Fixed64.FromRaw(raw[random.Next(raw.Length)])));
            var right = new FixedBoundVolume2D(
                new Vector2d(Fixed64.FromRaw(raw[random.Next(raw.Length)]), Fixed64.FromRaw(raw[random.Next(raw.Length)])),
                new Vector2d(Fixed64.FromRaw(raw[random.Next(raw.Length)]), Fixed64.FromRaw(raw[random.Next(raw.Length)])));
            BigInteger x = BigInteger.Max(left.Max.X.m_rawValue, right.Max.X.m_rawValue)
                - BigInteger.Min(left.Min.X.m_rawValue, right.Min.X.m_rawValue);
            BigInteger y = BigInteger.Max(left.Max.Y.m_rawValue, right.Max.Y.m_rawValue)
                - BigInteger.Min(left.Min.Y.m_rawValue, right.Min.Y.m_rawValue);
            BigInteger area = ((BigInteger)left.Max.X.m_rawValue - left.Min.X.m_rawValue)
                * ((BigInteger)left.Max.Y.m_rawValue - left.Min.Y.m_rawValue);
            BigInteger delta = (x * y - area) >> 64;

            Assert.Equal((long)BigInteger.Min(delta, long.MaxValue), left.GetCost(right));
        }
    }

    [Fact]
    public void EqualityAndHash_UseNormalizedEndpoints()
    {
        var bounds = Box(1, 2, 3, 4);
        var same = Box(3, 4, 1, 2);
        var differentMin = Box(0, 2, 3, 4);
        var differentMax = Box(1, 2, 3, 5);

        Assert.True(bounds.BoundsEquals(same));
        Assert.True(bounds.Equals(same));
        Assert.True(bounds.Equals((object)same));
        Assert.False(bounds.Equals((object)differentMin));
        Assert.False(bounds.Equals((object)null));
        Assert.False(bounds.Equals("bounds"));
        Assert.True(bounds == same);
        Assert.False(bounds == differentMin);
        Assert.False(bounds != same);
        Assert.True(bounds != differentMax);
        Assert.False(bounds.BoundsEquals(differentMin));
        Assert.False(bounds.BoundsEquals(differentMax));
        Assert.Equal(bounds.GetHashCode(), same.GetHashCode());
        Assert.Equal(SwiftHashTools.CombineHashCodes(bounds.Min.X.GetHashCode(), bounds.Min.Y.GetHashCode(),
            bounds.Max.X.GetHashCode(), bounds.Max.Y.GetHashCode()), bounds.GetHashCode());
        Assert.Equal($"Min: {bounds.Min}, Max: {bounds.Max}", bounds.ToString());
    }

    private static FixedBoundVolume2D Box(int minX, int minY, int maxX, int maxY) =>
        new(new Vector2d(minX, minY), new Vector2d(maxX, maxY));
}
