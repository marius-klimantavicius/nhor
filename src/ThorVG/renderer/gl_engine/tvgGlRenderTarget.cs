// Ported from ThorVG/src/renderer/gl_engine/tvgGlRenderTarget.h and tvgGlRenderTarget.cpp
// FBO/render target abstraction.

using System;

namespace ThorVG
{
    public unsafe class GlRenderTarget : IDisposable
    {
        public RenderRegion viewport;
        public uint width;
        public uint height;
        public uint resolvedFbo;
        public uint fbo;
        public uint colorTex;

        private uint colorBuffer;
        private uint depthStencilBuffer;

        public GlRenderTarget()
        {
        }

        public void Dispose()
        {
            Reset();
            GC.SuppressFinalize(this);
        }

        ~GlRenderTarget()
        {
            // Note: GL resources should be freed on the GL thread.
        }

        public void Init(uint width, uint height, int resolveId)
        {
            if (width == 0 || height == 0) return;

            this.width = width;
            this.height = height;

            // Generate FBO
            uint fboId;
            GL.glGenFramebuffers(1, &fboId);
            fbo = fboId;
            GL.GL_CHECK();

            GL.glBindFramebuffer(GL.GL_FRAMEBUFFER, fbo);
            GL.GL_CHECK();

            // Color renderbuffer with multisampling
            uint colorBuf;
            GL.glGenRenderbuffers(1, &colorBuf);
            colorBuffer = colorBuf;
            GL.GL_CHECK();
            GL.glBindRenderbuffer(GL.GL_RENDERBUFFER, colorBuffer);
            GL.GL_CHECK();
            GL.glRenderbufferStorageMultisample(GL.GL_RENDERBUFFER, 4, GL.GL_RGBA8, (int)width, (int)height);
            GL.GL_CHECK();

            // Depth/Stencil renderbuffer with multisampling
            uint dsBuf;
            GL.glGenRenderbuffers(1, &dsBuf);
            depthStencilBuffer = dsBuf;
            GL.GL_CHECK();

            GL.glBindRenderbuffer(GL.GL_RENDERBUFFER, depthStencilBuffer);
            GL.GL_CHECK();

            GL.glRenderbufferStorageMultisample(GL.GL_RENDERBUFFER, 4, GL.GL_DEPTH24_STENCIL8, (int)width, (int)height);
            GL.GL_CHECK();

            GL.glBindRenderbuffer(GL.GL_RENDERBUFFER, 0);
            GL.GL_CHECK();

            GL.glFramebufferRenderbuffer(GL.GL_FRAMEBUFFER, GL.GL_COLOR_ATTACHMENT0, GL.GL_RENDERBUFFER, colorBuffer);
            GL.GL_CHECK();
            GL.glFramebufferRenderbuffer(GL.GL_FRAMEBUFFER, GL.GL_DEPTH_STENCIL_ATTACHMENT, GL.GL_RENDERBUFFER, depthStencilBuffer);
            GL.GL_CHECK();

            // Resolve target texture
            uint tex;
            GL.glGenTextures(1, &tex);
            colorTex = tex;
            GL.GL_CHECK();

            GL.glBindTexture(GL.GL_TEXTURE_2D, colorTex);
            GL.GL_CHECK();
            GL.glTexImage2D(GL.GL_TEXTURE_2D, 0, (int)GL.GL_RGBA8, (int)width, (int)height, 0, GL.GL_RGBA, GL.GL_UNSIGNED_BYTE, null);
            GL.GL_CHECK();

            GL.glTexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_WRAP_S, (int)GL.GL_CLAMP_TO_EDGE);
            GL.GL_CHECK();
            GL.glTexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_WRAP_T, (int)GL.GL_CLAMP_TO_EDGE);
            GL.GL_CHECK();
            GL.glTexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_MIN_FILTER, (int)GL.GL_LINEAR);
            GL.GL_CHECK();
            GL.glTexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_MAG_FILTER, (int)GL.GL_LINEAR);
            GL.GL_CHECK();

            GL.glBindTexture(GL.GL_TEXTURE_2D, 0);
            GL.GL_CHECK();

            // Resolved framebuffer
            uint resolvedFboId;
            GL.glGenFramebuffers(1, &resolvedFboId);
            resolvedFbo = resolvedFboId;
            GL.GL_CHECK();
            GL.glBindFramebuffer(GL.GL_FRAMEBUFFER, resolvedFbo);
            GL.GL_CHECK();
            GL.glFramebufferTexture2D(GL.GL_FRAMEBUFFER, GL.GL_COLOR_ATTACHMENT0, GL.GL_TEXTURE_2D, colorTex, 0);
            GL.GL_CHECK();

            GL.glBindFramebuffer(GL.GL_FRAMEBUFFER, (uint)resolveId);
            GL.GL_CHECK();
        }

        public void Reset()
        {
            if (fbo == 0) return;

            GL.glBindFramebuffer(GL.GL_FRAMEBUFFER, 0);
            GL.GL_CHECK();

            uint fboId = fbo;
            GL.glDeleteFramebuffers(1, &fboId);
            GL.GL_CHECK();
            uint cbId = colorBuffer;
            GL.glDeleteRenderbuffers(1, &cbId);
            GL.GL_CHECK();
            uint dsId = depthStencilBuffer;
            GL.glDeleteRenderbuffers(1, &dsId);
            GL.GL_CHECK();
            uint rfId = resolvedFbo;
            GL.glDeleteFramebuffers(1, &rfId);
            GL.GL_CHECK();
            uint texId = colorTex;
            GL.glDeleteTextures(1, &texId);
            GL.GL_CHECK();

            fbo = colorBuffer = depthStencilBuffer = resolvedFbo = colorTex = 0;
        }

        public bool Invalid() => fbo == 0;
    }

    public unsafe class GlRenderTargetPool : IDisposable
    {
        private uint maxWidth;
        private uint maxHeight;

        // We use a managed list instead of Array<nint> for simplicity, since these are managed objects
        private readonly System.Collections.Generic.List<GlRenderTarget> _pool = new System.Collections.Generic.List<GlRenderTarget>();

        public GlRenderTargetPool(uint maxWidth, uint maxHeight)
        {
            this.maxWidth = maxWidth;
            this.maxHeight = maxHeight;
        }

        public void Dispose()
        {
            foreach (var rt in _pool)
            {
                rt.Dispose();
            }
            _pool.Clear();
            GC.SuppressFinalize(this);
        }

        ~GlRenderTargetPool()
        {
        }

        private static uint AlignPow2(uint value)
        {
            uint ret = 1;
            while (ret < value)
            {
                ret <<= 1;
            }
            return ret;
        }

        public GlRenderTarget GetRenderTarget(in RenderRegion vp, uint resolveId = 0)
        {
            uint w = vp.W();
            uint h = vp.H();

            // pow2 align width and height
            if (w >= maxWidth) w = maxWidth;
            else w = AlignPow2(w);
            if (w >= maxWidth) w = maxWidth;

            if (h >= maxHeight) h = maxHeight;
            else h = AlignPow2(h);
            if (h >= maxHeight) h = maxHeight;

            for (int i = 0; i < _pool.Count; i++)
            {
                var rt = _pool[i];
                if (rt.width == w && rt.height == h)
                {
                    rt.viewport = vp;
                    return rt;
                }
            }

            var newRt = new GlRenderTarget();
            newRt.Init(w, h, (int)resolveId);
            newRt.viewport = vp;
            _pool.Add(newRt);
            return newRt;
        }
    }
}
