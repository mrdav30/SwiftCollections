using System;
using System.Linq;
using Xunit;

namespace SwiftCollections.Observable.Tests
{
    public class ObservableArrayTests
    {
        [Fact]
        public void Constructor_Capacity_InitializesArray()
        {
            var array = new ObservableArray<int>(5);
            Assert.Equal(5, array.Capacity);
            Assert.All(array.ToArray(), value => Assert.Equal(default, value));
        }

        [Fact]
        public void Constructor_NullObservableProperties_ThrowsException()
        {
            Assert.Throws<ArgumentNullException>(() => new ObservableArray<int>(null));
        }

        [Fact]
        public void Indexer_Get_ReturnsCorrectValue()
        {
            var array = new ObservableArray<int>(5);
            array[2] = 42;

            Assert.Equal(42, array[2]);
        }

        [Fact]
        public void Indexer_Set_RaisesElementChangedEvent()
        {
            var array = new ObservableArray<int>(3);
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
            var array = new ObservableArray<int>(3);
            bool eventRaised = false;

            array.ElementChanged += (sender, args) => eventRaised = true;

            array[1] = default;

            Assert.False(eventRaised);
        }

        [Fact]
        public void ToArray_ReturnsCurrentStateOfArray()
        {
            var array = new ObservableArray<int>(3);
            array[0] = 10;
            array[1] = 20;
            array[2] = 30;

            var result = array.ToArray();

            Assert.Equal(new[] { 10, 20, 30 }, result);
        }

        [Fact]
        public void InvalidIndex_ThrowsException()
        {
            var array = new ObservableArray<int>(3);

            Assert.Throws<IndexOutOfRangeException>(() => array[-1]);
            Assert.Throws<IndexOutOfRangeException>(() => array[3]);
        }

        [Fact]
        public void ElementChanged_FiresOnPropertyChange()
        {
            var array = new ObservableArray<string>(3);
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
            var array = new ObservableArray<int>(3);
            string propertyName = null;

            array.PropertyChanged += (sender, e) => propertyName = e.PropertyName;

            array[1] = 100;

            Assert.Equal("Items[]", propertyName);
        }

        [Fact]
        public void HighCapacityInitialization_PerformsReasonably()
        {
            var array = new ObservableArray<int>(100000);
            Assert.Equal(100000, array.Capacity);
            Assert.All(array.ToArray(), value => Assert.Equal(default, value));
        }

        [Fact]
        public void CascadingUpdates_DoNotCreateInfiniteLoops()
        {
            var array = new ObservableArray<int>(3);

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
            var array = new ObservableArray<int>(size);
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
            var property = new ObservableProperty<string>("Hello");
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
            var array = new ObservableArray<int>(3);
            int subscriber1Count = 0;
            int subscriber2Count = 0;

            array.ElementChanged += (sender, args) => subscriber1Count++;
            array.ElementChanged += (sender, args) => subscriber2Count++;

            array[0] = 42;

            Assert.Equal(1, subscriber1Count);
            Assert.Equal(1, subscriber2Count);
        }
    }
}
