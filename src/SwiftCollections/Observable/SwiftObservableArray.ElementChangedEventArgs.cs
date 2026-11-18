//=======================================================================
// SwiftObservableArray.ElementChangedEventArgs.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;

namespace SwiftCollections.Observable;

public partial class SwiftObservableArray<TValue>
{
    /// <summary>
    /// Provides details about a changed element, including its index and new value.
    /// </summary>
    /// <typeparam name="T">The type of the changed value.</typeparam>
    /// <remarks>
    /// Captures index and value for one notification. Reference-type values are shared, not cloned.
    /// </remarks>
    public class ElementChangedEventArgs<T> : EventArgs
    {
        /// <summary>
        /// The index of the changed element.
        /// </summary>
        /// <value>The zero-based cell index reported at the time the notification was created.</value>
        public int Index { get; }

        /// <summary>
        /// The new value of the changed element.
        /// </summary>
        /// <value>The stored value reference or value-type copy; contained objects are not cloned.</value>
        public T NewValue { get; }

        internal ElementChangedEventArgs(int index, T newValue)
        {
            Index = index;
            NewValue = newValue;
        }
    }
}
