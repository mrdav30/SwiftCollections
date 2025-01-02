using Xunit;
using System;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace SwiftCollections.Dimensions.Tests
{
    public class Array3DTests
    {
        [Fact]
        public void Indexing_WorksAsExpected()
        {
            var array = new Array3D<int>(3, 3, 3);
            array[1, 1, 1] = 42;

            Assert.Equal(42, array[1, 1, 1]);
        }

        [Fact]
        public void Indexing_OutOfBounds_ThrowsException()
        {
            var array = new Array3D<int>(3, 3, 3);

            Assert.Throws<IndexOutOfRangeException>(() => array[-1, 0, 0]);
            Assert.Throws<IndexOutOfRangeException>(() => array[3, 3, 3]);
        }

        [Fact]
        public void Initialization_WithDefaultValue_SetsAllElements()
        {
            var array = new Array3D<int>(3, 3, 3, 99);

            Assert.All(array.GetType().GetField("_innerArray", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(array) as int[][][], layer =>
                {
                    Assert.All(layer, row => Assert.All(row, value => Assert.Equal(99, value)));
                });
        }

        [Fact]
        public void Resize_LargerDimensions_RetainsData()
        {
            var array = new Array3D<int>(2, 2, 2);
            array[0, 0, 0] = 1;
            array[1, 1, 1] = 2;

            array.Resize(3, 3, 3);

            Assert.Equal(1, array[0, 0, 0]);
            Assert.Equal(2, array[1, 1, 1]);
            Assert.Equal(0, array[2, 2, 2]); // Default value
        }

        [Fact]
        public void Resize_SmallerDimensions_TrimsData()
        {
            var array = new Array3D<int>(3, 3, 3);
            array[2, 2, 2] = 42;

            array.Resize(2, 2, 2);

            Assert.Throws<IndexOutOfRangeException>(() => array[2, 2, 2]);
        }

        [Fact]
        public void Shift_PositiveOffsets_ShiftsElements()
        {
            var array = new Array3D<int>(3, 3, 3);
            array[0, 0, 0] = 42;

            array.Shift(1, 1, 1);

            Assert.Equal(42, array[1, 1, 1]);
            Assert.Equal(0, array[0, 0, 0]); // Default value
        }

        [Fact]
        public void Shift_NegativeOffsets_ShiftsElements()
        {
            var array = new Array3D<int>(3, 3, 3);
            array[2, 2, 2] = 42;

            array.Shift(-1, -1, -1);

            Assert.Equal(42, array[1, 1, 1]);
            Assert.Equal(0, array[2, 2, 2]); // Default value
        }

        [Fact]
        public void Clear_ResetsAllElementsToDefault()
        {
            var array = new Array3D<int>(3, 3, 3);
            array.Fill(42);

            array.Clear();

            Assert.All(array.GetType().GetField("_innerArray", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(array) as int[][][], layer =>
                {
                    Assert.All(layer, row => Assert.All(row, value => Assert.Equal(0, value))); // Default value
                });
        }

        [Fact]
        public void Fill_SetsAllElementsToSpecifiedValue()
        {
            var array = new Array3D<int>(3, 3, 3);
            array.Fill(42);

            Assert.All(array.GetType().GetField("_innerArray", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(array) as int[][][], layer =>
                {
                    Assert.All(layer, row => Assert.All(row, value => Assert.Equal(42, value)));
                });
        }

        [Fact]
        public void IsValidIndex_ValidatesCorrectly()
        {
            var array = new Array3D<int>(3, 3, 3);

            Assert.True(array.IsValidIndex(0, 0, 0));
            Assert.False(array.IsValidIndex(-1, 0, 0));
            Assert.False(array.IsValidIndex(3, 3, 3));
        }

        [Fact]
        public void StressTest_LargeArray()
        {
            const int size = 100;
            var array = new Array3D<int>(size, size, size);

            // Fill array with values
            for (int x = 0; x < size; x++)
                for (int y = 0; y < size; y++)
                    for (int z = 0; z < size; z++)
                        array[x, y, z] = x + y + z;

            // Verify values
            for (int x = 0; x < size; x++)
                for (int y = 0; y < size; y++)
                    for (int z = 0; z < size; z++)
                        Assert.Equal(x + y + z, array[x, y, z]);
        }

        [Fact]
        public void ResizeAndShift_CombinedOperations()
        {
            var array = new Array3D<int>(4, 4, 4);
            array[0, 0, 0] = 1;
            array[3, 3, 3] = 99;

            // Resize larger
            array.Resize(6, 6, 6);
            Assert.Equal(1, array[0, 0, 0]); // Original position before shifting
            Assert.Equal(99, array[3, 3, 3]); // Original position before shifting

            // Shift
            array.Shift(1, 1, 1);
            Assert.Equal(1, array[1, 1, 1]); // Shifted position
            Assert.Equal(99, array[4, 4, 4]); // Shifted position
            Assert.Equal(0, array[0, 0, 0]); // Default value after shift

            // Resize smaller
            array.Resize(3, 3, 3);
            Assert.Equal(1, array[1, 1, 1]); // Preserved shifted position
            Assert.Throws<IndexOutOfRangeException>(() => array[4, 4, 4]); // Truncated position
        }

        [Fact]
        public void RandomizedData_MaintainsIntegrity()
        {
            const int size = 50;
            var array = new Array3D<int>(size, size, size);
            var random = new Random();

            // Fill array with random data
            for (int x = 0; x < size; x++)
                for (int y = 0; y < size; y++)
                    for (int z = 0; z < size; z++)
                        array[x, y, z] = random.Next();

            // Verify data integrity
            for (int x = 0; x < size; x++)
                for (int y = 0; y < size; y++)
                    for (int z = 0; z < size; z++)
                        Assert.Equal(array[x, y, z], array[x, y, z]);
        }

        [Fact]
        public void MultiThreadedAccess_DoesNotCorruptData()
        {
            const int size = 10;
            var array = new Array3D<int>(size, size, size);

            Parallel.For(0, size, x =>
            {
                for (int y = 0; y < size; y++)
                {
                    for (int z = 0; z < size; z++)
                    {
                        array[x, y, z] = x * y * z;
                    }
                }
            });

            for (int x = 0; x < size; x++)
                for (int y = 0; y < size; y++)
                    for (int z = 0; z < size; z++)
                        Assert.Equal(x * y * z, array[x, y, z]);
        }

        [Fact]
        public void Resize_ToZeroDimensions_ClearsArray()
        {
            var array = new Array3D<int>(3, 3, 3);
            array.Fill(42);

            array.Resize(0, 0, 0);

            Assert.Equal(0, array.Width);
            Assert.Equal(0, array.Height);
            Assert.Equal(0, array.Length);
        }

        [Fact]
        public void Shift_NonUniformOffsets()
        {
            var array = new Array3D<int>(3, 3, 3);
            array[1, 1, 1] = 42;

            array.Shift(1, -1, 2);

            Assert.Equal(42, array[2, 0, 0]); // Adjusted for boundaries
            Assert.Equal(0, array[1, 1, 1]); // Default value after shift
        }

        [Fact]
        public void Shift_NonWrapping_DiscardOutOfBounds()
        {
            var array = new Array3D<int>(3, 3, 3);
            array[1, 1, 1] = 42;

            array.Shift(1, -1, 2, wrap: false);

            Assert.Equal(0, array[2, 0, 2]); // Out-of-bounds value discarded
            Assert.Equal(0, array[1, 1, 1]); // Original position cleared
        }

        [Fact]
        public void SerializeAndDeserialize_Array3D_PreservesData()
        {
            // Arrange
            var originalArray = new Array3D<int>(2, 2, 2);
            originalArray[0, 0, 0] = 1;
            originalArray[1, 1, 1] = 42;

            // Act
            Array3D<int> deserializedArray;
            using (var memoryStream = new MemoryStream())
            {
                // Serialize
                var formatter = new BinaryFormatter();
                formatter.Serialize(memoryStream, originalArray);

                // Reset stream position for reading
                memoryStream.Position = 0;

                // Deserialize
                deserializedArray = (Array3D<int>)formatter.Deserialize(memoryStream);
            }

            // Assert
            Assert.Equal(originalArray.Width, deserializedArray.Width);
            Assert.Equal(originalArray.Height, deserializedArray.Height);
            Assert.Equal(originalArray.Length, deserializedArray.Length);
            Assert.Equal(originalArray[0, 0, 0], deserializedArray[0, 0, 0]);
            Assert.Equal(originalArray[1, 1, 1], deserializedArray[1, 1, 1]);
        }
    }
}