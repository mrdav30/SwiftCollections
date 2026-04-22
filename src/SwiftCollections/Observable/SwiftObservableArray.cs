using MemoryPack;
using System;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace SwiftCollections.Observable;

/// <summary>
/// Represents an array of observable properties, raising events whenever an element is updated.
/// Designed for performance-critical scenarios in Unity game development.
/// </summary>
/// <typeparam name="TValue">The type of elements in the array.</typeparam>
[Serializable]
[JsonConverter(typeof(SwiftStateJsonConverterFactory))]
[MemoryPackable]
public partial class SwiftObservableArray<TValue> : INotifyPropertyChanged
{
    #region Fields

    /// <summary>
    /// Represents the collection of observable properties managed by this instance.
    /// </summary>
    /// <remarks>Each element in the array corresponds to an individual observable property. 
    /// The array may be null or empty if no properties are currently managed.
    /// </remarks>
    protected SwiftObservableProperty<TValue>[] _items;

    /// <summary>
    /// Represents the event handler that is invoked when a property value changes on an item.
    /// </summary>
    /// <remarks>
    /// This handler is typically used to subscribe to property change notifications for items within a collection or container.
    /// Derived classes can use this field to manage event subscriptions for item property changes.
    /// </remarks>
    protected PropertyChangedEventHandler _itemChangedHandler;

    #endregion

    #region Events

    /// <summary>
    /// Raised when any element in the array changes, providing the index and the new value.
    /// </summary>
    public event EventHandler<ElementChangedEventArgs<TValue>>? ElementChanged;

    /// <summary>
    /// Raised when the array's state changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

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

        internal ElementChangedEventArgs(int index, T newValue)
        {
            Index = index;
            NewValue = newValue;
        }
    }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the SwiftObservableArray class with the specified capacity.
    /// </summary>
    /// <remarks>
    /// Each element in the array is initialized with a new <see cref="SwiftObservableProperty{TValue}"/> instance.
    /// The capacity determines the fixed size of the array and cannot be changed after construction.
    /// </remarks>
    /// <param name="capacity">The number of elements the array can contain. Must be a positive integer.</param>
    public SwiftObservableArray(int capacity)
    {
        SwiftThrowHelper.ThrowIfNegativeOrZero(capacity, nameof(capacity));

        _items = new SwiftObservableProperty<TValue>[capacity];
        _itemChangedHandler = (sender, e) => OnItemChanged(sender);

        for (int i = 0; i < capacity; i++)
        {
            _items[i] = new SwiftObservableProperty<TValue>
            {
                Index = i
            };
            _items[i].PropertyChanged += _itemChangedHandler;
        }
    }

    /// <summary>
    /// Initializes a new instance of the SwiftObservableArray class with the specified observable properties.
    /// </summary>
    /// <remarks>
    /// Each property in the array is assigned its index and subscribed to change notifications.
    /// Changes to any property will be observed by the array.
    /// </remarks>
    /// <param name="observableProperties">An array of <see cref="SwiftObservableProperty{TValue}"/> instances to be managed by the array. Cannot be null.</param>
    public SwiftObservableArray(SwiftObservableProperty<TValue>[] observableProperties)
    {
        SwiftThrowHelper.ThrowIfNull(observableProperties, nameof(observableProperties));

        _items = observableProperties;
        _itemChangedHandler = (sender, e) => OnItemChanged(sender);

        for (int i = 0; i < _items.Length; i++)
        {
            _items[i].Index = i;
            _items[i].PropertyChanged += _itemChangedHandler;
        }
    }

    ///  <summary>
    ///  Initializes a new instance of the <see cref="SwiftObservableArray{TValue}"/> class with the specified <see cref="SwiftArrayState{TValue}"/>.
    ///  </summary>
    ///  <param name="state">The state containing the internal array, count, offset, and version for initialization.</param>
    [MemoryPackConstructor]
    public SwiftObservableArray(SwiftArrayState<TValue> state)
    {
        State = state;

        SwiftThrowHelper.ThrowIfNull(_items, nameof(_items));
        SwiftThrowHelper.ThrowIfNull(_itemChangedHandler, nameof(_itemChangedHandler));
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the total number of elements that the internal data structure can hold without resizing.
    /// </summary>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Capacity => _items.Length;

    /// <summary>
    /// Gets or sets the value at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the element to get or set. Must be within the valid range of the collection.</param>
    [JsonIgnore]
    [MemoryPackIgnore]
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

    /// <summary>
    /// Gets or sets the current state of the array, including the values of all items.
    /// </summary>
    /// <remarks>
    /// Setting this property replaces the entire array state, including all item values and their order. 
    /// Any existing item change handlers are reset when the state is set.
    /// </remarks>
    [JsonInclude]
    [MemoryPackInclude]
    public SwiftArrayState<TValue> State
    {
        get
        {
            var values = new TValue[_items.Length];

            for (int i = 0; i < _items.Length; i++)
                values[i] = _items[i].Value;

            return new SwiftArrayState<TValue>(values);
        }
        internal set
        {
            SwiftThrowHelper.ThrowIfNull(value.Items, nameof(value.Items));

            var values = value.Items;

            int capacity = values.Length;

            _items = new SwiftObservableProperty<TValue>[capacity];
            _itemChangedHandler = (sender, e) => OnItemChanged(sender);

            for (int i = 0; i < capacity; i++)
            {
                _items[i] = new SwiftObservableProperty<TValue>(values[i])
                {
                    Index = i
                };
                _items[i].PropertyChanged += _itemChangedHandler;
            }
        }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Returns an array containing all elements in the collection.
    /// </summary>
    /// <returns>
    /// An array of type TValue that contains the values of the collection in order. 
    /// The array will be empty if the collection contains no elements.
    /// </returns>
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

    private void OnItemChanged(object? sender)
    {
        if (sender is SwiftObservableProperty<TValue> property)
        {
            int index = property.Index;

            ElementChanged?.Invoke(
                this,
                new ElementChangedEventArgs<TValue>(index, property.Value)
            );

            OnPropertyChanged("Items[]");
        }
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}
