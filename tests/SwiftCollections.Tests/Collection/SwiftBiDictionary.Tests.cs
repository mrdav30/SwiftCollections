using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
using MemoryPack;
#endif

namespace SwiftCollections.Tests;

public class SwiftBiDictionaryTests
{
    [Fact]
    public void IndexerDispatch_EquivalentKeyUpdateRetainsStoredKeyInReverseMapping()
    {
        string originalKey = new("One".ToCharArray());
        var dictionary = new SwiftBiDictionary<string, string>(StringComparer.OrdinalIgnoreCase, StringComparer.Ordinal)
        {
            { originalKey, "First" }
        };

        dictionary["oNe"] = "Third";

        Assert.Same(originalKey, dictionary.GetKey("Third"));
        ((SwiftDictionary<string, string>)dictionary).SetComparer(StringComparer.Ordinal);
        Assert.Equal("Third", dictionary[dictionary.GetKey("Third")]);
        Assert.False(dictionary.ContainsValue("First"));
        Assert.Single(dictionary);
    }

    [Theory]
    [InlineData("base")]
    [InlineData("generic")]
    [InlineData("non-generic")]
    public void IndexerDispatch_MaintainsBothDirectionsAndRejectsDuplicateValues(string dispatch)
    {
        var dictionary = new SwiftBiDictionary<int, int> { { 1, 10 } };

        var beforeUpdate = dictionary.GetEnumerator();
        Assert.True(beforeUpdate.MoveNext());
        SetViaDispatch(dictionary, dispatch, 1, 20);
        Assert.Throws<InvalidOperationException>(() => beforeUpdate.MoveNext());
        Assert.Single(dictionary);
        Assert.Equal(20, dictionary[1]);
        Assert.False(dictionary.ContainsValue(10));
        Assert.Equal(1, dictionary.GetKey(20));

        SetViaDispatch(dictionary, dispatch, 2, 30);
        Assert.Equal(2, dictionary.Count);
        Assert.Equal(30, dictionary[2]);
        Assert.Equal(2, dictionary.GetKey(30));

        var enumerator = dictionary.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        SetViaDispatch(dictionary, dispatch, 1, 20);
        Assert.Throws<ArgumentException>(() => SetViaDispatch(dictionary, dispatch, 1, 30));
        Assert.Throws<ArgumentException>(() => SetViaDispatch(dictionary, dispatch, 3, 20));
        Assert.True(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());

        Assert.Equal(2, dictionary.Count);
        Assert.Equal(20, dictionary[1]);
        Assert.Equal(30, dictionary[2]);
        Assert.False(dictionary.ContainsKey(3));
        Assert.False(dictionary.ContainsValue(10));
        Assert.Equal(1, dictionary.GetKey(20));
        Assert.Equal(2, dictionary.GetKey(30));

        var pairs = (ICollection<KeyValuePair<int, int>>)dictionary;
        Assert.True(pairs.Remove(new KeyValuePair<int, int>(1, 20)));
        Assert.False(dictionary.ContainsValue(20));
        pairs.Add(new KeyValuePair<int, int>(3, 40));
        Assert.Equal(3, dictionary.GetKey(40));
        ((IDictionary)dictionary).Clear();
        Assert.Empty(dictionary);
        Assert.False(dictionary.ContainsValue(30));
        Assert.False(dictionary.ContainsValue(40));
    }

    [Theory]
    [InlineData("base")]
    [InlineData("generic")]
    [InlineData("non-generic")]
    public void IndexerDispatch_UsesForwardAndReverseComparers(string dispatch)
    {
        var dictionary = new SwiftBiDictionary<string, string>(StringComparer.OrdinalIgnoreCase, StringComparer.OrdinalIgnoreCase)
        {
            { "One", "First" },
            { "Two", "Second" }
        };

        SetViaDispatch(dictionary, dispatch, "oNe", "Third");
        Assert.Equal("Third", dictionary["ONE"]);
        Assert.False(dictionary.ContainsValue("FIRST"));
        Assert.True(StringComparer.OrdinalIgnoreCase.Equals("One", dictionary.GetKey("THIRD")));

        var enumerator = dictionary.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        SetViaDispatch(dictionary, dispatch, "ONE", "THIRD");
        Assert.Equal("Third", dictionary["one"]);
        Assert.Throws<ArgumentException>(() => SetViaDispatch(dictionary, dispatch, "one", "SECOND"));
        Assert.Throws<ArgumentException>(() => SetViaDispatch(dictionary, dispatch, "Three", "third"));
        Assert.True(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());

        Assert.Equal(2, dictionary.Count);
        Assert.Equal("Third", dictionary["one"]);
        Assert.Equal("Second", dictionary["two"]);
        Assert.False(dictionary.ContainsKey("Three"));
        Assert.False(dictionary.ContainsValue("First"));
        Assert.True(StringComparer.OrdinalIgnoreCase.Equals("One", dictionary.GetKey("third")));
        Assert.True(StringComparer.OrdinalIgnoreCase.Equals("Two", dictionary.GetKey("second")));
    }

    [Theory]
    [InlineData("concrete")]
    [InlineData("base")]
    [InlineData("generic")]
    [InlineData("non-generic")]
    public void IndexerDispatch_RejectsNullKeysAndValuesBeforeMutation(string dispatch)
    {
        var dictionary = new SwiftBiDictionary<string, string>
        {
            { "One", "First" },
            { "Two", "Second" }
        };

        Assert.Throws<ArgumentNullException>(() => SetViaDispatch(dictionary, dispatch, null, "Third"));
        Assert.Throws<ArgumentNullException>(() => SetViaDispatch(dictionary, dispatch, "One", null));
        Assert.Equal("First", dictionary["One"]);
        Assert.Equal("One", dictionary.GetKey("First"));
        Assert.Throws<ArgumentNullException>(() => SetViaDispatch(dictionary, dispatch, "Three", null));
        Assert.Equal(2, dictionary.Count);
        Assert.False(dictionary.ContainsKey("Three"));
        Assert.Equal("First", dictionary["One"]);
        Assert.Equal("Second", dictionary["Two"]);
        Assert.Equal("One", dictionary.GetKey("First"));
        Assert.Equal("Two", dictionary.GetKey("Second"));
    }

    [Fact]
    public void IndexerDispatch_NonGenericTypeErrorsLeaveBothDirectionsUnchanged()
    {
        var dictionary = new SwiftBiDictionary<string, string> { { "One", "First" } };
        var adapter = (IDictionary)dictionary;

        Assert.Throws<ArgumentException>(() => adapter[1] = "Second");
        Assert.Throws<ArgumentException>(() => adapter["One"] = 2);
        Assert.Single(dictionary);
        Assert.Equal("First", dictionary["One"]);
        Assert.Equal("One", dictionary.GetKey("First"));
        Assert.False(dictionary.ContainsValue("Second"));
    }

    private static void SetViaDispatch<TKey, TValue>(SwiftBiDictionary<TKey, TValue> dictionary,
        string dispatch, TKey key, TValue value) where TKey : notnull where TValue : notnull
    {
        switch (dispatch)
        {
            case "concrete": dictionary[key] = value; break;
            case "base": ((SwiftDictionary<TKey, TValue>)dictionary)[key] = value; break;
            case "generic": ((IDictionary<TKey, TValue>)dictionary)[key] = value; break;
            case "non-generic": ((IDictionary)dictionary)[key] = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(dispatch));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void StateRestore_CustomDistinctKeysOrValuesNeedComparersBeforeInsertion(bool equalKeys)
    {
        var comparer = EqualityComparer<string>.Create((x, y) => ReferenceEquals(x, y), _ => 0);
        string firstKey = new('k', 1);
        string secondKey = new(equalKeys ? 'k' : 'l', 1);
        string firstValue = new('v', 1);
        string secondValue = new(equalKeys ? 'w' : 'v', 1);
        var source = new SwiftBiDictionary<string, string>(comparer, comparer)
        {
            { firstKey, firstValue },
            { secondKey, secondValue }
        };
        SwiftDictionaryState<string, string> state = source.State;

        var defaultRestore = new SwiftBiDictionary<string, string>(state);
        Assert.Single(defaultRestore);
        Assert.Same(state.Items[0].Key, defaultRestore.State.Items[0].Key);
        Assert.Same(state.Items[0].Value, defaultRestore.State.Items[0].Value);
        defaultRestore.SetComparer(comparer, comparer);
        Assert.Single(defaultRestore);

        var restored = new SwiftBiDictionary<string, string>(comparer, comparer);
        foreach (KeyValuePair<string, string> item in state.Items)
            restored.Add(item.Key, item.Value);

        Assert.Equal(2, restored.Count);
        Assert.Same(firstValue, restored[firstKey]);
        Assert.Same(secondValue, restored[secondKey]);
        Assert.Same(firstKey, restored.GetKey(firstValue));
        Assert.Same(secondKey, restored.GetKey(secondValue));
        Assert.Equal(2, source.Count);
        Assert.Equal(2, state.Items.Length);
    }

    [Fact]
    public void BiDictionary_Add_DuplicateValues_Test()
    {
        // Arrange
        var biDict = new SwiftBiDictionary<string, int>
        {
            { "One", 1 },
            { "Two", 1 } // Duplicate value
        };

        // Act
        bool containsKeyOne = biDict.TryGetKey(1, out _);

        // Assert
        Assert.True(containsKeyOne);
        Assert.Single(biDict);
        Assert.False(biDict.ContainsKey("Two"));
    }

    [Fact]
    public void BiDictionary_Remove_NonExistent_Test()
    {
        // Arrange
        var biDict = new SwiftBiDictionary<string, int>
        {
            { "One", 1 }
        };

        // Act
        bool removed = biDict.Remove("Two"); // Non-existent key
        bool removedValue = biDict.Remove("One", 2); // Non-existent value for existing key
        bool removedPair = biDict.Remove("Two", 2); // Non-existent pair

        // Assert
        Assert.False(removed);
        Assert.False(removedValue);
        Assert.False(removedPair);
    }

    [Fact]
    public void BiDictionary_ReverseLookup_NonExistent_Test()
    {
        // Arrange
        var biDict = new SwiftBiDictionary<string, int>
        {
            { "One", 1 }
        };

        // Act
        bool found = biDict.TryGetKey(2, out string key);

        // Assert
        Assert.False(found);
        Assert.Null(key);
    }

    [Fact]
    public void BiDictionary_Clear_Test()
    {
        // Arrange
        var biDict = new SwiftBiDictionary<string, int>
        {
            { "One", 1 },
            { "Two", 2 }
        };

        // Act
        biDict.Clear();

        // Assert
        Assert.Empty(biDict);
        Assert.False(biDict.ContainsKey("One"));
        Assert.False(biDict.ContainsKey("Two"));
        Assert.False(biDict.TryGetKey(1, out _));
        Assert.False(biDict.TryGetKey(2, out _));
    }

    [Fact]
    public void BiDictionary_UpdateValue_Test()
    {
        // Arrange
        var biDict = new SwiftBiDictionary<string, int>
        {
            { "One", 1 }
        };

        // Act
        biDict["One"] = 2; // Update value
        bool foundOld = biDict.TryGetKey(1, out string oldKey);
        bool foundNew = biDict.TryGetKey(2, out string newKey);

        // Assert
        Assert.False(foundOld);
        Assert.True(foundNew);
        Assert.Equal("One", newKey);
    }

    [Fact]
    public void BiDictionary_IndexerSet_SameValue_IsNoOp()
    {
        var biDict = new SwiftBiDictionary<string, int>
        {
            { "One", 1 }
        };

        biDict["One"] = 1;

        Assert.Single(biDict);
        Assert.Equal(1, biDict["One"]);
        Assert.True(biDict.TryGetKey(1, out string key));
        Assert.Equal("One", key);
    }

    [Fact]
    public void BiDictionary_IndexerSet_NewKey_AddsForwardAndReverseMappings()
    {
        var biDict = new SwiftBiDictionary<string, int>
        {
            ["One"] = 1
        };

        Assert.Single(biDict);
        Assert.Equal(1, biDict["One"]);
        Assert.True(biDict.ContainsValue(1));
        Assert.True(biDict.TryGetKey(1, out string key));
        Assert.Equal("One", key);
    }

    [Fact]
    public void BiDictionary_IndexerSet_DuplicateValue_ThrowsAndLeavesExistingMappings()
    {
        var biDict = new SwiftBiDictionary<string, int>
        {
            { "One", 1 },
            { "Two", 2 }
        };

        Assert.Throws<ArgumentException>(() => biDict["One"] = 2);

        Assert.Equal(1, biDict["One"]);
        Assert.Equal(2, biDict["Two"]);
        Assert.True(biDict.TryGetKey(1, out string firstKey));
        Assert.Equal("One", firstKey);
        Assert.True(biDict.TryGetKey(2, out string secondKey));
        Assert.Equal("Two", secondKey);
    }

    [Fact]
    public void BiDictionary_Remove_KeyValuePair_RemovesBothDirections()
    {
        var biDict = new SwiftBiDictionary<string, int>
        {
            { "One", 1 },
            { "Two", 2 }
        };

        bool removed = biDict.Remove("One", 1);

        Assert.True(removed);
        Assert.False(biDict.ContainsKey("One"));
        Assert.False(biDict.ContainsValue(1));
        Assert.True(biDict.ContainsKey("Two"));
        Assert.True(biDict.ContainsValue(2));
    }

    /// <summary>
    /// Tests the serialization and deserialization of the <see cref="BiDictionary{T1, T2}"/> to ensure data integrity.
    /// </summary>
    [Fact]
    public void BiDictionary_SerializationDeserialization_WithReverseMap_Test()
    {
        // Arrange
        var originalBiDict = new SwiftBiDictionary<string, int>
        {
            { "One", 1 },
            { "Two", 2 },
            { "Three", 3 }
        };

        // Act
        var jsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };
        var json = JsonSerializer.SerializeToUtf8Bytes(originalBiDict, jsonOptions);
        var deserializedBiDict = JsonSerializer.Deserialize<SwiftBiDictionary<string, int>>(json, jsonOptions);

        // Assert
        Assert.NotNull(deserializedBiDict);
        Assert.Equal(originalBiDict.Count, deserializedBiDict.Count);

        foreach (var kvp in originalBiDict)
        {
            Assert.True(deserializedBiDict.ContainsKey(kvp.Key));
            Assert.Equal(kvp.Value, deserializedBiDict[kvp.Key]);

            // Test reverse lookup
            Assert.True(deserializedBiDict.TryGetKey(kvp.Value, out string key));
            Assert.Equal(kvp.Key, key);
        }
    }

    /// <summary>
    /// Tests the serialization and deserialization of an empty <see cref="BiDictionary{T1, T2}"/>.
    /// </summary>
    [Fact]
    public void BiDictionary_SerializationDeserialization_Empty_Test()
    {
        // Arrange
        var originalBiDict = new SwiftBiDictionary<string, int>();

        var jsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };
        var json = JsonSerializer.SerializeToUtf8Bytes(originalBiDict, jsonOptions);
        var deserializedBiDict = JsonSerializer.Deserialize<SwiftBiDictionary<string, int>>(json, jsonOptions);

        // Assert
        Assert.NotNull(deserializedBiDict);
        Assert.Empty(deserializedBiDict);

        // Test adding a new entry after deserialization
        deserializedBiDict.Add("One", 1);
        Assert.Single(deserializedBiDict);
        Assert.True(deserializedBiDict.ContainsKey("One"));
        Assert.Equal(1, deserializedBiDict["One"]);
        Assert.True(deserializedBiDict.TryGetKey(1, out string key));
        Assert.Equal("One", key);
    }

    [Fact]
    public void BiDictionary_StateConstructor_IgnoresDuplicateValues()
    {
        var state = new SwiftDictionaryState<string, int>(
            new[]
            {
                new KeyValuePair<string, int>("One", 1),
                new KeyValuePair<string, int>("Two", 1)
            });

        var dictionary = new SwiftBiDictionary<string, int>(state);

        Assert.Single(dictionary);
        Assert.True(dictionary.ContainsKey("One"));
        Assert.False(dictionary.ContainsKey("Two"));
        Assert.True(dictionary.TryGetKey(1, out string key));
        Assert.Equal("One", key);
    }

#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
    [Fact]
    public void SwiftBiDictionary_MemoryPackSerialization_RoundTripMaintainsData()
    {
        var originalValue = new SwiftBiDictionary<string, int>
        {
            { "one", 1 },
            { "two", 2 },
            { "three", 3 }
        };

        byte[] bytes = MemoryPackSerializer.Serialize(originalValue);
        SwiftBiDictionary<string, int> deserializedValue = MemoryPackSerializer.Deserialize<SwiftBiDictionary<string, int>>(bytes);

        // Check that deserialized values match the original
        Assert.Equal(originalValue.Count, deserializedValue.Count);
        Assert.Equal(originalValue, deserializedValue);
    }
#endif

    [Fact]
    public void BiDictionary_CustomComparer_RoundTrip()
    {
        var keyComparer = StringComparer.OrdinalIgnoreCase;
        var valueComparer = StringComparer.OrdinalIgnoreCase;

        var dict = new SwiftBiDictionary<string, string>(keyComparer, valueComparer)
        {
            { "Key", "Value" }
        };

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(dict);

        var result = JsonSerializer.Deserialize<SwiftBiDictionary<string, string>>(json);

        Assert.Same(keyComparer, result.Comparer);
        Assert.True(result.ContainsKey("key"));
        Assert.True(result.ContainsValue("value"));
        Assert.True(result.TryGetKey("value", out string key));
        Assert.Equal("Key", key);
    }

    [Fact]
    public void BiDictionary_SetComparer_RehashesForwardAndReverseMapsOutsideInitialProbeSample()
    {
        var keyComparer = new SelectiveIntHashComparer((15, 14));
        var valueComparer = new SelectiveIntHashComparer((115, 114));
        var dictionary = new SwiftBiDictionary<int, int>(keyComparer, valueComparer);

        for (int i = 0; i < 8; i++)
            dictionary.Add(i, 100 + i);

        dictionary.Add(15, 115);

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(dictionary.State);
        var state = JsonSerializer.Deserialize<SwiftDictionaryState<int, int>>(json);
        var result = new SwiftBiDictionary<int, int>(state);

        Assert.True(result.ContainsKey(15));
        Assert.True(result.ContainsValue(115));

        result.SetComparer(keyComparer, valueComparer);

        Assert.True(result.ContainsKey(15));
        Assert.True(result.ContainsValue(115));
        Assert.True(result.TryGetKey(115, out int key));
        Assert.Equal(15, key);
    }

    [Fact]
    public void BiDictionary_SetComparer_WithSameComparers_IsNoOp()
    {
        var keyComparer = StringComparer.OrdinalIgnoreCase;
        var valueComparer = StringComparer.OrdinalIgnoreCase;
        var dictionary = new SwiftBiDictionary<string, string>(keyComparer, valueComparer)
        {
            ["Key"] = "Value"
        };

        dictionary.SetComparer(keyComparer, valueComparer);

        Assert.True(dictionary.ContainsKey("key"));
        Assert.True(dictionary.ContainsValue("value"));
        Assert.Equal("Value", dictionary["key"]);
    }

    [Fact]
    public void BiDictionary_TryAdd_DuplicateKey_ShouldReturnFalse()
    {
        // Arrange
        var biDict = new SwiftBiDictionary<string, int>
        {
            { "One", 1 }
        };

        // Act
        bool result = biDict.Add("One", 2); // Duplicate key

        // Assert
        Assert.False(result);
        Assert.Equal(1, biDict["One"]); // Value should remain unchanged
    }

    [Fact]
    public void BiDictionary_TryAdd_UniqueKeyValue_ShouldReturnTrue()
    {
        // Arrange
        var biDict = new SwiftBiDictionary<string, int>();

        // Act
        bool result1 = biDict.Add("One", 1);
        bool result2 = biDict.Add("Two", 2);

        // Assert
        Assert.True(result1);
        Assert.True(result2);
        Assert.Equal(2, biDict.Count);
        Assert.Equal(1, biDict["One"]);
        Assert.Equal(2, biDict["Two"]);
    }

    [Fact]
    public void BiDictionary_Add_UpdateValue_ShouldSucceed()
    {
        // Arrange
        var biDict = new SwiftBiDictionary<string, int>
        {
            { "One", 1 }
        };

        // Act
        biDict["One"] = 2; // Update value

        // Assert
        Assert.Equal(2, biDict["One"]);
    }

    [Fact]
    public void BiDictionary_AddAndRetrieveReverse_Test()
    {
        // Arrange
        var biDict = new SwiftBiDictionary<string, int>
        {
            { "One", 1 },
            { "Two", 2 }
        };

        // Act
        bool foundOne = biDict.TryGetKey(1, out string keyOne);
        bool foundTwo = biDict.TryGetKey(2, out string keyTwo);

        // Assert
        Assert.True(foundOne);
        Assert.Equal("One", keyOne);
        Assert.True(foundTwo);
        Assert.Equal("Two", keyTwo);
    }

    [Fact]
    public void BiDictionary_RemoveAndVerifyReverseMap_Test()
    {
        // Arrange
        var biDict = new SwiftBiDictionary<string, int>
        {
            { "One", 1 },
            { "Two", 2 }
        };

        // Act
        bool removed = biDict.Remove("One");
        bool reverseExists = biDict.TryGetKey(1, out _);

        // Assert
        Assert.True(removed);
        Assert.False(reverseExists);
    }

    [Fact]
    public void BiDictionary_ConstructorsFromDictionaryAndGetKey_PreserveReverseLookup()
    {
        var source = new System.Collections.Generic.Dictionary<string, int>
        {
            ["One"] = 1,
            ["Two"] = 2
        };

        var withDefaultComparers = new SwiftBiDictionary<string, int>(source);
        var withCustomComparers = new SwiftBiDictionary<string, int>(source, StringComparer.OrdinalIgnoreCase, System.Collections.Generic.EqualityComparer<int>.Default);

        Assert.Equal("One", withDefaultComparers.GetKey(1));
        Assert.Equal("Two", withCustomComparers.GetKey(2));
        Assert.True(withCustomComparers.ContainsKey("one"));
    }
}
