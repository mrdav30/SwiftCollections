using MemoryPack;
using System.Text.Json;
using Xunit;

namespace SwiftCollections.Observable.Tests;

public class SwiftObservablePropertyTests
{
    [Fact]
    public void Constructor_Default_InitializesWithDefaultValue()
    {
        var property = new SwiftObservableProperty<int>();
        Assert.Equal(default, property.Value);
    }

    [Fact]
    public void Constructor_Value_InitializesWithProvidedValue()
    {
        var property = new SwiftObservableProperty<int>(42);
        Assert.Equal(42, property.Value);
    }

    [Fact]
    public void Value_Set_RaisesPropertyChangedEvent()
    {
        var property = new SwiftObservableProperty<int>(42);
        string propertyName = null;
        property.PropertyChanged += (sender, e) => propertyName = e.PropertyName;

        property.Value = 100;

        Assert.Equal("Value", propertyName);
        Assert.Equal(100, property.Value);
    }

    [Fact]
    public void Value_Set_DoesNotRaiseEventWhenValueIsSame()
    {
        var property = new SwiftObservableProperty<int>(42);
        bool eventRaised = false;
        property.PropertyChanged += (sender, e) => eventRaised = true;

        property.Value = 42;

        Assert.False(eventRaised);
    }

    #region Serialization

    [Fact]
    public void JsonSerialization_RoundTrip_PreservesValue()
    {
        var property = new SwiftObservableProperty<int>(42);

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(property);

        var result = JsonSerializer.Deserialize<SwiftObservableProperty<int>>(json);

        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void MemoryPack_RoundTrip_PreservesValue()
    {
        var property = new SwiftObservableProperty<int>(42);

        byte[] bytes = MemoryPackSerializer.Serialize(property);

        var result = MemoryPackSerializer.Deserialize<SwiftObservableProperty<int>>(bytes);

        Assert.Equal(42, result.Value);
    }

    #endregion
}
