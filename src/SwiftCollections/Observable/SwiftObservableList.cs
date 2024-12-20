using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

namespace SwiftCollections.Observable
{
    /// <summary>
    /// Represents an observable extension of the high-performance <see cref="SwiftList{T}"/>.
    /// Notifies listeners of changes to its items and structure for reactive programming scenarios.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    public class SwiftObservableList<T> : SwiftList<T>, INotifyPropertyChanged, INotifyCollectionChanged
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

        #endregion

        #region Methods

        /// <summary>
        /// Adds an element to the end of the list.
        /// Raises collection and property change notifications.
        /// </summary>
        public new void Add(T item)
        {
            base.Add(item);
            OnCollectionChanged(NotifyCollectionChangedAction.Add, item, _count - 1);
            OnPropertyChanged(nameof(_count));
        }

        /// <summary>
        /// Removes the first occurrence of a specific object from the list.
        /// Raises collection and property change notifications.
        /// </summary>
        public new bool Remove(T item)
        {
            int index = IndexOf(item);
            if (index >= 0)
            {
                var removedItem = this[index];
                base.RemoveAt(index);
                OnCollectionChanged(NotifyCollectionChangedAction.Remove, removedItem, index);
                OnPropertyChanged(nameof(_count));
                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes the element at the specified index.
        /// Raises collection and property change notifications.
        /// </summary>
        public new void RemoveAt(int index)
        {
            var removedItem = this[index];
            base.RemoveAt(index);
            OnCollectionChanged(NotifyCollectionChangedAction.Remove, removedItem, index);
            OnPropertyChanged(nameof(_count));
        }

        /// <summary>
        /// Inserts an element into the list at the specified index.
        /// Raises collection and property change notifications.
        /// </summary>
        public new void Insert(int index, T item)
        {
            base.Insert(index, item);
            OnCollectionChanged(NotifyCollectionChangedAction.Add, item, index);
            OnPropertyChanged(nameof(_count));
        }

        /// <summary>
        /// Clears all elements from the list.
        /// Raises a reset collection change notification.
        /// </summary>
        public new void Clear()
        {
            if (_count > 0)
            {
                base.Clear();
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                OnPropertyChanged(nameof(_count));
            }
        }

        /// <summary>
        /// Sets or gets the element at the specified index.
        /// Raises collection change notifications on modification.
        /// </summary>
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
}
