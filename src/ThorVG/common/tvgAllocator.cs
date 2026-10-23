// Ported from ThorVG/src/common/tvgAllocator.h
// In C++ these are custom allocators wrapping std::malloc/calloc/realloc/free.
// Managed storage elsewhere remains managed; these methods own native buffers.

using System;
using System.Runtime.InteropServices;

namespace ThorVG
{
    /// <summary>
    /// Separate memory allocators for clean customization.
    /// Mirrors the C++ tvg::malloc/calloc/realloc/free template functions.
    /// In C# most allocations use managed arrays; these are provided for
    /// structural parity with C++ code that uses raw unmanaged buffers.
    /// </summary>
    public static unsafe class TvgAllocator
    {
        public static IntPtr Malloc(int size)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(size);
            return Malloc((nuint)size);
        }

        public static IntPtr Malloc(nuint size) => (IntPtr)NativeMemory.Alloc(size);

        public static IntPtr Calloc(int nmem, int size)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(nmem);
            ArgumentOutOfRangeException.ThrowIfNegative(size);
            return Calloc((nuint)nmem, (nuint)size);
        }

        public static IntPtr Calloc(nuint nmem, nuint size) => (IntPtr)NativeMemory.AllocZeroed(nmem, size);

        public static IntPtr Realloc(IntPtr ptr, int size)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(size);
            return Realloc(ptr, (nuint)size);
        }

        public static IntPtr Realloc(IntPtr ptr, nuint size) => (IntPtr)NativeMemory.Realloc((void*)ptr, size);

        public static void Free(IntPtr ptr)
        {
            NativeMemory.Free((void*)ptr);
        }
    }
}
