using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Xunit;

namespace SwiftCollections.Observable.Tests
{
    public class SwiftObservableListTests
    {
        [Fact]
        public void Add_RaisesCollectionChangedEvent()
        {
            var list = new ObservableSwiftList<int>();
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
            var list = new ObservableSwiftList<int> { 42 };
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
            var list = new ObservableSwiftList<int> { 42 };
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
            var list = new ObservableSwiftList<int> { 42, 100 };
            NotifyCollectionChangedEventArgs eventArgs = null;

            list.CollectionChanged += (sender, e) => eventArgs = e;

            list.Clear();

            Assert.NotNull(eventArgs);
            Assert.Equal(NotifyCollectionChangedAction.Reset, eventArgs.Action);
        }

        [Fact]
        public void PropertyChangedEvent_RaisedOnAdd()
        {
            var list = new ObservableSwiftList<int>();
            string propertyName = null;

            list.PropertyChanged += (sender, e) => propertyName = e.PropertyName;

            list.Add(42);

            Assert.Equal(nameof(list.Count), propertyName);
        }

        [Fact]
        public void PropertyChangedEvent_RaisedOnRemove()
        {
            var list = new ObservableSwiftList<int> { 42 };
            string propertyName = null;

            list.PropertyChanged += (sender, e) => propertyName = e.PropertyName;

            list.Remove(42);

            Assert.Equal(nameof(list.Count), propertyName);
        }

        [Fact]
        public void PropertyChangedEvent_RaisedOnReplace()
        {
            var list = new ObservableSwiftList<int> { 42 };
            string propertyName = null;

            list.PropertyChanged += (sender, e) => propertyName = e.PropertyName;

            list[0] = 100;

            Assert.Equal("InnerArray[]", propertyName);
        }

        [Fact]
        public void Enumerator_ReturnsAllItems()
        {
            var list = new ObservableSwiftList<int> { 42, 100 };

            var items = list.ToList();

            Assert.Contains(42, items);
            Assert.Contains(100, items);
        }

        [Fact]
        public void Add_MultipleItems_RaisesEventForEach()
        {
            var list = new ObservableSwiftList<int>();
            int eventCount = 0;

            list.CollectionChanged += (sender, e) => eventCount++;

            list.Add(42);
            list.Add(100);

            Assert.Equal(2, eventCount);
        }

        [Fact]
        public void Insert_RaisesCollectionChangedEvent()
        {
            var list = new ObservableSwiftList<int> { 42 };
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
            var list = new ObservableSwiftList<int> { 42, 100 };
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
            var list = new ObservableSwiftList<int>();

            Assert.Throws<ArgumentOutOfRangeException>(() => list[0] = 42);
            Assert.Throws<ArgumentOutOfRangeException>(() => list.RemoveAt(0));
        }

        [Fact]
        public void Events_RaisedInCorrectOrder()
        {
            var list = new ObservableSwiftList<int>();
            var events = new List<string>();

            list.CollectionChanged += (sender, e) => events.Add("CollectionChanged");
            list.PropertyChanged += (sender, e) => events.Add("PropertyChanged");

            list.Add(42);

            Assert.Equal(new[] { "CollectionChanged", "PropertyChanged" }, events);
        }

        [Fact]
        public void NoEvent_OnDuplicateSet()
        {
            var list = new ObservableSwiftList<int> { 42 };
            bool eventRaised = false;

            list.CollectionChanged += (sender, e) => eventRaised = true;
            list.PropertyChanged += (sender, e) => eventRaised = true;

            list[0] = 42; // No change

            Assert.False(eventRaised);
        }

        [Fact]
        public void ModifyDuringEnumeration_ThrowsException()
        {
            var list = new ObservableSwiftList<int> { 42, 100 };

            var enumerator = list.GetEnumerator();
            list.Add(200); // Modify during enumeration

            Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
        }

        [Fact]
        public void EventHandler_AddAndRemove()
        {
            var list = new ObservableSwiftList<int>();
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
            var list = new ObservableSwiftList<int>();
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
        public void StressTest_RepeatedOperations()
        {
            var list = new ObservableSwiftList<int>();

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
            var list = new ObservableSwiftList<int>();
            bool eventRaised = false;

            list.CollectionChanged += (sender, e) => eventRaised = true;

            Assert.Throws<ArgumentOutOfRangeException>(() => list.RemoveAt(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => list[0] = 42);

            Assert.False(eventRaised);
        }

        [Fact]
        public void HandlesNullValues()
        {
            var list = new ObservableSwiftList<string> { null, "Test" };
            list[0] = "Updated";
            list.Remove("Test");

            Assert.Single(list);
            Assert.Equal("Updated", list[0]);
        }

        [Fact]
        public void IntegrationWithObservableDictionary()
        {
            var dictionary = new ObservableSwiftDictionary<string, ObservableSwiftList<int>>
            {
                { "Key1", new ObservableSwiftList<int> { 1, 2, 3 } }
            };

            bool eventRaised = false;
            dictionary["Key1"].CollectionChanged += (sender, e) => eventRaised = true;

            dictionary["Key1"].Add(4);

            Assert.True(eventRaised);
            Assert.Contains(4, dictionary["Key1"]);
        }
    }
}
