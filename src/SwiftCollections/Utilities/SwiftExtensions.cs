using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SwiftCollections
{
    public static class SwiftExtensions
    {
        /// <summary>
        /// Attempts to retrieve an element from the array at the specified index.
        /// Returns true if the index is valid and the element is retrieved; otherwise, returns false.
        /// </summary>
        /// <typeparam name="T">The type of elements in the array.</typeparam>
        /// <param name="array">The array from which to retrieve the element.</param>
        /// <param name="index">The index of the element to retrieve.</param>
        /// <param name="result">
        /// When this method returns, contains the element at the specified index if the index is valid;
        /// otherwise, the default value for the type of the element.
        /// </param>
        /// <returns>
        /// True if the element at the specified index was retrieved successfully; otherwise, false.
        /// </returns>
        public static bool TryIndex<T>(this T[] array, int index, out T result)
        {
            if (array != null)
            {
                if (index < 0)
                {
                    // Support negative indices to access elements from the end
                    index = array.Length + index;
                }
                if (index >= 0 && index < array.Length)
                {
                    result = array[index];
                    return true;
                }
            }
            result = default;
            return false;
        }

        /// <summary>
        /// Creates a <see cref="HashSet{T}"/> from an <see cref="IEnumerable{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of elements in the collection.</typeparam>
        /// <param name="source">The collection to create a hash set from.</param>
        /// <returns>A hash set containing the elements from the source collection.</returns>
        public static SwiftHashSet<T> ToHashSet<T>(this IEnumerable<T> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new SwiftHashSet<T>(source);
        }

        /// <summary>
        /// An iterator that yields the elements of the source collection in a random order using the specified random number generator.
        /// </summary>
        /// <typeparam name="T">The type of elements in the collection.</typeparam>
        /// <param name="source">The collection to shuffle.</param>
        /// <param name="rng">The random number generator to use for shuffling.</param>
        /// <returns>An iterator that yields the shuffled elements.</returns>
        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source, Random rng)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (rng == null) throw new ArgumentNullException("rng");

            SwiftList<T> buffer = new SwiftList<T>(source);
            int n = buffer.Count;
            while (n > 0)
            {
                int k = rng.Next(n);
                n--;
                // Swap the selected element with the last unshuffled element
                (buffer[k], buffer[n]) = (buffer[n], buffer[k]);
                yield return buffer[n];
            }
        }

        /// <summary>
        /// Shuffles the elements of the list in place using the specified random number generator.
        /// </summary>
        /// <typeparam name="T">The type of elements in the list.</typeparam>
        /// <param name="list">The list to shuffle.</param>
        /// <param name="rng">The random number generator to use for shuffling.</param>
        public static void ShuffleInPlace<T>(this IList<T> list, Random rng)
        {
            if (list == null) throw new ArgumentNullException(nameof(list));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                (list[n], list[k]) = (list[k], list[n]);
            }
        }

        /// <summary>
        /// Determines whether a sequence contains any elements.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"> The <see cref="IEnumerable{T}"/> to check for emptiness.</param>
        /// <returns>
        /// true if the source sequence contains any elements; otherwise, false.
        /// </returns>
        public static bool Any<T>(this IEnumerable<T> source)
        {
            if(source == null) ThrowHelper.ThrowArgumentNullException(nameof(source));
            using IEnumerator<T> enumerator = source.GetEnumerator();
            return enumerator.MoveNext();
        }

        /// <summary>
        /// Determines whether the collection is not null and contains any elements.
        /// </summary>
        /// <typeparam name="T">The type of elements in the collection.</typeparam>
        /// <param name="source">The collection to check.</param>
        /// <returns>
        /// True if the collection is not null and contains at least one element; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNotNullOrEmpty<T>(this IEnumerable<T> source) => source != null && source.Any();
    }
}
