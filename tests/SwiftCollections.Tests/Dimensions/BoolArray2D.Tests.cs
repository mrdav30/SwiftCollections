using Xunit;

namespace SwiftCollections.Dimensions.Tests
{
    public class BoolArray2DTests
    {
        [Fact]
        public void Toggle_FlipsValue()
        {
            var boolArray = new BoolArray2D(3, 3, false);
            boolArray.Toggle(1, 1);

            Assert.True(boolArray[1, 1]);
            boolArray.Toggle(1, 1);
            Assert.False(boolArray[1, 1]);
        }

        [Fact]
        public void ToggleLargeRegion()
        {
            const int size = 1000;
            var boolArray = new BoolArray2D(size, size, false);

            for (int x = 0; x < size; x++)
                for (int y = 0; y < size; y++)
                    boolArray.Toggle(x, y);

            Assert.Equal(size * size, boolArray.CountTrue());
        }

        [Fact]
        public void SetRegion_SetsSpecifiedValues()
        {
            var boolArray = new BoolArray2D(5, 5);
            boolArray.SetRegion(1, 1, 3, 3, true);

            for (int x = 1; x <= 3; x++)
                for (int y = 1; y <= 3; y++)
                    Assert.True(boolArray[x, y]);

            Assert.False(boolArray[0, 0]);
        }

        [Fact]
        public void SetRegion_OutOfBounds_IgnoresOutOfBoundCells()
        {
            var boolArray = new BoolArray2D(5, 5);
            boolArray.SetRegion(3, 3, 5, 5, true);

            Assert.Equal(4, boolArray.CountTrue()); // Only valid cells within bounds are set
        }

        [Fact]
        public void CountTrue_ReturnsAccurateCount()
        {
            var boolArray = new BoolArray2D(4, 4, false);
            boolArray[1, 1] = true;
            boolArray[2, 2] = true;

            Assert.Equal(2, boolArray.CountTrue());
        }

        [Fact]
        public void CombinedOperations_WorkCorrectly()
        {
            var boolArray = new BoolArray2D(5, 5, false);

            boolArray.SetRegion(0, 0, 3, 3, true);
            Assert.Equal(9, boolArray.CountTrue());

            boolArray.Shift(1, 1);
            Assert.Equal(9, boolArray.CountTrue());
            Assert.True(boolArray[1, 1]);
            Assert.False(boolArray[0, 0]);
        }

    }
}
