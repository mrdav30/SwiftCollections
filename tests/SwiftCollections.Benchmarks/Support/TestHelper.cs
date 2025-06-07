using System;

namespace SwiftCollections.Benchmarks
{
    public static class TestHelper
    {
        private static readonly Random Random = new Random();

        /// <summary>
        /// Generates a random string of the specified length.
        /// </summary>
        /// <param name="length">The length of the string to generate.</param>
        /// <returns>A random string consisting of uppercase letters, lowercase letters, and digits.</returns>
        public static string GenerateRandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var stringChars = new char[length];

            lock (Random) // Ensure thread safety if tests run in parallel
            {
                for (int i = 0; i < length; i++)
                    stringChars[i] = chars[Random.Next(chars.Length)];
            }

            return new string(stringChars);
        }

        /// <summary>
        /// Generates a random int in the specified range.
        /// </summary>
        public static int GenerateRandomInt(int minValue, int maxValue)
        {
            lock (Random)
                return Random.Next(minValue, maxValue);
        }

        /// <summary>
        /// Generates a random Array of the specified length.
        /// </summary>
        public static T[] GenerateRandomArray<T>(Func<T> elementGenerator, int length)
        {
            var array = new T[length];
            for (int i = 0; i < length; i++)
                array[i] = elementGenerator();
            return array;
        }
    }
}
