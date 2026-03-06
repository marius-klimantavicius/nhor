using Xunit;

namespace ThorVG.Tests
{
    public class tvgGlGpuBufferTests
    {
        // ---- GlGpuBuffer.Target enum -------------------------------------

        [Fact]
        public void GlGpuBuffer_Target_ArrayBuffer()
        {
            Assert.Equal(GL.GL_ARRAY_BUFFER, (uint)GlGpuBuffer.Target.ARRAY_BUFFER);
        }

        [Fact]
        public void GlGpuBuffer_Target_ElementArrayBuffer()
        {
            Assert.Equal(GL.GL_ELEMENT_ARRAY_BUFFER, (uint)GlGpuBuffer.Target.ELEMENT_ARRAY_BUFFER);
        }

        [Fact]
        public void GlGpuBuffer_Target_UniformBuffer()
        {
            Assert.Equal(GL.GL_UNIFORM_BUFFER, (uint)GlGpuBuffer.Target.UNIFORM_BUFFER);
        }

        [Fact]
        public void GlGpuBuffer_Target_AllDistinct()
        {
            var values = new[]
            {
                GlGpuBuffer.Target.ARRAY_BUFFER,
                GlGpuBuffer.Target.ELEMENT_ARRAY_BUFFER,
                GlGpuBuffer.Target.UNIFORM_BUFFER
            };

            // All values should be distinct
            Assert.Equal(3, new System.Collections.Generic.HashSet<GlGpuBuffer.Target>(values).Count);
        }

        // Note: GlGpuBuffer, GlStageBuffer, and GlGpuBufferAlign cannot be
        // constructed/tested without a real OpenGL context, since their
        // constructors call GL functions (glGenBuffers, glGenVertexArrays, etc.)
        // The API surface existence is verified at compile time.
    }
}
