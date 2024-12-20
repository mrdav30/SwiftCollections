using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace SwiftCollections
{
    internal static class ThrowHelper
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static Exception ThrowInvalidOperationException(string message)
        {
            throw new InvalidOperationException(message);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static T ThrowInvalidOperationException<T>(string message)
        {
            throw new InvalidOperationException(message);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentOutOfRangeException(string msg = null)
        {
            throw new ArgumentOutOfRangeException(msg);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static T ThrowArgumentOutOfRangeException<T>()
        {
            throw new ArgumentOutOfRangeException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowIndexOutOfRangeException(string msg = null)
        {
            throw new IndexOutOfRangeException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static T ThrowIndexOutOfRangeException<T>()
        {
            throw new IndexOutOfRangeException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentNullException(string paramName)
        {
            throw new ArgumentNullException(paramName);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static T ThrowArgumentNullException<T>(string paramName)
        {
            throw new ArgumentNullException(paramName);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException(string msg)
        {
            throw new ArgumentException(msg);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static T ThrowArgumentException<T>(string msg)
        {
            throw new ArgumentException(msg);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowKeyNotFoundException()
        {
            throw new KeyNotFoundException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static T ThrowKeyNotFoundException<T>()
        {
            throw new KeyNotFoundException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowNotSupportedException(string msg = null)
        {
            throw new NotSupportedException(msg);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static T ThrowNotSupportedException<T>(string msg = null)
        {
            throw new NotSupportedException(msg);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowSerializationException(string msg)
        {
            throw new SerializationException(msg);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static T ThrowSerializationException<T>(string msg)
        {
            throw new SerializationException(msg);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowObjectDisposedException(string msg)
        {
            throw new ObjectDisposedException(msg);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static T ThrowObjectDisposedException<T>(string msg)
        {
            throw new ObjectDisposedException(msg);
        }
    }
}
