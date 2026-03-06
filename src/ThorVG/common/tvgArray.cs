// Ported from ThorVG/src/common/tvgArray.h

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ThorVG
{
    /// <summary>
    /// A simple, growable, unmanaged-memory array that mirrors the C++
    /// <c>tvg::Array&lt;T&gt;</c>.  <typeparamref name="T"/> must be an
    /// unmanaged (blittable) type so we can use raw pointers.
    /// </summary>
    public unsafe struct Array<T> : IDisposable where T : unmanaged
    {
        public T* data;
        public uint count;
        public uint reserved;

        public Array(int size)
        {
            data = null;
            count = 0;
            reserved = 0;
            Reserve((uint)size);
        }

        // ---- Push -------------------------------------------------------

        public void Push(T element)
        {
            if (count + 1 > reserved)
            {
                reserved = count + (count + 2) / 2;
                data = Realloc(data, reserved);
            }
            data[count++] = element;
        }

        public void Push(in Array<T> rhs)
        {
            if (rhs.count == 0) return;
            Grow(rhs.count);
            Unsafe.CopyBlock(data + count, rhs.data, rhs.count * (uint)sizeof(T));
            count += rhs.count;
        }

        // ---- Reserve / Grow ---------------------------------------------

        public bool Reserve(uint size)
        {
            if (size > reserved)
            {
                reserved = size;
                data = Realloc(data, reserved);
            }
            return true;
        }

        public bool Grow(uint size)
        {
            return Reserve(count + size);
        }

        // ---- Indexer -----------------------------------------------------

        public ref T this[uint idx]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref data[idx];
        }

        public ref T this[int idx]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref data[idx];
        }

        // ---- Copy -------------------------------------------------------

        public void CopyFrom(in Array<T> rhs)
        {
            Reserve(rhs.count);
            if (rhs.count > 0)
                Unsafe.CopyBlock(data, rhs.data, rhs.count * (uint)sizeof(T));
            count = rhs.count;
        }

        // ---- Move -------------------------------------------------------

        public void MoveTo(ref Array<T> to)
        {
            to.Reset();
            to.data = data;
            to.count = count;
            to.reserved = reserved;

            data = null;
            count = reserved = 0;
        }

        // ---- Iterators / accessors --------------------------------------

        public T* Begin() => data;
        public T* End() => data + count;

        public ref T Last() => ref data[count - 1];
        public ref T First() => ref data[0];

        /// <summary>
        /// Grows if full, then returns a ref to the slot at <c>count</c>
        /// and increments count.  Mirrors C++ <c>next()</c>.
        /// </summary>
        public ref T Next()
        {
            if (Full()) Grow(count + 1);
            return ref data[count++];
        }

        // ---- Removal / clear -------------------------------------------

        public void Pop()
        {
            if (count > 0) --count;
        }

        public void Reset()
        {
            if (data != null)
            {
                NativeMemory.Free(data);
                data = null;
            }
            count = reserved = 0;
        }

        public void Clear()
        {
            count = 0;
        }

        // ---- Queries ----------------------------------------------------

        public bool Empty() => count == 0;
        public bool Full() => count == reserved;

        // ---- Conversion -------------------------------------------------

        /// <summary>
        /// Creates a managed array copy of the current contents.
        /// </summary>
        public T[] ToArray()
        {
            if (count == 0) return System.Array.Empty<T>();
            var arr = new T[count];
            fixed (T* dst = arr)
            {
                Unsafe.CopyBlock(dst, data, count * (uint)sizeof(T));
            }
            return arr;
        }

        // ---- IDisposable ------------------------------------------------

        public void Dispose()
        {
            if (data != null)
            {
                NativeMemory.Free(data);
                data = null;
            }
            count = reserved = 0;
        }

        // ---- Internal allocator ----------------------------------------

        private static T* Realloc(T* ptr, uint elementCount)
        {
            var bytes = (nuint)(elementCount * (uint)sizeof(T));
            return (T*)NativeMemory.Realloc(ptr, bytes);
        }
    }
}
