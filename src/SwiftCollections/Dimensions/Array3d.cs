using System;
using System.Collections;
using System.Collections.Generic;

namespace SwiftCollections.Dimensions
{
    /// <summary>
    /// Represents a generic, flattened 3D array with efficient indexing and resizing capabilities.
    /// Optimized for use in performance-critical applications like game grids.
    /// </summary>
    /// <typeparam name="T">The type of elements in the 3D array.</typeparam>
    [Serializable]
    public class Array3D<T> : IEnumerable<T>, IEnumerable
    {
        #region Fields and Properties

        private T[] _innerArray;

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
                return _innerArray[GetIndex(x, y, z)];
            }
            set
            {
                ValidateIndex(x, y, z);
                _innerArray[GetIndex(x, y, z)] = value;
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
            _innerArray = new T[width * height * length];
        }

        /// <summary>
        /// Resizes the 3D array to the specified dimensions.
        /// Retains existing data where possible.
        /// </summary>
        public void Resize(int newWidth, int newHeight, int newLength)
        {
            var newArray = new T[newWidth * newHeight * newLength];

            int minWidth = Math.Min(Width, newWidth);
            int minHeight = Math.Min(Height, newHeight);
            int minLength = Math.Min(Length, newLength);

            for (int x = 0; x < minWidth; x++)
            {
                for (int y = 0; y < minHeight; y++)
                {
                    for (int z = 0; z < minLength; z++)
                    {
                        int srcIndex = GetIndex(x, y, z);
                        int dstIndex = x * (newHeight * newLength) + y * newLength + z;
                        newArray[dstIndex] = _innerArray[srcIndex];
                    }
                }
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
            var newArray = new T[Width * Height * Length];

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    for (int z = 0; z < Length; z++)
                    {
                        int newX = wrap ? (x + xOffset + Width) % Width : x + xOffset;
                        int newY = wrap ? (y + yOffset + Height) % Height : y + yOffset;
                        int newZ = wrap ? (z + zOffset + Length) % Length : z + zOffset;

                        if (IsValidIndex(newX, newY, newZ))
                        {
                            int srcIndex = GetIndex(x, y, z);
                            int dstIndex = GetIndex(newX, newY, newZ);
                            newArray[dstIndex] = _innerArray[srcIndex];
                        }
                    }
                }
            }

            _innerArray = newArray;
        }

        /// <summary>
        /// Clears all elements in the array.
        /// </summary>
        public void Clear() => Array.Clear(_innerArray, 0, _innerArray.Length);

        /// <summary>
        /// Fills the entire array with the specified value.
        /// </summary>
        public void Fill(T value)
        {
            for (int i = 0; i < _innerArray.Length; i++)
                _innerArray[i] = value;
        }

        public virtual int GetIndex(int x, int y, int z)
        {
            return x * (Height * Length) + y * Length + z;
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
