using MemoryPack;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;


#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#endif
#if !NET8_0_OR_GREATER
using System.Text.Json.Serialization.Shim;
#endif

namespace SwiftCollections.Observable;

/// <summary>
/// Represents an observable extension of the high-performance <see cref="SwiftList{T}"/>.
/// Notifies listeners of changes to its items and structure for reactive programming scenarios.
/// </summary>
/// <typeparam name="T">The type of elements in the list.</typeparam>
[Serializable]
[JsonConverter(typeof(SwiftStateJsonConverterFactory))]
[MemoryPackable]
public partial class SwiftObservableList<T> : SwiftList<T>, INotifyPropertyChanged, INotifyCollectionChanged
{
    #region Events

    /// <summary>
    /// Raised when a property on the list changes.
    /// </summary>
    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>
    /// Raised when the list's collection is modified.
    /// </summary>
    public event NotifyCollectionChangedEventHandler CollectionChanged;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="SwiftObservableList{T}"/> class.
    /// </summary>
    public SwiftObservableList() : base() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SwiftObservableList{T}"/> class with the specified initial capacity.
    /// </summary>
    public SwiftObservableList(int capacity) : base(capacity) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SwiftObservableList{T}"/> class that contains elements copied from the specified collection.
    /// </summary>
    public SwiftObservableList(IEnumerable<T> collection) : base(collection) { }

    ///  <summary>
    ///  Initializes a new instance of the <see cref="SwiftObservableList{T}"/> class with the specified <see cref="SwiftArrayState{T}"/>.
    ///  </summary>
    ///  <param name="state">The state containing the internal array, count, offset, and version for initialization.</param>
    [MemoryPackConstructor]
    public SwiftObservableList(SwiftArrayState<T> state) : base(state) { }

    #endregion

    #region Properties

    /// <summary>
    /// Sets or gets the element at the specified index.
    /// Raises collection change notifications on modification.
    /// </summary>
    [JsonIgnore]
    [MemoryPackIgnore]
    public new T this[int index]
    {
        get => base[index];
        set
        {
            T oldValue = base[index];
            if (!Equals(oldValue, value))
            {
                base[index] = value;
                OnCollectionChanged(NotifyCollectionChangedAction.Replace, oldValue, value, index);
                OnPropertyChanged("InnerArray[]");
            }
        }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Adds an element to the end of the list.
    /// Raises collection and property change notifications.
    /// </summary>
    public override void Add(T item)
    {
        base.Add(item);
        OnCollectionChanged(NotifyCollectionChangedAction.Add, item, _count - 1);
        OnPropertyChanged(nameof(Count));
    }

    public override void AddRange(IEnumerable<T> items)
    {
        if (items == null) ThrowHelper.ThrowArgumentNullException(nameof(items));

        foreach (T item in items)
            Add(item);
    }

    /// <summary>
    /// Removes the first occurrence of a specific object from the list.
    /// Raises collection and property change notifications.
    /// </summary>
    public override bool Remove(T item)
    {
        int index = IndexOf(item);
        if (index >= 0)
        {
            var removedItem = this[index];
            base.RemoveAt(index);
            OnCollectionChanged(NotifyCollectionChangedAction.Remove, removedItem, index);
            OnPropertyChanged(nameof(Count));
            return true;
        }

        return false;
    }

    /// <summary>
    /// Removes the element at the specified index.
    /// Raises collection and property change notifications.
    /// </summary>
    public override void RemoveAt(int index)
    {
        var removedItem = this[index];
        base.RemoveAt(index);
        OnCollectionChanged(NotifyCollectionChangedAction.Remove, removedItem, index);
        OnPropertyChanged(nameof(Count));
    }

    public override int RemoveAll(Predicate<T> match)
    {
        if (match == null) ThrowHelper.ThrowArgumentNullException(nameof(match));

        int i = 0;
        // Move to the first element that should be removed
        while (i < _count && !match(_innerArray[i])) i++;

        if (i >= _count) return 0;  // No items to remove

        int j = i + 1;
        while (j < _count)
        {
            // Find the next element to keep
            while (j < _count && match(_innerArray[j])) j++;

            if (j < _count)
                _innerArray[i++] = _innerArray[j++];
        }

        // Clear out the trailing elements to ensure no lingering references
        Array.Clear(_innerArray, i, _count - i);

        int removedCount = _count - i;
        _count = i;

        _version++;

        return removedCount;
    }

    /// <summary>
    /// Inserts an element into the list at the specified index.
    /// Raises collection and property change notifications.
    /// </summary>
    public override void Insert(int index, T item)
    {
        base.Insert(index, item);
        OnCollectionChanged(NotifyCollectionChangedAction.Add, item, index);
        OnPropertyChanged(nameof(Count));
    }

    /// <summary>
    /// Clears all elements from the list.
    /// Raises a reset collection change notification.
    /// </summary>
    public override void Clear()
    {
        if (_count > 0)
        {
            base.Clear();
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            OnPropertyChanged(nameof(Count));
        }
    }

    #endregion

    #region Notifications

    /// <summary>
    /// Raises the <see cref="PropertyChanged"/> event.
    /// </summary>
    /// <param name="propertyName">The name of the property that changed.</param>
    protected virtual void OnPropertyChanged(string propertyName)
    {
        var handler = PropertyChanged;
        handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Raises the <see cref="CollectionChanged"/> event for the specified action and items.
    /// </summary>
    protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs args)
    {
        var handler = CollectionChanged;
        handler?.Invoke(this, args);
    }

    private void OnCollectionChanged(NotifyCollectionChangedAction action, T item, int index)
    {
        var args = new NotifyCollectionChangedEventArgs(action, item, index);
        OnCollectionChanged(args);
    }

    private void OnCollectionChanged(NotifyCollectionChangedAction action, T oldItem, T newItem, int index)
    {
        var args = new NotifyCollectionChangedEventArgs(action, newItem, oldItem, index);
        OnCollectionChanged(args);
    }

    #endregion
}
