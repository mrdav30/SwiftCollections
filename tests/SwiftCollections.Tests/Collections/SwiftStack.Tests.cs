using FluentAssertions;
using System;
using System.Collections.Generic;

#if NET48_OR_GREATER
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
#endif

#if NET8_0_OR_GREATER
using System.Text.Json;
using System.Text.Json.Serialization;
#endif

using Xunit;

namespace SwiftCollections.Tests
{
    public class SwiftStackTests
    {
        [Fact]
        public void Push_ShouldAddElementToStack()
        {
            // Arrange
            var stack = new SwiftStack<int>();

            // Act
            stack.Push(1);

            // Assert
            stack.Count.Should().Be(1);
            stack.Peek().Should().Be(1);
        }

        [Fact]
        public void Pop_ShouldRemoveAndReturnTopElement()
        {
            // Arrange
            var stack = new SwiftStack<int>();
            stack.Push(1);
            stack.Push(2);

            // Act
            int result = stack.Pop();

            // Assert
            result.Should().Be(2);
            stack.Count.Should().Be(1);
            stack.Peek().Should().Be(1);
        }

        [Fact]
        public void Pop_OnEmptyStack_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var stack = new SwiftStack<int>();

            // Act
            Action act = () => stack.Pop();

            // Assert
            act.Should().Throw<InvalidOperationException>().WithMessage("Stack is empty.");
        }

        [Fact]
        public void Peek_ShouldReturnTopElementWithoutRemovingIt()
        {
            // Arrange
            var stack = new SwiftStack<string>();
            stack.Push("first");
            stack.Push("second");

            // Act
            string result = stack.Peek();

            // Assert
            result.Should().Be("second");
            stack.Count.Should().Be(2);
        }

        [Fact]
        public void Peek_OnEmptyStack_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var stack = new SwiftStack<double>();

            // Act
            Action act = () => stack.Peek();

            // Assert
            act.Should().Throw<InvalidOperationException>().WithMessage("Stack is empty.");
        }

        [Fact]
        public void Clear_ShouldRemoveAllElements()
        {
            // Arrange
            var stack = new SwiftStack<int>();
            stack.Push(1);
            stack.Push(2);

            // Act
            stack.Clear();

            // Assert
            stack.Count.Should().Be(0);
            stack.Should().BeEmpty();
        }

        [Fact]
        public void FastClear_ShouldResetCountWithoutClearingArray()
        {
            // Arrange
            var stack = new SwiftStack<int>();
            stack.Push(1);
            stack.Push(2);
            int capacityBeforeClear = stack.Capacity;

            // Act
            stack.FastClear();

            // Assert
            stack.Count.Should().Be(0);
            stack.Capacity.Should().Be(capacityBeforeClear);
        }

        [Fact]
        public void EnsureCapacity_ShouldIncreaseCapacity()
        {
            // Arrange
            var stack = new SwiftStack<int>(2);
            int capacityBefore = stack.Capacity;

            // Act
            stack.EnsureCapacity(10);

            // Assert
            stack.Capacity.Should().BeGreaterThanOrEqualTo(10);
            stack.Capacity.Should().BeGreaterThan(capacityBefore);
        }

        [Fact]
        public void Capacity_ShouldGrowAsElementsAreAdded()
        {
            // Arrange
            var stack = new SwiftStack<int>();
            int initialCapacity = stack.Capacity;

            // Act
            for (int i = 0; i < initialCapacity + 1; i++)
            {
                stack.Push(i);
            }

            // Assert
            stack.Capacity.Should().BeGreaterThan(initialCapacity);
            stack.Count.Should().Be(initialCapacity + 1);
        }

        [Fact]
        public void Indexer_Get_ShouldReturnElementAtIndex()
        {
            // Arrange
            var stack = new SwiftStack<string>();
            stack.Push("first");
            stack.Push("second");
            stack.Push("third");

            // Act
            string result = stack[1];

            // Assert
            result.Should().Be("second");
        }

        [Fact]
        public void Indexer_Set_ShouldModifyElementAtIndex()
        {
            // Arrange
            var stack = new SwiftStack<string>();
            stack.Push("first");
            stack.Push("second");
            stack.Push("third");

            // Act
            stack[1] = "modified";

            // Assert
            stack[1].Should().Be("modified");
        }

        [Fact]
        public void Indexer_Get_InvalidIndex_ShouldThrowArgumentOutOfRangeException()
        {
            // Arrange
            var stack = new SwiftStack<int>();
            stack.Push(1);

            // Act
            Action act = () => { var item = stack[2]; };

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void Contains_ShouldReturnTrueIfItemExists()
        {
            // Arrange
            var stack = new SwiftStack<int>();
            stack.Push(1);
            stack.Push(2);
            stack.Push(3);

            // Act
            bool result = stack.Contains(2);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void Contains_ShouldReturnFalseIfItemDoesNotExist()
        {
            // Arrange
            var stack = new SwiftStack<int>();
            stack.Push(1);
            stack.Push(2);

            // Act
            bool result = stack.Contains(3);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void CopyTo_ShouldCopyElementsToArray()
        {
            // Arrange
            var stack = new SwiftStack<int>();
            stack.Push(1);
            stack.Push(2);
            stack.Push(3);
            int[] array = new int[5];

            // Act
            stack.CopyTo(array, 1);

            // Assert
            array[1].Should().Be(1);
            array[2].Should().Be(2);
            array[3].Should().Be(3);
        }

        [Fact]
        public void CopyTo_WithNullArray_ShouldThrowArgumentNullException()
        {
            // Arrange
            var stack = new SwiftStack<int>();

            // Act
            Action act = () => stack.CopyTo(null, 0);

            // Assert
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void CopyTo_WithInvalidIndex_ShouldThrowArgumentOutOfRangeException()
        {
            // Arrange
            var stack = new SwiftStack<int>();
            int[] array = new int[5];

            // Act
            Action act = () => stack.CopyTo(array, -1);

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void CopyTo_WithInsufficientArrayLength_ShouldThrowArgumentException()
        {
            // Arrange
            var stack = new SwiftStack<int>();
            stack.Push(1);
            stack.Push(2);
            int[] array = new int[2];

            // Act
            Action act = () => stack.CopyTo(array, 1);

            // Assert
            act.Should().Throw<ArgumentException>().WithMessage("Destination array is not long enough.");
        }

        [Fact]
        public void Enumerator_ShouldEnumerateElementsInLifoOrder()
        {
            // Arrange
            var stack = new SwiftStack<int>();
            stack.Push(1);
            stack.Push(2);
            stack.Push(3);
            var expected = new List<int> { 3, 2, 1 };

            // Act
            var result = new List<int>();
            foreach (var item in stack)
            {
                result.Add(item);
            }

            // Assert
            result.Should().Equal(expected);
        }

        [Fact]
        public void Enumerator_Reset_ShouldRestartEnumeration()
        {
            // Arrange
            var stack = new SwiftStack<int>();
            stack.Push(1);
            stack.Push(2);

            var enumerator = stack.GetEnumerator();

            // Act
            enumerator.MoveNext();
            enumerator.MoveNext();
            enumerator.Reset();
            bool hasMoreElements = enumerator.MoveNext();

            // Assert
            hasMoreElements.Should().BeTrue();
            enumerator.Current.Should().Be(2);
        }

        [Fact]
        public void Enumerator_Reset_AfterCompleteEnumeration_ShouldRestart()
        {
            // Arrange
            var stack = new SwiftStack<int>();
            stack.Push(10);
            stack.Push(20);
            stack.Push(30);

            var enumerator = stack.GetEnumerator();
            var firstRun = new List<int>();
            var secondRun = new List<int>();

            // Act
            while (enumerator.MoveNext())
            {
                firstRun.Add(enumerator.Current);
            }

            enumerator.Reset();

            while (enumerator.MoveNext())
            {
                secondRun.Add(enumerator.Current);
            }

            // Assert
            firstRun.Should().Equal(30, 20, 10);
            secondRun.Should().Equal(30, 20, 10);
        }

        [Fact]
        public void Enumerator_Reset_AfterPartialEnumeration_ShouldRestartFromBeginning()
        {
            // Arrange
            var stack = new SwiftStack<int>();
            stack.Push(5);
            stack.Push(10);
            stack.Push(15);

            var enumerator = stack.GetEnumerator();
            var partialRun = new List<int>();
            var fullRunAfterReset = new List<int>();

            // Act
            enumerator.MoveNext(); // Moves to 15
            partialRun.Add(enumerator.Current);

            enumerator.MoveNext(); // Moves to 10
            partialRun.Add(enumerator.Current);

            enumerator.Reset();

            while (enumerator.MoveNext())
            {
                fullRunAfterReset.Add(enumerator.Current);
            }

            // Assert
            partialRun.Should().Equal(15, 10);
            fullRunAfterReset.Should().Equal(15, 10, 5);
        }

        [Fact]
        public void Enumerator_OnEmptyStack_ShouldNotEnumerate()
        {
            // Arrange
            var stack = new SwiftStack<int>();

            var enumerator = stack.GetEnumerator();

            // Act
            bool hasElements = enumerator.MoveNext();

            // Assert
            hasElements.Should().BeFalse();
        }

        [Fact]
        public void ToString_ShouldReturnMeaningfulRepresentation()
        {
            // Arrange
            var stack = new SwiftStack<int>();
            stack.Push(1);
            stack.Push(2);

            // Act
            string result = stack.ToString();

            // Assert
            result.Should().Contain("SwiftStack");
            result.Should().Contain("Count = 2");
        }

        [Fact]
        public void Constructor_WithInitialCapacity_ShouldSetCapacity()
        {
            // Act
            var stack = new SwiftStack<int>(20);

            // Assert
            stack.Capacity.Should().BeGreaterThanOrEqualTo(20);
            stack.Count.Should().Be(0);
        }

        [Fact]
        public void Enumerator_Dispose_ShouldPreventFurtherEnumeration()
        {
            // Arrange
            var stack = new SwiftStack<int>();
            stack.Push(1);
            stack.Push(2);
            var enumerator = stack.GetEnumerator();

            // Act
            enumerator.MoveNext();
            enumerator.Dispose();

            // Assert
            // Calling MoveNext() after Dispose() should not throw but return false
            enumerator.MoveNext().Should().BeFalse();
        }

        [Fact]
        public void Enumerator_MultipleEnumerations_ShouldWorkIndependently()
        {
            // Arrange
            var stack = new SwiftStack<int>();
            for (int i = 1; i <= 5; i++)
            {
                stack.Push(i);
            }

            var enumerator1 = stack.GetEnumerator();
            var enumerator2 = stack.GetEnumerator();

            var items1 = new List<int>();
            var items2 = new List<int>();

            // Act
            while (enumerator1.MoveNext())
            {
                items1.Add(enumerator1.Current);
                if (enumerator1.Current == 3)
                {
                    // Start the second enumerator when the first reaches 3
                    while (enumerator2.MoveNext())
                    {
                        items2.Add(enumerator2.Current);
                    }
                }
            }

            // Assert
            items1.Should().Equal(5, 4, 3, 2, 1);
            items2.Should().Equal(5, 4, 3, 2, 1);
        }

        [Fact]
        public void SwiftStack_Serialization_RoundTripMaintainsData()
        {
            var originalStack = new SwiftStack<int>();
            for (int i = 0; i < 100; i++)
            {
                originalStack.Push(i);
            }

            // Serialize the SwiftStack
#if NET48_OR_GREATER
            var formatter = new BinaryFormatter();
            using var stream = new MemoryStream();
            formatter.Serialize(stream, originalStack);

            // Reset stream position and deserialize
            stream.Seek(0, SeekOrigin.Begin);
            var deserializedStack = (SwiftStack<int>)formatter.Deserialize(stream);
#endif

#if NET8_0_OR_GREATER
            var jsonOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                ReferenceHandler = ReferenceHandler.IgnoreCycles,
                IncludeFields = true,
                IgnoreReadOnlyProperties = true
            };
            jsonOptions.Converters.Add(new SwiftStackJsonConverter<int>());
            var json = JsonSerializer.SerializeToUtf8Bytes(originalStack, jsonOptions);
            var deserializedStack = JsonSerializer.Deserialize<SwiftStack<int>>(json, jsonOptions);
#endif

            // Verify that the deserialized list matches the original
            Assert.Equal(originalStack.Count, deserializedStack.Count);
            for (int i = 0; i < originalStack.Count; i++)
            {
                Assert.Equal(originalStack[i], deserializedStack[i]);
            }
        }
    }
}
