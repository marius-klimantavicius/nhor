using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ThorVG;

[InlineArray(8)]
public struct InlineBuffer8<T>
{
    private T _element0;
}

/// <summary>
/// A mutable value-type list with an 8-element inline buffer that avoids heap
/// allocation for small collections.  When count exceeds 8, spills to a heap T[].
/// <para>
/// Because this is a struct, mutations require the variable to be passed by
/// <c>ref</c> or used in-place (field, array element, etc.).
/// </para>
/// </summary>
public struct ValueList<T>
{
    private InlineBuffer8<T> _inline;
    private T[]? _overflow;
    private int _count;

    private const int InlineCapacity = 8;

    public readonly int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count;
    }

    public readonly bool IsEmpty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count == 0;
    }

    public readonly ref T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)index >= (uint)_count)
                ThrowIndexOutOfRange();
            if (_overflow != null)
                return ref _overflow[index];
            return ref Unsafe.Add(ref GetInlineRef(), index);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T item)
    {
        if (_overflow != null)
        {
            if (_count == _overflow.Length)
                GrowOverflow();
            _overflow[_count++] = item;
        }
        else if (_count < InlineCapacity)
        {
            Unsafe.Add(ref Unsafe.As<InlineBuffer8<T>, T>(ref _inline), _count) = item;
            _count++;
        }
        else
        {
            SpillToOverflow(item);
        }
    }

    public void Insert(int index, T item)
    {
        if ((uint)index > (uint)_count)
            ThrowIndexOutOfRange();

        if (_overflow != null)
        {
            if (_count == _overflow.Length)
                GrowOverflow();
            Array.Copy(_overflow, index, _overflow, index + 1, _count - index);
            _overflow[index] = item;
            _count++;
        }
        else if (_count < InlineCapacity)
        {
            var span = GetInlineSpan();
            for (int i = _count; i > index; i--)
                span[i] = span[i - 1];
            span[index] = item;
            _count++;
        }
        else
        {
            // Spill then insert
            var arr = new T[InlineCapacity * 2];
            var src = GetInlineSpan()[.._count];
            src[..index].CopyTo(arr.AsSpan());
            arr[index] = item;
            src[index..].CopyTo(arr.AsSpan(index + 1));
            _overflow = arr;
            _count++;
        }
    }

    public void RemoveAt(int index)
    {
        if ((uint)index >= (uint)_count)
            ThrowIndexOutOfRange();

        _count--;
        if (_overflow != null)
        {
            Array.Copy(_overflow, index + 1, _overflow, index, _count - index);
            _overflow[_count] = default!;
        }
        else
        {
            var span = GetInlineSpan();
            for (int i = index; i < _count; i++)
                span[i] = span[i + 1];
            span[_count] = default!;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        if (_overflow != null)
        {
            Array.Clear(_overflow, 0, _count);
            _overflow = null;
        }
        else if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            GetInlineSpan()[.._count].Clear();
        }
        _count = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Span<T> AsSpan()
    {
        if (_overflow != null)
            return _overflow.AsSpan(0, _count);
        return MemoryMarshal.CreateSpan(ref GetInlineRef(), _count);
    }

    public readonly ReadOnlySpan<T> AsReadOnlySpan() => AsSpan();

    public readonly T[] ToArray()
    {
        if (_count == 0) return Array.Empty<T>();
        var arr = new T[_count];
        AsSpan().CopyTo(arr);
        return arr;
    }

    public void Sort(Comparison<T> comparison)
    {
        AsSpan().Sort(comparison);
    }

    /// <summary>Clears and copies all items from <paramref name="source"/>.</summary>
    public void CopyFrom(ref ValueList<T> source)
    {
        Clear();
        var span = source.AsSpan();
        for (int i = 0; i < span.Length; i++)
            Add(span[i]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Enumerator GetEnumerator() => new(AsSpan());

    // --- private helpers ---

    /// <summary>Returns a mutable ref to the first element of the inline buffer.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly ref T GetInlineRef()
    {
        return ref Unsafe.As<InlineBuffer8<T>, T>(ref Unsafe.AsRef(in _inline));
    }

    /// <summary>Returns a full-capacity span over the inline buffer (non-readonly context).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Span<T> GetInlineSpan()
    {
        return MemoryMarshal.CreateSpan(
            ref Unsafe.As<InlineBuffer8<T>, T>(ref _inline), InlineCapacity);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void SpillToOverflow(T item)
    {
        var arr = new T[InlineCapacity * 2];
        GetInlineSpan()[.._count].CopyTo(arr);
        arr[_count] = item;
        _overflow = arr;
        _count++;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void GrowOverflow()
    {
        var newArr = new T[_overflow!.Length * 2];
        Array.Copy(_overflow, newArr, _count);
        _overflow = newArr;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowIndexOutOfRange() =>
        throw new ArgumentOutOfRangeException("index");

    // --- Struct enumerator ---

    public ref struct Enumerator
    {
        private readonly Span<T> _span;
        private int _index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(Span<T> span)
        {
            _span = span;
            _index = -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext() => ++_index < _span.Length;

        public readonly ref T Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _span[_index];
        }
    }
}
