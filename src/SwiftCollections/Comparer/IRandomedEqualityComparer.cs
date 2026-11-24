//=======================================================================
// IRandomedEqualityComparer.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

namespace SwiftCollections;

/// <summary>
/// Marks comparers that already use randomized hashing so collision handling does not replace them again.
/// </summary>
internal interface IRandomedEqualityComparer { }
