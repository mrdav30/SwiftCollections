using System;

namespace SwiftCollections.Dimensions
{
    /// <summary>
    /// Represents a 2D array specifically designed to handle short integer values.
    /// </summary>
    [Serializable]
    public class ShortArray2D : Array2D<short>
    {
        #region Constructors

        public ShortArray2D() { }

        public ShortArray2D(int width, int height) : base(width, height) { }

        public ShortArray2D(int width, int height, short defaultValue) : base(width, height, defaultValue) { }

        #endregion

        #region Collection Management

        /// <summary>
        /// Scales all elements in the array by the specified factor.
        /// </summary>
        public void Scale(short factor)
        {
            for (int i = 0; i < InnerArray.Length; i++)
                InnerArray[i] = (short)(InnerArray[i] * factor);
        }

        /// <summary>
        /// Normalizes all elements in the array to a specified range.
        /// </summary>
        public void Normalize(short min, short max)
        {
            short currentMin = short.MaxValue;
            short currentMax = short.MinValue;

            // Find current min and max
            foreach (var value in InnerArray)
            {
                if (value < currentMin) currentMin = value;
                if (value > currentMax) currentMax = value;
            }

            // Normalize values
            for (int i = 0; i < InnerArray.Length; i++)
                InnerArray[i] = (short)(((InnerArray[i] - currentMin) / (float)(currentMax - currentMin)) * (max - min) + min);
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Clones a 2D array into a new instance of ShortArray2D.
        /// </summary>
        public static ShortArray2D Clone(short[,] source)
        {
            int width = source.GetLength(0);
            int height = source.GetLength(1);

            var array2D = new ShortArray2D(width, height);
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                    array2D[x, y] = source[x, y];
            }
            return array2D;
        }

        #endregion
    }
}
