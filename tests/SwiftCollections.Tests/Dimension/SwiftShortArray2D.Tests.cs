using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
using MemoryPack;
#endif

namespace SwiftCollections.Dimensions.Tests;

public class SwiftShortArray2DTests
{
    [Fact]
    public void Constructor_Default_CreatesEmptyArray()
    {
        var shortArray = new SwiftShortArray2D();

        Assert.Equal(0, shortArray.Width);
        Assert.Equal(0, shortArray.Height);
        Assert.Equal(0, shortArray.Length);
    }

    [Fact]
    public void Normalize_AdjustsValuesToRange()
    {
        var shortArray = new SwiftShortArray2D(2, 2);
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
    public void Normalize_ConstantInput_MapsToMin()
    {
        var shortArray = new SwiftShortArray2D(3, 3, 10);
        shortArray.Normalize(0, 100);

        Assert.All(shortArray.InnerArray, value => Assert.Equal(0, value));
    }

    [Theory]
    [InlineData(5, 10, 20)]
    [InlineData(-5, -20, -10)]
    [InlineData(5, 20, 10)]
    [InlineData(short.MinValue, short.MaxValue, short.MinValue)]
    [InlineData(short.MaxValue, short.MinValue, short.MaxValue)]
    [InlineData(5, -7, -7)]
    public void Normalize_ConstantInput_FillsRequestedMin(short value, short min, short max)
    {
        var array = new SwiftShortArray2D(2, 3, value);
        short[] storage = array.InnerArray;

        array.Normalize(min, max);

        Assert.Same(storage, array.InnerArray);
        Assert.Equal((2, 3), (array.Width, array.Height));
        Assert.All(storage, item => Assert.Equal(min, item));
    }

    [Theory]
    [InlineData(0, 3, 10, -100, 100, -40)]
    [InlineData(0, 6, 10, -100, 0, -40)]
    [InlineData(0, 53, 100, 0, 100, 53)]
    [InlineData(9239, 9628, 11054, -31979, -8370, -26918)]
    [InlineData(0, 1, 3, -10, 10, -3)]
    [InlineData(0, 2, 3, -10, 10, 3)]
    [InlineData(0, 1, 3, 10, -10, 3)]
    [InlineData(0, 2, 3, 10, -10, -3)]
    [InlineData(0, 1, short.MaxValue, short.MinValue, short.MaxValue, -32765)]
    [InlineData(short.MinValue, 0, short.MaxValue, short.MinValue, short.MaxValue, 0)]
    [InlineData(short.MinValue, 0, short.MaxValue, short.MaxValue, short.MinValue, -1)]
    [InlineData(short.MinValue, 0, short.MaxValue, -123, -123, -123)]
    public void Normalize_InterpolatesExactlyAndTruncatesFinalResultTowardZero(
        short sourceMin, short middle, short sourceMax, short min, short max, short expectedMiddle)
    {
        var array = new SwiftShortArray2D(new short[,] { { sourceMin }, { middle }, { sourceMax } });
        short[] storage = array.InnerArray;

        array.Normalize(min, max);

        Assert.Same(storage, array.InnerArray);
        Assert.Equal((3, 1), (array.Width, array.Height));
        Assert.Equal(min, array[0, 0]);
        Assert.Equal(expectedMiddle, array[1, 0]);
        Assert.Equal(max, array[2, 0]);
    }

    [Theory]
    [InlineData(short.MinValue, short.MaxValue)]
    [InlineData(short.MaxValue, short.MinValue)]
    [InlineData(-100, 0)]
    [InlineData(0, 100)]
    public void Normalize_FullInt16Domain_MatchesExactRationalOracle(short min, short max)
    {
        var array = new SwiftShortArray2D(256, 256);
        short[] storage = array.InnerArray;
        for (int i = 0; i < storage.Length; i++)
            storage[i] = (short)(short.MinValue + i);

        array.Normalize(min, max);

        Assert.Same(storage, array.InnerArray);
        Assert.Equal((256, 256), (array.Width, array.Height));
        for (int i = 0; i < storage.Length; i++)
        {
            BigInteger numerator = (BigInteger)i * (max - min) + (BigInteger)min * ushort.MaxValue;
            short expected = (short)(numerator / ushort.MaxValue);
            Assert.Equal(expected, storage[i]);
        }
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 3)]
    [InlineData(3, 0)]
    public void Normalize_EmptyInput_PreservesShapeAndStorage(int width, int height)
    {
        var array = new SwiftShortArray2D(width, height);
        short[] storage = array.InnerArray;

        array.Normalize(20, 10);

        Assert.Equal((width, height), (array.Width, array.Height));
        Assert.Same(storage, array.InnerArray);
        Assert.Empty(storage);
    }

    [Fact]
    public void Normalize_RestoredConstantState_UsesTheSamePolicy()
    {
        var source = new SwiftShortArray2D(2, 2, 5);
        var restored = new SwiftShortArray2D(source.State);

        restored.Normalize(10, 20);

        Assert.All(restored.InnerArray, item => Assert.Equal((short)10, item));
        Assert.All(source.InnerArray, item => Assert.Equal((short)5, item));
    }

    [Fact]
    public void Scale_MultipliesValues()
    {
        var shortArray = new SwiftShortArray2D(3, 3, 2);
        shortArray.Scale(3);

        Assert.All(shortArray.InnerArray, value => Assert.Equal(6, value));
    }

    [Fact]
    public void Clone_CreatesExactCopy()
    {
        SwiftShortArray2D source = new(new short[,] { { 1, 2 }, { 3, 4 } });
        var clonedArray = source.Clone();

        Assert.Equal(2, clonedArray.Width);
        Assert.Equal(2, clonedArray.Height);
        Assert.Equal(3, clonedArray[1, 0]);
    }

    [Fact]
    public void HeightMapSimulation()
    {
        short[,] heights = { { 10, 20 }, { 30, 40 } };
        var heightMap = new SwiftShortArray2D(heights);

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
        var originalArray = new SwiftShortArray2D(3, 3, (short)0);
        originalArray[0, 0] = 10;
        originalArray[1, 1] = 20;
        originalArray[2, 2] = 30;

        // Act
        var jsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };
        var json = JsonSerializer.SerializeToUtf8Bytes(originalArray, jsonOptions);
        var deserializedArray = JsonSerializer.Deserialize<SwiftShortArray2D>(json, jsonOptions);

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

        originalArray.Normalize(-100, 200);
        deserializedArray.Normalize(-100, 200);
        Assert.Equal(originalArray.InnerArray, deserializedArray.InnerArray);
        Assert.Equal((short)-100, deserializedArray[0, 1]);
        Assert.Equal((short)0, deserializedArray[0, 0]);
        Assert.Equal((short)200, deserializedArray[2, 2]);
    }

#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
    [Fact]
    public void ShortArray2D_MemoryPackSerialization_RoundTripMaintainsData()
    {
        // Arrange
        var originalValue = new SwiftShortArray2D(3, 3, (short)0);
        originalValue[0, 0] = 10;
        originalValue[1, 1] = 20;
        originalValue[2, 2] = 30;

        // Act
        byte[] bytes = MemoryPackSerializer.Serialize(originalValue);
        SwiftShortArray2D deserializedValue = MemoryPackSerializer.Deserialize<SwiftShortArray2D>(bytes);

        // Check that deserialized values match the original
        Assert.Equal(originalValue.Length, deserializedValue.Length);
        Assert.Equal(originalValue, deserializedValue);

        originalValue.Normalize(-100, 200);
        deserializedValue.Normalize(-100, 200);
        Assert.Equal(originalValue.InnerArray, deserializedValue.InnerArray);
        Assert.Equal((short)-100, deserializedValue[0, 1]);
        Assert.Equal((short)0, deserializedValue[0, 0]);
        Assert.Equal((short)200, deserializedValue[2, 2]);
    }
#endif
}
