//=======================================================================
// SwiftDictionary.Persistence.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Collections.Generic;
using Chronicler.Diagnostics;
using SwiftCollections.Utility;

namespace SwiftCollections;

public partial class SwiftDictionary<TKey, TValue> where TKey : notnull
{
    internal virtual bool HasAdditionalComparerRoles => false;

    internal SwiftDictionary(SwiftDictionaryState<TKey, TValue> state, IEqualityComparer<TKey> comparer)
    {
        _entries = null!;
        _comparer = comparer;
        RestoreEntries(state, comparer);
    }

    internal static SwiftDictionary<TKey, TValue> RestoreTrusted(SwiftDictionaryState<TKey, TValue> state,
        IEqualityComparer<TKey> comparer) => new(state, comparer);

    // Used only while constructing an unpublished owner, including inherited dictionary families.
    internal void RestoreEntries(SwiftDictionaryState<TKey, TValue> state, IEqualityComparer<TKey> comparer)
    {
        ChronicleGuard.ThrowIfNull(state.Items, nameof(state));
        ChronicleGuard.ThrowIfNull(comparer, nameof(comparer));
        int capacity = SwiftHashTools.GetPersistenceCapacity(state.Items.Length, DefaultCapacity, 82);
        var entries = new Entry[capacity];
        int mask = capacity - 1, last = 0, maximumStep = 0;
        foreach (KeyValuePair<TKey, TValue> item in state.Items)
        {
            ChronicleGuard.ThrowIfNullGeneric(item.Key, nameof(state));
            int hash = comparer.GetHashCode(item.Key) & 0x7FFFFFFF;
            int index = hash & mask, step = 1;
            uint limit = (uint)capacity * 2;
            while (entries[index].IsUsed)
            {
                if (entries[index].HashCode == hash && comparer.Equals(entries[index].Key, item.Key))
                    throw new InvalidOperationException("Persisted dictionary contains duplicate equal keys.");
                if ((uint)step >= limit)
                    throw new InvalidOperationException("Persisted dictionary cannot place an entry in its bounded probe sequence.");
                index = unchecked(index + step * step) & mask;
                step++;
            }
            entries[index] = new Entry { Key = item.Key, Value = item.Value, HashCode = hash, IsUsed = true };
            if (index > last) last = index;
            if (step > maximumStep) maximumStep = step;
        }

        _entries = entries;
        _comparer = comparer;
        _entryMask = mask;
        _count = state.Items.Length;
        _lastIndex = last;
        _maxStepCount = maximumStep;
        _nextResizeCount = (uint)(capacity * _LoadFactorThreshold);
        _adaptiveResizeFactor = 4;
        _movingFillRate = 0;
        _version = 0;
    }
}
