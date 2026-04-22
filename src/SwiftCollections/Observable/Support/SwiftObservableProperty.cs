using MemoryPack;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace SwiftCollections.Observable;

/// <summary>
/// Represents a property of type <typeparamref name="TValue"/> that raises a <see cref="PropertyChanged"/> event when its value changes.
/// </summary>
/// <typeparam name="TValue">The type of the value being observed.</typeparam>
[Serializable]
[MemoryPackable]
public partial class SwiftObservableProperty<TValue> : INotifyPropertyChanged
{
    #region Fields

    internal int Index;

    [JsonInclude]
    [MemoryPackInclude]
    private TValue _value;

    #endregion

    #region Events

    /// <summary>
    /// Raised whenever the property's value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="SwiftObservableProperty{TValue}"/> class with the default value of <typeparamref name="TValue"/>.
    /// </summary>
    [MemoryPackConstructor]
    public SwiftObservableProperty() : this(default!) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SwiftObservableProperty{TValue}"/> class with the specified initial value.
    /// </summary>
    /// <param name="value">The initial value of the property.</param>
    public SwiftObservableProperty(TValue value)
    {
        _value = value;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the property's value. Raises the <see cref="PropertyChanged"/> event when the value changes.
    /// </summary>
    [JsonIgnore]
    [MemoryPackIgnore]
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
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}
