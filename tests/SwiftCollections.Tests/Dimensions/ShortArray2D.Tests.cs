using System;
using Xunit;

namespace SwiftCollections.Dimensions.Tests
{
    public class ShortArray2DTests
    {
        [Fact]
        public void Normalize_AdjustsValuesToRange()
        {
            var shortArray = new ShortArray2D(2, 2);
            shortArray[0, 0] = 10;
            shortArray[0, 1] = 20;
            shortArray[1, 0] = 30;
            shortArray[1, 1] = 40;

            shortArray.Normalize(0, 100);

            Assert.Equal(0, shortArray[0, 0]);
            Assert.Equal(33, shortArray[0, 1]);
            Assert.Equal(66, shortArray[1, 0]);
            Assert.Equal(100, shortArray[1, 1]);
        }

        [Fact]
        public void Normalize_SingleValue_NoChange()
        {
            var shortArray = new ShortArray2D(3, 3, 10);
            shortArray.Normalize(0, 100);

            Assert.All(shortArray.InnerArray, value => Assert.Equal(0, value));
        }

        [Fact]
        public void Scale_MultipliesValues()
        {
            var shortArray = new ShortArray2D(3, 3, 2);
            shortArray.Scale(3);

            Assert.All(shortArray.InnerArray, value => Assert.Equal(6, value));
        }

        [Fact]
        public void Clone_CreatesExactCopy()
        {
            short[,] source = { { 1, 2 }, { 3, 4 } };
            var clonedArray = ShortArray2D.Clone(source);

            Assert.Equal(2, clonedArray.Width);
            Assert.Equal(2, clonedArray.Height);
            Assert.Equal(3, clonedArray[1, 0]);
        }

        [Fact]
        public void HeightMapSimulation()
        {
            short[,] heights = { { 10, 20 }, { 30, 40 } };
            var heightMap = ShortArray2D.Clone(heights);

            heightMap.Normalize(0, 100);
            Assert.Equal(0, heightMap[0, 0]);
            Assert.Equal(100, heightMap[1, 1]);

            heightMap.Scale(2);
            Assert.Equal(0, heightMap[0, 0]);
            Assert.Equal(200, heightMap[1, 1]);
        }
    }
}
