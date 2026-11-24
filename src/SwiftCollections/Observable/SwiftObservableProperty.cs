//=======================================================================
// SwiftObservableProperty.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using MemoryPack;

namespace SwiftCollections.Observable;

/// <summary>
/// Represents a property of type <typeparamref name="TValue" /> that raises a <see
/// cref="E:SwiftCollections.Observable.SwiftObservableProperty`1.PropertyChanged" /> event when its value
/// changes.
/// </summary>
/// <typeparam name="TValue">The type of the value being observed.</typeparam>
/// <remarks>
/// Notifications are synchronous on the calling thread after mutation; subscriber exceptions propagate without
/// rollback and can prevent subsequent notifications. No synchronization or reentrancy guard is provided.
/// Equality uses object.Equals; assigning an equal value suppresses notification.
/// </remarks>
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
    /// Initializes a new instance of the <see cref="T:SwiftCollections.Observable.SwiftObservableProperty`1" />
    /// class with the default value of <typeparamref name="TValue" />.
    /// </summary>
    [MemoryPackConstructor]
    public SwiftObservableProperty() : this(default!) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.Observable.SwiftObservableProperty`1" />
    /// class with the specified initial value.
    /// </summary>
    /// <param name="value">The value to store; reference values are retained without cloning.</param>
    public SwiftObservableProperty(TValue value)
    {
        _value = value;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the property's value. Raises the <see
    /// cref="E:SwiftCollections.Observable.SwiftObservableProperty`1.PropertyChanged" /> event when the value
    /// changes.
    /// </summary>
    /// <value>The stored value reference or value-type copy; contained objects are not cloned.</value>
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
    /// Raises the <see cref="E:SwiftCollections.Observable.SwiftObservableProperty`1.PropertyChanged" /> event
    /// with the specified property name.
    /// </summary>
    /// <param name="propertyName">The property name placed in the event arguments. Omission uses the caller
    /// member name; an explicit empty string is allowed.</param>
    /// <remarks>
    /// Invokes current subscribers synchronously. Exceptions propagate and stop dispatch; no reentrancy or
    /// rollback guard is applied.
    /// </remarks>
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}
