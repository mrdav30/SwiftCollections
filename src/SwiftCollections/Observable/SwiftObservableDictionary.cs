using MemoryPack;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace SwiftCollections.Observable;

/// <summary>
/// Represents a dictionary that notifies listeners of changes to its items and structure.
/// </summary>
/// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
/// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
[Serializable]
[JsonConverter(typeof(SwiftStateJsonConverterFactory))]
[MemoryPackable]
public partial class SwiftObservableDictionary<TKey, TValue> : SwiftDictionary<TKey, TValue>, INotifyPropertyChanged, INotifyCollectionChanged
    where TKey : notnull
{
    #region Events

    /// <summary>
    /// Raised when a property on the dictionary changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raised when the dictionary's collection is modified.
    /// </summary>
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the SwiftObservableDictionary class.
    /// </summary>
    public SwiftObservableDictionary() : base() { }

    ///  <summary>
    ///  Initializes a new instance of the <see cref="SwiftObservableDictionary{TKey, TValue}"/> class with the specified <see cref="SwiftDictionaryState{TKey, TValue}"/>.
    ///  </summary>
    ///  <param name="state">The state containing the internal array, count, offset, and version for initialization.</param>
    [MemoryPackConstructor]
    public SwiftObservableDictionary(SwiftDictionaryState<TKey, TValue> state) : base(state) { }

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the value associated with the specified key.
    /// Raises property and collection change notifications.
    /// </summary>
    [JsonIgnore]
    [MemoryPackIgnore]
    public new TValue this[TKey key]
    {
        get => base[key];
        set
        {
            if (TryGetValue(key, out TValue oldValue))
            {
                if (!Equals(oldValue, value))
                {
                    base[key] = value;
                    OnCollectionChanged(NotifyCollectionChangedAction.Replace, key, oldValue, value);
                    OnPropertyChanged("Entries[]");
                }
            }
        }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Adds an element with the provided key and value to the dictionary.
    /// Raises a collection change notification.
    /// </summary>
    public override bool Add(TKey key, TValue value)
    {
        bool result = base.Add(key, value);
        OnCollectionChanged(NotifyCollectionChangedAction.Add, key, value);
        OnPropertyChanged("Entries[]"); // Notify that the dictionary contents have changed
        return result;
    }

    /// <summary>
    /// Removes the element with the specified key from the dictionary.
    /// Raises a collection change notification if the key existed.
    /// </summary>
    public override bool Remove(TKey key)
    {
        if (TryGetValue(key, out TValue value))
        {
            bool removed = base.Remove(key);
            if (removed)
            {
                OnCollectionChanged(NotifyCollectionChangedAction.Remove, key, value);
                OnPropertyChanged("Entries[]"); // Notify that the dictionary contents have changed
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Clears all elements from the dictionary.
    /// Raises a reset collection change notification.
    /// </summary>
    public override void Clear()
    {
        if (Count > 0)
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

    private void OnCollectionChanged(NotifyCollectionChangedAction action, TKey key, TValue value)
    {
        var entry = new KeyValuePair<TKey, TValue>(key, value);
        var args = new NotifyCollectionChangedEventArgs(action, entry);
        OnCollectionChanged(args);
    }

    private void OnCollectionChanged(NotifyCollectionChangedAction action, TKey key, TValue oldValue, TValue newValue)
    {
        var oldEntry = new KeyValuePair<TKey, TValue>(key, oldValue);
        var newEntry = new KeyValuePair<TKey, TValue>(key, newValue);
        var args = new NotifyCollectionChangedEventArgs(action, newEntry, oldEntry);
        OnCollectionChanged(args);
    }

    #endregion
}
