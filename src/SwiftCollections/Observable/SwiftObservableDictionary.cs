//=======================================================================
// SwiftObservableDictionary.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.Json.Serialization;
using Chronicler;
using MemoryPack;
using SwiftCollections.Serialization;

namespace SwiftCollections.Observable;

/// <summary>
/// Extends a dictionary with synchronous notifications from its observable mutation members.
/// </summary>
/// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
/// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
/// <remarks>
/// Notifications are synchronous on the calling thread after mutation; subscriber exceptions propagate without
/// rollback and can prevent subsequent notifications. No synchronization or reentrancy guard is provided. Only
/// the observable mutation overrides raise notifications, including indexer assignments through base and
/// dictionary interfaces. Direct backing-storage writes and mutations without observable overrides can bypass
/// notifications.
/// </remarks>
[Serializable]
[JsonConverter(typeof(SwiftCollectionJsonConverterFactory))]
[MemoryPackable(GenerateType.NoGenerate)]
public partial class SwiftObservableDictionary<TKey, TValue> : SwiftDictionary<TKey, TValue>, IStateBacked<SwiftDictionaryState<TKey, TValue>>, INotifyPropertyChanged, INotifyCollectionChanged
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

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.Observable.SwiftObservableDictionary`2" />
    /// class with the specified <see cref="T:SwiftCollections.SwiftDictionaryState`2" />.
    /// </summary>
    /// <param name="state">The state to shallow-copy; Items must be non-null. Event subscribers are not
    /// serialized.</param>
    /// <exception cref="T:System.ArgumentNullException">state.Items is null, including default state.</exception>
    /// <remarks>
    /// This raw-state constructor uses Swift default equality and keeps the first mapping for each equal key.
    /// The raw DTO carries no comparer metadata; versioned JSON and standard MemoryPack owner persistence instead
    /// restore supported rules without restoration notifications. To select custom equality explicitly, create
    /// an empty observable dictionary, call SetComparer, then add
    /// the state pairs. Applying the comparer after restore cannot recover discarded mappings.
    /// </remarks>
    [MemoryPackConstructor]
    public SwiftObservableDictionary(SwiftDictionaryState<TKey, TValue> state) : base(state) { }

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the value associated with the specified key. Raises property and collection change
    /// notifications.
    /// </summary>
    /// <param name="key">The key to locate or insert; null is rejected for insertion and treated as absent by
    /// base lookup.</param>
    /// <value>The current element; assignment changes storage only when object.Equals reports a
    /// difference.</value>
    /// <exception cref="T:System.Collections.Generic.KeyNotFoundException">The getter index/key is absent or
    /// outside the permitted range.</exception>
    /// <remarks>
    /// A successful change raises CollectionChanged first, then PropertyChanged. Events run after mutation and
    /// exceptions do not roll it back. Base and dictionary-interface assignments use this override.
    /// </remarks>
    [JsonIgnore]
    [MemoryPackIgnore]
    public override TValue this[TKey key]
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
            else
            {
                Add(key, value);
            }
        }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Attempts to add a mapping, notifying subscribers only when insertion succeeds.
    /// </summary>
    /// <param name="key">The key to insert, compared using the current key comparer. Null is rejected.</param>
    /// <param name="value">The value to store; reference values are retained without cloning.</param>
    /// <returns>true if a mapping was inserted; false if an equal key already exists, without notifications.</returns>
    /// <exception cref="T:System.ArgumentNullException">key is null.</exception>
    /// <remarks>
    /// Successful insertion changes storage before CollectionChanged(Add), followed by PropertyChanged(Entries[]).
    /// A subscriber exception does not roll back insertion and can prevent later notifications. Duplicate
    /// rejection preserves the existing mapping and Count, but the base load-threshold check can resize storage
    /// before lookup or key validation, invalidating enumerators even when insertion is rejected.
    /// </remarks>
    public override bool Add(TKey key, TValue value)
    {
        bool result = base.Add(key, value);
        if (result)
        {
            OnCollectionChanged(NotifyCollectionChangedAction.Add, key, value);
            OnPropertyChanged("Entries[]");
        }
        return result;
    }

    /// <summary>
    /// Removes the element with the specified key from the dictionary. Raises a collection change notification if
    /// the key existed.
    /// </summary>
    /// <param name="key">The key to locate or insert; null is rejected for insertion and treated as absent by
    /// base lookup.</param>
    /// <returns>true if one matching entry was removed; false if absent, with no notification.</returns>
    /// <remarks>
    /// On removal, raises CollectionChanged(Remove) then PropertyChanged(Entries[]). A subscriber exception
    /// occurs after the removal.
    /// </remarks>
    public override bool Remove(TKey key)
    {
        if (TryGetValue(key, out TValue value))
        {
            base.Remove(key);
            OnCollectionChanged(NotifyCollectionChangedAction.Remove, key, value);
            OnPropertyChanged("Entries[]"); // Notify that the dictionary contents have changed
            return true;
        }
        return false;
    }

    /// <summary>
    /// Clears all elements from the dictionary. Raises a reset collection change notification.
    /// </summary>
    /// <remarks>
    /// If non-empty, clears base storage then raises CollectionChanged(Reset), followed by
    /// PropertyChanged(Count). Empty collections emit nothing; capacity is preserved.
    /// </remarks>
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
    /// Raises the <see cref="E:SwiftCollections.Observable.SwiftObservableDictionary`2.PropertyChanged" /> event.
    /// </summary>
    /// <param name="propertyName">The property name placed in the event arguments. Forwarded as supplied,
    /// including null/empty names at runtime.</param>
    /// <remarks>
    /// Invokes current subscribers synchronously. Exceptions propagate and stop dispatch; no reentrancy or
    /// rollback guard is applied.
    /// </remarks>
    protected virtual void OnPropertyChanged(string propertyName)
    {
        var handler = PropertyChanged;
        handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Raises the <see cref="E:SwiftCollections.Observable.SwiftObservableDictionary`2.CollectionChanged" />
    /// event for the specified action and items.
    /// </summary>
    /// <param name="args">The collection-change event arguments forwarded to subscribers without copying or
    /// validation.</param>
    /// <remarks>
    /// Invokes current subscribers synchronously. Exceptions propagate and stop dispatch; no reentrancy or
    /// rollback guard is applied.
    /// </remarks>
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
