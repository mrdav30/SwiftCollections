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
    [InlineData("public-add", false)]
    [InlineData("public-add", true)]
    [InlineData("interface-add", false)]
    [InlineData("interface-add", true)]
    [InlineData("interface-insert", false)]
    [InlineData("interface-insert", true)]
    [InlineData("interface-replace", false)]
    [InlineData("interface-replace", true)]
    [InlineData("typed-add", false)]
    [InlineData("typed-add", true)]
    [InlineData("typed-replace", false)]
    [InlineData("typed-replace", true)]
    public void ObjectAdapter_SubscriberInvalidCastPropagatesAfterCommittedMutation(string operation, bool throwFromProperty)
    {
        var dictionary = new SwiftObservableDictionary<int, int>();
        bool replace = operation.EndsWith("replace", StringComparison.Ordinal);
        if (replace)
            dictionary.Add(1, 10);
        var failure = new InvalidCastException("Subscriber failed.");
        var notifications = new List<string>();
        dictionary.CollectionChanged += (sender, args) =>
        {
            Assert.Same(dictionary, sender);
            Assert.Equal(replace ? NotifyCollectionChangedAction.Replace : NotifyCollectionChangedAction.Add, args.Action);
            Assert.Equal(20, dictionary[1]);
            Assert.Single(dictionary);
            notifications.Add("Collection");
            if (!throwFromProperty)
                throw failure;
        };
        dictionary.PropertyChanged += (sender, args) =>
        {
            Assert.Same(dictionary, sender);
            Assert.Equal("Entries[]", args.PropertyName);
            Assert.Equal(20, dictionary[1]);
            notifications.Add("Property");
            throw failure;
        };

        InvalidCastException actual = Assert.Throws<InvalidCastException>(() =>
        {
            switch (operation)
            {
                case "public-add": dictionary.Add((object)1, (object)20); break;
                case "interface-add": ((IDictionary)dictionary).Add(1, 20); break;
                case "interface-insert":
                case "interface-replace": ((IDictionary)dictionary)[1] = 20; break;
                case "typed-add": dictionary.Add(1, 20); break;
                case "typed-replace": dictionary[1] = 20; break;
                default: throw new ArgumentOutOfRangeException(nameof(operation));
            }
        });

        Assert.Same(failure, actual);
        Assert.Equal(new KeyValuePair<int, int>(1, 20), Assert.Single(dictionary));
        Assert.Equal(throwFromProperty ? new[] { "Collection", "Property" } : new[] { "Collection" }, notifications);
    }

    [Theory]
    [InlineData("concrete")]
    [InlineData("base")]
    [InlineData("pair")]
    [InlineData("generic")]
    [InlineData("collection")]
    [InlineData("non-generic")]
    public void AddNotification_DuplicateThroughEveryContractPreservesContentsAndIteration(string contract)
    {
        var dictionary = new SwiftObservableDictionary<string, int> { { "Existing", 100 }, { "Kept", 200 } };
        KeyValuePair<string, int>[] original = dictionary.ToArray();
        int capacity = dictionary.Capacity;
        var enumerator = dictionary.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        var visited = new List<KeyValuePair<string, int>> { enumerator.Current };
        var notifications = new List<string>();
        dictionary.CollectionChanged += (_, args) => notifications.Add($"Collection:{args.Action}");
        dictionary.PropertyChanged += (_, args) => notifications.Add($"Property:{args.PropertyName}");

        bool? added = AddThroughContract(dictionary, contract, "Existing", 999);

        Assert.Equal(contract is "concrete" or "base" ? false : (bool?)null, added);
        Assert.Empty(notifications);
        Assert.Equal(original.Length, dictionary.Count);
        Assert.Equal(capacity, dictionary.Capacity);
        Assert.Equal(original, dictionary.ToArray());
        while (enumerator.MoveNext())
            visited.Add(enumerator.Current);
        Assert.Equal(original, visited);
    }

    [Theory]
    [InlineData("concrete")]
    [InlineData("base")]
    [InlineData("pair")]
    [InlineData("generic")]
    [InlineData("collection")]
    [InlineData("non-generic")]
    public void AddNotification_SuccessThroughEveryContractRaisesOneOrderedPairAfterMutation(string contract)
    {
        var dictionary = new SwiftObservableDictionary<string, int>();
        var notifications = new List<string>();
        dictionary.CollectionChanged += (sender, args) =>
        {
            Assert.Same(dictionary, sender);
            Assert.Equal(NotifyCollectionChangedAction.Add, args.Action);
            Assert.Null(args.OldItems);
            var item = Assert.IsType<KeyValuePair<string, int>>(Assert.Single(args.NewItems.Cast<object>()));
            Assert.Equal(new KeyValuePair<string, int>("Added", 300), item);
            Assert.Single(dictionary);
            Assert.Equal(300, dictionary["Added"]);
            notifications.Add("Collection:Add");
        };
        dictionary.PropertyChanged += (sender, args) =>
        {
            Assert.Same(dictionary, sender);
            Assert.Equal("Entries[]", args.PropertyName);
            Assert.Equal(300, dictionary["Added"]);
            notifications.Add($"Property:{args.PropertyName}");
        };

        bool? added = AddThroughContract(dictionary, contract, "Added", 300);

        Assert.Equal(contract is "concrete" or "base" ? true : (bool?)null, added);
        Assert.Equal(new[] { "Collection:Add", "Property:Entries[]" }, notifications);
        Assert.Single(dictionary);
        Assert.Equal(300, dictionary["Added"]);
    }

    [Fact]
    public void AddNotification_EqualKeysRetainOriginalMappingWithoutNotifications()
    {
        var dictionary = new SwiftObservableDictionary<string, int>();
        dictionary.SetComparer(StringComparer.OrdinalIgnoreCase);
        string original = new("Key".ToCharArray());
        string equivalent = new("KEY".ToCharArray());
        dictionary.Add(original, 100);
        int collectionEvents = 0, propertyEvents = 0;
        dictionary.CollectionChanged += (_, _) => collectionEvents++;
        dictionary.PropertyChanged += (_, _) => propertyEvents++;

        Assert.False(dictionary.Add(equivalent, 999));

        Assert.Equal(0, collectionEvents);
        Assert.Equal(0, propertyEvents);
        KeyValuePair<string, int> retained = Assert.Single(dictionary);
        Assert.Same(original, retained.Key);
        Assert.Equal(100, retained.Value);
        Assert.Equal(100, dictionary[equivalent]);
    }

    [Fact]
    public void AddNotification_DuplicateDoesNotInvokeThrowingSubscribers()
    {
        var dictionary = new SwiftObservableDictionary<string, int> { { "Existing", 100 } };
        dictionary.CollectionChanged += (_, _) => throw new ArithmeticException("Unexpected collection notification.");
        dictionary.PropertyChanged += (_, _) => throw new InvalidOperationException("Unexpected property notification.");

        Assert.False(dictionary.Add("Existing", 200));

        Assert.Single(dictionary);
        Assert.Equal(100, dictionary["Existing"]);
    }

    [Fact]
    public void AddNotification_InvalidInputsDoNotMutateOrNotify()
    {
        var dictionary = new SwiftObservableDictionary<string, int> { { "Existing", 100 } };
        int collectionEvents = 0, propertyEvents = 0;
        dictionary.CollectionChanged += (_, _) => collectionEvents++;
        dictionary.PropertyChanged += (_, _) => propertyEvents++;

        Assert.Throws<ArgumentNullException>(() => dictionary.Add(null, 200));
        Assert.Throws<ArgumentNullException>(() => ((IDictionary<string, int>)dictionary).Add(null, 200));
        Assert.Throws<ArgumentNullException>(() => ((ICollection<KeyValuePair<string, int>>)dictionary).Add(new(null, 200)));
        Assert.Throws<ArgumentNullException>(() => ((IDictionary)dictionary).Add(null, 200));
        Assert.Throws<ArgumentException>(() => ((IDictionary)dictionary).Add(1, 200));
        Assert.Throws<ArgumentException>(() => ((IDictionary)dictionary).Add("Added", "Wrong type"));
        Assert.Throws<ArgumentNullException>(() => ((IDictionary)dictionary).Add("Added", null));

        Assert.Equal(0, collectionEvents);
        Assert.Equal(0, propertyEvents);
        Assert.Equal(new KeyValuePair<string, int>("Existing", 100), Assert.Single(dictionary));
    }

    [Fact]
    public void AddNotification_HashFailureDoesNotMutateOrNotify()
    {
        var failure = new ArithmeticException("Hash failed.");
        var dictionary = new SwiftObservableDictionary<string, int>();
        dictionary.SetComparer(EqualityComparer<string>.Create(
            static (left, right) => StringComparer.Ordinal.Equals(left, right),
            key => key == "Bad" ? throw failure : StringComparer.Ordinal.GetHashCode(key)));
        dictionary.Add("Existing", 100);
        int collectionEvents = 0, propertyEvents = 0;
        dictionary.CollectionChanged += (_, _) => collectionEvents++;
        dictionary.PropertyChanged += (_, _) => propertyEvents++;

        Assert.Same(failure, Assert.Throws<ArithmeticException>(() => dictionary.Add("Bad", 200)));

        Assert.Equal(0, collectionEvents);
        Assert.Equal(0, propertyEvents);
        Assert.Equal(new KeyValuePair<string, int>("Existing", 100), Assert.Single(dictionary));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddNotification_SubscriberFailureDoesNotRollBackSuccessfulInsertion(bool throwFromProperty)
    {
        var dictionary = new SwiftObservableDictionary<string, int>();
        var notifications = new List<string>();
        var failure = new ArithmeticException("Subscriber failed.");
        dictionary.CollectionChanged += (_, _) =>
        {
            Assert.Equal(300, dictionary["Added"]);
            notifications.Add("Collection:Add");
            if (!throwFromProperty)
                throw failure;
        };
        dictionary.PropertyChanged += (_, _) =>
        {
            notifications.Add("Property:Entries[]");
            throw failure;
        };

        Assert.Same(failure, Assert.Throws<ArithmeticException>(() => dictionary.Add("Added", 300)));

        Assert.Single(dictionary);
        Assert.Equal(300, dictionary["Added"]);
        Assert.Equal(throwFromProperty ? new[] { "Collection:Add", "Property:Entries[]" } : new[] { "Collection:Add" }, notifications);
    }

    [Fact]
    public void AddNotification_DuplicateAtLoadThresholdMayResizeWithoutNotifying()
    {
        var dictionary = new SwiftObservableDictionary<int, int>();
        for (int i = 0; i < 6; i++)
            dictionary.Add(i, i * 10);
        int capacity = dictionary.Capacity;
        Assert.Equal(SwiftDictionary<int, int>.DefaultCapacity, capacity);
        var enumerator = dictionary.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        int collectionEvents = 0, propertyEvents = 0;
        dictionary.CollectionChanged += (_, _) => collectionEvents++;
        dictionary.PropertyChanged += (_, _) => propertyEvents++;

        Assert.False(dictionary.Add(0, 999));

        Assert.Equal(0, collectionEvents);
        Assert.Equal(0, propertyEvents);
        Assert.Equal(6, dictionary.Count);
        Assert.True(dictionary.Capacity > capacity);
        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
        for (int i = 0; i < 6; i++)
            Assert.Equal(i * 10, dictionary[i]);
    }

    [Theory]
    [InlineData(false)]
#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
    [InlineData(true)]
#endif
    public void AddNotification_RoundTripPreservesDuplicateRejectionAndSuccessfulContinuation(bool binary)
    {
        var source = new SwiftObservableDictionary<string, int> { { "Existing", 100 } };
        SwiftObservableDictionary<string, int> restored;
#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
        if (binary)
            restored = MemoryPackSerializer.Deserialize<SwiftObservableDictionary<string, int>>(MemoryPackSerializer.Serialize(source));
        else
#endif
            restored = JsonSerializer.Deserialize<SwiftObservableDictionary<string, int>>(JsonSerializer.Serialize(source));
        var notifications = new List<string>();
        restored.CollectionChanged += (_, args) => notifications.Add($"Collection:{args.Action}");
        restored.PropertyChanged += (_, args) => notifications.Add($"Property:{args.PropertyName}");

        Assert.False(restored.Add("Existing", 999));

        Assert.Empty(notifications);
        Assert.True(restored.Add("Added", 300));
        Assert.Equal(new[] { "Collection:Add", "Property:Entries[]" }, notifications);
        Assert.Equal(100, restored["Existing"]);
        Assert.Equal(300, restored["Added"]);
        Assert.Equal(2, restored.Count);
        Assert.Single(source);
    }

    private static bool? AddThroughContract(SwiftObservableDictionary<string, int> dictionary,
        string contract, string key, int value)
    {
        switch (contract)
        {
            case "concrete":
                return dictionary.Add(key, value);
            case "base":
                return ((SwiftDictionary<string, int>)dictionary).Add(key, value);
            case "pair":
                dictionary.Add(new KeyValuePair<string, int>(key, value));
                break;
            case "generic":
                ((IDictionary<string, int>)dictionary).Add(key, value);
                break;
            case "collection":
                ((ICollection<KeyValuePair<string, int>>)dictionary).Add(new(key, value));
                break;
            case "non-generic":
                ((IDictionary)dictionary).Add(key, value);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(contract));
        }
        return null;
    }

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
    public void Indexer_UnchangedValue_DoesNotRaiseCollectionChanged()
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
