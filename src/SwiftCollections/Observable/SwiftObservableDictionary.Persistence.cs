//=======================================================================
// SwiftObservableDictionary.Persistence.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System.Collections.Generic;

namespace SwiftCollections.Observable;

public partial class SwiftObservableDictionary<TKey, TValue> where TKey : notnull
{
    internal new static SwiftObservableDictionary<TKey, TValue> RestoreTrusted(SwiftDictionaryState<TKey, TValue> state,
        IEqualityComparer<TKey> comparer) => new(state, comparer);

    private SwiftObservableDictionary(SwiftDictionaryState<TKey, TValue> state, IEqualityComparer<TKey> comparer)
        : base(state, comparer) { }
}
