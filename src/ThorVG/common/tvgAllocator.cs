// Ported from ThorVG/src/common/tvgAllocator.h
// In C++ these are custom allocators wrapping std::malloc/calloc/realloc/free.
// In C# we use GC-managed arrays, so these are simple pass-through helpers
// for the rare cases where unmanaged memory is needed (matching the C++ API shape).

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
            return Marshal.AllocHGlobal(size);
        }

        public static IntPtr Calloc(int nmem, int size)
        {
            var total = nmem * size;
            var ptr = Marshal.AllocHGlobal(total);
            new Span<byte>((void*)ptr, total).Clear();
            return ptr;
        }

        public static IntPtr Realloc(IntPtr ptr, int size)
        {
            return Marshal.ReAllocHGlobal(ptr, (IntPtr)size);
        }

        public static void Free(IntPtr ptr)
        {
            if (ptr != IntPtr.Zero)
                Marshal.FreeHGlobal(ptr);
        }
    }
}
