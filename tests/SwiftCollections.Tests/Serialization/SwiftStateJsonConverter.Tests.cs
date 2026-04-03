using System;
using System.Text;
using System.Text.Json;
using Xunit;

namespace SwiftCollections.Tests;

public class SwiftStateJsonConverterTests
{
    [Fact]
    public void Write_EmitsStatePayload()
    {
        var converter = new SwiftStateJsonConverter<TestStateContainer, int>(
            factory: state => new TestStateContainer(state),
            stateGetter: container => container.State);

        using var buffer = new System.IO.MemoryStream();
        using var writer = new Utf8JsonWriter(buffer);

        converter.Write(writer, new TestStateContainer(42), new JsonSerializerOptions());
        writer.Flush();

        Assert.Equal("{\"State\":42}", Encoding.UTF8.GetString(buffer.ToArray()));
    }

    [Fact]
    public void Read_RehydratesCollectionFromState()
    {
        var converter = new SwiftStateJsonConverter<TestStateContainer, int>(
            factory: state => new TestStateContainer(state),
            stateGetter: container => container.State);

        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes("{\"State\":42}"));
        reader.Read();

        TestStateContainer result = converter.Read(ref reader, typeof(TestStateContainer), new JsonSerializerOptions());

        Assert.Equal(42, result.State);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("{}")]
    [InlineData("{\"Other\":42}")]
    [InlineData("{\"State\":42,\"Other\":1}")]
    public void Read_ThrowsForInvalidPayloadShape(string json)
    {
        var converter = new SwiftStateJsonConverter<TestStateContainer, int>(
            factory: state => new TestStateContainer(state),
            stateGetter: container => container.State);

        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json));
        reader.Read();

        try
        {
            converter.Read(ref reader, typeof(TestStateContainer), new JsonSerializerOptions());
            throw new Xunit.Sdk.XunitException("Expected JsonException to be thrown.");
        }
        catch (JsonException)
        {
        }
    }

    private sealed class TestStateContainer
    {
        public TestStateContainer(int state)
        {
            State = state;
        }

        public int State { get; }
    }
}
