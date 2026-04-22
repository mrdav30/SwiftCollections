using MemoryPack;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace SwiftCollections.Tests;

public class SwiftDictionaryTests
{
    [Fact]
    public void Constructor_Default_CreatesEmptyDictionary()
    {
        var dictionary = new SwiftDictionary<int, string>();
        Assert.Empty(dictionary);
    }

    [Fact]
    public void Constructor_DefaultStringComparer_UsesDeterministicHashesAcrossInstances()
    {
        var first = new SwiftDictionary<string, int>();
        var second = new SwiftDictionary<string, int>();

        Assert.NotSame(EqualityComparer<string>.Default, first.Comparer);
        Assert.Equal(first.Comparer.GetHashCode("Hello"), second.Comparer.GetHashCode("Hello"));
    }

    [Fact]
    public void Constructor_DefaultObjectComparer_UsesDeterministicStringHashesAcrossInstances()
    {
        var first = new SwiftDictionary<object, int>();
        var second = new SwiftDictionary<object, int>();

        Assert.NotSame(EqualityComparer<object>.Default, first.Comparer);
        Assert.Equal(first.Comparer.GetHashCode("Hello"), second.Comparer.GetHashCode("Hello"));
    }

    [Fact]
    public void Constructor_WithCapacity_CreatesDictionaryWithCapacity()
    {
        int capacity = 100;
        var dictionary = new SwiftDictionary<int, string>(capacity);
        Assert.Empty(dictionary);
        // Internally, capacity is set. No direct way to test, but ensure no errors occur.
    }

    [Fact]
    public void Constructor_WithIDictionary_CopiesElements()
    {
        var source = new Dictionary<int, string>
        {
            [1] = "One",
            [2] = "Two"
        };
        var dictionary = new SwiftDictionary<int, string>(source);

        Assert.Equal(source.Count, dictionary.Count);
        foreach (var kvp in source)
        {
            Assert.Equal(kvp.Value, dictionary[kvp.Key]);
        }
    }

    [Fact]
    public void Constructor_WithIEnumerable_CopiesElements()
    {
        var source = new List<KeyValuePair<int, string>>
        {
            new(1, "One"),
            new(2, "Two")
        };
        var dictionary = new SwiftDictionary<int, string>(source);

        Assert.Equal(source.Count, dictionary.Count);
        foreach (var kvp in source)
        {
            Assert.Equal(kvp.Value, dictionary[kvp.Key]);
        }
    }

    [Fact]
    public void Constructor_WithNullIDictionary_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new SwiftDictionary<int, string>((IDictionary<int, string>)null));
    }

    [Fact]
    public void Constructor_WithNullIEnumerable_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new SwiftDictionary<int, string>((IEnumerable<KeyValuePair<int, string>>)null));
    }

    [Fact]
    public void Add_NewKey_AddsElement()
    {
        var dictionary = new SwiftDictionary<int, string>();
        dictionary.Add(1, "One");

        Assert.Single(dictionary);
        Assert.Equal("One", dictionary[1]);
    }

    [Fact]
    public void TryAdd_DuplicateKey_ReturnsFalse()
    {
        var dictionary = new SwiftDictionary<int, string>();
        dictionary.Add(1, "One");

        Assert.False(dictionary.Add(1, "One"));
    }

    [Fact]
    public void Add_NullKey_ThrowsArgumentNullException()
    {
        var dictionary = new SwiftDictionary<string, string>();

        Assert.Throws<ArgumentNullException>(() => dictionary.Add(null, "Value"));
    }

    [Fact]
    public void Indexer_Get_ExistingKey_ReturnsValue()
    {
        var dictionary = new SwiftDictionary<int, string>();
        dictionary.Add(1, "One");

        string value = dictionary[1];

        Assert.Equal("One", value);
    }

    [Fact]
    public void Indexer_Get_NonExistingKey_ThrowsKeyNotFoundException()
    {
        var dictionary = new SwiftDictionary<int, string>();

        Assert.Throws<KeyNotFoundException>(() => { var value = dictionary[1]; });
    }

    [Fact]
    public void Indexer_Set_NewKey_AddsElement()
    {
        var dictionary = new SwiftDictionary<int, string>();
        dictionary[1] = "One";

        Assert.Equal("One", dictionary[1]);
        Assert.Single(dictionary);
    }

    [Fact]
    public void Indexer_Set_NewKeys_TriggersResizeAndAddsCorrectly()
    {
        // Use small initial capacity to trigger resize quickly
        var dictionary = new SwiftDictionary<int, string>(4);

        int itemCount = 1000;

        for (int i = 0; i < itemCount; i++)
        {
            dictionary[i] = $"Value {i}";
        }

        // Verify all keys are present and correct
        Assert.Equal(itemCount, dictionary.Count);

        for (int i = 0; i < itemCount; i++)
        {
            Assert.True(dictionary.ContainsKey(i));
            Assert.Equal($"Value {i}", dictionary[i]);
        }
    }

    [Fact]
    public void Indexer_Set_ExistingKeys_UpdatesValuesAcrossResizes()
    {
        // Small initial capacity to force resizes
        var dictionary = new SwiftDictionary<int, string>(4);

        int itemCount = 1000;

        // Add initial values
        for (int i = 0; i < itemCount; i++)
        {
            dictionary[i] = $"Initial {i}";
        }

        // Now update all values
        for (int i = 0; i < itemCount; i++)
        {
            dictionary[i] = $"Updated {i}";
        }

        // Verify updated values
        Assert.Equal(itemCount, dictionary.Count);

        for (int i = 0; i < itemCount; i++)
        {
            Assert.True(dictionary.ContainsKey(i));
            Assert.Equal($"Updated {i}", dictionary[i]);
        }
    }

    [Fact]
    public void Indexer_Set_MixedNewAndExistingKeys_BehavesCorrectly()
    {
        var dictionary = new SwiftDictionary<int, string>(4);

        int initialInsertCount = 500;
        int mixedOperationCount = 1000;

        // Step 1: Add some initial keys
        for (int i = 0; i < initialInsertCount; i++)
        {
            dictionary[i] = $"Initial {i}";
        }

        // Step 2: Mix updates to existing keys and insert new keys
        for (int i = 0; i < mixedOperationCount; i++)
        {
            int key = i % (initialInsertCount * 2); // will cause overlap and new keys
            dictionary[key] = $"Updated {key}";
        }

        // Step 3: Verify consistency of all keys present
        int expectedCount = initialInsertCount * 2; // due to modulo range used

        Assert.Equal(expectedCount, dictionary.Count);

        for (int i = 0; i < expectedCount; i++)
        {
            Assert.True(dictionary.ContainsKey(i));
            Assert.Equal($"Updated {i}", dictionary[i]);
        }
    }

    [Fact]
    public void Indexer_Set_ExistingKey_UpdatesValue()
    {
        var dictionary = new SwiftDictionary<int, string>();
        dictionary[1] = "One";
        dictionary[1] = "Uno";

        Assert.Equal("Uno", dictionary[1]);
        Assert.Single(dictionary);
    }

    [Fact]
    public void Remove_ExistingKey_ReturnsTrue()
    {
        var dictionary = new SwiftDictionary<int, string>();
        dictionary.Add(1, "One");

        bool removed = dictionary.Remove(1);

        Assert.True(removed);
        Assert.False(dictionary.ContainsKey(1));
        Assert.Empty(dictionary);
    }

    [Fact]
    public void Remove_NonExistingKey_ReturnsFalse()
    {
        var dictionary = new SwiftDictionary<int, string>();

        bool removed = dictionary.Remove(1);

        Assert.False(removed);
    }

    [Fact]
    public void Remove_NullKey_ReturnsFalse()
    {
        var dictionary = new SwiftDictionary<string, string>();

        Assert.False(dictionary.Remove(null));
    }

    [Fact]
    public void ContainsKey_ExistingKey_ReturnsTrue()
    {
        var dictionary = new SwiftDictionary<int, string>();
        dictionary.Add(1, "One");

        bool contains = dictionary.ContainsKey(1);

        Assert.True(contains);
    }

    [Fact]
    public void ContainsKey_NonExistingKey_ReturnsFalse()
    {
        var dictionary = new SwiftDictionary<int, string>();

        bool contains = dictionary.ContainsKey(1);

        Assert.False(contains);
    }

    [Fact]
    public void ICollection_Contains_KeyValuePair_Existing_ReturnsTrue()
    {
        var dictionary = new SwiftDictionary<int, string>();
        dictionary.Add(1, "One");

        var kvp = new KeyValuePair<int, string>(1, "One");
        bool contains = ((ICollection<KeyValuePair<int, string>>)dictionary).Contains(kvp);

        Assert.True(contains);
    }

    [Fact]
    public void ICollection_Contains_KeyValuePair_NonExisting_ReturnsFalse()
    {
        var dictionary = new SwiftDictionary<int, string>();
        dictionary.Add(1, "One");

        var kvp = new KeyValuePair<int, string>(2, "Two");
        bool contains = ((ICollection<KeyValuePair<int, string>>)dictionary).Contains(kvp);

        Assert.False(contains);
    }

    [Fact]
    public void TryGetValue_ExistingKey_ReturnsTrue()
    {
        var dictionary = new SwiftDictionary<int, string>(100);
        dictionary.Add(1, "One");
        dictionary.Add(2, "One");
        dictionary.Add(3, "One");
        dictionary.Add(4, "One");
        dictionary.Add(5, "One");
        dictionary.Add(6, "One");
        dictionary.Add(7, "One");
        dictionary.Add(8, "One");
        dictionary.Add(9, "One");
        dictionary.Add(10, "One");
        dictionary.Add(11, "One");

        bool found = dictionary.TryGetValue(5, out string value);

        Assert.True(found);
        Assert.Equal("One", value);
    }

    [Fact]
    public void TryGetValue_NonExistingKey_ReturnsFalse()
    {
        var dictionary = new SwiftDictionary<int, string>();

        bool found = dictionary.TryGetValue(1, out string value);

        Assert.False(found);
        Assert.Null(value);
    }

    [Fact]
    public void Keys_ReturnsAllKeys()
    {
        var dictionary = new SwiftDictionary<int, string>
        {
            [1] = "One",
            [2] = "Two",
            [3] = "Three"
        };

        var keys = dictionary.Keys;

        Assert.Equal(3, keys.Count);
        Assert.Contains(1, keys);
        Assert.Contains(2, keys);
        Assert.Contains(3, keys);
    }

    [Fact]
    public void Values_ReturnsAllValues()
    {
        var dictionary = new SwiftDictionary<int, string>
        {
            [1] = "One",
            [2] = "Two",
            [3] = "Three"
        };

        var values = dictionary.Values;

        Assert.Equal(3, values.Count);
        Assert.Contains("One", values);
        Assert.Contains("Two", values);
        Assert.Contains("Three", values);
    }

    [Fact]
    public void Keys_IsReadOnly_ReturnsTrue()
    {
        var dictionary = new SwiftDictionary<int, string>();
        var keys = dictionary.Keys;

        Assert.True(keys.IsReadOnly);
    }

    [Fact]
    public void Values_IsReadOnly_ReturnsTrue()
    {
        var dictionary = new SwiftDictionary<int, string>();
        var values = dictionary.Values;

        Assert.True(values.IsReadOnly);
    }

    [Fact]
    public void Enumeration_ReturnsAllElements()
    {
        var dictionary = new SwiftDictionary<int, string>
        {
            [1] = "One",
            [2] = "Two",
            [3] = "Three"
        };

        var elements = new List<KeyValuePair<int, string>>();

        foreach (var kvp in dictionary)
        {
            elements.Add(kvp);
        }

        Assert.Equal(3, elements.Count);
        Assert.Contains(new KeyValuePair<int, string>(1, "One"), elements);
        Assert.Contains(new KeyValuePair<int, string>(2, "Two"), elements);
        Assert.Contains(new KeyValuePair<int, string>(3, "Three"), elements);
    }

    [Fact]
    public void Add_NullValue_AllowsNull()
    {
        var dictionary = new SwiftDictionary<int, string>();
        dictionary.Add(1, null);

        Assert.Null(dictionary[1]);
    }

    [Fact]
    public void Indexer_Set_NullValue_AllowsNull()
    {
        var dictionary = new SwiftDictionary<int, string>();
        dictionary[1] = null;

        Assert.Null(dictionary[1]);
    }

    [Fact]
    public void Clear_RemovesAllElements()
    {
        var dictionary = new SwiftDictionary<int, string>
        {
            [1] = "One",
            [2] = "Two"
        };

        dictionary.Clear();

        Assert.Empty(dictionary);
        Assert.False(dictionary.ContainsKey(1));
        Assert.False(dictionary.ContainsKey(2));
    }

    [Fact]
    public void EnsureCapacity_IncreasesCapacity()
    {
        var dictionary = new SwiftDictionary<int, string>();

        int capacityBefore = dictionary.Capacity;
        dictionary.EnsureCapacity(100);

        Assert.True(dictionary.Capacity >= 100);
        Assert.True(dictionary.Capacity > capacityBefore);
    }

    [Fact]
    public void KeyCollection_CopyTo_CopiesKeys()
    {
        var dictionary = new SwiftDictionary<int, string>
        {
            [1] = "One",
            [2] = "Two"
        };

        int[] array = new int[2];
        dictionary.Keys.CopyTo(array, 0);

        Assert.Contains(1, array);
        Assert.Contains(2, array);
    }

    [Fact]
    public void ValueCollection_CopyTo_CopiesValues()
    {
        var dictionary = new SwiftDictionary<int, string>
        {
            [1] = "One",
            [2] = "Two"
        };

        string[] array = new string[2];
        dictionary.Values.CopyTo(array, 0);

        Assert.Contains("One", array);
        Assert.Contains("Two", array);
    }

    [Fact]
    public void IDictionary_Add_NullKey_ThrowsArgumentNullException()
    {
        IDictionary dictionary = new SwiftDictionary<string, string>();

        Assert.Throws<ArgumentNullException>(() => dictionary.Add(null, "Value"));
    }

    [Fact]
    public void IDictionary_Add_InvalidKeyType_ThrowsArgumentException()
    {
        IDictionary dictionary = new SwiftDictionary<int, string>();

        Assert.Throws<ArgumentException>(() => dictionary.Add("InvalidKey", "Value"));
    }

    [Fact]
    public void IDictionary_IndexerSet_AddsAndUpdatesEntries()
    {
        IDictionary dictionary = new SwiftDictionary<int, string>();

        dictionary[1] = "One";
        dictionary[1] = "Uno";

        Assert.Equal("Uno", dictionary[1]);
        Assert.True(dictionary.Contains(1));
    }

    [Fact]
    public void IDictionary_IndexerGet_ReturnsExistingValueAndNullForMissingOrWronglyTypedKeys()
    {
        IDictionary dictionary = new SwiftDictionary<int, string>
        {
            [1] = "One"
        };

        Assert.Equal("One", dictionary[1]);
        Assert.Null(dictionary[2]);
        Assert.Null(dictionary["1"]);
        Assert.Throws<ArgumentNullException>(() => _ = dictionary[null]);
    }

    [Fact]
    public void IDictionary_IndexerSet_ThrowsForInvalidKeyOrValueTypes()
    {
        IDictionary dictionary = new SwiftDictionary<int, string>();

        Assert.Throws<ArgumentException>(() => dictionary["bad"] = "Value");
        Assert.Throws<ArgumentException>(() => dictionary[1] = 42);
    }

    [Fact]
    public void IDictionary_ContainsAndRemove_UseTypedKeys()
    {
        IDictionary dictionary = new SwiftDictionary<int, string>
        {
            [1] = "One",
            [2] = "Two"
        };

        Assert.True(dictionary.Contains(1));
        Assert.False(dictionary.Contains("1"));

        dictionary.Remove(1);

        Assert.False(dictionary.Contains(1));
        Assert.Single(dictionary);
    }

    [Fact]
    public void Constructor_WithEmptyState_InitializesDefaultDictionary()
    {
        var dictionary = new SwiftDictionary<int, string>(new SwiftDictionaryState<int, string>(Array.Empty<KeyValuePair<int, string>>()));

        Assert.Empty(dictionary);
        Assert.True(dictionary.Capacity >= SwiftDictionary<int, string>.DefaultCapacity);
    }

    [Fact]
    public void TrimExcess_ShrinksCapacityAndPreservesEntries()
    {
        var dictionary = new SwiftDictionary<int, string>(256);

        for (int i = 0; i < 12; i++)
            dictionary.Add(i, $"Value {i}");

        int originalCapacity = dictionary.Capacity;

        dictionary.TrimExcess();

        Assert.True(dictionary.Capacity < originalCapacity);

        for (int i = 0; i < 12; i++)
            Assert.Equal($"Value {i}", dictionary[i]);
    }

    [Fact]
    public void Remove_ProbesPastDeletedEntriesInCollisionChain()
    {
        var comparer = new SelectiveIntHashComparer((1, 0), (9, 0), (17, 0));
        var dictionary = new SwiftDictionary<int, string>(8, comparer)
        {
            [1] = "One",
            [9] = "Nine"
        };

        Assert.True(dictionary.Remove(1));
        Assert.False(dictionary.Remove(17));
        Assert.True(dictionary.ContainsKey(9));
        Assert.Equal("Nine", dictionary[9]);
    }

    [Fact]
    public void ICollectionOfKeyValuePair_Remove_ReturnsFalseWhenValueDoesNotMatch()
    {
        var dictionary = new SwiftDictionary<int, string>
        {
            [1] = "One"
        };

        bool removed = ((ICollection<KeyValuePair<int, string>>)dictionary).Remove(new KeyValuePair<int, string>(1, "Uno"));

        Assert.False(removed);
        Assert.Equal("One", dictionary[1]);
    }

    [Fact]
    public void ICollection_CopyTo_DictionaryEntryArray_CopiesEntries()
    {
        ICollection dictionary = new SwiftDictionary<int, string>
        {
            [1] = "One",
            [2] = "Two"
        };

        var destination = new DictionaryEntry[3];

        dictionary.CopyTo(destination, 1);

        Assert.Null(destination[0].Key);
        Assert.Contains(destination, entry => entry.Key is int key && key == 1 && (string)entry.Value == "One");
        Assert.Contains(destination, entry => entry.Key is int key && key == 2 && (string)entry.Value == "Two");
    }

    [Fact]
    public void ICollection_CopyTo_ObjectArray_CopiesKeyValuePairs()
    {
        ICollection dictionary = new SwiftDictionary<int, string>
        {
            [1] = "One",
            [2] = "Two"
        };

        var destination = new object[2];

        dictionary.CopyTo(destination, 0);

        Assert.Contains(destination, item => item is KeyValuePair<int, string> pair && pair.Key == 1 && pair.Value == "One");
        Assert.Contains(destination, item => item is KeyValuePair<int, string> pair && pair.Key == 2 && pair.Value == "Two");
    }

    [Fact]
    public void ICollection_CopyTo_ThrowsForInvalidShapeOrType()
    {
        ICollection dictionary = new SwiftDictionary<int, string>
        {
            [1] = "One"
        };

        Array nonZeroLowerBound = Array.CreateInstance(typeof(KeyValuePair<int, string>), new[] { 2 }, new[] { 1 });

        Assert.Throws<ArgumentException>(() => dictionary.CopyTo(new KeyValuePair<int, string>[1, 1], 0));
        Assert.Throws<ArgumentException>(() => dictionary.CopyTo(nonZeroLowerBound, 0));
        Assert.Throws<ArgumentException>(() => dictionary.CopyTo(new int[1], 0));
    }

    [Fact]
    public void KeyAndValueCollections_ICollectionCopyTo_CopyProjectedItems()
    {
        var dictionary = new SwiftDictionary<int, string>
        {
            [1] = "One",
            [2] = "Two"
        };

        var keyObjects = new object[3];
        var valueObjects = new object[3];

        ((ICollection)dictionary.Keys).CopyTo(keyObjects, 1);
        ((ICollection)dictionary.Values).CopyTo(valueObjects, 1);

        Assert.Null(keyObjects[0]);
        Assert.Contains(1, keyObjects);
        Assert.Contains(2, keyObjects);

        Assert.Null(valueObjects[0]);
        Assert.Contains("One", valueObjects);
        Assert.Contains("Two", valueObjects);
    }

    [Fact]
    public void KeyAndValueCollections_ICollectionCopyTo_ThrowForInvalidArrayTypes()
    {
        var dictionary = new SwiftDictionary<int, string>
        {
            [1] = "One",
            [2] = "Two"
        };

        Assert.Throws<ArgumentException>(() => ((ICollection)dictionary.Keys).CopyTo(new string[2], 0));
        Assert.Throws<ArgumentException>(() => ((ICollection)dictionary.Values).CopyTo(new int[2], 0));
    }

    [Fact]
    public void DictionaryEnumerator_Reset_RestartsAndIEnumeratorCurrentReturnsDictionaryEntry()
    {
        IDictionary dictionary = new SwiftDictionary<int, string>
        {
            [1] = "One",
            [2] = "Two"
        };

        var enumerator = dictionary.GetEnumerator();

        Assert.True(enumerator.MoveNext());
        var current = (DictionaryEntry)((IEnumerator)enumerator).Current;

        Assert.Equal(enumerator.Key, current.Key);
        Assert.Equal(enumerator.Value, current.Value);

        enumerator.Reset();

        Assert.True(enumerator.MoveNext());
        Assert.Contains((int)enumerator.Key, new[] { 1, 2 });
    }

    [Fact]
    public void DictionaryEnumerator_EntryAfterEnumerationEnds_Throws()
    {
        IDictionary dictionary = new SwiftDictionary<int, string>
        {
            [1] = "One"
        };

        IDictionaryEnumerator enumerator = dictionary.GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());
        Assert.Throws<InvalidOperationException>(() => _ = enumerator.Entry);
    }

    [Fact]
    public void KeyAndValueEnumerators_Reset_RestartsEnumeration()
    {
        var dictionary = new SwiftDictionary<int, string>
        {
            [1] = "One",
            [2] = "Two"
        };

        IEnumerator keyEnumerator = ((IEnumerable)dictionary.Keys).GetEnumerator();
        IEnumerator valueEnumerator = ((IEnumerable)dictionary.Values).GetEnumerator();

        Assert.True(keyEnumerator.MoveNext());
        Assert.True(valueEnumerator.MoveNext());

        Assert.Contains((int)keyEnumerator.Current, new[] { 1, 2 });
        Assert.Contains((string)valueEnumerator.Current, new[] { "One", "Two" });

        keyEnumerator.Reset();
        valueEnumerator.Reset();

        Assert.True(keyEnumerator.MoveNext());
        Assert.True(valueEnumerator.MoveNext());
    }

    [Fact]
    public void KeyAndValueEnumerators_MoveNextThrowAfterParentMutation()
    {
        var dictionary = new SwiftDictionary<int, string>
        {
            [1] = "One"
        };

        IEnumerator keyEnumerator = ((IEnumerable)dictionary.Keys).GetEnumerator();
        IEnumerator valueEnumerator = ((IEnumerable)dictionary.Values).GetEnumerator();

        dictionary[2] = "Two";

        Assert.Throws<InvalidOperationException>(() => keyEnumerator.MoveNext());
        Assert.Throws<InvalidOperationException>(() => valueEnumerator.MoveNext());
    }

    [Fact]
    public void SwitchToRandomizedComparer_ActivatesAfterHeavyProbeChain()
    {
        var dictionary = new SwiftDictionary<string, int>(256);
        string[] keys = CollisionStringFactory.CreateMaskedCollisions(dictionary.Comparer, dictionary.Capacity - 1, 110);

        Assert.IsNotAssignableFrom<IRandomedEqualityComparer>(dictionary.Comparer);

        for (int i = 0; i < keys.Length; i++)
            dictionary.Add(keys[i], i);

        Assert.IsAssignableFrom<IRandomedEqualityComparer>(dictionary.Comparer);

        for (int i = 0; i < keys.Length; i++)
            Assert.Equal(i, dictionary[keys[i]]);
    }

    [Fact]
    public void DictionaryAndCollectionAdapterMembers_ExposeExpectedState()
    {
        var dictionary = new SwiftDictionary<int, string>();
        var generic = (ICollection<KeyValuePair<int, string>>)dictionary;
        var keyed = (IDictionary<int, string>)dictionary;
        var nongeneric = (IDictionary)dictionary;

        generic.Add(new KeyValuePair<int, string>(1, "One"));
        keyed.Add(2, "Two");

        Assert.False(generic.IsReadOnly);
        Assert.False(nongeneric.IsReadOnly);
        Assert.False(nongeneric.IsFixedSize);
        Assert.False(((ICollection)dictionary).IsSynchronized);
        Assert.NotNull(((ICollection)dictionary).SyncRoot);
        Assert.NotNull(nongeneric.Values);
        Assert.True(generic.Remove(new KeyValuePair<int, string>(1, "One")));
        Assert.True(((ICollection<int>)dictionary.Keys).Contains(2));
        Assert.True(((ICollection<string>)dictionary.Values).Contains("Two"));

        IDictionaryEnumerator enumerator = nongeneric.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.Equal(enumerator.Key, enumerator.Entry.Key);
    }

    [Fact]
    public void KeyAndValueCollectionAdapters_ExposeSyncRootAndUnsupportedMembers()
    {
        var dictionary = new SwiftDictionary<int, string>
        {
            [1] = "One",
            [2] = "Two"
        };

        var keys = (ICollection<int>)dictionary.Keys;
        var values = (ICollection<string>)dictionary.Values;
        var keyCollection = (ICollection)dictionary.Keys;
        var valueCollection = (ICollection)dictionary.Values;

        Assert.False(keyCollection.IsSynchronized);
        Assert.False(valueCollection.IsSynchronized);
        Assert.NotNull(keyCollection.SyncRoot);
        Assert.NotNull(valueCollection.SyncRoot);
        Assert.True(keys.Contains(1));
        Assert.True(values.Contains("Two"));
        Assert.False(keys.Remove(1));
        Assert.False(values.Remove("One"));
        Assert.Throws<NotSupportedException>(() => keys.Add(3));
        Assert.Throws<NotSupportedException>(() => keys.Clear());
        Assert.Throws<NotSupportedException>(() => values.Add("Three"));
        Assert.Throws<NotSupportedException>(() => values.Clear());
    }

    [Fact]
    public void ICollection_CopyTo_ArrayTooSmall_ThrowsArgumentException()
    {
        var dictionary = new SwiftDictionary<int, string>
        {
            [1] = "One",
            [2] = "Two"
        };

        var array = new KeyValuePair<int, string>[1];

        Assert.Throws<ArgumentException>(() => ((ICollection)dictionary).CopyTo(array, 0));
    }

    [Fact]
    public void SwiftDictionary_CanHandle_LargeInserts()
    {
        var swiftDictionary = new SwiftDictionary<string, int>();
        for (int i = 0; i < 100000; i++)
            swiftDictionary.Add(TestHelper.GenerateRandomString(10), i);
        return;
    }

    [Fact]
    public void Add_RandomStringKeys_AddsElements()
    {
        var dictionary = new SwiftDictionary<string, string>();
        int itemCount = 1000;
        var keys = new HashSet<string>();

        for (int i = 0; i < itemCount; i++)
        {
            string key;
            do
            {
                key = TestHelper.GenerateRandomString(10);
            } while (!keys.Add(key)); // Ensure unique keys

            string value = TestHelper.GenerateRandomString(20);
            dictionary.Add(key, value);

            Assert.Equal(value, dictionary[key]);
        }

        Assert.Equal(itemCount, dictionary.Count);
    }

    [Fact]
    public void ContainsKey_RandomStringKeys_ReturnsTrue()
    {
        var dictionary = new SwiftDictionary<string, string>();
        var keys = new List<string>();

        for (int i = 0; i < 1000; i++)
        {
            string key;
            do
            {
                key = TestHelper.GenerateRandomString(10);
            } while (dictionary.ContainsKey(key)); // Ensure unique keys

            string value = TestHelper.GenerateRandomString(20);
            dictionary.Add(key, value);
            keys.Add(key);
        }

        foreach (var key in keys)
        {
            Assert.True(dictionary.ContainsKey(key));
        }
    }

    [Fact]
    public void Remove_RandomStringKeys_RemovesElements()
    {
        var dictionary = new SwiftDictionary<string, string>();
        var keys = new List<string>();

        for (int i = 0; i < 1000; i++)
        {
            string key;
            do
            {
                key = TestHelper.GenerateRandomString(10);
            } while (dictionary.ContainsKey(key)); // Ensure unique keys

            string value = TestHelper.GenerateRandomString(20);
            dictionary.Add(key, value);
            keys.Add(key);
        }

        foreach (var key in keys)
        {
            bool removed = dictionary.Remove(key);
            Assert.True(removed);
        }

        Assert.Empty(dictionary);
    }

    [Fact]
    public void TryGetValue_RandomStringKeys_ReturnsCorrectValues()
    {
        var dictionary = new SwiftDictionary<string, string>();
        var keyValuePairs = new Dictionary<string, string>();

        for (int i = 0; i < 1000; i++)
        {
            string key;
            do
            {
                key = TestHelper.GenerateRandomString(10);
            } while (dictionary.ContainsKey(key)); // Ensure unique keys

            string value = TestHelper.GenerateRandomString(20);
            dictionary.Add(key, value);
            keyValuePairs.Add(key, value);
        }

        foreach (var kvp in keyValuePairs)
        {
            bool found = dictionary.TryGetValue(kvp.Key, out string value);
            Assert.True(found);
            Assert.Equal(kvp.Value, value);
        }
    }

    [Fact]
    public void TestSerializationDeserialization_EmptyDictionary()
    {
        // Create an empty SwiftDictionary
        var originalDict = new SwiftDictionary<string, int>();

        var jsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };
        var json = JsonSerializer.SerializeToUtf8Bytes(originalDict, jsonOptions);
        var deserializedDict = JsonSerializer.Deserialize<SwiftDictionary<string, int>>(json, jsonOptions);

        // Verify that the deserialized dictionary is empty
        Assert.Empty(deserializedDict);
        Assert.NotNull(deserializedDict);

        // Verify that we can add entries to the deserialized dictionary
        deserializedDict.Add("test", 1);
        Assert.Single(deserializedDict);
        Assert.Equal(1, deserializedDict["test"]);
    }

    [Fact]
    public void SwiftDictionary_MemoryPackSerialization_RoundTripMaintainsData()
    {
        var originalValue = new SwiftDictionary<string, int>
        {
            { "one", 1 },
            { "two", 2 },
            { "three", 3 }
        };

        byte[] bytes = MemoryPackSerializer.Serialize(originalValue);
        SwiftDictionary<string, int> deserializedValue = MemoryPackSerializer.Deserialize<SwiftDictionary<string, int>>(bytes);

        // Check that deserialized values match the original
        Assert.Equal(originalValue.Count, deserializedValue.Count);
        Assert.Equal(originalValue, deserializedValue);
    }

    [Fact]
    public void Dictionary_CustomComparer_RoundTrip()
    {
        var comparer = StringComparer.OrdinalIgnoreCase;

        var dict = new SwiftDictionary<string, int>(8, comparer);
        dict.Add("Hello", 1);

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(dict);

        var result = JsonSerializer.Deserialize<SwiftDictionary<string, int>>(json);

        Assert.DoesNotContain("hello", result);
        Assert.Equal(new SwiftDictionary<string, int>().Comparer.GetHashCode("Hello"), result.Comparer.GetHashCode("Hello"));

        result.SetComparer(comparer);

        Assert.Contains("hello", result);
    }

    [Fact]
    public void Dictionary_SetComparer_RehashesEntriesOutsideInitialProbeSample()
    {
        var comparer = new SelectiveIntHashComparer((15, 14));
        var dictionary = new SwiftDictionary<int, string>(16, comparer);

        for (int i = 0; i < 8; i++)
            dictionary.Add(i, $"Value {i}");

        dictionary.Add(15, "Target");

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(dictionary);
        var result = JsonSerializer.Deserialize<SwiftDictionary<int, string>>(json);

        Assert.True(result.ContainsKey(15));

        result.SetComparer(comparer);

        Assert.True(result.ContainsKey(15));
        Assert.Equal("Target", result[15]);
    }
}
