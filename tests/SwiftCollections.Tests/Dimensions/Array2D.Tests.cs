using System;

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
    public class Array2DTests
    {
        [Fact]
        public void Indexing_WorksAsExpected()
        {
            var array = new Array2D<int>(3, 3);
            array[1, 1] = 42;
            Assert.Equal(42, array[1, 1]);
        }

        [Fact]
        public void Resize_PreservesExistingValues()
        {
            var array = new Array2D<int>(2, 2);
            array[0, 0] = 1;
            array[1, 1] = 2;
            array.Resize(3, 3);

            Assert.Equal(1, array[0, 0]);
            Assert.Equal(2, array[1, 1]);
            Assert.Equal(0, array[2, 2]); // Default value
        }

        [Fact]
        public void Shift_MovesElementsCorrectly()
        {
            var array = new Array2D<int>(3, 3, 0);
            array[1, 1] = 42;
            array.Shift(1, 1);

            Assert.Equal(0, array[1, 1]);
            Assert.Equal(42, array[2, 2]);
        }

        [Fact]
        public void Shift_WrapAround_ValidIndices()
        {
            var array = new Array2D<int>(3, 3);
            array[1, 1] = 42;

            array.Shift(1, 1);

            Assert.Equal(42, array[2, 2]); // Wrapped around
            Assert.Equal(0, array[1, 1]); // Original position cleared
        }

        [Fact]
        public void Shift_NonWrapping_DiscardOutOfBounds()
        {
            var array = new Array2D<int>(3, 3);
            array[1, 1] = 42;

            // Shift with large offsets that push the value out of bounds
            array.Shift(3, 3, wrap: false);

            Assert.Equal(0, array[1, 1]); // Original position cleared
            Assert.All(array, value => Assert.Equal(0, value)); // All values are default
        }

        [Fact]
        public void Fill_SetsAllElements()
        {
            var array = new Array2D<int>(2, 2);
            array.Fill(99);

            Assert.All(array.InnerArray, item => Assert.Equal(99, item));
        }

        [Fact]
        public void OutOfBoundsAccess_ThrowsException()
        {
            var array = new Array2D<int>(3, 3);
            Assert.Throws<IndexOutOfRangeException>(() => array[-1, 0]);
            Assert.Throws<IndexOutOfRangeException>(() => array[3, 3]);
        }

        [Fact]
        public void Resize_SmallerDimensions_TrimsData()
        {
            var array = new Array2D<int>(4, 4);
            array.Fill(42);
            array.Resize(2, 2);

            Assert.Equal(2, array.Width);
            Assert.Equal(2, array.Height);
            Assert.All(array.InnerArray, item => Assert.Equal(42, item));
        }

        [Fact]
        public void LargeArray_PerformanceAndCorrectness()
        {
            const int size = 1000;
            var array = new Array2D<int>(size, size);
            array.Fill(99);

            Assert.Equal(size * size, array.InnerArray.Length);
            Assert.All(array.InnerArray, value => Assert.Equal(99, value));
        }

        [Fact]
        public void SerializeDeserialize_MaintainsDataAndStructure()
        {
            // Arrange
            var originalArray = new Array2D<int>(3, 3);
            originalArray[0, 0] = 42;
            originalArray[1, 1] = 99;
            originalArray[2, 2] = 7;

#if NET48_OR_GREATER
            // Serialize the Array2D object
            byte[] serializedData;
            using (var memoryStream = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(memoryStream, originalArray);
                serializedData = memoryStream.ToArray();
            }

            // Deserialize the Array2D object
            Array2D<int> deserializedArray;
            using (var memoryStream = new MemoryStream(serializedData))
            {
                var formatter = new BinaryFormatter();
                deserializedArray = (Array2D<int>)formatter.Deserialize(memoryStream);
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
            var deserializedArray = JsonSerializer.Deserialize<Array2D<int>>(json, jsonOptions);
#endif

            // Assert
            Assert.Equal(originalArray.Width, deserializedArray.Width);
            Assert.Equal(originalArray.Height, deserializedArray.Height);

            for (int x = 0; x < originalArray.Width; x++)
            {
                for (int y = 0; y < originalArray.Height; y++)
                    Assert.Equal(originalArray[x, y], deserializedArray[x, y]);
            }
        }
    }
}
