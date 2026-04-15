using FixedMathSharp;
using Xunit;

namespace SwiftCollections.Query.Tests
{
    public class FixedBoundVolumeTests
    {
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
        public void BoundsEquals_IgnoresMetadataMaterialization()
        {
            var untouched = new FixedBoundVolume(new Vector3d(0, 0, 0), new Vector3d(2, 2, 2));
            var materialized = new FixedBoundVolume(new Vector3d(0, 0, 0), new Vector3d(2, 2, 2));

            _ = materialized.Center;

            Assert.True(untouched.BoundsEquals(materialized));
            Assert.True(untouched.Equals(materialized));
        }
    }
}
