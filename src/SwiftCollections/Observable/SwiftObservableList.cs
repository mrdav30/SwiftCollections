//=======================================================================
// SwiftObservableList.cs
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
using Chronicler.Diagnostics;
using Chronicler.Serialization;
using MemoryPack;

namespace SwiftCollections.Observable;

/// <summary>
/// Extends a list with synchronous notifications from its observable mutation members.
/// </summary>
/// <typeparam name="T">The type of elements in the list.</typeparam>
/// <remarks>
/// Notifications are synchronous on the calling thread after mutation; subscriber exceptions propagate without
/// rollback and can prevent subsequent notifications. No synchronization or reentrancy guard is provided. Only
/// the explicitly overridden/hidden mutation members raise notifications. Base-typed indexer writes, direct
/// buffers, inherited FastClear, sort, reverse, and other non-overridden mutators can bypass notifications.
/// </remarks>
[Serializable]
[JsonConverter(typeof(StateJsonConverterFactory))]
[MemoryPackable]
public partial class SwiftObservableList<T> : SwiftList<T>, IStateBacked<SwiftArrayState<T>>, INotifyPropertyChanged, INotifyCollectionChanged
{
    #region Events

    /// <summary>
    /// Raised when a property on the list changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raised when the list's collection is modified.
    /// </summary>
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.Observable.SwiftObservableList`1" />
    /// class.
    /// </summary>
    public SwiftObservableList() : base() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.Observable.SwiftObservableList`1" /> class
    /// with the specified initial capacity.
    /// </summary>
    /// <param name="capacity">The initial cell capacity; zero starts empty; negative values use the base list
    /// minimum allocation.</param>
    public SwiftObservableList(int capacity) : base(capacity) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.Observable.SwiftObservableList`1" /> class
    /// that contains elements copied from the specified collection.
    /// </summary>
    /// <param name="collection">The elements to shallow-copy in source order. Null reference sources are
    /// rejected. A source enumerable must remain stable during copying.</param>
    public SwiftObservableList(IEnumerable<T> collection) : base(collection) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.Observable.SwiftObservableList`1" /> class
    /// with the specified <see cref="T:SwiftCollections.SwiftArrayState`1" />.
    /// </summary>
    /// <param name="state">The state to shallow-copy; Items must be non-null. Event subscribers are not
    /// serialized.</param>
    /// <exception cref="T:System.ArgumentNullException">state.Items is null, including default state.</exception>
    [MemoryPackConstructor]
    public SwiftObservableList(SwiftArrayState<T> state) : base(state) { }

    #endregion

    #region Properties

    /// <summary>
    /// Sets or gets the element at the specified index. Raises collection change notifications on modification.
    /// </summary>
    /// <param name="index">The zero-based logical index; must address an existing cell or element.</param>
    /// <value>The current element; assignment changes storage only when object.Equals reports a
    /// difference.</value>
    /// <exception cref="T:System.ArgumentOutOfRangeException">The getter index/key is absent or outside the
    /// permitted range.</exception>
    /// <remarks>
    /// A successful change raises CollectionChanged/ElementChanged first, then PropertyChanged. Events run after
    /// mutation and exceptions do not roll it back. Hidden indexers are bypassed by base-typed calls.
    /// </remarks>
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
    /// Adds an element to the end of the list. Raises collection and property change notifications.
    /// </summary>
    /// <param name="item">The element to store or compare; reference values are not cloned.</param>
    /// <remarks>
    /// Mutates through the base implementation, then emits CollectionChanged(Add), followed by
    /// PropertyChanged(Count). An event-handler exception leaves the added element present.
    /// </remarks>
    public override void Add(T item)
    {
        base.Add(item);
        OnCollectionChanged(NotifyCollectionChangedAction.Add, item, _count - 1);
        OnPropertyChanged(nameof(Count));
    }

    /// <summary>
    /// Adds the elements of the specified collection to the end of the current collection.
    /// </summary>
    /// <param name="items">The elements to shallow-copy in source order. Null reference sources are rejected. A
    /// source enumerable must remain stable during copying.</param>
    /// <remarks>
    /// Appends in source order by calling Add for every element; raises one notification pair per added element.
    /// Empty input raises none. Enumeration or subscriber failure leaves a partially appended prefix.
    /// </remarks>
    public override void AddRange(IEnumerable<T> items)
    {
        ChronicleGuard.ThrowIfNull(items, nameof(items));

        if (items is ICollection<T> collection)
        {
            AddKnownCountRange(collection, collection.Count);
            return;
        }

        if (items is IReadOnlyCollection<T> readOnlyCollection)
        {
            AddKnownCountRange(readOnlyCollection, readOnlyCollection.Count);
            return;
        }

        foreach (T item in items)
            Add(item);
    }

    private void AddKnownCountRange(IEnumerable<T> items, int count)
    {
        if (count > 0)
        {
            EnsureAdditionalCapacity(count);

            foreach (T item in items)
                Add(item);
        }
    }

    /// <summary>
    /// Adds the elements of the specified array to the end of the list. Raises collection and property change
    /// notifications for each added item.
    /// </summary>
    /// <param name="items">The elements to shallow-copy in source order. Null reference sources are rejected. A
    /// source enumerable must remain stable during copying.</param>
    /// <remarks>
    /// Appends in source order by calling Add for every element; raises one notification pair per added element.
    /// Empty input raises none. Enumeration or subscriber failure leaves a partially appended prefix.
    /// </remarks>
    public override void AddRange(T[] items)
    {
        ChronicleGuard.ThrowIfNull(items, nameof(items));
        AddRange(items.AsSpan());
    }

    /// <summary>
    /// Adds the elements of the specified span to the end of the list. Raises collection and property change
    /// notifications for each added item.
    /// </summary>
    /// <param name="items">The elements to shallow-copy in source order. Null reference sources are rejected. A
    /// source enumerable must remain stable during copying.</param>
    /// <remarks>
    /// Appends in source order by calling Add for every element; raises one notification pair per added element.
    /// Empty input raises none. Enumeration or subscriber failure leaves a partially appended prefix.
    /// </remarks>
    public override void AddRange(ReadOnlySpan<T> items)
    {
        EnsureAdditionalCapacity(items.Length);

        for (int i = 0; i < items.Length; i++)
            Add(items[i]);
    }

    private void EnsureAdditionalCapacity(int additionalCount)
    {
        if (additionalCount > 0)
        {
            long requiredCount = (long)_count + additionalCount;
            ChronicleGuard.ThrowIfTrue(requiredCount > int.MaxValue, message: "The collection is too large.");
            EnsureCapacity((int)requiredCount);
        }
    }

    /// <summary>
    /// Removes the first occurrence of a specific object from the list. Raises collection and property change
    /// notifications.
    /// </summary>
    /// <param name="item">The element to store or compare; reference values are not cloned.</param>
    /// <returns>true if one matching entry was removed; false if absent, with no notification.</returns>
    /// <remarks>
    /// On removal, raises CollectionChanged(Remove) then PropertyChanged(Count). A subscriber exception occurs
    /// after the removal.
    /// </remarks>
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
    /// Removes the element at the specified index. Raises collection and property change notifications.
    /// </summary>
    /// <param name="index">The zero-based logical index; must address an existing cell or element.</param>
    /// <remarks>
    /// Changes base storage, then raises the corresponding collection event followed by Count. Subscriber
    /// exceptions leave the mutation applied.
    /// </remarks>
    public override void RemoveAt(int index)
    {
        var removedItem = this[index];
        base.RemoveAt(index);
        OnCollectionChanged(NotifyCollectionChangedAction.Remove, removedItem, index);
        OnPropertyChanged(nameof(Count));
    }

    /// <summary>
    /// Removes all elements from the collection that match the conditions defined by the specified predicate.
    /// </summary>
    /// <param name="match">The non-null predicate; must not mutate this collection.</param>
    /// <returns>The number removed; zero emits no notifications.</returns>
    /// <remarks>
    /// Uses base compaction, then raises Reset and Count once if any item was removed. A throwing predicate can
    /// leave partial compaction; no rollback is provided.
    /// </remarks>
    public override int RemoveAll(Predicate<T> match)
    {
        int removedCount = base.RemoveAll(match);

        if (removedCount > 0)
        {
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            OnPropertyChanged(nameof(Count));
        }

        return removedCount;
    }

    /// <summary>
    /// Inserts an element into the list at the specified index. Raises collection and property change
    /// notifications.
    /// </summary>
    /// <param name="index">The zero-based logical index; Count is also allowed when inserting.</param>
    /// <param name="item">The element to store or compare; reference values are not cloned.</param>
    /// <remarks>
    /// Changes base storage, then raises the corresponding collection event followed by Count. Subscriber
    /// exceptions leave the mutation applied.
    /// </remarks>
    public override void Insert(int index, T item)
    {
        base.Insert(index, item);
        OnCollectionChanged(NotifyCollectionChangedAction.Add, item, index);
        OnPropertyChanged(nameof(Count));
    }

    /// <summary>
    /// Clears all elements from the list. Raises a reset collection change notification.
    /// </summary>
    /// <remarks>
    /// If non-empty, clears base storage then raises CollectionChanged(Reset), followed by
    /// PropertyChanged(Count). Empty collections emit nothing; capacity is preserved.
    /// </remarks>
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
    /// Raises the <see cref="E:SwiftCollections.Observable.SwiftObservableList`1.PropertyChanged" /> event.
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
    /// Raises the <see cref="E:SwiftCollections.Observable.SwiftObservableList`1.CollectionChanged" /> event for
    /// the specified action and items.
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
