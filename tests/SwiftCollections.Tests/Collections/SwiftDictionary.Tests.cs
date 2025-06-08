using System;
using System.Collections;
using System.Collections.Generic;

#if NET48_OR_GREATER
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
#endif

#if NET8_0_OR_GREATER
using System.Text.Json;
using System.Text.Json.Serialization;
#endif

using Xunit;

namespace SwiftCollections.Tests
{
    public class SwiftDictionaryTests
    {
        [Fact]
        public void Constructor_Default_CreatesEmptyDictionary()
        {
            var dictionary = new SwiftDictionary<int, string>();
            Assert.Empty(dictionary);
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
                new KeyValuePair<int, string>(1, "One"),
                new KeyValuePair<int, string>(2, "Two")
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

#if NET48_OR_GREATER
            // Serialize the dictionary to a memory stream
            var formatter = new BinaryFormatter();
            var stream = new MemoryStream();
            formatter.Serialize(stream, originalDict);

            // Reset the stream position to the beginning
            stream.Seek(0, SeekOrigin.Begin);

            // Deserialize the dictionary from the memory stream
            var deserializedDict = (SwiftDictionary<string, int>)formatter.Deserialize(stream);
#endif

#if NET8_0_OR_GREATER
            var jsonOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                ReferenceHandler = ReferenceHandler.IgnoreCycles,
                IncludeFields = true,
                IgnoreReadOnlyProperties = true
            };
            var json = JsonSerializer.SerializeToUtf8Bytes(originalDict, jsonOptions);
            var deserializedDict = JsonSerializer.Deserialize<SwiftDictionary<string, int>>(json, jsonOptions);
#endif

            // Verify that the deserialized dictionary is empty
            Assert.Empty(deserializedDict);
            Assert.NotNull(deserializedDict);

            // Verify that we can add entries to the deserialized dictionary
            deserializedDict.Add("test", 1);
            Assert.Single(deserializedDict);
            Assert.Equal(1, deserializedDict["test"]);
        }
    }
}
