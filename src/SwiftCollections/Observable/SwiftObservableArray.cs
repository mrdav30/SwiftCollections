//=======================================================================
// SwiftObservableArray.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.ComponentModel;
using System.Text.Json.Serialization;
using Chronicler;
using Chronicler.Diagnostics;
using Chronicler.Serialization;
using MemoryPack;

namespace SwiftCollections.Observable;

/// <summary>
/// Stores observable property cells and forwards their value-change notifications.
/// </summary>
/// <typeparam name="TValue">The type of elements in the array.</typeparam>
/// <remarks>
/// Notifications are synchronous on the calling thread after mutation; subscriber exceptions propagate without
/// rollback and can prevent subsequent notifications. No synchronization or reentrancy guard is provided.
/// Supplied observable properties are retained and subscribed; do not replace array elements or share properties
/// across owners with different indices.
/// </remarks>
[Serializable]
[JsonConverter(typeof(StateJsonConverterFactory))]
[MemoryPackable]
public partial class SwiftObservableArray<TValue> : IStateBacked<SwiftArrayState<TValue>>, INotifyPropertyChanged
{
    #region Fields

    /// <summary>
    /// Represents the collection of observable properties managed by this instance.
    /// </summary>
    /// <remarks>
    /// Each element in the array corresponds to an individual observable property. The array may be null or empty
    /// if no properties are currently managed.
    /// </remarks>
    protected SwiftObservableProperty<TValue>[] _items;

    /// <summary>
    /// Represents the event handler that is invoked when a property value changes on an item.
    /// </summary>
    /// <remarks>
    /// This handler is typically used to subscribe to property change notifications for items within a collection
    /// or container. Derived classes can use this field to manage event subscriptions for item property changes.
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

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the SwiftObservableArray class with the specified capacity.
    /// </summary>
    /// <param name="capacity">The initial cell capacity; must be strictly positive.</param>
    /// <exception cref="T:System.ArgumentOutOfRangeException">capacity is zero or negative.</exception>
    /// <remarks>
    /// Each element in the array is initialized with a new <see
    /// cref="T:SwiftCollections.Observable.SwiftObservableProperty`1" /> instance. The capacity determines the
    /// fixed size of the array and cannot be changed after construction.
    /// </remarks>
    public SwiftObservableArray(int capacity)
    {
        ChronicleGuard.ThrowIfNegativeOrZero(capacity, nameof(capacity));

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
    /// <param name="observableProperties">The non-null array to retain directly. Each non-null property has its
    /// Index changed and is subscribed; the array and property objects remain shared.</param>
    /// <exception cref="T:System.ArgumentNullException">observableProperties is null.</exception>
    /// <exception cref="T:System.NullReferenceException">An array entry is null; earlier entries may already have
    /// been indexed/subscribed.</exception>
    /// <remarks>
    /// Each property in the array is assigned its index and subscribed to change notifications. Changes to any
    /// property will be observed by the array.
    /// </remarks>
    public SwiftObservableArray(SwiftObservableProperty<TValue>[] observableProperties)
    {
        ChronicleGuard.ThrowIfNull(observableProperties, nameof(observableProperties));

        _items = observableProperties;
        _itemChangedHandler = (sender, e) => OnItemChanged(sender);

        for (int i = 0; i < _items.Length; i++)
        {
            _items[i].Index = i;
            _items[i].PropertyChanged += _itemChangedHandler;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.Observable.SwiftObservableArray`1" />
    /// class with the specified <see cref="T:SwiftCollections.SwiftArrayState`1" />.
    /// </summary>
    /// <param name="state">The state to shallow-copy; Items must be non-null. Event subscribers are not
    /// serialized.</param>
    /// <exception cref="T:System.ArgumentNullException">state.Items is null, including default state.</exception>
    [MemoryPackConstructor]
    public SwiftObservableArray(SwiftArrayState<TValue> state)
    {
        State = state;

        ChronicleGuard.ThrowIfNull(_items, nameof(_items));
        ChronicleGuard.ThrowIfNull(_itemChangedHandler, nameof(_itemChangedHandler));
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the total number of elements that the internal data structure can hold without resizing.
    /// </summary>
    /// <value>The number of observable cells, equal to the retained property-array length.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Capacity => _items.Length;

    /// <summary>
    /// Gets or sets the value at the specified index.
    /// </summary>
    /// <param name="index">The zero-based logical index; must address an existing cell or element.</param>
    /// <value>The current element; assignment changes storage only when object.Equals reports a
    /// difference.</value>
    /// <exception cref="T:System.IndexOutOfRangeException">The getter index/key is absent or outside the
    /// permitted range.</exception>
    /// <remarks>
    /// A changed cell value raises ElementChanged first, then PropertyChanged with the name Items[]. Events run
    /// after mutation, on the calling thread; subscriber exceptions do not roll back the stored value and can
    /// prevent the subsequent notification.
    /// </remarks>
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
    /// Gets a shallow snapshot of the observable cell values.
    /// </summary>
    /// <value>A newly allocated item array; event handlers and observable wrappers are not part of the
    /// snapshot.</value>
    /// <remarks>
    /// Restore creates fresh observable wrappers and subscriptions; referenced element objects remain shared.
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
            ChronicleGuard.ThrowIfNull(value.Items, nameof(value.Items));

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
    /// <returns>A new array with shallow copies of each cell value.</returns>
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
