using System;
using System.Collections;
using System.Collections.Generic;

namespace SwiftCollections.Tests;

internal sealed class CapacityObservingReadOnlyCollection<T> : IReadOnlyCollection<T>
{
    private readonly T[] _items;
    private readonly Func<int> _observeCapacity;

    public CapacityObservingReadOnlyCollection(T[] items, Func<int> observeCapacity)
    {
        _items = items;
        _observeCapacity = observeCapacity;
        ObservedCapacity = -1;
    }

    public int Count => _items.Length;

    public int ObservedCapacity { get; private set; }

    public IEnumerator<T> GetEnumerator() => new Enumerator(this);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private sealed class Enumerator : IEnumerator<T>
    {
        private readonly CapacityObservingReadOnlyCollection<T> _owner;
        private int _index = -1;

        public Enumerator(CapacityObservingReadOnlyCollection<T> owner)
        {
            _owner = owner;
        }

        public T Current => _owner._items[_index];

        object IEnumerator.Current => Current!;

        public bool MoveNext()
        {
            if (_index < 0)
                _owner.ObservedCapacity = _owner._observeCapacity();

            _index++;
            return _index < _owner._items.Length;
        }

        public void Reset()
        {
            _index = -1;
        }

        public void Dispose()
        {
        }
    }
}
