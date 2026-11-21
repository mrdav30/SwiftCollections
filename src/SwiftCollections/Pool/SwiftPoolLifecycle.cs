//=======================================================================
// SwiftPoolLifecycle.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Runtime.CompilerServices;
using Chronicler.Diagnostics;
using SwiftCollections.Lazy;

namespace SwiftCollections.Pool;

/// <summary>
/// Store as a mutable field: copying this state would separate disposal and lazy-owner replacement.
/// </summary>
/// <typeparam name="T"></typeparam>
internal struct SwiftPoolLifecycle<T> where T : class
{
    private volatile SwiftLazyDisposable<SwiftObjectPool<T>>? _pool;

    internal SwiftPoolLifecycle(Func<SwiftObjectPool<T>> factory)
    {
        _pool = new SwiftLazyDisposable<SwiftObjectPool<T>>(factory);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal SwiftObjectPool<T> GetPool(object owner) => GetUsablePool(owner).Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Release(T? value, object owner, bool ignoreNullReturns)
    {
        var pool = GetUsablePool(owner);
        if (!ignoreNullReturns || value is not null)
            pool.Value.Release(value!);
    }

    internal void Clear()
    {
        var pool = _pool;
        if (pool is not null && pool.IsValueCreated)
            pool.Value.Clear();
    }

    internal void Flush(Func<SwiftObjectPool<T>> factory)
    {
        var pool = _pool;
        if (pool is not null && pool.IsValueCreated)
        {
            pool.Value.Dispose();
            _pool = new SwiftLazyDisposable<SwiftObjectPool<T>>(factory);
        }
    }

    internal void Dispose()
    {
        var pool = _pool;
        if (pool is not null)
        {
            if (pool.IsValueCreated)
            {
                pool.Value.Clear();
                pool.Value.Dispose();
            }
            _pool = null;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private SwiftLazyDisposable<SwiftObjectPool<T>> GetUsablePool(object owner)
    {
        var pool = _pool;
        if (pool is null)
            ChronicleGuard.ThrowIfDisposed(true, GetPoolName(owner));
        return pool!;
    }

    private static string GetPoolName(object owner)
    {
        string name = owner.GetType().Name;
        int arity = name.IndexOf('`');
        return arity >= 0 ? name.Substring(0, arity) : name;
    }
}
