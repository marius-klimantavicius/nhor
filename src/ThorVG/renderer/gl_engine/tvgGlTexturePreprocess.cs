// Ported from pinned ThorVG/src/renderer/gpu_engine/gl/tvgGlTextureMgr.cpp.
using System.Collections.Generic;

namespace ThorVG
{
    public unsafe partial class TextureMgr
    {
        private const int PREP_PREMULTIPLY = 1, PREP_BGR = 2;
        private struct Request { public uint texture, width, height; public int flags; }
        private readonly List<Request> prepRequests = new();
        private GlProgram? prepProgram;
        private uint prepStagingFbo, prepTargetFbo, prepVao, prepStagingTex, prepStagingWidth, prepStagingHeight;
        private int prepFlagsLoc = -1;
        private const string PREPROCESS_VERTEX = "void main(){vec2 p=vec2(gl_VertexID==2?3.0:-1.0,gl_VertexID==1?3.0:-1.0);gl_Position=vec4(p,0.0,1.0);}";
        private const string PREPROCESS_FRAGMENT = "uniform sampler2D uTexture;uniform int uFlags;out vec4 FragColor;void main(){vec4 c=texelFetch(uTexture,ivec2(gl_FragCoord.xy),0);if((uFlags&2)!=0)c=c.bgra;if((uFlags&1)!=0)c.rgb*=c.a;FragColor=c;}";

        private void RequestPreprocess(uint texture, uint width, uint height, int flags)
        {
            if (texture == 0) return;
            for (int i = 0; i < prepRequests.Count; ++i)
            {
                if (prepRequests[i].texture != texture) continue;
                if (flags != 0 && width != 0 && height != 0) prepRequests[i] = new Request { texture = texture, width = width, height = height, flags = flags };
                else { prepRequests[i] = prepRequests[prepRequests.Count - 1]; prepRequests.RemoveAt(prepRequests.Count - 1); }
                return;
            }
            if (flags != 0 && width != 0 && height != 0) prepRequests.Add(new Request { texture = texture, width = width, height = height, flags = flags });
        }

        public void Upload(GlStateCache state, uint texture, RenderSurface surface, FilterMethod filter, bool initialize)
        {
            int flags = 0;
            if (surface.channelSize == sizeof(uint))
            {
                bool supported = true;
                if (surface.cs == ColorSpace.ARGB8888 || surface.cs == ColorSpace.ARGB8888S) flags |= PREP_BGR;
                else if (surface.cs != ColorSpace.ABGR8888 && surface.cs != ColorSpace.ABGR8888S) supported = false;
                if (supported && !surface.premultiplied) flags |= PREP_PREMULTIPLY;
            }
            state.BindTexture2D(GL.GL_TEXTURE0, texture);
            GL.glTexImage2D(GL.GL_TEXTURE_2D, 0, (int)GL.GL_RGBA8, (int)surface.w, (int)surface.h, 0, GL.GL_RGBA, GL.GL_UNSIGNED_BYTE, surface.buf8);
            if (initialize)
            {
                GL.glTexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_WRAP_S, (int)GL.GL_CLAMP_TO_EDGE);
                GL.glTexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_WRAP_T, (int)GL.GL_CLAMP_TO_EDGE);
                GL.glTexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_MIN_FILTER, (int)(filter == FilterMethod.Bilinear ? GL.GL_LINEAR : GL.GL_NEAREST));
                GL.glTexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_MAG_FILTER, (int)(filter == FilterMethod.Bilinear ? GL.GL_LINEAR : GL.GL_NEAREST));
            }
            state.BindTexture2D(GL.GL_TEXTURE0, 0);
            RequestPreprocess(texture, surface.w, surface.h, flags);
        }

        public bool FlushPreprocess(GlStateCache state, int restoreFbo)
        {
            if (prepRequests.Count == 0) return true;
            if (prepProgram == null)
            {
                prepProgram = new GlProgram(PREPROCESS_VERTEX, PREPROCESS_FRAGMENT);
                prepFlagsLoc = prepProgram.GetUniformLocation("uFlags\0"u8);
            }
            uint id;
            if (prepStagingFbo == 0) { GL.glGenFramebuffers(1, &id); prepStagingFbo = id; }
            if (prepTargetFbo == 0) { GL.glGenFramebuffers(1, &id); prepTargetFbo = id; }
            if (prepVao == 0) { GL.glGenVertexArrays(1, &id); prepVao = id; }
            if (prepStagingTex == 0) { GL.glGenTextures(1, &id); prepStagingTex = id; }
            if (prepStagingFbo == 0 || prepTargetFbo == 0 || prepVao == 0 || prepStagingTex == 0) return false;
            state.BindFramebuffer(GL.GL_FRAMEBUFFER, prepStagingFbo); state.ColorMask(1, 1, 1, 1);
            state.Disable(GL.GL_BLEND); state.Disable(GL.GL_CULL_FACE); state.Disable(GL.GL_DITHER);
            state.Disable(GL.GL_RASTERIZER_DISCARD); state.Disable(GL.GL_SCISSOR_TEST);
#if !THORVG_GL_TARGET_GLES
            state.Disable(GL.GL_COLOR_LOGIC_OP);
#endif
            state.UseProgram(prepProgram.GetProgramId()); prepProgram.SetSampler(GlShaderUniform.Texture, 0); state.BindVertexArray(prepVao);
            int currentFlags = 0;
            foreach (var request in prepRequests)
            {
                if (request.width > prepStagingWidth || request.height > prepStagingHeight)
                {
                    bool attach = prepStagingWidth == 0;
                    state.BindTexture2D(GL.GL_TEXTURE0, prepStagingTex);
                    GL.glTexImage2D(GL.GL_TEXTURE_2D, 0, (int)GL.GL_RGBA8, (int)request.width, (int)request.height, 0, GL.GL_RGBA, GL.GL_UNSIGNED_BYTE, null);
                    prepStagingWidth = request.width; prepStagingHeight = request.height;
                    if (attach) GL.glFramebufferTexture2D(GL.GL_FRAMEBUFFER, GL.GL_COLOR_ATTACHMENT0, GL.GL_TEXTURE_2D, prepStagingTex, 0);
                }
                state.BindFramebuffer(GL.GL_DRAW_FRAMEBUFFER, prepStagingFbo);
                state.Viewport(0, 0, (int)request.width, (int)request.height); state.BindTexture2D(GL.GL_TEXTURE0, request.texture);
                if (currentFlags != request.flags) { currentFlags = request.flags; GL.glUniform1iv(prepFlagsLoc, 1, &currentFlags); }
                GL.glDrawArrays(GL.GL_TRIANGLES, 0, 3);
                state.BindFramebuffer(GL.GL_DRAW_FRAMEBUFFER, prepTargetFbo);
                GL.glFramebufferTexture2D(GL.GL_DRAW_FRAMEBUFFER, GL.GL_COLOR_ATTACHMENT0, GL.GL_TEXTURE_2D, request.texture, 0);
                GL.glBlitFramebuffer(0, 0, (int)request.width, (int)request.height, 0, 0, (int)request.width, (int)request.height, GL.GL_COLOR_BUFFER_BIT, GL.GL_NEAREST);
            }
            prepRequests.Clear();
            GL.glFramebufferTexture2D(GL.GL_DRAW_FRAMEBUFFER, GL.GL_COLOR_ATTACHMENT0, GL.GL_TEXTURE_2D, 0, 0);
            state.BindTexture2D(GL.GL_TEXTURE0, 0); state.BindVertexArray(0); state.BindFramebuffer(GL.GL_FRAMEBUFFER, (uint)restoreFbo); state.Enable(GL.GL_DITHER);
            return true;
        }

        private void ClearPreprocess()
        {
            prepRequests.Clear(); prepProgram?.Dispose(); prepProgram = null;
            uint id;
            if (prepStagingFbo != 0) { id = prepStagingFbo; GL.glDeleteFramebuffers(1, &id); }
            if (prepTargetFbo != 0) { id = prepTargetFbo; GL.glDeleteFramebuffers(1, &id); }
            if (prepStagingTex != 0) { id = prepStagingTex; GL.glDeleteTextures(1, &id); }
            if (prepVao != 0) { id = prepVao; GL.glDeleteVertexArrays(1, &id); }
            prepStagingFbo = prepTargetFbo = prepStagingTex = prepVao = prepStagingWidth = prepStagingHeight = 0;
            prepFlagsLoc = -1;
        }
    }
}
