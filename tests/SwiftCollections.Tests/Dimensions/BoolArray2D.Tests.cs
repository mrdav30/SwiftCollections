#if NET48_OR_GREATER
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
#endif

#if NET8_0_OR_GREATER
using System.Text.Json;
using System.Text.Json.Serialization;
#endif

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

        [Fact]
        public void BoolArray2D_Serialization_Deserialization()
        {
            // Arrange
            var originalArray = new BoolArray2D(3, 3, false);
            originalArray[0, 0] = true;
            originalArray[1, 1] = true;
            originalArray[2, 2] = true;

            // Act
#if NET48_OR_GREATER
            BoolArray2D deserializedArray;
            using (var stream = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(stream, originalArray);

                stream.Seek(0, SeekOrigin.Begin);
                deserializedArray = (BoolArray2D)formatter.Deserialize(stream);
            }
#endif

#if NET8_0_OR_GREATER
            var jsonOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                ReferenceHandler = ReferenceHandler.IgnoreCycles,
                IncludeFields = true,
                IgnoreReadOnlyProperties = true
            };
            jsonOptions.Converters.Add(new Array2DJsonConverter());
            var json = JsonSerializer.SerializeToUtf8Bytes(originalArray, jsonOptions);
            var deserializedArray = JsonSerializer.Deserialize<BoolArray2D>(json, jsonOptions);
#endif

            // Assert
            Assert.Equal(originalArray.Width, deserializedArray.Width);
            Assert.Equal(originalArray.Height, deserializedArray.Height);

            for (int x = 0; x < originalArray.Width; x++)
            {
                for (int y = 0; y < originalArray.Height; y++)
                {
                    Assert.Equal(originalArray[x, y], deserializedArray[x, y]);
                }
            }
        }
    }
}
