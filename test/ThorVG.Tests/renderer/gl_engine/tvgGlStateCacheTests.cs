using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xunit;

namespace ThorVG.Tests
{
    // Driver-call trace tests complement the real-context native pixel harness.
    // xunit.runner.json serializes collections, so swapping these entrypoints is isolated.
    public unsafe class tvgGlStateCacheTests
    {
        private static readonly int[] calls = new int[12];
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })] private static void Program(uint p) => ++calls[0];
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })] private static void Vao(uint p) => ++calls[1];
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })] private static void Buffer(uint t, uint p) => ++calls[2];
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })] private static void EnableAttribute(uint p) => ++calls[3];
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })] private static void DisableAttribute(uint p) => ++calls[4];
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })] private static void Pointer(uint i, int s, uint t, byte n, int stride, void* p) => ++calls[5];
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })] private static void Constant(uint i, float x, float y, float z, float w) => ++calls[6];
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })] private static void Fbo(uint t, uint p) => ++calls[7];
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })] private static void Viewport(int x, int y, int w, int h) => ++calls[8];
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })] private static void Stencil(uint f, int r, uint m) => ++calls[9];
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })] private static void StencilSeparate(uint face, uint f, int r, uint m) => ++calls[10];

        [Fact]
        public void DriverCalls_AreCachedAndReplayedAfterInvalidation()
        {
            var program = GL.glUseProgram; var vao = GL.glBindVertexArray; var buffer = GL.glBindBuffer;
            var enable = GL.glEnableVertexAttribArray; var disable = GL.glDisableVertexAttribArray;
            var pointer = GL.glVertexAttribPointer; var constant = GL.glVertexAttrib4f;
            var fbo = GL.glBindFramebuffer; var viewport = GL.glViewport;
            var stencil = GL.glStencilFunc; var separate = GL.glStencilFuncSeparate;
            System.Array.Clear(calls);
            GL.glUseProgram = &Program; GL.glBindVertexArray = &Vao; GL.glBindBuffer = &Buffer;
            GL.glEnableVertexAttribArray = &EnableAttribute; GL.glDisableVertexAttribArray = &DisableAttribute;
            GL.glVertexAttribPointer = &Pointer; GL.glVertexAttrib4f = &Constant;
            GL.glBindFramebuffer = &Fbo; GL.glViewport = &Viewport;
            GL.glStencilFunc = &Stencil; GL.glStencilFuncSeparate = &StencilSeparate;
            try
            {
                var state = new GlStateCache();
                state.UseProgram(3); state.UseProgram(3); Assert.Equal(1, calls[0]);
                state.Viewport(1, 2, 64, 32); state.Viewport(1, 2, 64, 32); Assert.Equal(1, calls[8]);
                state.BindFramebuffer(GL.GL_FRAMEBUFFER, 1); state.BindFramebuffer(GL.GL_READ_FRAMEBUFFER, 1);
                state.BindFramebuffer(GL.GL_DRAW_FRAMEBUFFER, 2); state.BindFramebuffer(GL.GL_FRAMEBUFFER, 2);
                state.BindFramebuffer(GL.GL_READ_FRAMEBUFFER, 2); Assert.Equal(3, calls[7]);
                state.StencilFunc(GL.GL_ALWAYS, 1, 255);
                state.StencilFuncSeparate(GL.GL_FRONT, GL.GL_ALWAYS, 1, 255); Assert.Equal(0, calls[10]);
                state.StencilFuncSeparate(GL.GL_BACK, GL.GL_EQUAL, 2, 255);
                state.StencilFunc(GL.GL_ALWAYS, 1, 255); Assert.Equal(2, calls[9]); Assert.Equal(1, calls[10]);
                state.BindVertexArray(1); state.BeginVertexLayout();
                state.SetVertexAttribPointer(1, 4, GL.GL_UNSIGNED_BYTE, 1, 4, 0, 8);
                state.SetVertexAttribPointer(1, 4, GL.GL_UNSIGNED_BYTE, 1, 4, 0, 8);
                state.EndVertexLayout(); Assert.Equal(1, calls[2]); Assert.Equal(1, calls[3]); Assert.Equal(1, calls[5]);
                state.BeginVertexLayout(); state.SetVertexAttrib4f(1, 1, 0, 0, 1); state.EndVertexLayout();
                state.SetVertexAttrib4f(1, 1, 0, 0, 1); Assert.Equal(1, calls[4]); Assert.Equal(1, calls[6]);
                state.SetVertexAttribPointer(1, 4, GL.GL_UNSIGNED_BYTE, 1, 4, 0, 8);
                state.SetVertexAttrib4f(1, 1, 0, 0, 1); Assert.Equal(2, calls[6]);
                // Element-array and pointer bindings belong to the VAO; generic
                // constants do not. A new VAO must replay the pointer and enable.
                state.BindVertexArray(2); state.SetVertexAttribPointer(1, 4, GL.GL_UNSIGNED_BYTE, 1, 4, 0, 8);
                Assert.Equal(2, calls[5]); Assert.Equal(3, calls[3]);
                state.BeginVertexLayout(); state.EndVertexLayout(); Assert.Equal(3, calls[4]);
                int before = calls[0]; state.Invalidate(); Assert.Equal(before, calls[0]);
                state.UseProgram(3); state.Viewport(1, 2, 64, 32); Assert.Equal(2, calls[0]); Assert.Equal(2, calls[8]);
            }
            finally
            {
                GL.glUseProgram = program; GL.glBindVertexArray = vao; GL.glBindBuffer = buffer;
                GL.glEnableVertexAttribArray = enable; GL.glDisableVertexAttribArray = disable;
                GL.glVertexAttribPointer = pointer; GL.glVertexAttrib4f = constant;
                GL.glBindFramebuffer = fbo; GL.glViewport = viewport;
                GL.glStencilFunc = stencil; GL.glStencilFuncSeparate = separate;
            }
        }
    }
}
