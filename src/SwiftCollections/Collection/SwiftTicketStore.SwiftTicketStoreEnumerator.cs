//=======================================================================
// SwiftTicketStore.SwiftTicketStoreEnumerator.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System.Collections;
using System.Collections.Generic;
using Chronicler.Diagnostics;

namespace SwiftCollections;

public sealed partial class SwiftTicketStore<T>
{
    /// <summary>Enumerates the store's live values in dense order without scanning sparse lookup storage.</summary>
    /// <remarks>Obtain through GetEnumerator; default construction is not initialized. MoveNext and Reset
    /// check a captured finite structural version. Copies advance independently. Reference/span value writes
    /// bypass version checks. Interface enumeration boxes this struct; individual values are copied shallowly.</remarks>
    public struct SwiftTicketStoreEnumerator : IEnumerator<T>
    {
        private readonly SwiftTicketStore<T> _owner;
        private readonly uint _version;
        private int _index;
        private T _current;

        internal SwiftTicketStoreEnumerator(SwiftTicketStore<T> owner)
        {
            _owner = owner;
            _version = owner._version;
            _index = -1;
            _current = default!;
        }

        /// <summary>Gets the shallow value cached by the last successful MoveNext.</summary>
        /// <value>The selected value; read only after MoveNext succeeds and before another advance/reset/dispose.</value>
        /// <remarks>This getter does not check position or structural version.</remarks>
        public readonly T Current => _current;

        /// <summary>Gets the cached selected value after checking enumeration position.</summary>
        /// <value>The selected shallow value, boxed for value types.</value>
        /// <exception cref="System.InvalidOperationException">Enumeration is before its first or after its last value.</exception>
        readonly object IEnumerator.Current
        {
            get
            {
                ChronicleGuard.ThrowIfTrue((uint)_index >= (uint)_owner._count, message: "No current value.");
                return _current!;
            }
        }

        /// <summary>Advances to the next live dense value.</summary>
        /// <returns>True when Current selects a value; false at the end, clearing the cached value.</returns>
        /// <exception cref="System.InvalidOperationException">The owner's structural version changed.</exception>
        public bool MoveNext()
        {
            ChronicleGuard.ThrowIfTrue(_version != _owner._version, message: "Collection modified.");
            int next = _index + 1;
            if (next >= _owner._count)
            {
                _index = _owner._count;
                _current = default!;
                return false;
            }
            _index = next;
            _current = _owner._values[next];
            return true;
        }

        /// <summary>Rewinds this enumerator and clears its cached value.</summary>
        /// <exception cref="System.InvalidOperationException">The owner's structural version changed.</exception>
        public void Reset()
        {
            ChronicleGuard.ThrowIfTrue(_version != _owner._version, message: "Collection modified.");
            _index = -1;
            _current = default!;
        }

        /// <summary>Ends this enumerator and releases its cached value without changing the owner.</summary>
        /// <remarks>Do not use Current after disposal. Reset can rewind while the captured version still matches.</remarks>
        public void Dispose()
        {
            _index = _owner._count;
            _current = default!;
        }
    }
}

