// Ported from ThorVG/src/renderer/gl_engine/tvgGlGpuBuffer.h and tvgGlGpuBuffer.cpp
// GPU memory buffer (VBO/IBO) management.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ThorVG
{
    public unsafe class GlGpuBuffer : IDisposable
    {
        public enum Target : uint
        {
            ARRAY_BUFFER = GL.GL_ARRAY_BUFFER,
            ELEMENT_ARRAY_BUFFER = GL.GL_ELEMENT_ARRAY_BUFFER,
            UNIFORM_BUFFER = GL.GL_UNIFORM_BUFFER,
        }

        private uint mGlBufferId;

        public GlGpuBuffer()
        {
            uint bufferId;
            GL.glGenBuffers(1, &bufferId);
            mGlBufferId = bufferId;
            Debug.Assert(mGlBufferId != 0);
        }

        public void Dispose()
        {
            if (mGlBufferId != 0)
            {
                uint id = mGlBufferId;
                GL.glDeleteBuffers(1, &id);
                mGlBufferId = 0;
            }
            GC.SuppressFinalize(this);
        }

        ~GlGpuBuffer()
        {
            // Note: GL resources should be freed on the GL thread.
        }

        public void UpdateBufferData(Target target, uint size, void* data)
        {
            GL.glBufferData((uint)target, (nint)size, data, GL.GL_STATIC_DRAW);
            GL.GL_CHECK();
        }

        public void Bind(Target target)
        {
            GL.glBindBuffer((uint)target, mGlBufferId);
            GL.GL_CHECK();
        }

        public void Unbind(Target target)
        {
            GL.glBindBuffer((uint)target, 0);
            GL.GL_CHECK();
        }

        public uint GetBufferId() => mGlBufferId;
    }

    /************************************************************************/
    /* GlStageBuffer                                                         */
    /************************************************************************/

    /// <summary>
    /// Helper to get GPU uniform buffer offset alignment. Mirrors C++ _getGpuBufferAlign().
    /// </summary>
    internal static unsafe class GlGpuBufferAlign
    {
        private static int _offset;

        public static int Get()
        {
            if (_offset == 0)
            {
                int val;
                GL.glGetIntegerv(GL.GL_UNIFORM_BUFFER_OFFSET_ALIGNMENT, &val);
                GL.GL_CHECK();
                _offset = val;
            }
            return _offset;
        }
    }

    public unsafe class GlStageBuffer : IDisposable
    {
        private uint mVao;
        private GlGpuBuffer mGpuBuffer;
        private GlGpuBuffer mGpuIndexBuffer;
        private Array<byte> mStageBuffer;
        private Array<byte> mIndexBuffer;

        public GlStageBuffer()
        {
            mGpuBuffer = new GlGpuBuffer();
            mGpuIndexBuffer = new GlGpuBuffer();

            uint vao;
            GL.glGenVertexArrays(1, &vao);
            mVao = vao;
        }

        public void Dispose()
        {
            if (mVao != 0)
            {
                uint vao = mVao;
                GL.glDeleteVertexArrays(1, &vao);
                mVao = 0;
            }
            mGpuBuffer?.Dispose();
            mGpuBuffer = null!;
            mGpuIndexBuffer?.Dispose();
            mGpuIndexBuffer = null!;
            mStageBuffer.Dispose();
            mIndexBuffer.Dispose();
            GC.SuppressFinalize(this);
        }

        ~GlStageBuffer()
        {
            // Note: GL resources should be freed on the GL thread.
        }

        public uint Push(void* data, uint size, bool alignGpuOffset = false)
        {
            if (alignGpuOffset) AlignOffset(size);

            uint offset = mStageBuffer.count;

            if (mStageBuffer.reserved - mStageBuffer.count < size)
            {
                mStageBuffer.Grow(Math.Max(size, mStageBuffer.reserved));
            }

            Unsafe.CopyBlock(mStageBuffer.data + offset, data, size);

            mStageBuffer.count += size;

            return offset;
        }

        public uint PushIndex(void* data, uint size)
        {
            uint offset = mIndexBuffer.count;

            if (mIndexBuffer.reserved - mIndexBuffer.count < size)
            {
                mIndexBuffer.Grow(Math.Max(size, mIndexBuffer.reserved));
            }

            Unsafe.CopyBlock(mIndexBuffer.data + offset, data, size);

            mIndexBuffer.count += size;

            return offset;
        }

        public bool FlushToGPU()
        {
            if (mStageBuffer.Empty() || mIndexBuffer.Empty())
            {
                mStageBuffer.Clear();
                mIndexBuffer.Clear();
                return false;
            }

            mGpuBuffer.Bind(GlGpuBuffer.Target.ARRAY_BUFFER);
            mGpuBuffer.UpdateBufferData(GlGpuBuffer.Target.ARRAY_BUFFER, mStageBuffer.count, mStageBuffer.data);
            mGpuBuffer.Unbind(GlGpuBuffer.Target.ARRAY_BUFFER);

            mGpuIndexBuffer.Bind(GlGpuBuffer.Target.ELEMENT_ARRAY_BUFFER);
            mGpuIndexBuffer.UpdateBufferData(GlGpuBuffer.Target.ELEMENT_ARRAY_BUFFER, mIndexBuffer.count, mIndexBuffer.data);
            mGpuIndexBuffer.Unbind(GlGpuBuffer.Target.ELEMENT_ARRAY_BUFFER);

            mStageBuffer.Clear();
            mIndexBuffer.Clear();

            return true;
        }

        public void Bind()
        {
            GL.glBindVertexArray(mVao);
            mGpuBuffer.Bind(GlGpuBuffer.Target.ARRAY_BUFFER);
            mGpuBuffer.Bind(GlGpuBuffer.Target.UNIFORM_BUFFER);
            mGpuIndexBuffer.Bind(GlGpuBuffer.Target.ELEMENT_ARRAY_BUFFER);
        }

        public void Unbind()
        {
            GL.glBindVertexArray(0);
            mGpuBuffer.Unbind(GlGpuBuffer.Target.ARRAY_BUFFER);
            mGpuBuffer.Unbind(GlGpuBuffer.Target.UNIFORM_BUFFER);
            mGpuIndexBuffer.Unbind(GlGpuBuffer.Target.ELEMENT_ARRAY_BUFFER);
        }

        public uint GetBufferId()
        {
            return mGpuBuffer.GetBufferId();
        }

        private void AlignOffset(uint size)
        {
            uint alignment = (uint)GlGpuBufferAlign.Get();

            if (mStageBuffer.count % alignment == 0) return;

            uint offset = alignment - mStageBuffer.count % alignment;

            if (mStageBuffer.count + offset + size > mStageBuffer.reserved)
            {
                mStageBuffer.Grow(Math.Max(offset + size, mStageBuffer.reserved));
            }

            mStageBuffer.count += offset;
        }
    }
}
