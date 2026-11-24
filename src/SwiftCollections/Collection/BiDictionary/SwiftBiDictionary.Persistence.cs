//=======================================================================
// SwiftBiDictionary.Persistence.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System.Collections.Generic;

namespace SwiftCollections;

public partial class SwiftBiDictionary<T1, T2> where T1 : notnull where T2 : notnull
{
    internal override bool HasAdditionalComparerRoles => true;
    internal IEqualityComparer<T2> ConfiguredReverseComparer => _reverseComparer;
    internal IEqualityComparer<T2> EffectiveReverseComparer => _reverseMap.Comparer;

    internal static SwiftBiDictionary<T1, T2> RestoreTrusted(SwiftDictionaryState<T1, T2> state,
        IEqualityComparer<T1> forward, IEqualityComparer<T2> configuredReverse,
        IEqualityComparer<T2> effectiveReverse) => new(state, forward, configuredReverse, effectiveReverse);

    private SwiftBiDictionary(SwiftDictionaryState<T1, T2> state, IEqualityComparer<T1> forward,
        IEqualityComparer<T2> configuredReverse, IEqualityComparer<T2> effectiveReverse) : base(state, forward)
    {
        _reverseMap = SwiftDictionary<T2, T1>.RestoreReversedTrusted(state, effectiveReverse);
        _reverseComparer = configuredReverse;
    }
}
