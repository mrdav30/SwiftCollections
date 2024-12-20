using Xunit;

namespace SwiftCollections.Observable.Tests
{
    public class ObservablePropertyTests
    {
        [Fact]
        public void Constructor_Default_InitializesWithDefaultValue()
        {
            var property = new ObservableProperty<int>();
            Assert.Equal(default, property.Value);
        }

        [Fact]
        public void Constructor_Value_InitializesWithProvidedValue()
        {
            var property = new ObservableProperty<int>(42);
            Assert.Equal(42, property.Value);
        }

        [Fact]
        public void Value_Set_RaisesPropertyChangedEvent()
        {
            var property = new ObservableProperty<int>(42);
            string propertyName = null;
            property.PropertyChanged += (sender, e) => propertyName = e.PropertyName;

            property.Value = 100;

            Assert.Equal("Value", propertyName);
            Assert.Equal(100, property.Value);
        }

        [Fact]
        public void Value_Set_DoesNotRaiseEventWhenValueIsSame()
        {
            var property = new ObservableProperty<int>(42);
            bool eventRaised = false;
            property.PropertyChanged += (sender, e) => eventRaised = true;

            property.Value = 42;

            Assert.False(eventRaised);
        }
    }
}
