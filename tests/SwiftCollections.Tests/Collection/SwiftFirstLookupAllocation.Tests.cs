using System;
using System.Runtime.CompilerServices;
using Xunit;

namespace SwiftCollections.Tests;

public class SwiftFirstLookupAllocationTests
{
    // Private types prevent unrelated tests from warming these generic initializers.
    [Fact]
    public void Dictionary_FirstLookupAndRemovalAfterAdd_ShouldNotAllocate()
    {
        AssertDictionaryFirstLookup(1);
        AssertDictionaryFirstLookup(new GenericKey<DictionaryKeyTag>(1));
        AssertDictionaryFirstLookup<GenericKey<DictionaryKeyTag>?>(new GenericKey<DictionaryKeyTag>(1));
        AssertDictionaryFirstLookup(new ReferenceKey());
    }

    [Fact]
    public void HashSet_FirstLookupAndRemovalAfterAdd_ShouldNotAllocate()
    {
        AssertHashSetFirstLookup(SetKey.Present);
        AssertHashSetFirstLookup(new GenericKey<HashSetKeyTag>(1));
        AssertHashSetFirstLookup<GenericKey<HashSetKeyTag>?>(new GenericKey<HashSetKeyTag>(1));
        AssertHashSetFirstLookup(new ReferenceKey());
    }

    private static void AssertDictionaryFirstLookup<TKey>(TKey key)
    {
        var value = new DictionaryValue();
        var dictionary = new SwiftDictionary<TKey, DictionaryValue>(4);
        Assert.True(dictionary.Add(key, value));

        long before = GC.GetAllocatedBytesForCurrentThread();
        bool found = LookupAndRemove(dictionary, key, value);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(found);
        Assert.Empty(dictionary);
        Assert.Equal(0L, allocated);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool LookupAndRemove<TKey>(SwiftDictionary<TKey, DictionaryValue> dictionary,
        TKey key, DictionaryValue expected) =>
        dictionary.TryGetValue(key, out DictionaryValue value)
        && ReferenceEquals(value, expected) && dictionary.Remove(key);

    private static void AssertHashSetFirstLookup<T>(T value)
    {
        var set = new SwiftHashSet<T>(4);
        Assert.True(set.Add(value));

        long before = GC.GetAllocatedBytesForCurrentThread();
        bool found = LookupAndRemove(set, value);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(found);
        Assert.Empty(set);
        Assert.Equal(0L, allocated);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool LookupAndRemove<T>(SwiftHashSet<T> set, T value) =>
        set.Contains(value) && set.Remove(value);

    private enum SetKey { Present = 1 }
    private sealed class DictionaryValue { }
    private sealed class ReferenceKey { }
    private sealed class DictionaryKeyTag { }
    private sealed class HashSetKeyTag { }

    private readonly struct GenericKey<T> : IEquatable<GenericKey<T>>
    {
        private readonly int _value;
        public GenericKey(int value) => _value = value;
        public bool Equals(GenericKey<T> other) => _value == other._value;
        public override bool Equals(object obj) => obj is GenericKey<T> other && Equals(other);
        public override int GetHashCode() => _value;
    }
}
