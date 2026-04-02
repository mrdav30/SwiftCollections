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

    protected SwiftObservableProperty<TValue>[] _items;

    protected PropertyChangedEventHandler _itemChangedHandler;

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
    }

    #endregion

    #region Properties

    [JsonIgnore]
    [MemoryPackIgnore]
    public int Capacity => _items.Length;

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
            var values = value.Items;

            _itemChangedHandler = (sender, e) => OnItemChanged(sender);

            _items = new SwiftObservableProperty<TValue>[values.Length];

            for (int i = 0; i < values.Length; i++)
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
