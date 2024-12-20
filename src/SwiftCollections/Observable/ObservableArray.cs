using System;
using System.ComponentModel;

namespace SwiftCollections.Observable
{
    /// <summary>
    /// Represents an array of observable properties, raising events whenever an element is updated.
    /// Designed for performance-critical scenarios in Unity game development.
    /// </summary>
    /// <typeparam name="TValue">The type of elements in the array.</typeparam>
    public class ObservableArray<TValue> : INotifyPropertyChanged
    {
        #region Fields

        private readonly ObservableProperty<TValue>[] _items;
        private readonly PropertyChangedEventHandler _itemChangedHandler;

        #endregion

        #region Events

        /// <summary>
        /// Raised when any element in the array changes, providing the index and the new value.
        /// </summary>
        public event EventHandler<ElementChangedEventArgs<TValue>> ElementChanged;

        /// <summary>
        /// Raised when the array's state changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Nested Types

        /// <summary>
        /// Provides details about a changed element, including its index and new value.
        /// </summary>
        public class ElementChangedEventArgs<T> : EventArgs
        {
            /// <summary>
            /// The index of the changed element.
            /// </summary>
            public int Index { get; }

            /// <summary>
            /// The new value of the changed element.
            /// </summary>
            public T NewValue { get; }

            public ElementChangedEventArgs(int index, T newValue)
            {
                Index = index;
                NewValue = newValue;
            }
        }

        #endregion

        #region Constructors

        public ObservableArray(int capacity)
        {
            if (capacity <= 0)
                ThrowHelper.ThrowArgumentOutOfRangeException($"{nameof(capacity)} must be greater than zero.");

            _items = new ObservableProperty<TValue>[capacity];
            _itemChangedHandler = (sender, e) => OnItemChanged(sender);

            for (int i = 0; i < capacity; i++)
            {
                _items[i] = new ObservableProperty<TValue>();
                _items[i].PropertyChanged += _itemChangedHandler;
            }
        }

        public ObservableArray(ObservableProperty<TValue>[] observableProperties)
        {
            if (observableProperties == null)
                ThrowHelper.ThrowArgumentNullException(nameof(observableProperties));

            _items = observableProperties;
            _itemChangedHandler = (sender, e) => OnItemChanged(sender);

            foreach (var item in _items)
                item.PropertyChanged += _itemChangedHandler;
        }

        #endregion

        #region Properties

        public int Capacity => _items.Length;

        public TValue this[int index]
        {
            get
            {
                ValidateIndex(index);
                return _items[index].Value;
            }
            set
            {
                ValidateIndex(index);
                if (!Equals(_items[index].Value, value))
                    _items[index].Value = value;
            }
        }

        #endregion

        #region Methods

        public TValue[] ToArray()
        {
            var array = new TValue[_items.Length];
            for (int i = 0; i < _items.Length; i++)
                array[i] = _items[i].Value;
            return array;
        }

        private void ValidateIndex(int index)
        {
            if (index < 0 || index >= _items.Length)
                throw new IndexOutOfRangeException($"Index {index} is out of bounds for this array.");
        }

        private void OnItemChanged(object sender)
        {
            if (sender is ObservableProperty<TValue> property)
            {
                int index = Array.IndexOf(_items, property);
                if (index >= 0)
                {
                    // Raise strongly-typed event
                    ElementChanged?.Invoke(this, new ElementChangedEventArgs<TValue>(index, property.Value));

                    // Maintain backward compatibility by raising PropertyChanged
                    OnPropertyChanged("Items[]");
                }
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
