//=======================================================================
// ISwiftCloneable.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System.Collections.Generic;

namespace SwiftCollections;

/// <summary>
/// Defines copying element values into a caller-owned collection after clearing it.
/// </summary>
/// <typeparam name="T">The type of elements contained in the collection to be cloned.</typeparam>
/// <remarks>
/// Implementations clear caller-owned output before adding elements. Repository collections perform shallow
/// copies of element values; target equality/ordering rules can discard duplicates or reorder items. This
/// interface does not promise independent referenced objects or transactional failure.
/// </remarks>
public interface ISwiftCloneable<T>
{
    /// <summary>
    /// Clears a destination and copies element values into it.
    /// </summary>
    /// <param name="output">The mutable, non-null caller-owned destination. Do not pass the source itself;
    /// clearing output can empty the source.</param>
    /// <remarks>
    /// Element references are shared by repository implementations. Destination Add/Clear exceptions propagate
    /// and can leave partial output. Ordering follows the concrete source's documented copy order.
    /// </remarks>
    void CloneTo(ICollection<T> output);
}
