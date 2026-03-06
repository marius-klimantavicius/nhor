using Xunit;

namespace ThorVG.Tests
{
    public class tvgGlRenderTargetTests
    {
        // ---- GlRenderTarget construction and initial state ---------------

        [Fact]
        public void GlRenderTarget_DefaultIsInvalid()
        {
            using var rt = new GlRenderTarget();
            Assert.True(rt.Invalid());
        }

        [Fact]
        public void GlRenderTarget_DefaultValuesAreZero()
        {
            using var rt = new GlRenderTarget();
            Assert.Equal(0u, rt.fbo);
            Assert.Equal(0u, rt.resolvedFbo);
            Assert.Equal(0u, rt.colorTex);
            Assert.Equal(0u, rt.width);
            Assert.Equal(0u, rt.height);
        }

        [Fact]
        public void GlRenderTarget_Implements_IDisposable()
        {
            var rt = new GlRenderTarget();
            Assert.IsAssignableFrom<System.IDisposable>(rt);
            rt.Dispose();
        }

        [Fact]
        public void GlRenderTarget_Reset_WhenAlreadyInvalid_DoesNotThrow()
        {
            var rt = new GlRenderTarget();
            // Reset on an already-zero FBO should just return early
            rt.Reset();
            Assert.True(rt.Invalid());
            rt.Dispose();
        }

        [Fact]
        public void GlRenderTarget_Dispose_WhenAlreadyInvalid_DoesNotThrow()
        {
            var rt = new GlRenderTarget();
            rt.Dispose();
            // Double dispose should not throw
            rt.Dispose();
        }

        [Fact]
        public void GlRenderTarget_Viewport_CanBeSet()
        {
            using var rt = new GlRenderTarget();
            var vp = new RenderRegion(10, 20, 110, 120);
            rt.viewport = vp;
            Assert.Equal(10, rt.viewport.min.x);
            Assert.Equal(20, rt.viewport.min.y);
            Assert.Equal(110, rt.viewport.max.x);
            Assert.Equal(120, rt.viewport.max.y);
        }

        // ---- GlRenderTargetPool ------------------------------------------
        // Note: GlRenderTargetPool.GetRenderTarget calls GlRenderTarget.Init
        // which requires a real GL context. We can only test the construction
        // and disposal without a context.

        [Fact]
        public void GlRenderTargetPool_Implements_IDisposable()
        {
            var pool = new GlRenderTargetPool(1024, 1024);
            Assert.IsAssignableFrom<System.IDisposable>(pool);
            pool.Dispose();
        }

        [Fact]
        public void GlRenderTargetPool_DoubleDispose_DoesNotThrow()
        {
            var pool = new GlRenderTargetPool(512, 512);
            pool.Dispose();
            pool.Dispose();
        }
    }
}
