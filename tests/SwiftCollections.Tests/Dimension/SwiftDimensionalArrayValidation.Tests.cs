using System;
using System.Text.Json;
using Xunit;

#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
using System.Buffers;
using MemoryPack;
#endif

namespace SwiftCollections.Dimensions.Tests;

public class SwiftDimensionalArrayValidationTests
{
    [Theory]
    [InlineData(65536, 65536)] // The unchecked product wraps to zero.
    [InlineData(65536, 65537)] // The unchecked product wraps to a positive length.
    [InlineData(65536, 32768)] // The unchecked product wraps to a negative length.
    public void Array2D_Constructors_RejectUnrepresentableProducts(int width, int height)
    {
        Assert.Throws<OverflowException>(() => new SwiftArray2D<int>(width, height));
        Assert.Throws<OverflowException>(() => new SwiftArray2D<int>(width, height, 7));
    }

    [Theory]
    [InlineData(65536, 65536)]
    [InlineData(65536, 65537)]
    [InlineData(65536, 32768)]
    public void Array2DSpecializations_Constructors_RejectUnrepresentableProducts(int width, int height)
    {
        Assert.Throws<OverflowException>(() => new SwiftBoolArray2D(width, height));
        Assert.Throws<OverflowException>(() => new SwiftBoolArray2D(width, height, true));
        Assert.Throws<OverflowException>(() => new SwiftShortArray2D(width, height));
        Assert.Throws<OverflowException>(() => new SwiftShortArray2D(width, height, 7));
    }

    [Theory]
    [InlineData(-1, 0, 0, typeof(ArgumentOutOfRangeException))]
    [InlineData(2, 2, 1, typeof(ArgumentException))]
    [InlineData(65536, 65536, 0, typeof(OverflowException))]
    public void Array2DSpecializations_Restore_RejectsInvalidShapes(int width, int height, int length, Type exceptionType)
    {
        var boolState = new Array2DState<bool>(width, height, new bool[length]);
        var shortState = new Array2DState<short>(width, height, new short[length]);
        Assert.Throws(exceptionType, () => new SwiftBoolArray2D(boolState));
        Assert.Throws(exceptionType, () => new SwiftShortArray2D(shortState));

        string boolJson = JsonSerializer.Serialize(new { State = boolState });
        string shortJson = JsonSerializer.Serialize(new { State = shortState });
        Assert.Throws(exceptionType, () => JsonSerializer.Deserialize<SwiftBoolArray2D>(boolJson));
        Assert.Throws(exceptionType, () => JsonSerializer.Deserialize<SwiftShortArray2D>(shortJson));
    }

    [Theory]
    [InlineData(65536, 65536, 1)]
    [InlineData(65536, 65537, 1)]
    [InlineData(65536, 32768, 1)]
    [InlineData(65536, 1, 65536)]
    [InlineData(1, 65536, 65536)]
    [InlineData(int.MaxValue, int.MaxValue, 65536)] // The exact product also exceeds Int64.
    public void Array3D_Constructors_RejectUnrepresentableProducts(int width, int height, int depth)
    {
        Assert.Throws<OverflowException>(() => new SwiftArray3D<int>(width, height, depth));
        Assert.Throws<OverflowException>(() => new SwiftArray3D<int>(width, height, depth, 7));
    }

    [Theory]
    [InlineData(65536, 65536)]
    [InlineData(65536, 65537)]
    [InlineData(65536, 32768)]
    public void Array2D_Resize_RejectsUnrepresentableProductsWithoutMutation(int width, int height)
    {
        var array = new SwiftArray2D<int>(new[,] { { 1, 2 }, { 3, 4 } });
        int[] storage = array.InnerArray;

        Assert.Throws<OverflowException>(() => array.Resize(width, height));

        AssertUnchanged(array, storage);
    }

    [Theory]
    [InlineData(65536, 65536, 1)]
    [InlineData(65536, 65537, 1)]
    [InlineData(65536, 32768, 1)]
    [InlineData(65536, 1, 65536)]
    [InlineData(1, 65536, 65536)]
    [InlineData(int.MaxValue, int.MaxValue, 65536)]
    public void Array3D_Resize_RejectsUnrepresentableProductsWithoutMutation(int width, int height, int depth)
    {
        var array = new SwiftArray3D<int>(new Array3DState<int>(2, 2, 2, new[] { 1, 2, 3, 4, 5, 6, 7, 8 }));

        Assert.Throws<OverflowException>(() => array.Resize(width, height, depth));

        AssertUnchanged(array);
    }

    [Theory]
    [InlineData(-1, 2, 0, 2)]
    [InlineData(2, -1, 2, 0)]
    [InlineData(-1, -1, 0, 0)]
    [InlineData(int.MinValue, int.MaxValue, 0, int.MaxValue)]
    public void Array2D_Constructor_PreservesNegativeClamping(int width, int height, int expectedWidth, int expectedHeight)
    {
        var array = new SwiftArray2D<int>(width, height, 7);

        Assert.Equal(expectedWidth, array.Width);
        Assert.Equal(expectedHeight, array.Height);
        Assert.Equal(0, array.Size);
        Assert.Empty(array.InnerArray);
    }

    [Theory]
    [InlineData(-1, 2)]
    [InlineData(2, -1)]
    [InlineData(-1, -1)]
    [InlineData(int.MinValue, 0)]
    public void Array2D_Resize_RejectsNegativeDimensionsWithoutMutation(int width, int height)
    {
        var array = new SwiftArray2D<int>(new[,] { { 1, 2 }, { 3, 4 } });
        int[] storage = array.InnerArray;

        Assert.Throws<ArgumentOutOfRangeException>(() => array.Resize(width, height));

        AssertUnchanged(array, storage);
    }

    [Theory]
    [InlineData(-1, -1, 1)]
    [InlineData(-1, 1, 0)]
    [InlineData(1, -1, 0)]
    [InlineData(0, 1, -1)]
    [InlineData(0, 0, int.MinValue)]
    public void Array3D_ConstructorsAndResize_RejectNegativeDimensions(int width, int height, int depth)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SwiftArray3D<int>(width, height, depth));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SwiftArray3D<int>(width, height, depth, 7));

        var array = new SwiftArray3D<int>(new Array3DState<int>(2, 2, 2, new[] { 1, 2, 3, 4, 5, 6, 7, 8 }));

        Assert.Throws<ArgumentOutOfRangeException>(() => array.Resize(width, height, depth));

        AssertUnchanged(array);
    }

    [Theory]
    [InlineData(-1, 0, 0, typeof(ArgumentOutOfRangeException))]
    [InlineData(0, -1, 0, typeof(ArgumentOutOfRangeException))]
    [InlineData(-1, -1, 1, typeof(ArgumentOutOfRangeException))]
    [InlineData(2, 2, 1, typeof(ArgumentException))]
    [InlineData(2, 2, 5, typeof(ArgumentException))]
    [InlineData(0, 2, 1, typeof(ArgumentException))]
    [InlineData(65536, 65536, 0, typeof(OverflowException))]
    [InlineData(65536, 65537, 1, typeof(OverflowException))]
    public void Array2D_Restore_RejectsInvalidShapesWithoutMutation(int width, int height, int length, Type exceptionType)
    {
        var state = new Array2DState<int>(width, height, new int[length]);
        Assert.Throws(exceptionType, () => new SwiftArray2D<int>(state));

        var array = new SwiftArray2D<int>(new[,] { { 1, 2 }, { 3, 4 } });
        int[] storage = array.InnerArray;

        Assert.Throws(exceptionType, () => array.State = state);

        AssertUnchanged(array, storage);
    }

    [Theory]
    [InlineData(-1, -1, 1, 1, typeof(ArgumentOutOfRangeException))]
    [InlineData(-1, 0, 1, 0, typeof(ArgumentOutOfRangeException))]
    [InlineData(0, -1, 1, 0, typeof(ArgumentOutOfRangeException))]
    [InlineData(1, 0, -1, 0, typeof(ArgumentOutOfRangeException))]
    [InlineData(2, 2, 2, 1, typeof(ArgumentException))]
    [InlineData(2, 2, 2, 9, typeof(ArgumentException))]
    [InlineData(2, 2, 0, 1, typeof(ArgumentException))]
    [InlineData(65536, 65536, 1, 0, typeof(OverflowException))]
    [InlineData(int.MaxValue, int.MaxValue, 65536, 1, typeof(OverflowException))]
    public void Array3D_Restore_RejectsInvalidShapesWithoutMutation(int width, int height, int depth, int length, Type exceptionType)
    {
        var state = new Array3DState<int>(width, height, depth, new int[length]);
        Assert.Throws(exceptionType, () => new SwiftArray3D<int>(state));

        var array = new SwiftArray3D<int>(new Array3DState<int>(2, 2, 2, new[] { 1, 2, 3, 4, 5, 6, 7, 8 }));

        Assert.Throws(exceptionType, () => array.State = state);

        AssertUnchanged(array);
    }

    [Fact]
    public void Restore_NullData_PreservesExistingExceptionsAndInstances()
    {
        Assert.Throws<ArgumentNullException>(() => new SwiftArray2D<int>(default(Array2DState<int>)));
        Assert.Throws<NullReferenceException>(() => new SwiftArray3D<int>(default(Array3DState<int>)));

        var array2D = new SwiftArray2D<int>(new[,] { { 1, 2 }, { 3, 4 } });
        var array3D = new SwiftArray3D<int>(new Array3DState<int>(2, 2, 2, new[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
        int[] storage2D = array2D.InnerArray;

        Assert.Throws<ArgumentNullException>(() => array2D.State = default);
        Assert.Throws<NullReferenceException>(() => array3D.State = default);

        AssertUnchanged(array2D, storage2D);
        AssertUnchanged(array3D);
    }

    [Theory]
    [InlineData(0, int.MaxValue)]
    [InlineData(int.MaxValue, 0)]
    public void Array2D_ZeroAxis_AcceptsLargeOtherDimension(int width, int height)
    {
        var array = new SwiftArray2D<int>(width, height, 7);
        var restored = new SwiftArray2D<int>(new Array2DState<int>(width, height, Array.Empty<int>()));
        var resized = new SwiftArray2D<int>(2, 2, 7);
        resized.Resize(width, height);

        foreach (var result in new[] { array, restored, resized })
        {
            Assert.Equal(width, result.Width);
            Assert.Equal(height, result.Height);
            Assert.Equal(0, result.Size);
            Assert.Equal(0, result.Length);
            Assert.Empty(result.InnerArray);
        }
    }

    [Theory]
    [InlineData(0, int.MaxValue, int.MaxValue)]
    [InlineData(int.MaxValue, 0, int.MaxValue)]
    [InlineData(int.MaxValue, int.MaxValue, 0)]
    public void Array3D_ZeroAxis_AcceptsLargeOtherDimensions(int width, int height, int depth)
    {
        var array = new SwiftArray3D<int>(width, height, depth, 7);
        var restored = new SwiftArray3D<int>(new Array3DState<int>(width, height, depth, Array.Empty<int>()));
        var resized = new SwiftArray3D<int>(2, 2, 2, 7);
        resized.Resize(width, height, depth);

        foreach (var result in new[] { array, restored, resized })
        {
            Assert.Equal(width, result.Width);
            Assert.Equal(height, result.Height);
            Assert.Equal(depth, result.Depth);
            Assert.Equal(0, result.Size);
            Assert.Equal(0, result.Length);
            Assert.Empty(result.State.Data);
        }
    }

    [Fact]
    public void Restore_ValidState_CopiesStorageAndPreservesCoordinateLayout()
    {
        int[] data2D = { 1, 2, 3, 4, 5, 6 };
        int[] data3D = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
        var state2D = new Array2DState<int>(2, 3, data2D);
        var state3D = new Array3DState<int>(2, 3, 2, data3D);
        Assert.Same(data2D, state2D.Data);
        Assert.Same(data3D, state3D.Data);

        var array2D = new SwiftArray2D<int>(state2D);
        var array3D = new SwiftArray3D<int>(state3D);
        Assert.NotSame(data2D, array2D.InnerArray);
        Assert.NotSame(data3D, array3D.State.Data);
        Assert.Equal(6, array2D[1, 2]);
        Assert.Equal(12, array3D[1, 2, 1]);

        data2D[5] = data3D[11] = -1;
        Assert.Equal(6, array2D[1, 2]);
        Assert.Equal(12, array3D[1, 2, 1]);
        array2D.State.Data[5] = array3D.State.Data[11] = -2;
        Assert.Equal(6, array2D[1, 2]);
        Assert.Equal(12, array3D[1, 2, 1]);
    }

    [Fact]
    public void JsonRestore_RejectsDimensionDataMismatch()
    {
        Assert.Throws<ArgumentException>(() => JsonSerializer.Deserialize<SwiftArray2D<int>>(
            "{\"State\":{\"Width\":2,\"Height\":2,\"Data\":[7]}}"));
        Assert.Throws<ArgumentException>(() => JsonSerializer.Deserialize<SwiftArray3D<int>>(
            "{\"State\":{\"Width\":2,\"Height\":2,\"Depth\":2,\"Data\":[7]}}"));
    }

    [Fact]
    public void Serialization_EmptyArrays_PreserveNonzeroAxes()
    {
        var array2D = new SwiftArray2D<int>(0, 3);
        var array3D = new SwiftArray3D<int>(2, 0, 3);
        var json2D = JsonSerializer.Deserialize<SwiftArray2D<int>>(JsonSerializer.Serialize(array2D));
        var json3D = JsonSerializer.Deserialize<SwiftArray3D<int>>(JsonSerializer.Serialize(array3D));

        Assert.Equal((0, 3, 0, 0), (json2D.Width, json2D.Height, json2D.Size, json2D.Length));
        Assert.Equal((2, 0, 3, 0, 0), (json3D.Width, json3D.Height, json3D.Depth, json3D.Size, json3D.Length));

#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
        var memoryPack2D = MemoryPackSerializer.Deserialize<SwiftArray2D<int>>(MemoryPackSerializer.Serialize(array2D));
        var memoryPack3D = MemoryPackSerializer.Deserialize<SwiftArray3D<int>>(MemoryPackSerializer.Serialize(array3D));

        Assert.Equal((0, 3, 0, 0), (memoryPack2D.Width, memoryPack2D.Height, memoryPack2D.Size, memoryPack2D.Length));
        Assert.Equal((2, 0, 3, 0, 0), (memoryPack3D.Width, memoryPack3D.Height, memoryPack3D.Depth, memoryPack3D.Size, memoryPack3D.Length));
#endif
    }

#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
    [Fact]
    public void MemoryPackOverwrite_Invalid2DState_DoesNotMutateExistingInstance()
    {
        var array = new SwiftArray2D<int>(new[,] { { 1, 2 }, { 3, 4 } });
        var original = array;
        int[] storage = array.InnerArray;
        byte[] bytes = SerializeStateEnvelope(new Array2DState<int>(3, 3, new[] { 7 }));

        Assert.Throws<ArgumentException>(() => MemoryPackSerializer.Deserialize<SwiftArray2D<int>>(bytes));
        Assert.Throws<ArgumentException>(() => MemoryPackSerializer.Deserialize(bytes, ref array));

        Assert.Same(original, array);
        AssertUnchanged(array, storage);
    }

    [Fact]
    public void MemoryPackOverwrite_Invalid3DState_DoesNotMutateExistingInstance()
    {
        var array = new SwiftArray3D<int>(new Array3DState<int>(2, 2, 2, new[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
        var original = array;
        byte[] bytes = SerializeStateEnvelope(new Array3DState<int>(3, 3, 3, new[] { 7 }));

        Assert.Throws<ArgumentException>(() => MemoryPackSerializer.Deserialize<SwiftArray3D<int>>(bytes));
        Assert.Throws<ArgumentException>(() => MemoryPackSerializer.Deserialize(bytes, ref array));

        Assert.Same(original, array);
        AssertUnchanged(array);
    }

    private static byte[] SerializeStateEnvelope<TState>(TState state)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var optionalState = MemoryPackWriterOptionalStatePool.Rent(MemoryPackSerializerOptions.Default);
        var writer = new MemoryPackWriter<ArrayBufferWriter<byte>>(ref buffer, optionalState);
        writer.WriteObjectHeader(1);
        writer.WriteValue(state);
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }
#endif

    private static void AssertUnchanged(SwiftArray2D<int> array, int[] storage)
    {
        Assert.Equal((2, 2, 4, 4), (array.Width, array.Height, array.Size, array.Length));
        Assert.Same(storage, array.InnerArray);
        Assert.Equal(new[] { 1, 2, 3, 4 }, array.InnerArray);
    }

    private static void AssertUnchanged(SwiftArray3D<int> array)
    {
        Assert.Equal((2, 2, 2, 8, 8), (array.Width, array.Height, array.Depth, array.Size, array.Length));
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8 }, array.State.Data);
    }
}
