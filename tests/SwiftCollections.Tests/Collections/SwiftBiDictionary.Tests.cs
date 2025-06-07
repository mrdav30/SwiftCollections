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
    public class SwiftBiDictionaryTests
    {
        [Fact]
        public void BiDictionary_Add_DuplicateValues_Test()
        {
            // Arrange
            var biDict = new SwiftBiDictionary<string, int>();
            biDict.Add("One", 1);
            biDict.Add("Two", 1); // Duplicate value

            // Act
            bool containsKeyOne = biDict.TryGetKey(1, out _);

            // Assert
            Assert.True(containsKeyOne);
        }

        [Fact]
        public void BiDictionary_Remove_NonExistent_Test()
        {
            // Arrange
            var biDict = new SwiftBiDictionary<string, int>();
            biDict.Add("One", 1);

            // Act
            bool removed = biDict.Remove("Two"); // Non-existent key
            bool removedValue = biDict.Remove("One", 2); // Non-existent value for existing key

            // Assert
            Assert.False(removed);
            Assert.False(removedValue);
        }

        [Fact]
        public void BiDictionary_ReverseLookup_NonExistent_Test()
        {
            // Arrange
            var biDict = new SwiftBiDictionary<string, int>();
            biDict.Add("One", 1);

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
            var biDict = new SwiftBiDictionary<string, int>();
            biDict.Add("One", 1);
            biDict.Add("Two", 2);

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
            var biDict = new SwiftBiDictionary<string, int>();
            biDict.Add("One", 1);

            // Act
            biDict.Add("One", 2); // Update value
            bool foundOld = biDict.TryGetKey(1, out string oldKey);
            bool foundNew = biDict.TryGetKey(2, out string newKey);

            // Assert
            Assert.False(foundOld);
            Assert.True(foundNew);
            Assert.Equal("One", newKey);
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
#if NET48_OR_GREATER
            byte[] serializedData;
            using (var memoryStream = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(memoryStream, originalBiDict);
                serializedData = memoryStream.ToArray();
            }

            SwiftBiDictionary<string, int> deserializedBiDict;
            using (var memoryStream = new MemoryStream(serializedData))
            {
                var formatter = new BinaryFormatter();
                deserializedBiDict = (SwiftBiDictionary<string, int>)formatter.Deserialize(memoryStream);
            }
#endif

#if NET8_0_OR_GREATER
            var jsonOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                ReferenceHandler = ReferenceHandler.IgnoreCycles,
                IncludeFields = true,
                IgnoreReadOnlyProperties = true
            };
            var json = JsonSerializer.SerializeToUtf8Bytes(originalBiDict, jsonOptions);
            var deserializedBiDict = JsonSerializer.Deserialize<SwiftBiDictionary<string, int>>(json, jsonOptions);
#endif

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

            // Act
#if NET48_OR_GREATER
            byte[] serializedData;
            using (var memoryStream = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(memoryStream, originalBiDict);
                serializedData = memoryStream.ToArray();
            }

            SwiftBiDictionary<string, int> deserializedBiDict;
            using (var memoryStream = new MemoryStream(serializedData))
            {
                var formatter = new BinaryFormatter();
                deserializedBiDict = (SwiftBiDictionary<string, int>)formatter.Deserialize(memoryStream);
            }
#endif

#if NET8_0_OR_GREATER
            var jsonOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                ReferenceHandler = ReferenceHandler.IgnoreCycles,
                IncludeFields = true,
                IgnoreReadOnlyProperties = true
            };
            var json = JsonSerializer.SerializeToUtf8Bytes(originalBiDict, jsonOptions);
            var deserializedBiDict = JsonSerializer.Deserialize<SwiftBiDictionary<string, int>>(json, jsonOptions);
#endif

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
        public void BiDictionary_TryAdd_DuplicateKey_ShouldReturnFalse()
        {
            // Arrange
            var biDict = new SwiftBiDictionary<string, int>();
            biDict.Add("One", 1);

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
            var biDict = new SwiftBiDictionary<string, int>();
            biDict.Add("One", 1);

            // Act
            biDict["One"] = 2; // Update value

            // Assert
            Assert.Equal(2, biDict["One"]);
        }

        [Fact]
        public void BiDictionary_AddAndRetrieveReverse_Test()
        {
            // Arrange
            var biDict = new SwiftBiDictionary<string, int>();
            biDict.Add("One", 1);
            biDict.Add("Two", 2);

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
            var biDict = new SwiftBiDictionary<string, int>();
            biDict.Add("One", 1);
            biDict.Add("Two", 2);

            // Act
            bool removed = biDict.Remove("One");
            bool reverseExists = biDict.TryGetKey(1, out _);

            // Assert
            Assert.True(removed);
            Assert.False(reverseExists);
        }
    }
}
