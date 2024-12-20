using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

namespace SwiftCollections.Observable
{
    /// <summary>
    /// Represents a dictionary that notifies listeners of changes to its items and structure.
    /// </summary>
    /// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
    public class SwiftObservableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, INotifyPropertyChanged, INotifyCollectionChanged
    {
        #region Events

        /// <summary>
        /// Raised when a property on the dictionary changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raised when the dictionary's collection is modified.
        /// </summary>
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the value associated with the specified key.
        /// Raises property and collection change notifications.
        /// </summary>
        public new TValue this[TKey key]
        {
            get => base[key];
            set
            {
                if (TryGetValue(key, out TValue oldValue))
                {
                    if(!Equals(oldValue, value))
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
        public new void Add(TKey key, TValue value)
        {
            base.Add(key, value);
            OnCollectionChanged(NotifyCollectionChangedAction.Add, key, value);
            OnPropertyChanged("Entries[]"); // Notify that the dictionary contents have changed
        }

        /// <summary>
        /// Removes the element with the specified key from the dictionary.
        /// Raises a collection change notification if the key existed.
        /// </summary>
        public new bool Remove(TKey key)
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
        public new void Clear()
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
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Raises the <see cref="CollectionChanged"/> event for the specified action and items.
        /// </summary>
        protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs args)
        {
            CollectionChanged?.Invoke(this, args);
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
}
