using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace SwiftCollections
{
    public static class ThrowHelper
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Exception ThrowInvalidOperationException(string message)
        {
            throw new InvalidOperationException(message);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static T ThrowInvalidOperationException<T>(string message)
        {
            throw new InvalidOperationException(message);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowArgumentOutOfRangeException(string msg = null)
        {
            throw new ArgumentOutOfRangeException(msg);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static T ThrowArgumentOutOfRangeException<T>()
        {
            throw new ArgumentOutOfRangeException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowIndexOutOfRangeException(string msg = null)
        {
            throw new IndexOutOfRangeException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static T ThrowIndexOutOfRangeException<T>()
        {
            throw new IndexOutOfRangeException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowArgumentNullException(string paramName)
        {
            throw new ArgumentNullException(paramName);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static T ThrowArgumentNullException<T>(string paramName)
        {
            throw new ArgumentNullException(paramName);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowArgumentException(string msg)
        {
            throw new ArgumentException(msg);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static T ThrowArgumentException<T>(string msg)
        {
            throw new ArgumentException(msg);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowKeyNotFoundException()
        {
            throw new KeyNotFoundException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static T ThrowKeyNotFoundException<T>()
        {
            throw new KeyNotFoundException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowNotSupportedException(string msg = null)
        {
            throw new NotSupportedException(msg);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static T ThrowNotSupportedException<T>(string msg = null)
        {
            throw new NotSupportedException(msg);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowSerializationException(string msg)
        {
            throw new SerializationException(msg);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static T ThrowSerializationException<T>(string msg)
        {
            throw new SerializationException(msg);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowObjectDisposedException(string msg)
        {
            throw new ObjectDisposedException(msg);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static T ThrowObjectDisposedException<T>(string msg)
        {
            throw new ObjectDisposedException(msg);
        }
    }
}
