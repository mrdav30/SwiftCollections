using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Xunit;

namespace SwiftCollections.Observable.Tests
{
    public class SwiftObservableDictionaryTests
    {
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
        public void Replace_RaisesCollectionChangedEvent()
        {
            var dictionary = new SwiftObservableDictionary<string, int>
            {
                { "Key1", 100 }
            };
            NotifyCollectionChangedEventArgs eventArgs = null;

            dictionary.CollectionChanged += (sender, e) => eventArgs = e;

            dictionary["Key1"] = 200;

            Assert.NotNull(eventArgs);
            Assert.Equal(NotifyCollectionChangedAction.Replace, eventArgs.Action);
            Assert.Single(eventArgs.OldItems.Cast<KeyValuePair<string, int>>(), kvp => kvp.Key == "Key1" && kvp.Value == 100);
            Assert.Single(eventArgs.NewItems.Cast<KeyValuePair<string, int>>(), kvp => kvp.Key == "Key1" && kvp.Value == 200);
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
        public void CollectionChanged_RaisedOnActualValueChange()
        {
            var dictionary = new SwiftObservableDictionary<string, int>
            {
                { "Key1", 100 }
            };
            NotifyCollectionChangedEventArgs eventArgs = null;

            dictionary.CollectionChanged += (sender, e) => eventArgs = e;

            dictionary["Key1"] = 200; // Change the value

            Assert.NotNull(eventArgs);
            Assert.Equal(NotifyCollectionChangedAction.Replace, eventArgs.Action);
            Assert.Single(eventArgs.OldItems.Cast<KeyValuePair<string, int>>(), kvp => kvp.Key == "Key1" && kvp.Value == 100);
            Assert.Single(eventArgs.NewItems.Cast<KeyValuePair<string, int>>(), kvp => kvp.Key == "Key1" && kvp.Value == 200);
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
    }
}
