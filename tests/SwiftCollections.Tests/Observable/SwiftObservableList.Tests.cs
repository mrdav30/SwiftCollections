using MemoryPack;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace SwiftCollections.Observable.Tests;

public class SwiftObservableListTests
{
    [Fact]
    public void Add_RaisesCollectionChangedEvent()
    {
        var list = new SwiftObservableList<int>();
        NotifyCollectionChangedEventArgs eventArgs = null;

        list.CollectionChanged += (sender, e) => eventArgs = e;

        list.Add(42);

        Assert.NotNull(eventArgs);
        Assert.Equal(NotifyCollectionChangedAction.Add, eventArgs.Action);
        Assert.Single(eventArgs.NewItems.Cast<int>(), 42);
    }

    [Fact]
    public void Remove_RaisesCollectionChangedEvent()
    {
        var list = new SwiftObservableList<int> { 42 };
        NotifyCollectionChangedEventArgs eventArgs = null;

        list.CollectionChanged += (sender, e) => eventArgs = e;

        bool removed = list.Remove(42);

        Assert.True(removed);
        Assert.NotNull(eventArgs);
        Assert.Equal(NotifyCollectionChangedAction.Remove, eventArgs.Action);
        Assert.Single(eventArgs.OldItems.Cast<int>(), 42);
    }

    [Fact]
    public void Replace_RaisesCollectionChangedEvent()
    {
        var list = new SwiftObservableList<int> { 42 };
        NotifyCollectionChangedEventArgs eventArgs = null;

        list.CollectionChanged += (sender, e) => eventArgs = e;

        list[0] = 100;

        Assert.NotNull(eventArgs);
        Assert.Equal(NotifyCollectionChangedAction.Replace, eventArgs.Action);
        Assert.Single(eventArgs.OldItems.Cast<int>(), 42);
        Assert.Single(eventArgs.NewItems.Cast<int>(), 100);
    }

    [Fact]
    public void Clear_RaisesResetCollectionChangedEvent()
    {
        var list = new SwiftObservableList<int> { 42, 100 };
        NotifyCollectionChangedEventArgs eventArgs = null;

        list.CollectionChanged += (sender, e) => eventArgs = e;

        list.Clear();

        Assert.NotNull(eventArgs);
        Assert.Equal(NotifyCollectionChangedAction.Reset, eventArgs.Action);
    }

    [Fact]
    public void PropertyChangedEvent_RaisedOnAdd()
    {
        var list = new SwiftObservableList<int>();
        string propertyName = null;

        list.PropertyChanged += (sender, e) => propertyName = e.PropertyName;

        list.Add(42);

        Assert.Equal(nameof(list.Count), propertyName);
    }

    [Fact]
    public void PropertyChangedEvent_RaisedOnRemove()
    {
        var list = new SwiftObservableList<int> { 42 };
        string propertyName = null;

        list.PropertyChanged += (sender, e) => propertyName = e.PropertyName;

        list.Remove(42);

        Assert.Equal(nameof(list.Count), propertyName);
    }

    [Fact]
    public void PropertyChangedEvent_RaisedOnReplace()
    {
        var list = new SwiftObservableList<int> { 42 };
        string propertyName = null;

        list.PropertyChanged += (sender, e) => propertyName = e.PropertyName;

        list[0] = 100;

        Assert.Equal("InnerArray[]", propertyName);
    }

    [Fact]
    public void Enumerator_ReturnsAllItems()
    {
        var list = new SwiftObservableList<int> { 42, 100 };

        var items = list.ToList();

        Assert.Contains(42, items);
        Assert.Contains(100, items);
    }

    [Fact]
    public void Add_MultipleItems_RaisesEventForEach()
    {
        var list = new SwiftObservableList<int>();
        int eventCount = 0;

        list.CollectionChanged += (sender, e) => eventCount++;

        list.Add(42);
        list.Add(100);

        Assert.Equal(2, eventCount);
    }

    [Fact]
    public void Insert_RaisesCollectionChangedEvent()
    {
        var list = new SwiftObservableList<int> { 42 };
        NotifyCollectionChangedEventArgs eventArgs = null;

        list.CollectionChanged += (sender, e) => eventArgs = e;

        list.Insert(1, 100);

        Assert.NotNull(eventArgs);
        Assert.Equal(NotifyCollectionChangedAction.Add, eventArgs.Action);
        Assert.Single(eventArgs.NewItems.Cast<int>(), 100);
    }

    [Fact]
    public void RemoveAt_RaisesCollectionChangedEvent()
    {
        var list = new SwiftObservableList<int> { 42, 100 };
        NotifyCollectionChangedEventArgs eventArgs = null;

        list.CollectionChanged += (sender, e) => eventArgs = e;

        list.RemoveAt(0);

        Assert.NotNull(eventArgs);
        Assert.Equal(NotifyCollectionChangedAction.Remove, eventArgs.Action);
        Assert.Single(eventArgs.OldItems.Cast<int>(), 42);
    }

    [Fact]
    public void OutOfRange_ThrowsException()
    {
        var list = new SwiftObservableList<int>();

        Assert.Throws<ArgumentOutOfRangeException>(() => list[0] = 42);
        Assert.Throws<ArgumentOutOfRangeException>(() => list.RemoveAt(0));
    }

    [Fact]
    public void Events_RaisedInCorrectOrder()
    {
        var list = new SwiftObservableList<int>();
        var events = new List<string>();

        list.CollectionChanged += (sender, e) => events.Add("CollectionChanged");
        list.PropertyChanged += (sender, e) => events.Add("PropertyChanged");

        list.Add(42);

        Assert.Equal(new[] { "CollectionChanged", "PropertyChanged" }, events);
    }

    [Fact]
    public void NoEvent_OnDuplicateSet()
    {
        var list = new SwiftObservableList<int> { 42 };
        bool eventRaised = false;

        list.CollectionChanged += (sender, e) => eventRaised = true;
        list.PropertyChanged += (sender, e) => eventRaised = true;

        list[0] = 42; // No change

        Assert.False(eventRaised);
    }

    [Fact]
    public void ModifyDuringEnumeration_ThrowsException()
    {
        var list = new SwiftObservableList<int> { 42, 100 };

        var enumerator = list.GetEnumerator();
        list.Add(200); // Modify during enumeration

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void EventHandler_AddAndRemove()
    {
        var list = new SwiftObservableList<int>();
        bool eventRaised = false;

        NotifyCollectionChangedEventHandler handler = (sender, e) => eventRaised = true;

        list.CollectionChanged += handler;
        list.Add(42);

        Assert.True(eventRaised);

        eventRaised = false;
        list.CollectionChanged -= handler;
        list.Add(100);

        Assert.False(eventRaised);
    }

    [Fact]
    public void LargeList_Modifications()
    {
        const int size = 10000;
        var list = new SwiftObservableList<int>();
        int eventCount = 0;

        list.CollectionChanged += (sender, e) => eventCount++;

        for (int i = 0; i < size; i++)
        {
            list.Add(i + 1);
        }

        Assert.Equal(size, eventCount);
        Assert.Equal(size, list.Count);
    }

    [Fact]
    public void Constructors_WithCapacityAndCollection_InitializeExpectedContents()
    {
        var withCapacity = new SwiftObservableList<int>(4);
        var fromCollection = new SwiftObservableList<int>(new[] { 2, 3, 5 });

        Assert.Empty(withCapacity);
        Assert.Equal(new[] { 2, 3, 5 }, fromCollection.ToArray());
    }

    [Fact]
    public void AddRange_AddsItemsInOrder()
    {
        var list = new SwiftObservableList<int>();
        IEnumerable<int> items = new List<int> { 1, 2, 3 };

        list.AddRange(items);

        Assert.Equal(new[] { 1, 2, 3 }, list.ToArray());
        Assert.Equal(3, list.Count);
    }

    [Fact]
    public void AddRange_Array_RaisesNotificationsForEachItem()
    {
        var list = new SwiftObservableList<int>();
        var addedItems = new List<int>();
        int countNotifications = 0;

        list.CollectionChanged += (sender, e) => addedItems.Add((int)e.NewItems[0]);
        list.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(list.Count))
                countNotifications++;
        };

        list.AddRange(new[] { 1, 2, 3 });

        Assert.Equal(new[] { 1, 2, 3 }, list.ToArray());
        Assert.Equal(new[] { 1, 2, 3 }, addedItems);
        Assert.Equal(3, countNotifications);
    }

    [Fact]
    public void AddRange_ReadOnlySpan_RaisesNotificationsForEachItem()
    {
        var list = new SwiftObservableList<int>();
        var addedItems = new List<int>();
        int countNotifications = 0;

        list.CollectionChanged += (sender, e) => addedItems.Add((int)e.NewItems[0]);
        list.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(list.Count))
                countNotifications++;
        };

        list.AddRange(new[] { 4, 5, 6 }.AsSpan());

        Assert.Equal(new[] { 4, 5, 6 }, list.ToArray());
        Assert.Equal(new[] { 4, 5, 6 }, addedItems);
        Assert.Equal(3, countNotifications);
    }

    [Fact]
    public void AddRange_NullEnumerable_ThrowsArgumentNullException()
    {
        var list = new SwiftObservableList<int>();

        Assert.Throws<ArgumentNullException>(() => list.AddRange(null));
    }

    [Fact]
    public void RemoveAll_NullPredicate_ThrowsArgumentNullException()
    {
        var list = new SwiftObservableList<int>();

        Assert.Throws<ArgumentNullException>(() => list.RemoveAll(null));
    }

    [Fact]
    public void RemoveAll_NoMatches_ReturnsZeroAndLeavesListUntouched()
    {
        var list = new SwiftObservableList<int>(new[] { 1, 2, 3, 4 });

        int removed = list.RemoveAll(value => value > 10);

        Assert.Equal(0, removed);
        Assert.Equal(new[] { 1, 2, 3, 4 }, list.ToArray());
    }

    [Fact]
    public void RemoveAll_NoMatches_DoesNotRaiseNotifications()
    {
        var list = new SwiftObservableList<int>(new[] { 1, 2, 3, 4 });
        bool collectionChangedRaised = false;
        bool countChangedRaised = false;

        list.CollectionChanged += (sender, e) => collectionChangedRaised = true;
        list.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(list.Count))
                countChangedRaised = true;
        };

        int removed = list.RemoveAll(value => value > 10);

        Assert.Equal(0, removed);
        Assert.False(collectionChangedRaised);
        Assert.False(countChangedRaised);
    }

    [Fact]
    public void RemoveAll_RemovesMatchingValuesAndClearsTrailingReferences()
    {
        var list = new SwiftObservableList<string>(new[] { "keep-a", "drop", "keep-b", "drop", null });

        int removed = list.RemoveAll(value => value == "drop");

        Assert.Equal(2, removed);
        Assert.Equal(3, list.Count);
        Assert.Equal(new[] { "keep-a", "keep-b", null }, list.Take(list.Count).ToArray());
        Assert.Null(list.InnerArray[3]);
        Assert.Null(list.InnerArray[4]);
    }

    [Fact]
    public void RemoveAll_AllMatches_EmptiesTheList()
    {
        var list = new SwiftObservableList<int>(new[] { 2, 4, 6, 8 });

        int removed = list.RemoveAll(value => value % 2 == 0);

        Assert.Equal(4, removed);
        Assert.Empty(list);
    }

    [Fact]
    public void RemoveAll_WithMatches_RaisesResetAndCountChanged()
    {
        var list = new SwiftObservableList<int>(new[] { 1, 2, 3, 4 });
        NotifyCollectionChangedEventArgs args = null;
        var eventOrder = new List<string>();

        list.CollectionChanged += (sender, e) =>
        {
            args = e;
            eventOrder.Add("CollectionChanged");
        };
        list.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(list.Count))
                eventOrder.Add("PropertyChanged");
        };

        int removed = list.RemoveAll(value => value % 2 == 0);

        Assert.Equal(2, removed);
        Assert.Equal(new[] { 1, 3 }, list.ToArray());
        Assert.NotNull(args);
        Assert.Equal(NotifyCollectionChangedAction.Reset, args.Action);
        Assert.Equal(new[] { "CollectionChanged", "PropertyChanged" }, eventOrder);
    }

    [Fact]
    public void StressTest_RepeatedOperations()
    {
        var list = new SwiftObservableList<int>();

        for (int i = 0; i < 1000; i++)
        {
            list.Add(i);
            list[i] = i * 2;
        }

        for (int i = 0; i < 1000; i += 2)
        {
            list[i % 2] = i * 2;
            list.Remove(i);
        }

        Assert.Equal(501, list.Count);
    }

    [Fact]
    public void NoEvents_OnInvalidOperationsForEmptyList()
    {
        var list = new SwiftObservableList<int>();
        bool eventRaised = false;

        list.CollectionChanged += (sender, e) => eventRaised = true;

        Assert.Throws<ArgumentOutOfRangeException>(() => list.RemoveAt(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => list[0] = 42);

        Assert.False(eventRaised);
    }

    [Fact]
    public void HandlesNullValues()
    {
        var list = new SwiftObservableList<string> { null, "Test" };
        list[0] = "Updated";
        list.Remove("Test");

        Assert.Single(list);
        Assert.Equal("Updated", list[0]);
    }

    [Fact]
    public void IntegrationWithObservableDictionary()
    {
        var dictionary = new SwiftObservableDictionary<string, SwiftObservableList<int>>
        {
            { "Key1", new SwiftObservableList<int> { 1, 2, 3 } }
        };

        bool eventRaised = false;
        dictionary["Key1"].CollectionChanged += (sender, e) => eventRaised = true;

        dictionary["Key1"].Add(4);

        Assert.True(eventRaised);
        Assert.Contains(4, dictionary["Key1"]);
    }

    #region Serialization

    [Fact]
    public void Json_RoundTrip_PreservesValues()
    {
        var list = new SwiftObservableList<int> { 1, 2, 3 };

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(list);
        var result = JsonSerializer.Deserialize<SwiftObservableList<int>>(json);

        Assert.Equal(new[] { 1, 2, 3 }, result.ToArray());
    }

    [Fact]
    public void Json_RoundTrip_RebuildsEvents()
    {
        var list = new SwiftObservableList<int> { 10 };

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(list);
        var result = JsonSerializer.Deserialize<SwiftObservableList<int>>(json);

        NotifyCollectionChangedEventArgs args = null;
        result.CollectionChanged += (s, e) => args = e;

        result.Add(20);

        Assert.NotNull(args);
        Assert.Equal(NotifyCollectionChangedAction.Add, args.Action);
    }

    [Fact]
    public void Json_Deserialization_DoesNotFireEvents()
    {
        var list = new SwiftObservableList<int> { 5 };

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(list);
        var result = JsonSerializer.Deserialize<SwiftObservableList<int>>(json);

        int eventCount = 0;
        result.CollectionChanged += (s, e) => eventCount++;

        Assert.Equal(0, eventCount);
    }

    [Fact]
    public void MemoryPack_RoundTrip_PreservesValues()
    {
        var list = new SwiftObservableList<int> { 1, 2, 3 };

        byte[] bytes = MemoryPackSerializer.Serialize(list);
        var result = MemoryPackSerializer.Deserialize<SwiftObservableList<int>>(bytes);

        Assert.Equal(new[] { 1, 2, 3 }, result.ToArray());
    }

    [Fact]
    public void MemoryPack_RoundTrip_RebuildsEvents()
    {
        var list = new SwiftObservableList<int> { 10 };

        byte[] bytes = MemoryPackSerializer.Serialize(list);
        var result = MemoryPackSerializer.Deserialize<SwiftObservableList<int>>(bytes);

        NotifyCollectionChangedEventArgs args = null;
        result.CollectionChanged += (s, e) => args = e;

        result.Add(20);

        Assert.NotNull(args);
        Assert.Equal(NotifyCollectionChangedAction.Add, args.Action);
    }

    #endregion
}
