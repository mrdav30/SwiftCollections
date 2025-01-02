using System;
using System.Collections;
using System.Collections.Generic;

namespace SwiftCollections.Dimensions
{
    /// <summary>
    /// Represents a generic 3D array with efficient indexing and resizing capabilities.
    /// Optimized for use in performance-critical applications like game grids.
    /// </summary>
    /// <typeparam name="T">The type of elements in the 3D array.</typeparam>
    [Serializable]
    public class Array3D<T> : IEnumerable<T>, IEnumerable
    {
        #region Fields and Properties

        private T[][][] _innerArray;

        public int Width { get; private set; }
        public int Height { get; private set; }
        public int Length { get; private set; }

        /// <summary>
        /// Total number of elements in the 3D array.
        /// </summary>
        public int Size => Width * Height * Length;

        #endregion

        #region Constructors

        public Array3D() : this(0, 0, 0) { }

        public Array3D(int width, int height, int length)
        {
            Initialize(width, height, length);
        }

        public Array3D(int width, int height, int length, T defaultValue) : this(width, height, length)
        {
            Fill(defaultValue);
        }

        #endregion

        #region Indexer

        public T this[int x, int y, int z]
        {
            get
            {
                ValidateIndex(x, y, z);
                return _innerArray[x][y][z];
            }
            set
            {
                ValidateIndex(x, y, z);
                _innerArray[x][y][z] = value;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Initializes the 3D array with the specified dimensions.
        /// </summary>
        private void Initialize(int width, int height, int length)
        {
            Width = width;
            Height = height;
            Length = length;
            _innerArray = new T[width][][];
            for (int x = 0; x < width; x++)
            {
                _innerArray[x] = new T[height][];
                for (int y = 0; y < height; y++)
                    _innerArray[x][y] = new T[length];
            }
        }

        /// <summary>
        /// Resizes the 3D array to the specified dimensions.
        /// Retains existing data where possible.
        /// </summary>
        public void Resize(int newWidth, int newHeight, int newLength)
        {
            var newArray = new T[newWidth][][];
            for (int x = 0; x < newWidth; x++)
            {
                newArray[x] = new T[newHeight][];
                for (int y = 0; y < newHeight; y++)
                    newArray[x][y] = new T[newLength];
            }

            int minWidth = Math.Min(Width, newWidth);
            int minHeight = Math.Min(Height, newHeight);
            int minLength = Math.Min(Length, newLength);

            for (int x = 0; x < minWidth; x++)
            {
                for (int y = 0; y < minHeight; y++)
                    Array.Copy(_innerArray[x][y], 0, newArray[x][y], 0, minLength);
            }

            _innerArray = newArray;
            Width = newWidth;
            Height = newHeight;
            Length = newLength;
        }

        /// <summary>
        /// Shifts the elements in the array by the specified offsets along each axis.
        /// </summary>
        /// <param name="xOffset">The offset to apply along the X-axis.</param>
        /// <param name="yOffset">The offset to apply along the Y-axis.</param>
        /// <param name="zOffset">The offset to apply along the Z-axis.</param>
        /// <param name="wrap">
        /// Specifies whether to wrap elements that exceed the array's boundaries. 
        /// If <c>true</c>, values wrap around to the other side of the array.
        /// If <c>false</c>, values that exceed boundaries are discarded.
        /// </param>
        /// <remarks>
        /// - Wrapping behavior ensures that no data is lost during shifts.
        /// - Non-wrapping behavior discards elements that move out of bounds.
        /// </remarks>
        public void Shift(int xOffset, int yOffset, int zOffset, bool wrap = true)
        {
            var newArray = new T[Width][][];

            // Initialize the new array
            for (int x = 0; x < Width; x++)
            {
                newArray[x] = new T[Height][];
                for (int y = 0; y < Height; y++)
                {
                    newArray[x][y] = new T[Length];
                }
            }

            // Shift elements
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    for (int z = 0; z < Length; z++)
                    {
                        int newX = wrap ? (x + xOffset + Width) % Width : x + xOffset;
                        int newY = wrap ? (y + yOffset + Height) % Height : y + yOffset;
                        int newZ = wrap ? (z + zOffset + Length) % Length : z + zOffset;

                        // Check if the destination index is valid
                        if (IsValidIndex(newX, newY, newZ))
                        {
                            newArray[newX][newY][newZ] = _innerArray[x][y][z];
                        }
                    }
                }
            }

            _innerArray = newArray;
        }

        /// <summary>
        /// Clears all elements in the array.
        /// </summary>
        public void Clear()
        {
            foreach (var slice in _innerArray)
            {
                foreach (var row in slice)
                    Array.Clear(row, 0, row.Length);
            }
        }

        /// <summary>
        /// Fills the entire array with the specified value.
        /// </summary>
        public void Fill(T value)
        {
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    for (int z = 0; z < Length; z++)
                        _innerArray[x][y][z] = value;
                }
            }
        }

        /// <summary>
        /// Validates the specified indices.
        /// Throws an exception if the indices are out of bounds.
        /// </summary>
        public virtual void ValidateIndex(int x, int y, int z)
        {
            if (!IsValidIndex(x, y, z))
               ThrowHelper.ThrowIndexOutOfRangeException($"Invalid index ({x}, {y}, {z}) for dimensions ({Width}, {Height}, {Length}).");
        }

        /// <summary>
        /// Checks if the specified indices are within bounds.
        /// </summary>
        public virtual bool IsValidIndex(int x, int y, int z) =>
            x >= 0 && x < Width && y >= 0 && y < Height && z >= 0 && z < Length;

        #endregion

        #region IEnumerator Implementation

        /// <summary>
        /// Returns an enumerator that iterates through all elements in the 3D array.
        /// </summary>
        /// <returns>An enumerator for the 3D array.</returns>
        public IEnumerator<T> GetEnumerator()
        {
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    for (int z = 0; z < Length; z++)
                        yield return this[x, y, z];
                }
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through all elements in the 3D array (non-generic).
        /// </summary>
        /// <returns>An enumerator for the 3D array.</returns>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


        #endregion
    }
}
