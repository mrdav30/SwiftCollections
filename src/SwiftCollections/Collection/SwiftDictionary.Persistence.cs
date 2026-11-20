//=======================================================================
// SwiftDictionary.Persistence.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Chronicler.Diagnostics;
using SwiftCollections.Utility;

namespace SwiftCollections;

public partial class SwiftDictionary<TKey, TValue> where TKey : notnull
{
    internal virtual bool HasAdditionalComparerRoles => false;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal SwiftDictionary(SwiftDictionaryState<TKey, TValue> state, IEqualityComparer<TKey> comparer)
    {
        _entries = null!;
        _comparer = comparer;
        ChronicleGuard.ThrowIfNull(state.Items, nameof(state));
        RestoreEntries(state.Items, default, comparer);
    }

    internal static SwiftDictionary<TKey, TValue> RestoreTrusted(SwiftDictionaryState<TKey, TValue> state,
        IEqualityComparer<TKey> comparer) => new(state, comparer);

    internal static SwiftDictionary<TKey, TValue> RestoreReversedTrusted(SwiftDictionaryState<TValue, TKey> state,
        IEqualityComparer<TKey> comparer) => new(state.Items, comparer);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private SwiftDictionary(ReadOnlySpan<KeyValuePair<TValue, TKey>> reverseItems, IEqualityComparer<TKey> comparer)
    {
        _entries = null!;
        _comparer = comparer;
        RestoreEntries(default, reverseItems, comparer);
    }

    // Specialize direction in the constructors without duplicating this loop in every serializer.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RestoreEntries(ReadOnlySpan<KeyValuePair<TKey, TValue>> items,
        ReadOnlySpan<KeyValuePair<TValue, TKey>> reverseItems, IEqualityComparer<TKey> comparer)
    {
        ChronicleGuard.ThrowIfNull(comparer, nameof(comparer));
        bool reverse = !reverseItems.IsEmpty;
        int count = reverse ? reverseItems.Length : items.Length;
        int capacity = SwiftHashTools.GetPersistenceCapacity(count, DefaultCapacity, 82);
        var entries = new Entry[capacity];
        int mask = capacity - 1, last = 0, maximumStep = 0;
        for (int i = 0; i < count; i++)
        {
            KeyValuePair<TKey, TValue> item = reverse
                ? new KeyValuePair<TKey, TValue>(reverseItems[i].Value, reverseItems[i].Key)
                : items[i];
            ChronicleGuard.ThrowIfNullGeneric(item.Key, "state");
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
        _count = count;
        _lastIndex = last;
        _maxStepCount = maximumStep;
        _nextResizeCount = (uint)(capacity * _LoadFactorThreshold);
        _adaptiveResizeFactor = 4;
        _movingFillRate = 0;
        _version = 0;
    }
}
