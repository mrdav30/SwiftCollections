using Chronicler;
using SwiftCollections.Dimensions;
using SwiftCollections.Observable;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace SwiftCollections.Tests;

public class ChroniclerStateJsonConverterTests
{
    public static TheoryData<Type, Type> StateBackedTypes => new()
    {
        { typeof(SwiftList<int>), typeof(SwiftArrayState<int>) },
        { typeof(SwiftStack<int>), typeof(SwiftArrayState<int>) },
        { typeof(SwiftQueue<int>), typeof(SwiftArrayState<int>) },
        { typeof(SwiftHashSet<int>), typeof(SwiftArrayState<int>) },
        { typeof(SwiftPackedSet<int>), typeof(SwiftArrayState<int>) },
        { typeof(SwiftSortedList<int>), typeof(SwiftArrayState<int>) },
        { typeof(SwiftDictionary<string, int>), typeof(SwiftDictionaryState<string, int>) },
        { typeof(SwiftBiDictionary<string, int>), typeof(SwiftDictionaryState<string, int>) },
        { typeof(SwiftSparseMap<int>), typeof(SwiftSparseSetState<int>) },
        { typeof(SwiftBucket<int>), typeof(SwiftBucketState<int>) },
        { typeof(SwiftGenerationalBucket<int>), typeof(SwiftGenerationalBucketState<int>) },
        { typeof(SwiftArray2D<int>), typeof(Array2DState<int>) },
        { typeof(SwiftArray3D<int>), typeof(Array3DState<int>) },
        { typeof(SwiftBoolArray2D), typeof(Array2DState<bool>) },
        { typeof(SwiftShortArray2D), typeof(Array2DState<short>) },
        { typeof(SwiftObservableArray<int>), typeof(SwiftArrayState<int>) },
        { typeof(SwiftObservableList<int>), typeof(SwiftArrayState<int>) },
        { typeof(SwiftObservableDictionary<string, int>), typeof(SwiftDictionaryState<string, int>) }
    };

    [Theory]
    [MemberData(nameof(StateBackedTypes))]
    public void StateBackedTypes_UseChroniclerStateJsonConverterFactory(Type recordType, Type stateType)
    {
        var converterAttribute = recordType.GetCustomAttribute<JsonConverterAttribute>();
        Type stateBackedInterface = typeof(IStateBacked<>).MakeGenericType(stateType);
        var stateBackedInterfaces = recordType
            .GetInterfaces()
            .Where(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IStateBacked<>))
            .ToArray();

        Assert.NotNull(converterAttribute);
        Assert.Equal(typeof(StateJsonConverterFactory), converterAttribute.ConverterType);
        Assert.True(stateBackedInterface.IsAssignableFrom(recordType));
        Assert.Single(stateBackedInterfaces);
        Assert.NotNull(recordType.GetConstructor(new[] { stateType }));
        Assert.True(new StateJsonConverterFactory().CanConvert(recordType));
    }

    [Fact]
    public void StateJsonConverterFactory_DoesNotAcceptConventionOnlyStateTypes()
    {
        Assert.False(new StateJsonConverterFactory().CanConvert(typeof(ConventionOnlyStateContainer)));
    }

    [Fact]
    public void JsonRoundTrip_SwiftList_PreservesStateShapeAndItems()
    {
        var original = new SwiftList<int>(new[] { 1, 2, 3 });

        string json = JsonSerializer.Serialize(original);
        SwiftList<int> result = JsonSerializer.Deserialize<SwiftList<int>>(json)!;

        Assert.StartsWith("{\"State\":", json);
        Assert.Equal(new[] { 1, 2, 3 }, result);
    }

    [Fact]
    public void JsonRoundTrip_SwiftDictionary_PreservesStateShapeAndItems()
    {
        var original = new SwiftDictionary<string, int>
        {
            { "one", 1 },
            { "two", 2 }
        };

        string json = JsonSerializer.Serialize(original);
        SwiftDictionary<string, int> result = JsonSerializer.Deserialize<SwiftDictionary<string, int>>(json)!;

        Assert.StartsWith("{\"State\":", json);
        Assert.Equal(2, result.Count);
        Assert.Equal(1, result["one"]);
        Assert.Equal(2, result["two"]);
    }

    [Fact]
    public void JsonRoundTrip_SwiftArray2D_PreservesStateShapeAndItems()
    {
        var original = new SwiftArray2D<int>(2, 2);
        original[0, 0] = 1;
        original[1, 0] = 2;
        original[0, 1] = 3;
        original[1, 1] = 4;

        string json = JsonSerializer.Serialize(original);
        SwiftArray2D<int> result = JsonSerializer.Deserialize<SwiftArray2D<int>>(json)!;

        Assert.StartsWith("{\"State\":", json);
        Assert.Equal(2, result.Width);
        Assert.Equal(2, result.Height);
        Assert.Equal(1, result[0, 0]);
        Assert.Equal(2, result[1, 0]);
        Assert.Equal(3, result[0, 1]);
        Assert.Equal(4, result[1, 1]);
    }

    private sealed class ConventionOnlyStateContainer
    {
        public ConventionOnlyStateContainer(int state)
        {
            State = state;
        }

        public int State { get; }
    }
}
