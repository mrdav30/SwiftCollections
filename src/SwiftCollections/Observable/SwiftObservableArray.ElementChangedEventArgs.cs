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
    public class ElementChangedEventArgs<T> : EventArgs
    {
        /// <summary>
        /// The index of the changed element.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// The new value of the changed element.
        /// </summary>
        public T NewValue { get; }

        internal ElementChangedEventArgs(int index, T newValue)
        {
            Index = index;
            NewValue = newValue;
        }
    }
}
