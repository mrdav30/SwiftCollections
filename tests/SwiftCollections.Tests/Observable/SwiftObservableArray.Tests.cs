using MemoryPack;
using System;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace SwiftCollections.Observable.Tests;

public class SwiftObservableArrayTests
{
    [Fact]
    public void Constructor_Capacity_InitializesArray()
    {
        var array = new SwiftObservableArray<int>(5);
        Assert.Equal(5, array.Capacity);
        Assert.All(array.ToArray(), value => Assert.Equal(default, value));
    }

    [Fact]
    public void Constructor_NullObservableProperties_ThrowsException()
    {
        Assert.Throws<ArgumentNullException>(() => new SwiftObservableArray<int>(null));
    }

    [Fact]
    public void Indexer_Get_ReturnsCorrectValue()
    {
        var array = new SwiftObservableArray<int>(5);
        array[2] = 42;

        Assert.Equal(42, array[2]);
    }

    [Fact]
    public void Indexer_Set_RaisesElementChangedEvent()
    {
        var array = new SwiftObservableArray<int>(3);
        int eventIndex = -1;
        int newValue = -1;

        array.ElementChanged += (sender, args) =>
        {
            eventIndex = args.Index;
            newValue = args.NewValue;
        };

        array[1] = 99;

        Assert.Equal(1, eventIndex);
        Assert.Equal(99, newValue);
    }

    [Fact]
    public void Indexer_Set_DoesNotRaiseEventWhenValueIsSame()
    {
        var array = new SwiftObservableArray<int>(3);
        bool eventRaised = false;

        array.ElementChanged += (sender, args) => eventRaised = true;

        array[1] = default;

        Assert.False(eventRaised);
    }

    [Fact]
    public void ToArray_ReturnsCurrentStateOfArray()
    {
        var array = new SwiftObservableArray<int>(3);
        array[0] = 10;
        array[1] = 20;
        array[2] = 30;

        var result = array.ToArray();

        Assert.Equal(new[] { 10, 20, 30 }, result);
    }

    [Fact]
    public void InvalidIndex_ThrowsException()
    {
        var array = new SwiftObservableArray<int>(3);

        Assert.Throws<IndexOutOfRangeException>(() => array[-1]);
        Assert.Throws<IndexOutOfRangeException>(() => array[3]);
    }

    [Fact]
    public void ElementChanged_FiresOnPropertyChange()
    {
        var array = new SwiftObservableArray<string>(3);
        array[0] = "Hello";

        string changedValue = null;
        array.ElementChanged += (sender, args) =>
        {
            if (args.Index == 0)
                changedValue = args.NewValue;
        };

        array[0] = "World";

        Assert.Equal("World", changedValue);
    }

    [Fact]
    public void PropertyChanged_CompatibilityEventFires()
    {
        var array = new SwiftObservableArray<int>(3);
        string propertyName = null;

        array.PropertyChanged += (sender, e) => propertyName = e.PropertyName;

        array[1] = 100;

        Assert.Equal("Items[]", propertyName);
    }

    [Fact]
    public void HighCapacityInitialization_PerformsReasonably()
    {
        var array = new SwiftObservableArray<int>(100000);
        Assert.Equal(100000, array.Capacity);
        Assert.All(array.ToArray(), value => Assert.Equal(default, value));
    }

    [Fact]
    public void CascadingUpdates_DoNotCreateInfiniteLoops()
    {
        var array = new SwiftObservableArray<int>(3);

        // Cascading logic
        array.ElementChanged += (sender, args) =>
        {
            if (args.Index == 0)
            {
                array[1] = array[0] * 2;
            }
        };

        // This should not create an infinite loop
        array[0] = 10;

        Assert.Equal(10, array[0]);
        Assert.Equal(20, array[1]);
        Assert.Equal(0, array[2]);
    }

    [Fact]
    public void FrequentUpdates_TriggerAllEvents()
    {
        const int size = 1000;
        var array = new SwiftObservableArray<int>(size);
        int eventCount = 0;

        array.ElementChanged += (sender, args) => eventCount++;

        // Update all elements to a different value
        for (int i = 0; i < size; i++)
        {
            array[i] = i + 1; // Ensures the new value is always different
        }

        Assert.Equal(size, eventCount);
        Assert.Equal(Enumerable.Range(1, size).ToArray(), array.ToArray());
    }

    [Fact]
    public void NullValueHandling_RaisesEventsCorrectly()
    {
        var property = new SwiftObservableProperty<string>("Hello");
        string changedValue = null;

        property.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == "Value")
                changedValue = property.Value;
        };

        // Change to null
        property.Value = null;

        Assert.Null(changedValue);

        // Change back to non-null
        property.Value = "World";

        Assert.Equal("World", changedValue);
    }

    [Fact]
    public void MultipleSubscribers_HandleEventsCorrectly()
    {
        var array = new SwiftObservableArray<int>(3);
        int subscriber1Count = 0;
        int subscriber2Count = 0;

        array.ElementChanged += (sender, args) => subscriber1Count++;
        array.ElementChanged += (sender, args) => subscriber2Count++;

        array[0] = 42;

        Assert.Equal(1, subscriber1Count);
        Assert.Equal(1, subscriber2Count);
    }

    #region Serialization

    [Fact]
    public void JsonSerialization_RoundTrip_PreservesValues()
    {
        var array = new SwiftObservableArray<int>(3);
        array[0] = 10;
        array[1] = 20;
        array[2] = 30;

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(array);

        var result = JsonSerializer.Deserialize<SwiftObservableArray<int>>(json);

        Assert.Equal(new[] { 10, 20, 30 }, result.ToArray());
    }

    [Fact]
    public void JsonSerialization_RoundTrip_RebuildsEventSystem()
    {
        var array = new SwiftObservableArray<int>(2);
        array[0] = 5;

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(array);

        var result = JsonSerializer.Deserialize<SwiftObservableArray<int>>(json);

        int eventIndex = -1;
        result.ElementChanged += (s, e) => eventIndex = e.Index;

        result[0] = 99;

        Assert.Equal(0, eventIndex);
    }

    [Fact]
    public void JsonSerialization_DoesNotFireEventsDuringDeserialization()
    {
        var array = new SwiftObservableArray<int>(2);
        array[0] = 10;

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(array);

        int eventCount = 0;

        var result = JsonSerializer.Deserialize<SwiftObservableArray<int>>(json);

        result.ElementChanged += (s, e) => eventCount++;

        Assert.Equal(0, eventCount);
    }

    [Fact]
    public void JsonSerialization_PreservesNullValues()
    {
        var array = new SwiftObservableArray<string>(3);
        array[0] = "Hello";
        array[1] = null;
        array[2] = "World";

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(array);

        var result = JsonSerializer.Deserialize<SwiftObservableArray<string>>(json);

        Assert.Equal("Hello", result[0]);
        Assert.Null(result[1]);
        Assert.Equal("World", result[2]);
    }

    [Fact]
    public void JsonSerialization_StateStructure_IsCorrect()
    {
        var array = new SwiftObservableArray<int>(2);
        array[0] = 10;
        array[1] = 20;

        string json = JsonSerializer.Serialize(array);

        Assert.Contains("State", json);
        Assert.Contains("Items", json);
    }

    [Fact]
    public void MemoryPack_RoundTrip_PreservesValues()
    {
        var array = new SwiftObservableArray<int>(3);
        array[0] = 1;
        array[1] = 2;
        array[2] = 3;

        byte[] bytes = MemoryPackSerializer.Serialize(array);

        var result = MemoryPackSerializer.Deserialize<SwiftObservableArray<int>>(bytes);

        Assert.Equal(new[] { 1, 2, 3 }, result.ToArray());
    }

    [Fact]
    public void MemoryPack_RoundTrip_RebuildsEventSystem()
    {
        var array = new SwiftObservableArray<int>(2);
        array[1] = 50;

        byte[] bytes = MemoryPackSerializer.Serialize(array);

        var result = MemoryPackSerializer.Deserialize<SwiftObservableArray<int>>(bytes);

        int index = -1;
        result.ElementChanged += (s, e) => index = e.Index;

        result[1] = 99;

        Assert.Equal(1, index);
    }

    [Fact]
    public void MemoryPack_DoesNotFireEventsDuringDeserialization()
    {
        var array = new SwiftObservableArray<int>(2);
        array[0] = 123;

        byte[] bytes = MemoryPackSerializer.Serialize(array);

        var result = MemoryPackSerializer.Deserialize<SwiftObservableArray<int>>(bytes);

        int eventCount = 0;
        result.ElementChanged += (s, e) => eventCount++;

        Assert.Equal(0, eventCount);
    }

    #endregion
}
