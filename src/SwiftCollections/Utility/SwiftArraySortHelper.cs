//=======================================================================
// SwiftArraySortHelper.cs
//=======================================================================
// MIT License, Copyright (c) 2024–present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SwiftCollections;

internal static class SwiftArraySortHelper
{
    private const int InsertionSortThreshold = 16;

    public static void Sort<T>(T[] array, int index, int length, IComparer<T>? comparer)
    {
        if (length <= 1)
            return;

        if (comparer == null || ReferenceEquals(comparer, Comparer<T>.Default))
        {
            Array.Sort(array, index, length);
            return;
        }

        IntroSort(array, index, index + length - 1, FloorLog2(length) * 2, comparer);
    }

    public static void Sort<T, TComparer>(T[] array, int index, int length, TComparer comparer)
        where TComparer : struct, IComparer<T>
    {
        if (length <= 1)
            return;

        // Keep the struct-comparer path generic: it avoids boxing and lets the JIT devirtualize hot comparer calls.
        IntroSort(array, index, index + length - 1, FloorLog2(length) * 2, comparer);
    }

    private static void IntroSort<T>(T[] array, int lo, int hi, int depthLimit, IComparer<T> comparer)
    {
        while (hi > lo)
        {
            int partitionSize = hi - lo + 1;
            if (partitionSize <= InsertionSortThreshold)
            {
                InsertionSort(array, lo, hi, comparer);
                return;
            }

            if (depthLimit == 0)
            {
                HeapSort(array, lo, hi, comparer);
                return;
            }

            depthLimit--;
            int partition = PickPivotAndPartition(array, lo, hi, comparer);

            if (partition - lo < hi - partition)
            {
                IntroSort(array, lo, partition - 1, depthLimit, comparer);
                lo = partition + 1;
            }
            else
            {
                IntroSort(array, partition + 1, hi, depthLimit, comparer);
                hi = partition - 1;
            }
        }
    }

    private static int PickPivotAndPartition<T>(T[] array, int lo, int hi, IComparer<T> comparer)
    {
        int middle = lo + ((hi - lo) >> 1);

        SwapIfGreater(array, comparer, lo, middle);
        SwapIfGreater(array, comparer, lo, hi);
        SwapIfGreater(array, comparer, middle, hi);

        T pivot = array[middle];
        Swap(array, middle, hi - 1);

        int left = lo;
        int right = hi - 1;

        while (true)
        {
            while (comparer.Compare(array[++left], pivot) < 0)
            {
            }

            while (comparer.Compare(pivot, array[--right]) < 0)
            {
            }

            if (left >= right)
                break;

            Swap(array, left, right);
        }

        Swap(array, left, hi - 1);
        return left;
    }

    private static void HeapSort<T>(T[] array, int lo, int hi, IComparer<T> comparer)
    {
        int count = hi - lo + 1;
        for (int i = count >> 1; i >= 1; i--)
            DownHeap(array, i, count, lo, comparer);

        for (int i = count; i > 1; i--)
        {
            Swap(array, lo, lo + i - 1);
            DownHeap(array, 1, i - 1, lo, comparer);
        }
    }

    private static void DownHeap<T>(T[] array, int index, int count, int lo, IComparer<T> comparer)
    {
        T value = array[lo + index - 1];

        while (index <= (count >> 1))
        {
            int child = index << 1;
            if (child < count && comparer.Compare(array[lo + child - 1], array[lo + child]) < 0)
                child++;

            if (comparer.Compare(value, array[lo + child - 1]) >= 0)
                break;

            array[lo + index - 1] = array[lo + child - 1];
            index = child;
        }

        array[lo + index - 1] = value;
    }

    private static void InsertionSort<T>(T[] array, int lo, int hi, IComparer<T> comparer)
    {
        for (int i = lo + 1; i <= hi; i++)
        {
            T value = array[i];
            int j = i - 1;
            while (j >= lo && comparer.Compare(value, array[j]) < 0)
            {
                array[j + 1] = array[j];
                j--;
            }

            array[j + 1] = value;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SwapIfGreater<T>(T[] array, IComparer<T> comparer, int left, int right)
    {
        if (left != right && comparer.Compare(array[left], array[right]) > 0)
            Swap(array, left, right);
    }

    private static void IntroSort<T, TComparer>(T[] array, int lo, int hi, int depthLimit, TComparer comparer)
        where TComparer : struct, IComparer<T>
    {
        while (hi > lo)
        {
            int partitionSize = hi - lo + 1;
            if (partitionSize <= InsertionSortThreshold)
            {
                InsertionSort(array, lo, hi, comparer);
                return;
            }

            if (depthLimit == 0)
            {
                HeapSort(array, lo, hi, comparer);
                return;
            }

            depthLimit--;
            int partition = PickPivotAndPartition(array, lo, hi, comparer);

            if (partition - lo < hi - partition)
            {
                IntroSort(array, lo, partition - 1, depthLimit, comparer);
                lo = partition + 1;
            }
            else
            {
                IntroSort(array, partition + 1, hi, depthLimit, comparer);
                hi = partition - 1;
            }
        }
    }

    private static int PickPivotAndPartition<T, TComparer>(T[] array, int lo, int hi, TComparer comparer)
        where TComparer : struct, IComparer<T>
    {
        int middle = lo + ((hi - lo) >> 1);

        SwapIfGreater(array, comparer, lo, middle);
        SwapIfGreater(array, comparer, lo, hi);
        SwapIfGreater(array, comparer, middle, hi);

        T pivot = array[middle];
        Swap(array, middle, hi - 1);

        int left = lo;
        int right = hi - 1;

        while (true)
        {
            while (comparer.Compare(array[++left], pivot) < 0)
            {
            }

            while (comparer.Compare(pivot, array[--right]) < 0)
            {
            }

            if (left >= right)
                break;

            Swap(array, left, right);
        }

        Swap(array, left, hi - 1);
        return left;
    }

    private static void HeapSort<T, TComparer>(T[] array, int lo, int hi, TComparer comparer)
        where TComparer : struct, IComparer<T>
    {
        int count = hi - lo + 1;
        for (int i = count >> 1; i >= 1; i--)
            DownHeap(array, i, count, lo, comparer);

        for (int i = count; i > 1; i--)
        {
            Swap(array, lo, lo + i - 1);
            DownHeap(array, 1, i - 1, lo, comparer);
        }
    }

    private static void DownHeap<T, TComparer>(T[] array, int index, int count, int lo, TComparer comparer)
        where TComparer : struct, IComparer<T>
    {
        T value = array[lo + index - 1];

        while (index <= (count >> 1))
        {
            int child = index << 1;
            if (child < count && comparer.Compare(array[lo + child - 1], array[lo + child]) < 0)
                child++;

            if (comparer.Compare(value, array[lo + child - 1]) >= 0)
                break;

            array[lo + index - 1] = array[lo + child - 1];
            index = child;
        }

        array[lo + index - 1] = value;
    }

    private static void InsertionSort<T, TComparer>(T[] array, int lo, int hi, TComparer comparer)
        where TComparer : struct, IComparer<T>
    {
        for (int i = lo + 1; i <= hi; i++)
        {
            T value = array[i];
            int j = i - 1;
            while (j >= lo && comparer.Compare(value, array[j]) < 0)
            {
                array[j + 1] = array[j];
                j--;
            }

            array[j + 1] = value;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SwapIfGreater<T, TComparer>(T[] array, TComparer comparer, int left, int right)
        where TComparer : struct, IComparer<T>
    {
        if (left != right && comparer.Compare(array[left], array[right]) > 0)
            Swap(array, left, right);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Swap<T>(T[] array, int left, int right)
    {
        if (left == right)
            return;

        T value = array[left];
        array[left] = array[right];
        array[right] = value;
    }

    private static int FloorLog2(int value)
    {
        int result = 0;
        while (value >= 2)
        {
            result++;
            value >>= 1;
        }

        return result;
    }
}

internal readonly struct SwiftIntAscendingComparer : IComparer<int>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Compare(int x, int y)
    {
        return x.CompareTo(y);
    }
}
