using System;
using FixedMathSharp;
using Xunit;

namespace SwiftCollections.Query.Tests
{
    public class FixedBoundVolumeTests
    {
        [Fact]
        public void Constructor_NormalizesSwappedBounds()
        {
            var volume = new FixedBoundVolume(new Vector3d(4, -2, 6), new Vector3d(-4, 2, -6));

            Assert.Equal(new Vector3d(-4, -2, -6), volume.Min);
            Assert.Equal(new Vector3d(4, 2, 6), volume.Max);
        }

        [Fact]
        public void Union_CombinesBounds()
        {
            var left = new FixedBoundVolume(new Vector3d(0, 0, 0), new Vector3d(1, 1, 1));
            var right = new FixedBoundVolume(new Vector3d(1, 1, 1), new Vector3d(2, 2, 2));

            FixedBoundVolume union = left.Union(right);

            Assert.Equal(new Vector3d(0, 0, 0), union.Min);
            Assert.Equal(new Vector3d(2, 2, 2), union.Max);
        }

        [Fact]
        public void MetadataProperties_ReturnCenterSizeAndVolume()
        {
            var volume = new FixedBoundVolume(new Vector3d(1, 2, 3), new Vector3d(5, 8, 13));

            Assert.Equal(new Vector3d(3, 5, 8), volume.Center);
            Assert.Equal(new Vector3d(4, 6, 10), volume.Size);
            Assert.Equal((Fixed64)240, volume.Volume);
        }

        [Fact]
        public void MetadataProperties_FullDomainCenterIsExactAndUnrepresentableSizeThrows()
        {
            var sameSign = new FixedBoundVolume(
                new Vector3d(Fixed64.FromRaw(long.MaxValue - 3), Fixed64.Zero, Fixed64.Zero),
                new Vector3d(Fixed64.FromRaw(long.MaxValue - 1), Fixed64.One, Fixed64.One));

            Assert.Equal(Fixed64.FromRaw(long.MaxValue - 2), sameSign.Center.X);

            Fixed64 quarterDomain = Fixed64.FromRaw(1L << 62);
            var wide = new FixedBoundVolume(
                new Vector3d(-quarterDomain, Fixed64.Zero, Fixed64.Zero),
                new Vector3d(quarterDomain, Fixed64.One, Fixed64.One));

            Assert.Equal(new Vector3d(Fixed64.Zero, Fixed64.Half, Fixed64.Half), wide.Center);
            Assert.Throws<OverflowException>(() => _ = wide.Size);
        }

        [Fact]
        public void Intersects_ReturnsFalseForEachSeparatedAxis()
        {
            var volume = new FixedBoundVolume(new Vector3d(0, 0, 0), new Vector3d(1, 1, 1));

            Assert.False(volume.Intersects(new FixedBoundVolume(new Vector3d(2, 0, 0), new Vector3d(3, 1, 1))));
            Assert.False(volume.Intersects(new FixedBoundVolume(new Vector3d(-3, 0, 0), new Vector3d(-2, 1, 1))));
            Assert.False(volume.Intersects(new FixedBoundVolume(new Vector3d(0, 2, 0), new Vector3d(1, 3, 1))));
            Assert.False(volume.Intersects(new FixedBoundVolume(new Vector3d(0, -3, 0), new Vector3d(1, -2, 1))));
            Assert.False(volume.Intersects(new FixedBoundVolume(new Vector3d(0, 0, 2), new Vector3d(1, 1, 3))));
            Assert.False(volume.Intersects(new FixedBoundVolume(new Vector3d(0, 0, -3), new Vector3d(1, 1, -2))));
            Assert.True(volume.Intersects(new FixedBoundVolume(new Vector3d(1, 1, 1), new Vector3d(2, 2, 2))));
        }

        [Fact]
        public void GetCost_ReturnsAdditionalVolumeOrZeroWhenAlreadyContained()
        {
            var volume = new FixedBoundVolume(new Vector3d(0, 0, 0), new Vector3d(2, 2, 2));

            Assert.Equal(0, volume.GetCost(new FixedBoundVolume(new Vector3d(0, 0, 0), new Vector3d(1, 1, 1))));
            Assert.Equal(19, volume.GetCost(new FixedBoundVolume(new Vector3d(0, 0, 0), new Vector3d(3, 3, 3))));
        }

        [Fact]
        public void EqualityOperatorsAndObjectEquality_CompareBounds()
        {
            var left = new FixedBoundVolume(new Vector3d(0, 0, 0), new Vector3d(1, 1, 1));
            var same = new FixedBoundVolume(new Vector3d(0, 0, 0), new Vector3d(1, 1, 1));
            var different = new FixedBoundVolume(new Vector3d(1, 1, 1), new Vector3d(2, 2, 2));

            Assert.True(left == same);
            Assert.False(left != same);
            Assert.False(left == different);
            Assert.True(left != different);
            Assert.True(left.Equals((object)same));
            Assert.False(left.Equals((object)"not a volume"));
            Assert.Equal(left.GetHashCode(), same.GetHashCode());
            Assert.Contains("Min:", left.ToString());
        }
    }
}
