using MemoryPack;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace SwiftCollections.Dimensions.Tests;

public class SwiftShortArray2DTests
{
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
    public void Normalize_SingleValue_NoChange()
    {
        var shortArray = new SwiftShortArray2D(3, 3, 10);
        shortArray.Normalize(0, 100);

        Assert.All(shortArray.InnerArray, value => Assert.Equal(0, value));
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
    }

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
    }
}
