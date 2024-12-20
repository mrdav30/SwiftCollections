using System;

namespace SwiftCollections.Dimensions
{
    /// <summary>
    /// Represents a 2D array specifically designed to handle boolean values.
    /// </summary>
    [Serializable]
    public class BoolArray2D : Array2D<bool>
    {
        #region Constructors

        public BoolArray2D() { }

        public BoolArray2D(int width, int height) : base(width, height) { }

        public BoolArray2D(int width, int height, bool defaultValue) : base(width, height, defaultValue) { }

        #endregion

        #region Collection Management

        /// <summary>
        /// Toggles the value at the specified position in the array.
        /// </summary>
        public void Toggle(int x, int y)
        {
            this[x, y] = !this[x, y];
        }

        /// <summary>
        /// Sets all values within a rectangular region to the specified value.
        /// </summary>
        public void SetRegion(int xStart, int yStart, int width, int height, bool value)
        {
            for (int x = xStart; x < xStart + width; x++)
            {
                for (int y = yStart; y < yStart + height; y++)
                {
                    if (IsValidIndex(x, y))
                        this[x, y] = value;
                }
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Counts the number of true values in the array.
        /// </summary>
        public int CountTrue()
        {
            int count = 0;
            foreach (var cell in InnerArray)
            {
                if (cell) count++;
            }
            return count;
        }

        /// <summary>
        /// Clones a 2D array into a new instance of BoolArray2D.
        /// </summary>
        public static BoolArray2D Clone(bool[,] source)
        {
            int width = source.GetLength(0);
            int height = source.GetLength(1);

            var array2D = new BoolArray2D(width, height);
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
