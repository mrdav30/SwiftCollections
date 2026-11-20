using System;
using System.Collections.Generic;
using System.Text.Json;
#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
using MemoryPack;
#endif
using Xunit;

namespace SwiftCollections.Tests;

public partial class HashComparerPersistenceTests
{
    [Fact]
    public void JsonSave_DictionaryRejectsUnregisteredEqualityInsteadOfLosingItsContract()
    {
        var comparer = EqualityComparer<int>.Create(static (left, right) => left % 10 == right % 10, static value => value % 10);
        var source = new SwiftDictionary<int, string>(8, comparer) { { 1, "one" } };

        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(source));

        Assert.Same(comparer, source.Comparer);
        Assert.Equal("one", source[11]);
        Assert.Single(source);
    }

    [Fact]
    public void JsonSave_HashSetRejectsUnregisteredEqualityInsteadOfLosingItsContract()
    {
        var comparer = EqualityComparer<int>.Create(static (left, right) => left % 10 == right % 10, static value => value % 10);
        var source = new SwiftHashSet<int>(new[] { 1 }, comparer);

        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(source));

        Assert.Same(comparer, source.Comparer);
        bool containsEquivalent = source.Contains(11);
        Assert.True(containsEquivalent);
        Assert.Single(source);
    }

#if !SWIFTCOLLECTIONS_DISABLE_MEMORYPACK
    [Fact]
    public void MemoryPackSave_DictionaryRejectsUnregisteredEqualityInsteadOfLosingItsContract()
    {
        var comparer = EqualityComparer<int>.Create(static (left, right) => left % 10 == right % 10, static value => value % 10);
        var source = new SwiftDictionary<int, string>(8, comparer) { { 1, "one" } };

        Assert.Throws<NotSupportedException>(() => MemoryPackSerializer.Serialize(source));

        Assert.Same(comparer, source.Comparer);
        Assert.Equal("one", source[11]);
        Assert.Single(source);
    }

    [Fact]
    public void MemoryPackSave_HashSetRejectsUnregisteredEqualityInsteadOfLosingItsContract()
    {
        var comparer = EqualityComparer<int>.Create(static (left, right) => left % 10 == right % 10, static value => value % 10);
        var source = new SwiftHashSet<int>(new[] { 1 }, comparer);

        Assert.Throws<NotSupportedException>(() => MemoryPackSerializer.Serialize(source));

        Assert.Same(comparer, source.Comparer);
        bool containsEquivalent = source.Contains(11);
        Assert.True(containsEquivalent);
        Assert.Single(source);
    }
#endif
}
