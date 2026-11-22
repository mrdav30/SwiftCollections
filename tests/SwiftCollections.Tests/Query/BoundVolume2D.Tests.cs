using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using SwiftCollections.Utility;
using Xunit;

namespace SwiftCollections.Query.Tests;

public class BoundVolume2DTests
{
    [Fact]
    public void EndpointsAndMetadata_AreCopiedAndDefaultIsAnOriginPoint()
    {
        var min = new Vector2(-2, -3);
        var max = new Vector2(4, 5);
        var bounds = new BoundVolume2D(min, max);
        min.X = 99;
        max.Y = 99;

        Assert.Equal(new Vector2(-2, -3), bounds.Min);
        Assert.Equal(new Vector2(4, 5), bounds.Max);
        Assert.Equal(new Vector2(1, 1), bounds.Center);
        Assert.Equal(new Vector2(6, 8), bounds.Size);
        Assert.Equal(48d, bounds.Area);
        Assert.Equal(16, Unsafe.SizeOf<BoundVolume2D>());
        Assert.Equal(Vector2.Zero, default(BoundVolume2D).Min);
        Assert.Equal(Vector2.Zero, default(BoundVolume2D).Max);
        Assert.Equal(Vector2.Zero, default(BoundVolume2D).Center);
        Assert.Equal(Vector2.Zero, default(BoundVolume2D).Size);
        Assert.Equal(0d, default(BoundVolume2D).Area);
        Assert.True(default(BoundVolume2D).Intersects(default));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(float.PositiveInfinity)]
    public void Constructor_RejectsEveryNonfiniteCoordinateBeforeOrdering(float invalid)
    {
        Assert.Throws<ArgumentOutOfRangeException>("min", () => new BoundVolume2D(new Vector2(invalid, 0), Vector2.One));
        Assert.Throws<ArgumentOutOfRangeException>("min", () => new BoundVolume2D(new Vector2(0, invalid), Vector2.One));
        Assert.Throws<ArgumentOutOfRangeException>("max", () => new BoundVolume2D(Vector2.Zero, new Vector2(invalid, 1)));
        Assert.Throws<ArgumentOutOfRangeException>("max", () => new BoundVolume2D(Vector2.Zero, new Vector2(1, invalid)));
        Assert.Throws<ArgumentOutOfRangeException>("min", () => new BoundVolume2D(new Vector2(invalid), new Vector2(invalid)));
    }

    [Fact]
    public void Constructor_RejectsInversionOnEitherAxisWithoutNormalizing()
    {
        Assert.Throws<ArgumentException>("max", () => new BoundVolume2D(new Vector2(2, 0), Vector2.One));
        Assert.Throws<ArgumentException>("max", () => new BoundVolume2D(new Vector2(0, 2), Vector2.One));
        var point = new BoundVolume2D(Vector2.One, Vector2.One);
        Assert.Equal(Vector2.One, point.Min);
        Assert.Equal(Vector2.One, point.Max);
    }

    [Fact]
    public void Metadata_HandlesSameSignSumsFullDomainAndDegenerateSpansIndependently()
    {
        var high = new BoundVolume2D(new Vector2(float.MaxValue / 2), new Vector2(float.MaxValue));
        float midpoint = (float)(((double)(float.MaxValue / 2) + float.MaxValue) / 2);
        Assert.Equal(new Vector2(midpoint), high.Center);
        Assert.True(float.IsFinite(high.Center.X));
        Assert.True(high.Area > float.MaxValue);

        var full = new BoundVolume2D(new Vector2(-float.MaxValue), new Vector2(float.MaxValue));
        Assert.Equal(Vector2.Zero, full.Center);
        Assert.Equal(Math.Pow(2d * float.MaxValue, 2), full.Area);
        Assert.Throws<OverflowException>(() => full.Size);

        var wideLine = new BoundVolume2D(new Vector2(-float.MaxValue, 0), new Vector2(float.MaxValue, 0));
        var tallLine = new BoundVolume2D(new Vector2(0, -float.MaxValue), new Vector2(0, float.MaxValue));
        Assert.Throws<OverflowException>(() => wideLine.Size);
        Assert.Throws<OverflowException>(() => tallLine.Size);
        Assert.Equal(0d, wideLine.Area);
        Assert.Equal(0d, tallLine.Area);
        Assert.Equal(Vector2.Zero, wideLine.Center);
        Assert.Equal(Vector2.Zero, tallLine.Center);
    }

    [Fact]
    public void Metadata_PreservesSubnormalAreaAndAdjacentEndpointMidpoints()
    {
        var tiny = new BoundVolume2D(new Vector2(-float.Epsilon), new Vector2(float.Epsilon));
        Assert.Equal(Vector2.Zero, tiny.Center);
        Assert.Equal(new Vector2(2 * float.Epsilon), tiny.Size);
        Assert.Equal(4d * float.Epsilon * float.Epsilon, tiny.Area);
        Assert.True(tiny.Area > 0d);

        float next = MathF.BitIncrement(1f);
        var adjacent = new BoundVolume2D(Vector2.One, new Vector2(next));
        Assert.Equal(new Vector2((float)((1d + next) / 2)), adjacent.Center);
        Assert.Equal(new Vector2(next - 1f), adjacent.Size);
    }

    [Theory]
    [InlineData(float.Epsilon)]
    [InlineData(1f)]
    public void Size_RejectsExactOverflowEvenWhenDoubleSubtractionLosesTheExcess(float excess)
    {
        var positiveWidth = new BoundVolume2D(new Vector2(-excess, 0), new Vector2(float.MaxValue, 1));
        var negativeWidth = new BoundVolume2D(new Vector2(-float.MaxValue, 0), new Vector2(excess, 1));
        var positiveHeight = new BoundVolume2D(new Vector2(0, -excess), new Vector2(1, float.MaxValue));
        var negativeHeight = new BoundVolume2D(new Vector2(0, -float.MaxValue), new Vector2(1, excess));

        Assert.Throws<OverflowException>(() => positiveWidth.Size);
        Assert.Throws<OverflowException>(() => negativeWidth.Size);
        Assert.Throws<OverflowException>(() => positiveHeight.Size);
        Assert.Throws<OverflowException>(() => negativeHeight.Size);
    }

    [Fact]
    public void Size_AllowsExactMaximumAndSameSignSpans()
    {
        var symmetric = new BoundVolume2D(new Vector2(-float.MaxValue / 2), new Vector2(float.MaxValue / 2));
        var positive = new BoundVolume2D(Vector2.Zero, new Vector2(float.MaxValue));
        var negative = new BoundVolume2D(new Vector2(-float.MaxValue), Vector2.Zero);
        var positiveHalf = new BoundVolume2D(new Vector2(float.MaxValue / 2), new Vector2(float.MaxValue));
        var negativeHalf = new BoundVolume2D(new Vector2(-float.MaxValue), new Vector2(-float.MaxValue / 2));

        Assert.Equal(new Vector2(float.MaxValue), symmetric.Size);
        Assert.Equal(new Vector2(float.MaxValue), positive.Size);
        Assert.Equal(new Vector2(float.MaxValue), negative.Size);
        Assert.Equal(new Vector2(float.MaxValue / 2), positiveHalf.Size);
        Assert.Equal(new Vector2(float.MaxValue / 2), negativeHalf.Size);
    }

    [Fact]
    public void Size_AdmissionMatchesExactBinarySpanOracle()
    {
        float half = float.MaxValue / 2;
        float[] magnitudes =
        {
            0, float.Epsilon, 1, MathF.BitDecrement(half), half,
            MathF.BitIncrement(half), MathF.BitDecrement(float.MaxValue), float.MaxValue
        };
        foreach (float first in magnitudes)
            foreach (float second in magnitudes)
            {
                Check(first, second);
                Check(-first, second);
                Check(first, -second);
                Check(-first, -second);
            }

        uint state = 0x51AEB00F;
        for (int i = 0; i < 512; i++)
            Check(NextFinite(), NextFinite());

        float NextFinite()
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            int bits = unchecked((int)state);
            if ((bits & 0x7F800000) == 0x7F800000)
                bits &= ~0x00800000;
            return BitConverter.Int32BitsToSingle(bits);
        }

        static void Check(float first, float second)
        {
            float min = Math.Min(first, second);
            float max = Math.Max(first, second);
            var wide = new BoundVolume2D(new Vector2(min, 0), new Vector2(max, 1));
            var tall = new BoundVolume2D(new Vector2(0, min), new Vector2(1, max));
            bool overflow = ExactUnits(max) - ExactUnits(min) > ExactUnits(float.MaxValue);
            if (overflow)
            {
                Assert.Throws<OverflowException>(() => wide.Size);
                Assert.Throws<OverflowException>(() => tall.Size);
            }
            else
            {
                Assert.Equal(new Vector2(max - min, 1), wide.Size);
                Assert.Equal(new Vector2(1, max - min), tall.Size);
            }
        }

        static BigInteger ExactUnits(float value)
        {
            uint bits = unchecked((uint)BitConverter.SingleToInt32Bits(value));
            int exponent = (int)((bits >> 23) & 0xFF);
            BigInteger units = bits & 0x7FFFFF;
            if (exponent != 0)
                units = (units + (1 << 23)) << (exponent - 1);
            return (bits & 0x80000000) == 0 ? units : -units;
        }
    }

    [Fact]
    public void Intersects_IsInclusiveAndRejectsSeparationOnEverySide()
    {
        var bounds = new BoundVolume2D(Vector2.Zero, Vector2.One);
        Assert.True(bounds.Intersects(bounds));
        Assert.True(bounds.Intersects(new BoundVolume2D(Vector2.One, new Vector2(2))));
        Assert.True(bounds.Intersects(new BoundVolume2D(new Vector2(-1, 0), new Vector2(0, 1))));
        Assert.True(bounds.Intersects(default));
        Assert.False(bounds.Intersects(new BoundVolume2D(new Vector2(-2, 0), new Vector2(-1, 1))));
        Assert.False(bounds.Intersects(new BoundVolume2D(new Vector2(2, 0), new Vector2(3, 1))));
        Assert.False(bounds.Intersects(new BoundVolume2D(new Vector2(0, -2), new Vector2(1, -1))));
        Assert.False(bounds.Intersects(new BoundVolume2D(new Vector2(0, 2), new Vector2(1, 3))));
    }

    [Fact]
    public void UnionAndCost_UseWidenedAreaFloorAndFinalClamping()
    {
        var first = new BoundVolume2D(Vector2.Zero, Vector2.One);
        var second = new BoundVolume2D(new Vector2(-2, 0.5f), new Vector2(0.5f, 3));
        BoundVolume2D combined = first.Union(second);
        Assert.Equal(new Vector2(-2, 0), combined.Min);
        Assert.Equal(new Vector2(1, 3), combined.Max);
        Assert.Equal(Vector2.Zero, first.Min);
        Assert.Equal(Vector2.One, first.Max);
        Assert.Equal(new Vector2(-2, 0.5f), second.Min);
        Assert.Equal(8, first.GetCost(second));
        Assert.Equal(0, first.GetCost(default));
        Assert.Equal(0, first.GetCost(new BoundVolume2D(Vector2.Zero, new Vector2(1.25f, 1.5f))));
        Assert.Equal(2, first.GetCost(new BoundVolume2D(Vector2.Zero, new Vector2(1.75f, 2))));
        var full = new BoundVolume2D(new Vector2(-float.MaxValue), new Vector2(float.MaxValue));
        Assert.Equal(long.MaxValue, first.GetCost(full));
        Assert.Equal(0, full.GetCost(first));
        var line = new BoundVolume2D(new Vector2(-float.MaxValue, 0), new Vector2(float.MaxValue, 0));
        Assert.Equal(0, default(BoundVolume2D).GetCost(line));
    }

    [Fact]
    public void EqualityAndHash_UseEndpointsAndNormalizeSignedZeroHashes()
    {
        var value = new BoundVolume2D(new Vector2(-2, -1), new Vector2(3, 4));
        var same = new BoundVolume2D(new Vector2(-2, -1), new Vector2(3, 4));
        var otherMin = new BoundVolume2D(new Vector2(-3, -1), new Vector2(3, 4));
        var otherMax = new BoundVolume2D(new Vector2(-2, -1), new Vector2(3, 5));
        Assert.True(value.BoundsEquals(same));
        Assert.True(value.Equals(same));
        Assert.True(value.Equals((object)same));
        Assert.True(value == same);
        Assert.False(value != same);
        Assert.False(value.BoundsEquals(otherMin));
        Assert.False(value.BoundsEquals(otherMax));
        Assert.False(value.Equals((object)otherMin));
        Assert.False(value.Equals(null));
        Assert.False(value.Equals("bounds"));
        Assert.False(value == otherMax);
        Assert.True(value != otherMax);
        Assert.Equal(value.GetHashCode(), same.GetHashCode());
        Assert.Equal(SwiftHashTools.CombineHashCodes(
            BitConverter.SingleToInt32Bits(-2f), BitConverter.SingleToInt32Bits(-1f),
            BitConverter.SingleToInt32Bits(3f), BitConverter.SingleToInt32Bits(4f)), value.GetHashCode());

        float negativeZero = BitConverter.Int32BitsToSingle(int.MinValue);
        var signedZero = new BoundVolume2D(new Vector2(negativeZero), new Vector2(negativeZero));
        Assert.Equal(default, signedZero);
        Assert.Equal(default(BoundVolume2D).GetHashCode(), signedZero.GetHashCode());
        Assert.Equal($"Min: {value.Min}, Max: {value.Max}", value.ToString());
    }
}
