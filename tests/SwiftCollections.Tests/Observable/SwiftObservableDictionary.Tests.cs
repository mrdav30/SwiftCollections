using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text.Json;
using Xunit;

#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
using MemoryPack;
#endif

namespace SwiftCollections.Observable.Tests;

public class SwiftObservableDictionaryTests
{
    [Theory]
    [InlineData("base")]
    [InlineData("generic")]
    [InlineData("non-generic")]
    public void Indexer_ThroughDictionaryContracts_PreservesNotifications(string contract)
    {
        var dictionary = new SwiftObservableDictionary<string, int> { { "Existing", 100 } };
        var collectionEvents = new List<NotifyCollectionChangedEventArgs>();
        var notifications = new List<string>();
        dictionary.CollectionChanged += (_, args) =>
        {
            collectionEvents.Add(args);
            notifications.Add($"Collection:{args.Action}");
            var item = Assert.IsType<KeyValuePair<string, int>>(args.NewItems[0]);
            Assert.Equal(item.Value, dictionary[item.Key]);
        };
        dictionary.PropertyChanged += (_, args) => notifications.Add($"Property:{args.PropertyName}");

        SetThroughDictionaryContract(dictionary, contract, "Existing", 200);

        NotifyCollectionChangedEventArgs replaced = Assert.Single(collectionEvents);
        Assert.Equal(NotifyCollectionChangedAction.Replace, replaced.Action);
        Assert.Equal(new KeyValuePair<string, int>("Existing", 100), Assert.Single(replaced.OldItems.Cast<KeyValuePair<string, int>>()));
        Assert.Equal(new KeyValuePair<string, int>("Existing", 200), Assert.Single(replaced.NewItems.Cast<KeyValuePair<string, int>>()));
        Assert.Equal(new[] { "Collection:Replace", "Property:Entries[]" }, notifications);
        Assert.Single(dictionary);
        Assert.Equal(200, dictionary["Existing"]);
        collectionEvents.Clear();
        notifications.Clear();

        SetThroughDictionaryContract(dictionary, contract, "Existing", 200);

        Assert.Empty(collectionEvents);
        Assert.Empty(notifications);
        Assert.Single(dictionary);

        SetThroughDictionaryContract(dictionary, contract, "Added", 300);

        NotifyCollectionChangedEventArgs added = Assert.Single(collectionEvents);
        Assert.Equal(NotifyCollectionChangedAction.Add, added.Action);
        Assert.Null(added.OldItems);
        Assert.Equal(new KeyValuePair<string, int>("Added", 300), Assert.Single(added.NewItems.Cast<KeyValuePair<string, int>>()));
        Assert.Equal(new[] { "Collection:Add", "Property:Entries[]" }, notifications);
        Assert.Equal(2, dictionary.Count);
        Assert.Equal(200, dictionary["Existing"]);
        Assert.Equal(300, dictionary["Added"]);
    }

    [Fact]
    public void Indexer_InvalidDictionaryContractWrites_DoNotNotifyOrMutate()
    {
        var dictionary = new SwiftObservableDictionary<string, int> { { "Existing", 100 } };
        IDictionary nongeneric = dictionary;
        bool collectionChanged = false, propertyChanged = false;
        dictionary.CollectionChanged += (_, _) => collectionChanged = true;
        dictionary.PropertyChanged += (_, _) => propertyChanged = true;

        Assert.Throws<ArgumentException>(() => nongeneric[1] = 200);
        Assert.Throws<ArgumentException>(() => nongeneric["Existing"] = "Wrong value type");
        Assert.Throws<ArgumentNullException>(() => nongeneric["Existing"] = null);
        Assert.Throws<ArgumentNullException>(() => ((SwiftDictionary<string, int>)dictionary)[null] = 200);

        Assert.False(collectionChanged);
        Assert.False(propertyChanged);
        Assert.Single(dictionary);
        Assert.Equal(100, dictionary["Existing"]);
    }

    private static void SetThroughDictionaryContract(SwiftObservableDictionary<string, int> dictionary,
        string contract, string key, int value)
    {
        switch (contract)
        {
            case "base":
                ((SwiftDictionary<string, int>)dictionary)[key] = value;
                break;
            case "generic":
                ((IDictionary<string, int>)dictionary)[key] = value;
                break;
            case "non-generic":
                ((IDictionary)dictionary)[key] = value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(contract));
        }
    }

    [Fact]
    public void Add_RaisesCollectionChangedEvent()
    {
        var dictionary = new SwiftObservableDictionary<string, int>();
        NotifyCollectionChangedEventArgs eventArgs = null;

        dictionary.CollectionChanged += (sender, e) => eventArgs = e;

        dictionary.Add("Key1", 100);

        Assert.NotNull(eventArgs);
        Assert.Equal(NotifyCollectionChangedAction.Add, eventArgs.Action);
        Assert.Single(eventArgs.NewItems.Cast<KeyValuePair<string, int>>(), kvp => kvp.Key == "Key1" && kvp.Value == 100);
    }

    [Fact]
    public void Remove_RaisesCollectionChangedEvent()
    {
        var dictionary = new SwiftObservableDictionary<string, int>
        {
            { "Key1", 100 }
        };
        NotifyCollectionChangedEventArgs eventArgs = null;

        dictionary.CollectionChanged += (sender, e) => eventArgs = e;

        bool removed = dictionary.Remove("Key1");

        Assert.True(removed);
        Assert.NotNull(eventArgs);
        Assert.Equal(NotifyCollectionChangedAction.Remove, eventArgs.Action);
        Assert.Single(eventArgs.OldItems.Cast<KeyValuePair<string, int>>(), kvp => kvp.Key == "Key1" && kvp.Value == 100);
    }

    [Fact]
    public void Remove_MissingKey_ReturnsFalseWithoutNotifications()
    {
        var dictionary = new SwiftObservableDictionary<string, int>();
        bool collectionChanged = false;
        bool propertyChanged = false;

        dictionary.CollectionChanged += (_, _) => collectionChanged = true;
        dictionary.PropertyChanged += (_, _) => propertyChanged = true;

        bool removed = dictionary.Remove("missing");

        Assert.False(removed);
        Assert.False(collectionChanged);
        Assert.False(propertyChanged);
    }

    [Fact]
    public void Indexer_MissingKey_AddsAndRaisesOneNotificationPair()
    {
        var dictionary = new SwiftObservableDictionary<string, int>();
        var collectionEvents = new List<NotifyCollectionChangedEventArgs>();
        var propertyNames = new List<string>();
        dictionary.CollectionChanged += (_, e) => collectionEvents.Add(e);
        dictionary.PropertyChanged += (_, e) => propertyNames.Add(e.PropertyName);

        dictionary["Key1"] = 100;

        NotifyCollectionChangedEventArgs eventArgs = Assert.Single(collectionEvents);
        Assert.Equal(NotifyCollectionChangedAction.Add, eventArgs.Action);
        Assert.Single(eventArgs.NewItems.Cast<KeyValuePair<string, int>>(), pair => pair.Key == "Key1" && pair.Value == 100);
        Assert.Equal(new[] { "Entries[]" }, propertyNames);
        Assert.Equal(100, dictionary["Key1"]);
    }

    [Fact]
    public void Indexer_Replacement_RaisesOneNotificationPair()
    {
        var dictionary = new SwiftObservableDictionary<string, int> { { "Key1", 100 } };
        var collectionEvents = new List<NotifyCollectionChangedEventArgs>();
        var propertyNames = new List<string>();
        dictionary.CollectionChanged += (_, e) => collectionEvents.Add(e);
        dictionary.PropertyChanged += (_, e) => propertyNames.Add(e.PropertyName);

        dictionary["Key1"] = 200;

        NotifyCollectionChangedEventArgs eventArgs = Assert.Single(collectionEvents);
        Assert.Equal(NotifyCollectionChangedAction.Replace, eventArgs.Action);
        Assert.Single(eventArgs.OldItems.Cast<KeyValuePair<string, int>>(), pair => pair.Key == "Key1" && pair.Value == 100);
        Assert.Single(eventArgs.NewItems.Cast<KeyValuePair<string, int>>(), pair => pair.Key == "Key1" && pair.Value == 200);
        Assert.Equal(new[] { "Entries[]" }, propertyNames);
        Assert.Equal(200, dictionary["Key1"]);
    }

    [Fact]
    public void Clear_RaisesResetCollectionChangedEvent()
    {
        var dictionary = new SwiftObservableDictionary<string, int>
        {
            { "Key1", 100 },
            { "Key2", 200 }
        };
        NotifyCollectionChangedEventArgs eventArgs = null;

        dictionary.CollectionChanged += (sender, e) => eventArgs = e;

        dictionary.Clear();

        Assert.NotNull(eventArgs);
        Assert.Equal(NotifyCollectionChangedAction.Reset, eventArgs.Action);
    }

    [Fact]
    public void Clear_WhenEmpty_DoesNotNotify()
    {
        var dictionary = new SwiftObservableDictionary<string, int>();
        bool collectionChanged = false;
        bool propertyChanged = false;
        dictionary.CollectionChanged += (_, _) => collectionChanged = true;
        dictionary.PropertyChanged += (_, _) => propertyChanged = true;

        dictionary.Clear();

        Assert.False(collectionChanged);
        Assert.False(propertyChanged);
    }

    [Fact]
    public void PropertyChangedEvent_RaisedOnAdd()
    {
        var dictionary = new SwiftObservableDictionary<string, int>();
        string propertyName = null;

        dictionary.PropertyChanged += (sender, e) => propertyName = e.PropertyName;

        dictionary.Add("Key1", 100);

        Assert.Equal("Entries[]", propertyName);
    }

    [Fact]
    public void PropertyChangedEvent_RaisedOnRemove()
    {
        var dictionary = new SwiftObservableDictionary<string, int>
        {
            { "Key1", 100 }
        };
        string propertyName = null;

        dictionary.PropertyChanged += (sender, e) => propertyName = e.PropertyName;

        dictionary.Remove("Key1");

        Assert.Equal("Entries[]", propertyName);
    }

    [Fact]
    public void KeyNotFoundException_OnInvalidKeyAccess()
    {
        var dictionary = new SwiftObservableDictionary<string, int>();

        Assert.Throws<KeyNotFoundException>(() => dictionary["InvalidKey"]);
    }

    [Fact]
    public void CollectionChanged_NotRaisedOnDuplicateAdd()
    {
        var dictionary = new SwiftObservableDictionary<string, int>
        {
            { "Key1", 100 }
        };
        bool eventRaised = false;

        dictionary.CollectionChanged += (sender, e) => eventRaised = true;

        dictionary["Key1"] = 100; // No change to value

        Assert.False(eventRaised);
    }

    [Fact]
    public void Count_ReflectsCorrectNumberOfItems()
    {
        var dictionary = new SwiftObservableDictionary<string, int>();
        Assert.Empty(dictionary);

        dictionary.Add("Key1", 100);
        Assert.Single(dictionary);

        dictionary.Remove("Key1");
        Assert.Empty(dictionary);
    }

    [Fact]
    public void Enumerator_ReturnsAllItems()
    {
        var dictionary = new SwiftObservableDictionary<string, int>
        {
            { "Key1", 100 },
            { "Key2", 200 }
        };

        var items = dictionary.ToList();

        Assert.Contains(new KeyValuePair<string, int>("Key1", 100), items);
        Assert.Contains(new KeyValuePair<string, int>("Key2", 200), items);
    }

    #region Serialization

    [Fact]
    public void Json_RoundTrip_PreservesValues()
    {
        var dict = new SwiftObservableDictionary<string, int>
        {
            { "A", 1 },
            { "B", 2 }
        };

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(dict);
        var result = JsonSerializer.Deserialize<SwiftObservableDictionary<string, int>>(json);

        Assert.Equal(2, result.Count);
        Assert.Equal(1, result["A"]);
        Assert.Equal(2, result["B"]);
    }

    [Fact]
    public void Json_RoundTrip_RebuildsEvents()
    {
        var dict = new SwiftObservableDictionary<string, int>
        {
            { "Key", 1 }
        };

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(dict);
        var result = JsonSerializer.Deserialize<SwiftObservableDictionary<string, int>>(json);

        NotifyCollectionChangedEventArgs args = null;
        result.CollectionChanged += (s, e) => args = e;

        result["Key"] = 2;

        Assert.NotNull(args);
        Assert.Equal(NotifyCollectionChangedAction.Replace, args.Action);
    }

#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
    [Fact]
    public void MemoryPack_RoundTrip_PreservesValues()
    {
        var dict = new SwiftObservableDictionary<string, int>
        {
            { "A", 10 },
            { "B", 20 }
        };

        byte[] bytes = MemoryPackSerializer.Serialize(dict);
        var result = MemoryPackSerializer.Deserialize<SwiftObservableDictionary<string, int>>(bytes);

        Assert.Equal(10, result["A"]);
        Assert.Equal(20, result["B"]);
    }

    [Fact]
    public void MemoryPack_RoundTrip_RebuildsEvents()
    {
        var dict = new SwiftObservableDictionary<string, int>
        {
            { "Key", 1 }
        };

        byte[] bytes = MemoryPackSerializer.Serialize(dict);
        var result = MemoryPackSerializer.Deserialize<SwiftObservableDictionary<string, int>>(bytes);

        NotifyCollectionChangedEventArgs args = null;
        result.CollectionChanged += (s, e) => args = e;

        result["Key"] = 5;

        Assert.NotNull(args);
        Assert.Equal(NotifyCollectionChangedAction.Replace, args.Action);
    }
#endif

    [Fact]
    public void Json_RoundTrip_NestedObservableCollections()
    {
        var dict = new SwiftObservableDictionary<string, SwiftObservableList<int>>
        {
            { "Numbers", new SwiftObservableList<int> { 1, 2, 3 } }
        };

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(dict);

        var result = JsonSerializer.Deserialize<
            SwiftObservableDictionary<string, SwiftObservableList<int>>
        >(json);

        Assert.Equal(new[] { 1, 2, 3 }, result["Numbers"].ToArray());
    }

    #endregion
}
