//=======================================================================
// SwiftIntAscendingComparer.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SwiftCollections;

internal readonly struct SwiftIntAscendingComparer : IComparer<int>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Compare(int x, int y)
    {
        return x.CompareTo(y);
    }
}
