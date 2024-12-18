using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace SwiftCollections
{
    internal static class ThrowHelper
    {

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidOperationException(string message)
        {
            throw new InvalidOperationException(message);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentOutOfRangeException()
        {
            throw new ArgumentOutOfRangeException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowIndexOutOfRangeException()
        {
            throw new IndexOutOfRangeException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentNullException(string paramName)
        {
            throw new ArgumentNullException(paramName);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException(string msg)
        {
            throw new ArgumentException(msg);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowKeyNotFoundException()
        {
            throw new KeyNotFoundException();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowNotSupportedException(string msg = null)
        {
            throw new NotSupportedException(msg);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowSerializationException(string msg)
        {
            throw new SerializationException(msg);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowObjectDisposedException(string msg)
        {
            throw new ObjectDisposedException(msg);
        }
    }
}
