using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SwiftCollections.Observable
{
    /// <summary>
    /// Represents a property of type <typeparamref name="TValue"/> that raises a <see cref="PropertyChanged"/> event when its value changes.
    /// </summary>
    /// <typeparam name="TValue">The type of the value being observed.</typeparam>
    public class ObservableProperty<TValue> : INotifyPropertyChanged
    {
        #region Fields

        private TValue _value;

        #endregion

        #region Events

        /// <summary>
        /// Raised whenever the property's value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ObservableProperty{TValue}"/> class with the default value of <typeparamref name="TValue"/>.
        /// </summary>
        public ObservableProperty()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ObservableProperty{TValue}"/> class with the specified initial value.
        /// </summary>
        /// <param name="value">The initial value of the property.</param>
        public ObservableProperty(TValue value)
        {
            _value = value;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the property's value. Raises the <see cref="PropertyChanged"/> event when the value changes.
        /// </summary>
        public TValue Value
        {
            get => _value;
            set
            {
                if (!Equals(_value, value))
                {
                    _value = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Raises the <see cref="PropertyChanged"/> event with the specified property name.
        /// </summary>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
