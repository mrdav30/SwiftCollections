//=======================================================================
// SwiftHashSet.Entry.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

namespace SwiftCollections;

public sealed partial class SwiftHashSet<T>
    where T : notnull
{
    /// <summary>
    /// Represents a single value in the set, including its hash code for quick access.
    /// </summary>
    internal struct Entry
    {
        public T Value;
        public int HashCode;    // Lower 31 bits of hash code, -1 if deleted probe tombstone
        public bool IsUsed;
    }
}
