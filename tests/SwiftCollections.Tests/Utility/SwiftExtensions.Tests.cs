using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SwiftCollections.Tests;

public class SwiftExtensionsTests
{
    [Fact]
    public void Populate_OverloadsFillArraysUsingTheirProviders()
    {
        int counter = 0;
        int[] values = new int[3].Populate(() => ++counter);
        string[] indexedValues = new string[3].Populate(index => $"Value {index}");
        DisposableSpy[] references = new DisposableSpy[2].Populate();

        Assert.Equal(new[] { 1, 2, 3 }, values);
        Assert.Equal(new[] { "Value 0", "Value 1", "Value 2" }, indexedValues);
        Assert.NotNull(references[0]);
        Assert.NotNull(references[1]);
        Assert.NotSame(references[0], references[1]);
    }

    [Fact]
    public void TryIndex_SupportsNegativeIndicesAndRejectsInvalidAccess()
    {
        int[] values = { 10, 20, 30 };
        int[] missingArray = null;

        Assert.True(values.TryIndex(1, out int middle));
        Assert.True(values.TryIndex(-1, out int last));
        Assert.False(values.TryIndex(3, out int missing));
        Assert.False(values.TryIndex(-4, out int beforeStart));
        Assert.False(missingArray.TryIndex(0, out int fromNull));

        Assert.Equal(20, middle);
        Assert.Equal(30, last);
        Assert.Equal(default, missing);
        Assert.Equal(default, beforeStart);
        Assert.Equal(default, fromNull);
    }

    [Fact]
    public void Shuffle_ProducesDeterministicPermutationForSeed()
    {
        int[] source = { 1, 2, 3, 4, 5, 6 };
        int[] expected = SimulateShuffle(source, 1234);

        int[] actual = source.Shuffle(new Random(1234)).ToArray();

        Assert.Equal(expected, actual);
        Assert.Equal(source.OrderBy(value => value), actual.OrderBy(value => value));
    }

    [Fact]
    public void Shuffle_ThrowsForNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => SwiftExtensions.Shuffle<int>(null, new Random()).ToArray());
        Assert.Throws<ArgumentNullException>(() => new[] { 1, 2, 3 }.Shuffle(null).ToArray());
        Assert.Throws<ArgumentNullException>(() => SwiftExtensions.ShuffleInPlace<int>(null, new Random()));
        Assert.Throws<ArgumentNullException>(() => new List<int> { 1, 2, 3 }.ShuffleInPlace(null));
    }

    [Fact]
    public void ShuffleInPlace_UsesExpectedFisherYatesOrder()
    {
        var list = new List<int> { 1, 2, 3, 4, 5, 6 };
        int[] expected = SimulateShuffleInPlace(list, 4321);

        list.ShuffleInPlace(new Random(4321));

        Assert.Equal(expected, list);
    }

    [Fact]
    public void SequenceHelpers_HandlePopulationAndTailQueries()
    {
        var swiftList = new SwiftList<int> { 10, 20, 30, 40 };
        IEnumerable<int> queue = new Queue<int>(new[] { 10, 20, 30, 40 });

        Assert.True(new[] { 1 }.IsPopulated());
        Assert.False(Array.Empty<int>().IsPopulated());
        Assert.False(SwiftExtensions.IsPopulatedSafe<int>(null));
        Assert.True(new[] { 1 }.IsPopulatedSafe());
        Assert.Equal(40, swiftList.FromEnd(1));
        Assert.Equal(30, queue.FromEnd(2));
        Assert.Equal(40, queue.PopLast());
        Assert.Equal(30, queue.SecondToLast());

        Assert.Throws<ArgumentNullException>(() => SwiftExtensions.IsPopulated<int>(null));
        Assert.Throws<ArgumentOutOfRangeException>(() => swiftList.FromEnd(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => swiftList.FromEnd(5));
    }

    [Fact]
    public void SkipFromEnd_HandlesBoundsAndValidation()
    {
        IEnumerable<int> source = new[] { 1, 2, 3, 4 };

        Assert.Equal(new[] { 1, 2 }, source.SkipFromEnd(2).ToArray());
        Assert.Equal(new[] { 1, 2, 3, 4 }, source.SkipFromEnd(0).ToArray());
        Assert.Empty(new[] { 1, 2 }.SkipFromEnd(5));

        Assert.Throws<ArgumentNullException>(() => SwiftExtensions.SkipFromEnd<int>(null, 1).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => source.SkipFromEnd(-1).ToArray());
    }

    private static int[] SimulateShuffle(IEnumerable<int> source, int seed)
    {
        List<int> buffer = new List<int>(source);
        Random rng = new Random(seed);
        List<int> result = new List<int>(buffer.Count);
        int remaining = buffer.Count;

        while (remaining > 0)
        {
            int index = rng.Next(remaining);
            remaining--;
            (buffer[index], buffer[remaining]) = (buffer[remaining], buffer[index]);
            result.Add(buffer[remaining]);
        }

        return result.ToArray();
    }

    private static int[] SimulateShuffleInPlace(IList<int> source, int seed)
    {
        List<int> buffer = new List<int>(source);
        Random rng = new Random(seed);
        int remaining = buffer.Count;

        while (remaining > 1)
        {
            remaining--;
            int index = rng.Next(remaining + 1);
            (buffer[remaining], buffer[index]) = (buffer[index], buffer[remaining]);
        }

        return buffer.ToArray();
    }
}
