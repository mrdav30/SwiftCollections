using MemoryPack;
using System;
using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace SwiftCollections.Dimensions.Tests;

public class SwiftArray2DTests
{
    [Fact]
    public void DefaultConstructor_InitializesAnEmptyArray()
    {
        var array = new SwiftArray2D<int>();

        Assert.Equal(0, array.Width);
        Assert.Equal(0, array.Height);
        Assert.Equal(0, array.Size);
        Assert.Equal(0, array.Length);
        Assert.Empty(array.InnerArray);
    }

    [Fact]
    public void Indexing_WorksAsExpected()
    {
        var array = new SwiftArray2D<int>(3, 3);
        array[1, 1] = 42;
        Assert.Equal(42, array[1, 1]);
    }

    [Fact]
    public void Resize_PreservesExistingValues()
    {
        var array = new SwiftArray2D<int>(2, 2);
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
        var array = new SwiftArray2D<int>(3, 3, 0);
        array[1, 1] = 42;
        array.Shift(1, 1);

        Assert.Equal(0, array[1, 1]);
        Assert.Equal(42, array[2, 2]);
    }

    [Fact]
    public void Shift_WrapAround_ValidIndices()
    {
        var array = new SwiftArray2D<int>(3, 3);
        array[1, 1] = 42;

        array.Shift(1, 1);

        Assert.Equal(42, array[2, 2]); // Wrapped around
        Assert.Equal(0, array[1, 1]); // Original position cleared
    }

    [Fact]
    public void Shift_NonWrapping_DiscardOutOfBounds()
    {
        var array = new SwiftArray2D<int>(3, 3);
        array[1, 1] = 42;

        // Shift with large offsets that push the value out of bounds
        array.Shift(3, 3, wrap: false);

        Assert.Equal(0, array[1, 1]); // Original position cleared
        Assert.All(array, value => Assert.Equal(0, value)); // All values are default
    }

    [Fact]
    public void Shift_NonWrapping_WithNegativeOffsets_DiscardsOutOfBoundsEntries()
    {
        var array = new SwiftArray2D<int>(3, 3);
        array[1, 1] = 42;
        array[2, 2] = 99;

        array.Shift(-1, -1, wrap: false);

        Assert.Equal(42, array[0, 0]);
        Assert.Equal(99, array[1, 1]);
        Assert.Equal(0, array[2, 2]);
    }

    [Fact]
    public void Fill_SetsAllElements()
    {
        var array = new SwiftArray2D<int>(2, 2);
        array.Fill(99);

        Assert.All(array.InnerArray, item => Assert.Equal(99, item));
    }

    [Fact]
    public void OutOfBoundsAccess_ThrowsException()
    {
        var array = new SwiftArray2D<int>(3, 3);
        Assert.Throws<IndexOutOfRangeException>(() => array[-1, 0]);
        Assert.Throws<IndexOutOfRangeException>(() => array[3, 3]);
    }

    [Fact]
    public void Resize_SmallerDimensions_TrimsData()
    {
        var array = new SwiftArray2D<int>(4, 4);
        array.Fill(42);
        array.Resize(2, 2);

        Assert.Equal(2, array.Width);
        Assert.Equal(2, array.Height);
        Assert.All(array.InnerArray, item => Assert.Equal(42, item));
    }

    [Fact]
    public void AddRange_CopiesSourceAndResizesTheArray()
    {
        var array = new SwiftArray2D<int>(1, 1);
        int[,] source =
        {
            { 1, 2 },
            { 3, 4 }
        };

        array.AddRange(source);

        Assert.Equal(2, array.Width);
        Assert.Equal(2, array.Height);
        Assert.Equal(source, array.ToArray());
    }

    [Fact]
    public void Clear_ResetsAllElementsToDefault()
    {
        var array = new SwiftArray2D<int>(2, 2, 7);

        array.Clear();

        Assert.All(array.InnerArray, value => Assert.Equal(0, value));
    }

    [Fact]
    public void Resize_WithSameDimensions_DoesNotReplaceTheBackingArray()
    {
        var array = new SwiftArray2D<int>(2, 3);
        int[] originalBackingArray = array.InnerArray;

        array.Resize(2, 3);

        Assert.Same(originalBackingArray, array.InnerArray);
    }

    [Fact]
    public void ToArray_ReturnsACopyOfTheCurrentState()
    {
        var array = new SwiftArray2D<int>(2, 2);
        array[0, 0] = 1;
        array[0, 1] = 2;
        array[1, 0] = 3;
        array[1, 1] = 4;

        int[,] copy = array.ToArray();

        Assert.Equal(1, copy[0, 0]);
        Assert.Equal(2, copy[0, 1]);
        Assert.Equal(3, copy[1, 0]);
        Assert.Equal(4, copy[1, 1]);

        copy[0, 0] = 99;

        Assert.Equal(1, array[0, 0]);
    }

    [Fact]
    public void NonGenericEnumerator_ReturnsItemsInTraversalOrder()
    {
        var array = new SwiftArray2D<int>(2, 2);
        array[0, 0] = 1;
        array[0, 1] = 2;
        array[1, 0] = 3;
        array[1, 1] = 4;

        var enumerator = ((IEnumerable)array).GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.Equal(1, enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(2, enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(3, enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(4, enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void LargeArray_PerformanceAndCorrectness()
    {
        const int size = 1000;
        var array = new SwiftArray2D<int>(size, size);
        array.Fill(99);

        Assert.Equal(size * size, array.InnerArray.Length);
        Assert.All(array.InnerArray, value => Assert.Equal(99, value));
    }

    [Fact]
    public void SerializeDeserialize_MaintainsDataAndStructure()
    {
        // Arrange
        var originalArray = new SwiftArray2D<int>(3, 3);
        originalArray[0, 0] = 42;
        originalArray[1, 1] = 99;
        originalArray[2, 2] = 7;

        var jsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };
        var json = JsonSerializer.SerializeToUtf8Bytes(originalArray, jsonOptions);
        var deserializedArray = JsonSerializer.Deserialize<SwiftArray2D<int>>(json, jsonOptions);

        // Assert
        Assert.Equal(originalArray.Width, deserializedArray.Width);
        Assert.Equal(originalArray.Height, deserializedArray.Height);

        for (int x = 0; x < originalArray.Width; x++)
        {
            for (int y = 0; y < originalArray.Height; y++)
                Assert.Equal(originalArray[x, y], deserializedArray[x, y]);
        }
    }

    [Fact]
    public void Array2D_MemoryPackSerialization_RoundTripMaintainsData()
    {
        // Arrange
        var originalValue = new SwiftArray2D<int>(3, 3);
        originalValue[0, 0] = 42;
        originalValue[1, 1] = 99;
        originalValue[2, 2] = 7;

        // Act
        byte[] bytes = MemoryPackSerializer.Serialize(originalValue);
        SwiftArray2D<int> deserializedValue = MemoryPackSerializer.Deserialize<SwiftArray2D<int>>(bytes);

        // Check that deserialized values match the original
        Assert.Equal(originalValue.Length, deserializedValue.Length);
        Assert.Equal(originalValue, deserializedValue);
    }
}
