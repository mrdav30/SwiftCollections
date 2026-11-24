//=======================================================================
// SwiftHashSet.Persistence.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Collections.Generic;
using Chronicler.Diagnostics;
using SwiftCollections.Utility;

namespace SwiftCollections;

public sealed partial class SwiftHashSet<T> where T : notnull
{
    internal static SwiftHashSet<T> RestoreTrusted(SwiftArrayState<T> state, IEqualityComparer<T> comparer)
        => new(state, comparer);

    private SwiftHashSet(SwiftArrayState<T> state, IEqualityComparer<T> comparer)
    {
        ChronicleGuard.ThrowIfNull(state.Items, nameof(state));
        ChronicleGuard.ThrowIfNull(comparer, nameof(comparer));
        int capacity = SwiftHashTools.GetPersistenceCapacity(state.Items.Length, DefaultCapacity, 85);
        var entries = new Entry[capacity];
        int mask = capacity - 1, last = 0, maximumStep = 0, count = 0;
        foreach (T item in state.Items)
        {
            if (item is null) continue;
            int hash = comparer.GetHashCode(item) & 0x7FFFFFFF;
            int index = hash & mask, step = 1;
            // A fresh table has a spare slot, and cumulative-square probing visits every slot.
            while (entries[index].IsUsed)
            {
                if (entries[index].HashCode == hash && comparer.Equals(entries[index].Value, item))
                    throw new InvalidOperationException("Persisted hash set contains duplicate equal elements.");
                index = unchecked(index + step * step) & mask;
                step++;
            }
            entries[index] = new Entry { Value = item, HashCode = hash, IsUsed = true };
            count++;
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
