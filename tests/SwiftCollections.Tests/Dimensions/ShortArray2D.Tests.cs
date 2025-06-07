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
            ShortArray2D source = new ShortArray2D(new short[,] { { 1, 2 }, { 3, 4 } });
            var clonedArray = source.Clone();

            Assert.Equal(2, clonedArray.Width);
            Assert.Equal(2, clonedArray.Height);
            Assert.Equal(3, clonedArray[1, 0]);
        }

        [Fact]
        public void HeightMapSimulation()
        {
            short[,] heights = { { 10, 20 }, { 30, 40 } };
            var heightMap = new ShortArray2D(heights);

            heightMap.Normalize(0, 100);
            Assert.Equal(0, heightMap[0, 0]);
            Assert.Equal(100, heightMap[1, 1]);

            heightMap.Scale(2);
            Assert.Equal(0, heightMap[0, 0]);
            Assert.Equal(200, heightMap[1, 1]);
        }

        [Fact]
        public void ShortArray2D_Serialization_Deserialization()
        {
            // Arrange
            var originalArray = new ShortArray2D(3, 3, (short)0);
            originalArray[0, 0] = 10;
            originalArray[1, 1] = 20;
            originalArray[2, 2] = 30;

            // Act
#if NET48_OR_GREATER
            ShortArray2D deserializedArray;
            using (var stream = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(stream, originalArray);

                stream.Seek(0, SeekOrigin.Begin);
                deserializedArray = (ShortArray2D)formatter.Deserialize(stream);
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
            var deserializedArray = JsonSerializer.Deserialize<ShortArray2D>(json, jsonOptions);
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
