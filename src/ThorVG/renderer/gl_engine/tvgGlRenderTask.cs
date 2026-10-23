// Ported from ThorVG/src/renderer/gl_engine/tvgGlRenderTask.h and tvgGlRenderTask.cpp

using System;
using System.Collections.Generic;

namespace ThorVG
{
    public struct GlVertexLayout
    {
        public uint index;
        public uint size;
        public uint stride;
        public uint offset;
        public uint type;
        public byte normalized;
        // Optional VBO for this attribute. 0 means use the GL_ARRAY_BUFFER binding
        // captured at the start of GlRenderTask.Run(state).
        public uint arrayBufferId;

        public GlVertexLayout()
        {
            type = GL.GL_FLOAT;
            normalized = GL.GL_FALSE;
            arrayBufferId = 0;
        }
    }

    public enum GlBindingType
    {
        kUniformBuffer,
        kTexture,
    }

    public struct GlBindingResource
    {
        public GlBindingType type;
        public uint bindPoint;
        public int location;
        // GL object id used by this binding: texture id for kTexture, UBO id for kUniformBuffer.
        public uint resourceId;
        public uint bufferOffset;
        public uint bufferRange;

        // Uniform buffer constructor
        public GlBindingResource(uint index, int location, uint uniformBufferId, uint offset, uint range)
        {
            type = GlBindingType.kUniformBuffer;
            bindPoint = index;
            this.location = location;
            resourceId = uniformBufferId;
            bufferOffset = offset;
            bufferRange = range;
        }

        // Texture constructor
        public GlBindingResource(uint bindPoint, uint textureId, int location)
        {
            type = GlBindingType.kTexture;
            this.bindPoint = bindPoint;
            this.location = location;
            resourceId = textureId;
            bufferOffset = 0;
            bufferRange = 0;
        }
    }

    /************************************************************************/
    /* GlRenderTask                                                         */
    /************************************************************************/

    public unsafe class GlRenderTask : IDisposable
    {
#if THORVG_GL_TARGET_GLES
        protected static void ClearColorTarget(GlStateCache state, uint width, uint height)
        {
            state.Scissor(0, 0, (int)width, (int)height);
            state.ClearColor(0, 0, 0, 0);
            GL.glClear(GL.GL_COLOR_BUFFER_BIT);
        }
#endif
        internal GlProgram? mProgram;
        internal RenderRegion mViewport;
        internal uint mIndexOffset;
        internal uint mIndexCount;
        internal uint mArrayMode = GL.GL_TRIANGLES;
        internal uint mArrayOffset;
        internal Array<GlVertexLayout> mVertexLayout;
        internal Array<GlBindingResource> mBindingResources;
        internal float mDrawDepth;
        internal Matrix mViewMatrix;
        internal bool mUseViewMatrix;
        internal bool mUseVertexColor;
        internal bool mUseDrawArrays;
        private float mVertexColor0, mVertexColor1, mVertexColor2, mVertexColor3;

        private bool storageDisposed;

        // A task owns only its CPU arrays. Programs, VBOs and render targets
        // are borrowed and are released by their renderer on the GL thread.
        ~GlRenderTask() => ReleaseStorage();

        private void ReleaseStorage()
        {
            mVertexLayout.Dispose();
            mBindingResources.Dispose();
        }

        public void Dispose()
        {
            if (storageDisposed) return;
            storageDisposed = true;
            try { DisposeOwnedTasks(); }
            finally
            {
                ReleaseStorage();
                GC.SuppressFinalize(this);
            }
        }

        protected virtual void DisposeOwnedTasks() { }

        public GlRenderTask(GlProgram? program)
        {
            mProgram = program;
        }

        public GlRenderTask(GlProgram? program, GlRenderTask other) : this(program)
        {
            mVertexLayout.Push(other.mVertexLayout);
            mViewport = other.mViewport;
            mIndexOffset = other.mIndexOffset;
            mIndexCount = other.mIndexCount;
            mViewMatrix = other.mViewMatrix;
            mUseViewMatrix = other.mUseViewMatrix;
        }

        public virtual void Run()
        {
            Run(new GlStateCache());
        }

        public virtual void Run(GlStateCache state)
        {
            RunDraw(state);
        }

        /// <summary>
        /// Non-virtual draw method that executes the GL draw calls.
        /// Subclasses that need to call the base GlRenderTask draw logic
        /// (bypassing intermediate overrides) can call this directly.
        /// </summary>
        protected void RunDraw(GlStateCache state)
        {
            // bind shader
            state.UseProgram(mProgram!.GetProgramId());

            var dLoc = mProgram.GetUniformLocation(GlShaderUniform.Depth);
            if (dLoc >= 0)
            {
                GL.glUniform1f(dLoc, mDrawDepth);
            }

            var vLoc = mProgram.GetUniformLocation(GlShaderUniform.ViewMatrix);
            if (vLoc >= 0)
            {
                var viewMatrix = mUseViewMatrix ? mViewMatrix : TvgMath.Identity();
                var viewMat3 = stackalloc float[9];
                GlMatrixHelper.GetMatrix3(viewMatrix, new Span<float>(viewMat3, 9));
                GL.glUniformMatrix3fv(vLoc, 1, (byte)GL.GL_FALSE, viewMat3);
            }

            // setup scissor rect
            state.Scissor(mViewport.Sx(), mViewport.Sy(), mViewport.Sw(), mViewport.Sh());

            state.BeginVertexLayout();
            bool hasVertexColorLayout = false;
            uint defaultArrayBuffer = 0;
            for (uint i = 0; i < mVertexLayout.count; ++i)
            {
                ref var layout = ref mVertexLayout[i];
                uint source = layout.arrayBufferId;
                if (source == 0)
                {
                    // Compatibility with callers constructing the older public
                    // layout type; every internal layout supplies its source VBO.
                    if (defaultArrayBuffer == 0) { int current; GL.glGetIntegerv(GL.GL_ARRAY_BUFFER_BINDING, &current); defaultArrayBuffer = (uint)current; }
                    source = defaultArrayBuffer;
                }
                if (layout.index == 1) hasVertexColorLayout = true;
                state.SetVertexAttribPointer(layout.index, (int)layout.size, layout.type, layout.normalized,
                    (int)layout.stride, layout.offset, source);
            }
            if (mUseVertexColor && !hasVertexColorLayout)
                state.SetVertexAttrib4f(1, mVertexColor0, mVertexColor1, mVertexColor2, mVertexColor3);
            state.EndVertexLayout();

            // binding uniforms
            for (uint i = 0; i < mBindingResources.count; i++)
            {
                ref var binding = ref mBindingResources[i];
                if (binding.type == GlBindingType.kTexture)
                {
                    state.BindTexture2D(GL.GL_TEXTURE0 + binding.bindPoint, binding.resourceId);

                    mProgram.SetSampler(binding.location, (int)binding.bindPoint);
                }
                else if (binding.type == GlBindingType.kUniformBuffer)
                {
                    GL.glBindBufferRange(GL.GL_UNIFORM_BUFFER, binding.bindPoint, binding.resourceId,
                        (nint)binding.bufferOffset, (nint)binding.bufferRange);
                }
            }

            if (mUseDrawArrays) GL.glDrawArrays(mArrayMode, (int)mArrayOffset, (int)mIndexCount);
            else GL.glDrawElements(GL.GL_TRIANGLES, (int)mIndexCount, GL.GL_UNSIGNED_INT, (void*)(nuint)mIndexOffset);


        }

        public void AddVertexLayout(in GlVertexLayout layout)
        {
            mVertexLayout.Push(layout);
        }

        public void SetVertexColor(float r, float g, float b, float a)
        {
            mUseVertexColor = true;
            mVertexColor0 = r;
            mVertexColor1 = g;
            mVertexColor2 = b;
            mVertexColor3 = a;
        }

        public void AddBindResource(in GlBindingResource binding)
        {
            if (binding.type == GlBindingType.kUniformBuffer &&
                !mProgram!.SetUniformBlockBinding(binding.location, binding.bindPoint)) return;
            mBindingResources.Push(binding);
        }

        public void SetDrawRange(uint offset, uint count)
        {
            mUseDrawArrays = false;
            mIndexOffset = offset;
            mIndexCount = count;
        }

        public void SetViewport(in RenderRegion viewport)
        {
            mViewport = viewport;
            if (mViewport.max.x < mViewport.min.x) mViewport.max.x = mViewport.min.x;
            if (mViewport.max.y < mViewport.min.y) mViewport.max.y = mViewport.min.y;
        }

        public void SetDrawDepth(int depth) { mDrawDepth = (float)depth; }
        public void SetViewMatrix(in Matrix matrix) { mViewMatrix = matrix; mUseViewMatrix = true; }
        public virtual void NormalizeDrawDepth(int maxDepth) { mDrawDepth /= (float)maxDepth; }

        public GlProgram? GetProgram() { return mProgram; }
        public ref RenderRegion GetViewport() => ref mViewport;
        public float GetDrawDepth() { return mDrawDepth; }
        public ref Array<GlVertexLayout> GetVertexLayout() => ref mVertexLayout;
        public uint GetIndexOffset() { return mIndexOffset; }
        public uint GetIndexCount() { return mIndexCount; }
    }

    /************************************************************************/
    /* GlStencilCoverTask                                                   */
    /************************************************************************/

    public unsafe class GlStencilCoverTask : GlRenderTask
    {
        internal List<GlRenderTask> mStencilTasks = new List<GlRenderTask>();
        internal List<GlRenderTask> mCoverTasks = new List<GlRenderTask>();
        internal GlStencilMode mStencilMode;

        public GlStencilCoverTask(GlRenderTask stencil, GlRenderTask cover, GlStencilMode mode)
            : base(null)
        {
            mStencilTasks.Add(stencil);
            mCoverTasks.Add(cover);
            mStencilMode = mode;
        }

        protected override void DisposeOwnedTasks()
        {
            foreach (var task in mStencilTasks) task.Dispose();
            foreach (var task in mCoverTasks) task.Dispose();
            mStencilTasks.Clear();
            mCoverTasks.Clear();
        }

        public override void Run(GlStateCache state)
        {
            state.Enable(GL.GL_STENCIL_TEST);

            if (mStencilMode == GlStencilMode.Stroke)
            {
                state.StencilFunc(GL.GL_NOTEQUAL, 0x1, 0xFF);
                state.StencilOp(GL.GL_KEEP, GL.GL_KEEP, GL.GL_REPLACE);
            }
            else
            {
                state.StencilFuncSeparate(GL.GL_FRONT, GL.GL_ALWAYS, 0x0, 0xFF);
                state.StencilOpSeparate(GL.GL_FRONT, GL.GL_KEEP, GL.GL_KEEP, GL.GL_INCR_WRAP);

                state.StencilFuncSeparate(GL.GL_BACK, GL.GL_ALWAYS, 0x0, 0xFF);
                state.StencilOpSeparate(GL.GL_BACK, GL.GL_KEEP, GL.GL_KEEP, GL.GL_DECR_WRAP);
            }
            state.ColorMask(0, 0, 0, 0);

            for (int i = 0; i < mStencilTasks.Count; ++i) mStencilTasks[i].Run(state);

            if (mStencilMode == GlStencilMode.FillEvenOdd)
            {
                state.StencilFunc(GL.GL_NOTEQUAL, 0x00, 0x01);
                state.StencilOp(GL.GL_REPLACE, GL.GL_KEEP, GL.GL_REPLACE);
            }
            else
            {
                state.StencilFunc(GL.GL_NOTEQUAL, 0x0, 0xFF);
                state.StencilOp(GL.GL_KEEP, GL.GL_KEEP, GL.GL_REPLACE);
            }

            state.ColorMask(1, 1, 1, 1);

            for (int i = 0; i < mCoverTasks.Count; ++i) mCoverTasks[i].Run(state);

            state.Disable(GL.GL_STENCIL_TEST);
        }

        public override void NormalizeDrawDepth(int maxDepth)
        {
            for (int i = 0; i < mCoverTasks.Count; ++i) mCoverTasks[i].NormalizeDrawDepth(maxDepth);
            for (int i = 0; i < mStencilTasks.Count; ++i) mStencilTasks[i].NormalizeDrawDepth(maxDepth);
        }
    }

    /************************************************************************/
    /* GlComposeTask                                                        */
    /************************************************************************/

    public unsafe class GlComposeTask : GlRenderTask
    {
        private uint mTargetFbo;
        private GlRenderTarget? mFbo;
        private List<GlRenderTask> mTasks = new List<GlRenderTask>();
        private uint mRenderWidth;
        private uint mRenderHeight;

        public bool mClearBuffer = true;

        public GlComposeTask(GlProgram? program, uint target, GlRenderTarget? fbo, List<GlRenderTask> tasks)
            : base(program)
        {
            mTargetFbo = target;
            mFbo = fbo;
            mTasks.AddRange(tasks);
            tasks.Clear();
        }

        protected override void DisposeOwnedTasks()
        {
            foreach (var task in mTasks) task.Dispose();
            mTasks.Clear();
        }

        public override void Run(GlStateCache state)
        {
            state.BindFramebuffer(GL.GL_FRAMEBUFFER, GetSelfFbo());

            // we must clear all area of fbo
            state.Viewport(0, 0, (int)mFbo!.width, (int)mFbo.height);
            state.Scissor(0, 0, (int)mFbo.width, (int)mFbo.height);
            state.ClearColor(0, 0, 0, 0);
            state.ClearStencil(0);
            state.ClearDepth(0.0);
            state.DepthMask(1);

            GL.glClear(GL.GL_COLOR_BUFFER_BIT | GL.GL_STENCIL_BUFFER_BIT | GL.GL_DEPTH_BUFFER_BIT);
            state.DepthMask(0);

            state.Viewport(0, 0, (int)mRenderWidth, (int)mRenderHeight);
            state.Scissor(0, 0, (int)mRenderWidth, (int)mRenderHeight);

            for (int i = 0; i < mTasks.Count; i++)
            {
                mTasks[i].Run(state);
            }

#if THORVG_GL_TARGET_GLES
            uint* attachments = stackalloc uint[2] { GL.GL_STENCIL_ATTACHMENT, GL.GL_DEPTH_ATTACHMENT };
            GL.glInvalidateFramebuffer(GL.GL_FRAMEBUFFER, 2, attachments);
#endif
            // reset scissor box
            state.Scissor(0, 0, (int)mFbo.width, (int)mFbo.height);
            OnResolve(state);
        }

        public void SetRenderSize(uint width, uint height) { mRenderWidth = width; mRenderHeight = height; }

        protected uint GetTargetFbo() { return mTargetFbo; }
        protected uint GetSelfFbo() { return mFbo!.fbo; }
        protected uint GetResolveFboId() { return mFbo!.resolvedFbo; }

        protected void OnResolve(GlStateCache state)
        {
            state.BindFramebuffer(GL.GL_READ_FRAMEBUFFER, GetSelfFbo());
            state.BindFramebuffer(GL.GL_DRAW_FRAMEBUFFER, GetResolveFboId());
            GL.glBlitFramebuffer(0, 0, (int)mRenderWidth, (int)mRenderHeight, 0, 0, (int)mRenderWidth, (int)mRenderHeight, GL.GL_COLOR_BUFFER_BIT, GL.GL_NEAREST);
        }

        protected GlRenderTarget? GetFboTarget() { return mFbo; }
    }

    /************************************************************************/
    /* GlBlitTask                                                           */
    /************************************************************************/

    public unsafe class GlBlitTask : GlComposeTask
    {
        private uint mColorTex;
        private RenderRegion mTargetViewport;

        public GlBlitTask(GlProgram? program, uint target, GlRenderTarget? fbo, List<GlRenderTask> tasks)
            : base(program, target, fbo, tasks)
        {
            mColorTex = fbo?.colorTex ?? 0;
        }

        public override void Run(GlStateCache state)
        {
            base.Run(state);

            state.BindFramebuffer(GL.GL_FRAMEBUFFER, GetTargetFbo());
            state.Viewport(mTargetViewport.Sx(), mTargetViewport.Sy(), mTargetViewport.Sw(), mTargetViewport.Sh());

            if (mClearBuffer)
            {
                state.ClearColor(0, 0, 0, 0);
                GL.glClear(GL.GL_COLOR_BUFFER_BIT);
            }

            state.Disable(GL.GL_DEPTH_TEST);
            // make sure the blending is correct
            state.Enable(GL.GL_BLEND);
            state.BlendFunc(GL.GL_ONE, GL.GL_ONE_MINUS_SRC_ALPHA);

            RunDraw(state); // calls GlRenderTask's draw logic directly
        }

        public uint GetColorTexture() { return mColorTex; }
        public void SetTargetViewport(in RenderRegion vp) { mTargetViewport = vp; }
    }

    /************************************************************************/
    /* GlDrawBlitTask                                                       */
    /************************************************************************/

    public unsafe class GlDrawBlitTask : GlComposeTask
    {
        private GlRenderTask? mPrevTask;
        private uint mParentWidth;
        private uint mParentHeight;

        public GlDrawBlitTask(GlProgram? program, uint target, GlRenderTarget? fbo, List<GlRenderTask> tasks)
            : base(program, target, fbo, tasks)
        {
        }

        public void SetPrevTask(GlRenderTask? task) { mPrevTask = task; }
        public void SetParentSize(uint width, uint height) { mParentWidth = width; mParentHeight = height; }

        protected override void DisposeOwnedTasks()
        {
            mPrevTask?.Dispose();
            mPrevTask = null;
            base.DisposeOwnedTasks();
        }

        public override void Run(GlStateCache state)
        {
            mPrevTask?.Run(state);

            base.Run(state);

            state.BindFramebuffer(GL.GL_FRAMEBUFFER, GetTargetFbo());

            state.Viewport(0, 0, (int)mParentWidth, (int)mParentHeight);
            state.Scissor(0, 0, (int)mParentWidth, (int)mParentHeight);
            // Call GlRenderTask's draw logic directly, bypassing GlComposeTask.Run(state)
            RunDraw(state);
        }
    }

    /************************************************************************/
    /* GlSceneBlendTask                                                     */
    /************************************************************************/

    public unsafe class GlSceneBlendTask : GlComposeTask
    {
        private GlRenderTarget? mSrcFbo;
        private GlRenderTarget? mDstCopyFbo;
        private uint mParentWidth;
        private uint mParentHeight;

        public GlSceneBlendTask(GlProgram? program, uint target, GlRenderTarget? fbo, List<GlRenderTask> tasks)
            : base(program, target, fbo, tasks)
        {
        }

        public void SetParentSize(uint width, uint height) { mParentWidth = width; mParentHeight = height; }
        public void SetSrcTarget(GlRenderTarget? srcFbo) { mSrcFbo = srcFbo; }
        public void SetDstCopy(GlRenderTarget? dstCopyFbo) { mDstCopyFbo = dstCopyFbo; }

        public override void Run(GlStateCache state)
        {
            base.Run(state);

            ref var vp = ref GetViewport();
            var width = mSrcFbo!.width;
            var height = mSrcFbo.height;
            if (width <= 0 || height <= 0) return;

#if THORVG_GL_TARGET_GLES
            state.BindFramebuffer(GL.GL_DRAW_FRAMEBUFFER, mDstCopyFbo!.resolvedFbo);
            if (vp.min.x != 0 || vp.min.y != 0 || mDstCopyFbo.width != (uint)vp.Sw() || mDstCopyFbo.height != (uint)vp.Sh())
                ClearColorTarget(state, mDstCopyFbo.width, mDstCopyFbo.height);
            state.BindFramebuffer(GL.GL_READ_FRAMEBUFFER, mSrcFbo.fbo);
            state.Viewport(0, 0, (int)width, (int)height);
            state.Scissor(vp.min.x, vp.min.y, vp.Sw(), vp.Sh());
            GL.glBlitFramebuffer(vp.min.x, vp.min.y, vp.max.x, vp.max.y, vp.min.x, vp.min.y, vp.max.x, vp.max.y, GL.GL_COLOR_BUFFER_BIT, GL.GL_NEAREST);
#else
            state.BindFramebuffer(GL.GL_READ_FRAMEBUFFER, GetTargetFbo());
            state.BindFramebuffer(GL.GL_DRAW_FRAMEBUFFER, mDstCopyFbo!.resolvedFbo);
            state.Viewport(0, 0, (int)mDstCopyFbo.width, (int)mDstCopyFbo.height);
            state.Scissor(0, 0, (int)mDstCopyFbo.width, (int)mDstCopyFbo.height);
            GL.glBlitFramebuffer(vp.min.x, vp.min.y, vp.max.x, vp.max.y, 0, 0, vp.Sw(), vp.Sh(), GL.GL_COLOR_BUFFER_BIT, GL.GL_LINEAR);
#endif

            state.BindFramebuffer(GL.GL_FRAMEBUFFER, GetTargetFbo());
            state.Viewport(0, 0, (int)mParentWidth, (int)mParentHeight);
            state.Scissor(0, 0, (int)mParentWidth, (int)mParentHeight);

            state.Disable(GL.GL_DEPTH_TEST);
            state.BlendFunc(GL.GL_ONE, GL.GL_ZERO);
            RunDraw(state); // calls GlRenderTask's draw logic directly, bypassing GlComposeTask.Run(state)
            state.BlendFunc(GL.GL_ONE, GL.GL_ONE_MINUS_SRC_ALPHA);
            state.Enable(GL.GL_DEPTH_TEST);
        }
    }

    /************************************************************************/
    /* GlClipTask                                                           */
    /************************************************************************/

    public unsafe class GlClipTask : GlRenderTask
    {
        private GlRenderTask mClipTask;
        private GlRenderTask mMaskTask;

        public GlClipTask(GlRenderTask clip, GlRenderTask mask)
            : base(null)
        {
            mClipTask = clip;
            mMaskTask = mask;
        }

        protected override void DisposeOwnedTasks()
        {
            mClipTask?.Dispose();
            mMaskTask?.Dispose();
            mClipTask = mMaskTask = null!;
        }

        public override void Run(GlStateCache state)
        {
            state.Enable(GL.GL_STENCIL_TEST);
            state.ColorMask(0, 0, 0, 0);
            // draw clip path as normal stencil mask
            state.StencilFuncSeparate(GL.GL_FRONT, GL.GL_ALWAYS, 0x1, 0xFF);
            state.StencilOpSeparate(GL.GL_FRONT, GL.GL_KEEP, GL.GL_KEEP, GL.GL_INCR_WRAP);

            state.StencilFuncSeparate(GL.GL_BACK, GL.GL_ALWAYS, 0x1, 0xFF);
            state.StencilOpSeparate(GL.GL_BACK, GL.GL_KEEP, GL.GL_KEEP, GL.GL_DECR_WRAP);

            mClipTask.Run(state);

            // draw clip mask
            state.DepthMask(1);
            state.StencilFunc(GL.GL_EQUAL, 0x0, 0xFF);
            state.StencilOp(GL.GL_REPLACE, GL.GL_KEEP, GL.GL_REPLACE);

            mMaskTask.Run(state);

            state.ColorMask(1, 1, 1, 1);
            state.DepthMask(0);
            state.Disable(GL.GL_STENCIL_TEST);
        }

        public override void NormalizeDrawDepth(int maxDepth)
        {
            mClipTask.NormalizeDrawDepth(maxDepth);
            mMaskTask.NormalizeDrawDepth(maxDepth);
        }
    }

    /************************************************************************/
    /* GlDirectBlendTask                                                    */
    /************************************************************************/

    public unsafe class GlDirectBlendTask : GlRenderTask
    {
        private GlRenderTarget? mDstFbo;
        private GlRenderTarget? mDstCopyFbo;
        private RenderRegion mCopyRegion;

        public GlDirectBlendTask(GlProgram? program, GlRenderTarget? dstFbo, GlRenderTarget? dstCopyFbo, in RenderRegion copyRegion)
            : base(program)
        {
            mDstFbo = dstFbo;
            mDstCopyFbo = dstCopyFbo;
            mCopyRegion = copyRegion;
        }

        public override void Run(GlStateCache state)
        {
            var width = mCopyRegion.Sw();
            var height = mCopyRegion.Sh();
            if (width <= 0 || height <= 0) return;
            var x = mCopyRegion.Sx();
            var y = mCopyRegion.Sy();
            var fboW = (int)mDstFbo!.width;
            var fboH = (int)mDstFbo.height;
            if (fboW <= 0 || fboH <= 0) return;

            state.BindFramebuffer(GL.GL_READ_FRAMEBUFFER, mDstFbo.fbo);
            state.BindFramebuffer(GL.GL_DRAW_FRAMEBUFFER, mDstCopyFbo!.resolvedFbo);

#if THORVG_GL_TARGET_GLES
            if (x != 0 || y != 0 || mDstCopyFbo.width != (uint)width || mDstCopyFbo.height != (uint)height)
                ClearColorTarget(state, mDstCopyFbo.width, mDstCopyFbo.height);
            state.Viewport(0, 0, fboW, fboH);
            state.Scissor(x, y, width, height);
            GL.glBlitFramebuffer(x, y, x + width, y + height, x, y, x + width, y + height, GL.GL_COLOR_BUFFER_BIT, GL.GL_NEAREST);
#else
            state.Viewport(0, 0, width, height);
            state.Scissor(0, 0, width, height);
            GL.glBlitFramebuffer(x, y, x + width, y + height, 0, 0, width, height, GL.GL_COLOR_BUFFER_BIT, GL.GL_LINEAR);
#endif

            state.BindFramebuffer(GL.GL_FRAMEBUFFER, mDstFbo.fbo);
            var dstVp = mDstFbo.viewport;
            state.Viewport(0, 0, dstVp.Sw(), dstVp.Sh());

            state.BlendFunc(GL.GL_ONE, GL.GL_ZERO);
            base.Run(state);
            state.BlendFunc(GL.GL_ONE, GL.GL_ONE_MINUS_SRC_ALPHA);
        }
    }

    /************************************************************************/
    /* GlComplexBlendTask                                                   */
    /************************************************************************/

    public unsafe class GlComplexBlendTask : GlRenderTask
    {
        private GlRenderTarget? mDstFbo;
        private GlRenderTarget? mDstCopyFbo;
        private GlRenderTask mStencilTask;
        private GlComposeTask mComposeTask;

        public GlComplexBlendTask(GlProgram? program, GlRenderTarget? dstFbo, GlRenderTarget? dstCopyFbo, GlRenderTask stencilTask, GlComposeTask composeTask)
            : base(program)
        {
            mDstFbo = dstFbo;
            mDstCopyFbo = dstCopyFbo;
            mStencilTask = stencilTask;
            mComposeTask = composeTask;
        }

        protected override void DisposeOwnedTasks()
        {
            mStencilTask?.Dispose();
            mComposeTask?.Dispose();
            mStencilTask = null!;
            mComposeTask = null!;
        }

        public override void Run(GlStateCache state)
        {
            mComposeTask.Run(state);

            ref var vp = ref GetViewport();
            var width = mDstFbo!.width;
            var height = mDstFbo.height;
            if (width <= 0 || height <= 0) return;

            state.BindFramebuffer(GL.GL_READ_FRAMEBUFFER, mDstFbo.fbo);
            state.BindFramebuffer(GL.GL_DRAW_FRAMEBUFFER, mDstCopyFbo!.resolvedFbo);

#if THORVG_GL_TARGET_GLES
            if (vp.min.x != 0 || vp.min.y != 0 || mDstCopyFbo.width != (uint)vp.Sw() || mDstCopyFbo.height != (uint)vp.Sh())
                ClearColorTarget(state, mDstCopyFbo.width, mDstCopyFbo.height);
            state.Viewport(0, 0, (int)width, (int)height);
            state.Scissor(vp.min.x, vp.min.y, vp.Sw(), vp.Sh());
            GL.glBlitFramebuffer(vp.min.x, vp.min.y, vp.max.x, vp.max.y, vp.min.x, vp.min.y, vp.max.x, vp.max.y, GL.GL_COLOR_BUFFER_BIT, GL.GL_NEAREST);
#else
            var dstVp = mDstFbo.viewport;
            state.Viewport(0, 0, dstVp.Sw(), dstVp.Sh());
            state.Scissor(0, 0, dstVp.Sw(), dstVp.Sh());
            GL.glBlitFramebuffer(vp.min.x, vp.min.y, vp.max.x, vp.max.y, 0, 0, vp.Sw(), vp.Sh(), GL.GL_COLOR_BUFFER_BIT, GL.GL_LINEAR);
#endif

            state.BindFramebuffer(GL.GL_FRAMEBUFFER, mDstFbo.fbo);

            state.Enable(GL.GL_STENCIL_TEST);
            state.ColorMask(0, 0, 0, 0);
            state.StencilFuncSeparate(GL.GL_FRONT, GL.GL_ALWAYS, 0x0, 0xFF);
            state.StencilOpSeparate(GL.GL_FRONT, GL.GL_KEEP, GL.GL_KEEP, GL.GL_INCR_WRAP);

            state.StencilFuncSeparate(GL.GL_BACK, GL.GL_ALWAYS, 0x0, 0xFF);
            state.StencilOpSeparate(GL.GL_BACK, GL.GL_KEEP, GL.GL_KEEP, GL.GL_DECR_WRAP);

            mStencilTask.Run(state);

            state.ColorMask(1, 1, 1, 1);
            state.StencilFunc(GL.GL_NOTEQUAL, 0x0, 0xFF);
            state.StencilOp(GL.GL_REPLACE, GL.GL_KEEP, GL.GL_REPLACE);

            state.BlendFunc(GL.GL_ONE, GL.GL_ZERO);

            base.Run(state);

            state.Disable(GL.GL_STENCIL_TEST);
            state.BlendFunc(GL.GL_ONE, GL.GL_ONE_MINUS_SRC_ALPHA);
        }

        public override void NormalizeDrawDepth(int maxDepth)
        {
            mStencilTask.NormalizeDrawDepth(maxDepth);
            base.NormalizeDrawDepth(maxDepth);
        }
    }

    /************************************************************************/
    /* GlGaussianBlurTask                                                   */
    /************************************************************************/

    public unsafe class GlGaussianBlurTask : GlRenderTask
    {
        public GlRenderTask? horzTask;
        public GlRenderTask? vertTask;
        public RenderEffectGaussianBlur? effect;

        private GlRenderTarget? mDstFbo;
        private GlRenderTarget? mDstCopyFbo0;
        private GlRenderTarget? mDstCopyFbo1;

        public GlGaussianBlurTask(GlRenderTarget? dstFbo, GlRenderTarget? dstCopyFbo0, GlRenderTarget? dstCopyFbo1)
            : base(null)
        {
            mDstFbo = dstFbo;
            mDstCopyFbo0 = dstCopyFbo0;
            mDstCopyFbo1 = dstCopyFbo1;
        }

        protected override void DisposeOwnedTasks()
        {
            horzTask?.Dispose();
            vertTask?.Dispose();
            horzTask = vertTask = null;
        }

        public override void Run(GlStateCache state)
        {
            ref var vp = ref GetViewport();
            var width = (int)mDstFbo!.width;
            var height = (int)mDstFbo.height;

            // get target handles
            var dstCopyTexId0 = mDstCopyFbo0!.colorTex;
            var dstCopyTexId1 = mDstCopyFbo1!.colorTex;
            // get program properties
            var programHorz = horzTask!.GetProgram()!;
            var programVert = vertTask!.GetProgram()!;
            var horzSrcTextureLoc = programHorz.GetUniformLocation(GlShaderUniform.SourceTexture);
            var vertSrcTextureLoc = programVert.GetUniformLocation(GlShaderUniform.SourceTexture);

            state.Viewport(0, 0, width, height);
            state.Scissor(0, 0, width, height);
            // make a full copy of dst to intermediate buffers
            state.BindFramebuffer(GL.GL_READ_FRAMEBUFFER, mDstFbo.fbo);
            state.BindFramebuffer(GL.GL_DRAW_FRAMEBUFFER, mDstCopyFbo0.resolvedFbo);
            GL.glBlitFramebuffer(0, 0, width, height, 0, 0, width, height, GL.GL_COLOR_BUFFER_BIT, GL.GL_NEAREST);
            state.BindFramebuffer(GL.GL_FRAMEBUFFER, mDstFbo.fbo);

            state.Disable(GL.GL_BLEND);
            state.DepthFunc(GL.GL_ALWAYS);
            if (effect!.direction == 0)
            {
                state.BindFramebuffer(GL.GL_READ_FRAMEBUFFER, mDstFbo.fbo);
                state.BindFramebuffer(GL.GL_DRAW_FRAMEBUFFER, mDstCopyFbo1.resolvedFbo);
                GL.glBlitFramebuffer(0, 0, width, height, 0, 0, width, height, GL.GL_COLOR_BUFFER_BIT, GL.GL_NEAREST);
                // horizontal blur
                state.BindFramebuffer(GL.GL_FRAMEBUFFER, mDstCopyFbo1.resolvedFbo);
                horzTask.SetViewport(vp);
                horzTask.AddBindResource(new GlBindingResource(0, dstCopyTexId0, horzSrcTextureLoc));
                horzTask.Run(state);
                // vertical blur
                state.BindFramebuffer(GL.GL_FRAMEBUFFER, mDstFbo.fbo);
                vertTask.SetViewport(vp);
                vertTask.AddBindResource(new GlBindingResource(0, dstCopyTexId1, vertSrcTextureLoc));
                vertTask.Run(state);
            }
            else if (effect.direction == 1) // horizontal
            {
                horzTask.SetViewport(vp);
                horzTask.AddBindResource(new GlBindingResource(0, dstCopyTexId0, horzSrcTextureLoc));
                horzTask.Run(state);
            }
            else if (effect.direction == 2) // vertical
            {
                vertTask.SetViewport(vp);
                vertTask.AddBindResource(new GlBindingResource(0, dstCopyTexId0, vertSrcTextureLoc));
                vertTask.Run(state);
            }
            state.DepthFunc(GL.GL_GREATER);
            state.Enable(GL.GL_BLEND);
        }
    }

    /************************************************************************/
    /* GlEffectDropShadowTask                                               */
    /************************************************************************/

    public unsafe class GlEffectDropShadowTask : GlRenderTask
    {
        public GlRenderTask? horzTask;
        public GlRenderTask? vertTask;
        public RenderEffectDropShadow? effect;

        private GlRenderTarget? mDstFbo;
        private GlRenderTarget? mDstCopyFbo0;
        private GlRenderTarget? mDstCopyFbo1;

        public GlEffectDropShadowTask(GlProgram? program, GlRenderTarget? dstFbo, GlRenderTarget? dstCopyFbo0, GlRenderTarget? dstCopyFbo1)
            : base(program)
        {
            mDstFbo = dstFbo;
            mDstCopyFbo0 = dstCopyFbo0;
            mDstCopyFbo1 = dstCopyFbo1;
        }

        protected override void DisposeOwnedTasks()
        {
            horzTask?.Dispose();
            vertTask?.Dispose();
            horzTask = vertTask = null;
        }

        public override void Run(GlStateCache state)
        {
            ref var vp = ref GetViewport();
            var width = (int)mDstFbo!.width;
            var height = (int)mDstFbo.height;

            // get target handles
            var dstCopyTexId0 = mDstCopyFbo0!.colorTex;
            var dstCopyTexId1 = mDstCopyFbo1!.colorTex;
            // get program properties
            var programHorz = horzTask!.GetProgram()!;
            var programVert = vertTask!.GetProgram()!;
            var horzSrcTextureLoc = programHorz.GetUniformLocation(GlShaderUniform.SourceTexture);
            var vertSrcTextureLoc = programVert.GetUniformLocation(GlShaderUniform.SourceTexture);

            var srcTextureLoc = GetProgram()!.GetUniformLocation(GlShaderUniform.SourceTexture);
            var blrTextureLoc = GetProgram()!.GetUniformLocation(GlShaderUniform.BlurTexture);
            AddBindResource(new GlBindingResource(0, dstCopyTexId0, srcTextureLoc));
            AddBindResource(new GlBindingResource(1, dstCopyTexId1, blrTextureLoc));

            state.Viewport(0, 0, width, height);
            state.Scissor(0, 0, width, height);

            // make full copies of dst to intermediate buffers
            state.BindFramebuffer(GL.GL_READ_FRAMEBUFFER, mDstFbo.fbo);
            state.BindFramebuffer(GL.GL_DRAW_FRAMEBUFFER, mDstCopyFbo0.resolvedFbo);
            GL.glBlitFramebuffer(0, 0, width, height, 0, 0, width, height, GL.GL_COLOR_BUFFER_BIT, GL.GL_NEAREST);
            state.BindFramebuffer(GL.GL_READ_FRAMEBUFFER, mDstFbo.fbo);
            state.BindFramebuffer(GL.GL_DRAW_FRAMEBUFFER, mDstCopyFbo1.resolvedFbo);
            GL.glBlitFramebuffer(0, 0, width, height, 0, 0, width, height, GL.GL_COLOR_BUFFER_BIT, GL.GL_NEAREST);

            state.Disable(GL.GL_BLEND);
            state.DepthFunc(GL.GL_ALWAYS);
            // when sigma is 0, no blur is applied
            if (!TvgMath.Zero(effect!.sigma))
            {
                // horizontal blur
                state.BindFramebuffer(GL.GL_FRAMEBUFFER, mDstCopyFbo0.resolvedFbo);
                horzTask.SetViewport(vp);
                horzTask.AddBindResource(new GlBindingResource(0, dstCopyTexId1, horzSrcTextureLoc));
                horzTask.Run(state);
                // vertical blur
                state.BindFramebuffer(GL.GL_FRAMEBUFFER, mDstCopyFbo1.resolvedFbo);
                vertTask.SetViewport(vp);
                vertTask.AddBindResource(new GlBindingResource(0, dstCopyTexId0, vertSrcTextureLoc));
                vertTask.Run(state);
                // copy original image to intermediate buffer
                state.BindFramebuffer(GL.GL_READ_FRAMEBUFFER, mDstFbo.fbo);
                state.BindFramebuffer(GL.GL_DRAW_FRAMEBUFFER, mDstCopyFbo0.resolvedFbo);
                GL.glBlitFramebuffer(0, 0, width, height, 0, 0, width, height, GL.GL_COLOR_BUFFER_BIT, GL.GL_NEAREST);
            }
            // run drop shadow effect
            state.BindFramebuffer(GL.GL_FRAMEBUFFER, mDstFbo.fbo);
            base.Run(state);
            state.DepthFunc(GL.GL_GREATER);
            state.Enable(GL.GL_BLEND);
        }
    }

    /************************************************************************/
    /* GlEffectColorTransformTask                                           */
    /************************************************************************/

    public unsafe class GlEffectColorTransformTask : GlRenderTask
    {
        private GlRenderTarget? mDstFbo;
        private GlRenderTarget? mDstCopyFbo;

        public GlEffectColorTransformTask(GlProgram? program, GlRenderTarget? dstFbo, GlRenderTarget? dstCopyFbo)
            : base(program)
        {
            mDstFbo = dstFbo;
            mDstCopyFbo = dstCopyFbo;
        }

        public override void Run(GlStateCache state)
        {
            var width = (int)mDstFbo!.width;
            var height = (int)mDstFbo.height;
            // get target handles and pass to shader
            var dstCopyTexId = mDstCopyFbo!.colorTex;
            var srcTextureLoc = GetProgram()!.GetUniformLocation(GlShaderUniform.SourceTexture);
            AddBindResource(new GlBindingResource(0, dstCopyTexId, srcTextureLoc));

            state.Viewport(0, 0, width, height);
            state.Scissor(0, 0, width, height);
            // make full copy of dst to intermediate buffer
            state.BindFramebuffer(GL.GL_READ_FRAMEBUFFER, mDstFbo.fbo);
            state.BindFramebuffer(GL.GL_DRAW_FRAMEBUFFER, mDstCopyFbo.resolvedFbo);
            GL.glBlitFramebuffer(0, 0, width, height, 0, 0, width, height, GL.GL_COLOR_BUFFER_BIT, GL.GL_NEAREST);
            state.BindFramebuffer(GL.GL_FRAMEBUFFER, mDstFbo.fbo);

            // run transform
            state.Disable(GL.GL_BLEND);
            state.DepthFunc(GL.GL_ALWAYS);
            base.Run(state);
            state.DepthFunc(GL.GL_GREATER);
            state.Enable(GL.GL_BLEND);
        }
    }
}
