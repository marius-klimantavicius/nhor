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
        public uint gBufferId;
        public uint bufferOffset;
        public uint bufferRange;

        // Uniform buffer constructor
        public GlBindingResource(uint index, int location, uint bufferId, uint offset, uint range)
        {
            type = GlBindingType.kUniformBuffer;
            bindPoint = index;
            this.location = location;
            gBufferId = bufferId;
            bufferOffset = offset;
            bufferRange = range;
        }

        // Texture constructor
        public GlBindingResource(uint bindPoint, uint texId, int location)
        {
            type = GlBindingType.kTexture;
            this.bindPoint = bindPoint;
            this.location = location;
            gBufferId = texId;
            bufferOffset = 0;
            bufferRange = 0;
        }
    }

    /************************************************************************/
    /* GlRenderTask                                                         */
    /************************************************************************/

    public unsafe class GlRenderTask
    {
        private GlProgram? mProgram;
        private RenderRegion mViewport;
        private uint mIndexOffset;
        private uint mIndexCount;
        private Array<GlVertexLayout> mVertexLayout;
        private Array<GlBindingResource> mBindingResources;
        private float mDrawDepth;
        private Matrix mViewMatrix;
        private bool mUseViewMatrix;

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
            RunDraw();
        }

        /// <summary>
        /// Non-virtual draw method that executes the GL draw calls.
        /// Subclasses that need to call the base GlRenderTask draw logic
        /// (bypassing intermediate overrides) can call this directly.
        /// </summary>
        protected void RunDraw()
        {
            // bind shader
            mProgram!.Load();

            var dLoc = mProgram.GetUniformLocation("uDepth\0"u8);
            if (dLoc >= 0)
            {
                GL.glUniform1f(dLoc, mDrawDepth);
            }

            var vLoc = mProgram.GetUniformLocation("uViewMatrix\0"u8);
            if (vLoc >= 0)
            {
                var viewMatrix = mUseViewMatrix ? mViewMatrix : TvgMath.Identity();
                var viewMat3 = stackalloc float[9];
                GlMatrixHelper.GetMatrix3(viewMatrix, new Span<float>(viewMat3, 9));
                GL.glUniformMatrix3fv(vLoc, 1, (byte)GL.GL_FALSE, viewMat3);
            }

            // setup scissor rect
            GL.glScissor(mViewport.Sx(), mViewport.Sy(), mViewport.Sw(), mViewport.Sh());

            // setup attribute layout
            for (uint i = 0; i < mVertexLayout.count; i++)
            {
                ref var layout = ref mVertexLayout[i];
                GL.glEnableVertexAttribArray(layout.index);
                GL.glVertexAttribPointer(layout.index, (int)layout.size, GL.GL_FLOAT,
                    (byte)GL.GL_FALSE, (int)layout.stride,
                    (void*)(nuint)layout.offset);
            }

            // binding uniforms
            for (uint i = 0; i < mBindingResources.count; i++)
            {
                ref var binding = ref mBindingResources[i];
                if (binding.type == GlBindingType.kTexture)
                {
                    GL.glActiveTexture(GL.GL_TEXTURE0 + binding.bindPoint);
                    GL.glBindTexture(GL.GL_TEXTURE_2D, binding.gBufferId);

                    var bp = (int)binding.bindPoint;
                    mProgram.SetUniform1Value(binding.location, 1, &bp);
                }
                else if (binding.type == GlBindingType.kUniformBuffer)
                {
                    GL.glUniformBlockBinding(mProgram.GetProgramId(), (uint)binding.location, binding.bindPoint);
                    GL.glBindBufferRange(GL.GL_UNIFORM_BUFFER, binding.bindPoint, binding.gBufferId,
                        (int)binding.bufferOffset, (int)binding.bufferRange);
                }
            }

            GL.glDrawElements(GL.GL_TRIANGLES, (int)mIndexCount, GL.GL_UNSIGNED_INT, (void*)(nuint)mIndexOffset);

            // disable attribute layout
            for (uint i = 0; i < mVertexLayout.count; i++)
            {
                ref var layout = ref mVertexLayout[i];
                GL.glDisableVertexAttribArray(layout.index);
            }
        }

        public void AddVertexLayout(in GlVertexLayout layout)
        {
            mVertexLayout.Push(layout);
        }

        public void AddBindResource(in GlBindingResource binding)
        {
            mBindingResources.Push(binding);
        }

        public void SetDrawRange(uint offset, uint count)
        {
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
    }

    /************************************************************************/
    /* GlStencilCoverTask                                                   */
    /************************************************************************/

    public unsafe class GlStencilCoverTask : GlRenderTask
    {
        private GlRenderTask mStencilTask;
        private GlRenderTask mCoverTask;
        private GlStencilMode mStencilMode;

        public GlStencilCoverTask(GlRenderTask stencil, GlRenderTask cover, GlStencilMode mode)
            : base(null)
        {
            mStencilTask = stencil;
            mCoverTask = cover;
            mStencilMode = mode;
        }

        public override void Run()
        {
            GL.glEnable(GL.GL_STENCIL_TEST);

            if (mStencilMode == GlStencilMode.Stroke)
            {
                GL.glStencilFunc(GL.GL_NOTEQUAL, 0x1, 0xFF);
                GL.glStencilOp(GL.GL_KEEP, GL.GL_KEEP, GL.GL_REPLACE);
            }
            else
            {
                GL.glStencilFuncSeparate(GL.GL_FRONT, GL.GL_ALWAYS, 0x0, 0xFF);
                GL.glStencilOpSeparate(GL.GL_FRONT, GL.GL_KEEP, GL.GL_KEEP, GL.GL_INCR_WRAP);

                GL.glStencilFuncSeparate(GL.GL_BACK, GL.GL_ALWAYS, 0x0, 0xFF);
                GL.glStencilOpSeparate(GL.GL_BACK, GL.GL_KEEP, GL.GL_KEEP, GL.GL_DECR_WRAP);
            }
            GL.glColorMask(0, 0, 0, 0);

            mStencilTask.Run();

            if (mStencilMode == GlStencilMode.FillEvenOdd)
            {
                GL.glStencilFunc(GL.GL_NOTEQUAL, 0x00, 0x01);
                GL.glStencilOp(GL.GL_REPLACE, GL.GL_KEEP, GL.GL_REPLACE);
            }
            else
            {
                GL.glStencilFunc(GL.GL_NOTEQUAL, 0x0, 0xFF);
                GL.glStencilOp(GL.GL_KEEP, GL.GL_KEEP, GL.GL_REPLACE);
            }

            GL.glColorMask(1, 1, 1, 1);

            mCoverTask.Run();

            GL.glDisable(GL.GL_STENCIL_TEST);
        }

        public override void NormalizeDrawDepth(int maxDepth)
        {
            mCoverTask.NormalizeDrawDepth(maxDepth);
            mStencilTask.NormalizeDrawDepth(maxDepth);
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

        public override void Run()
        {
            GL.glBindFramebuffer(GL.GL_FRAMEBUFFER, GetSelfFbo());

            // we must clear all area of fbo
            GL.glViewport(0, 0, (int)mFbo!.width, (int)mFbo.height);
            GL.glScissor(0, 0, (int)mFbo.width, (int)mFbo.height);
            GL.glClearColor(0, 0, 0, 0);
            GL.glClearStencil(0);
            GL.glClearDepth(0.0);
            GL.glDepthMask(1);

            GL.glClear(GL.GL_COLOR_BUFFER_BIT | GL.GL_STENCIL_BUFFER_BIT | GL.GL_DEPTH_BUFFER_BIT);
            GL.glDepthMask(0);

            GL.glViewport(0, 0, (int)mRenderWidth, (int)mRenderHeight);
            GL.glScissor(0, 0, (int)mRenderWidth, (int)mRenderHeight);

            for (int i = 0; i < mTasks.Count; i++)
            {
                mTasks[i].Run();
            }

            // reset scissor box
            GL.glScissor(0, 0, (int)mFbo.width, (int)mFbo.height);
            OnResolve();
        }

        public void SetRenderSize(uint width, uint height) { mRenderWidth = width; mRenderHeight = height; }

        protected uint GetTargetFbo() { return mTargetFbo; }
        protected uint GetSelfFbo() { return mFbo!.fbo; }
        protected uint GetResolveFboId() { return mFbo!.resolvedFbo; }

        protected void OnResolve()
        {
            GL.glBindFramebuffer(GL.GL_READ_FRAMEBUFFER, GetSelfFbo());
            GL.glBindFramebuffer(GL.GL_DRAW_FRAMEBUFFER, GetResolveFboId());
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

        public override void Run()
        {
            base.Run();

            GL.glBindFramebuffer(GL.GL_FRAMEBUFFER, GetTargetFbo());
            GL.glViewport(mTargetViewport.Sx(), mTargetViewport.Sy(), mTargetViewport.Sw(), mTargetViewport.Sh());

            if (mClearBuffer)
            {
                GL.glClearColor(0, 0, 0, 0);
                GL.glClear(GL.GL_COLOR_BUFFER_BIT);
            }

            GL.glDisable(GL.GL_DEPTH_TEST);
            // make sure the blending is correct
            GL.glEnable(GL.GL_BLEND);
            GL.glBlendFunc(GL.GL_ONE, GL.GL_ONE_MINUS_SRC_ALPHA);

            RunDraw(); // calls GlRenderTask's draw logic directly
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

        public override void Run()
        {
            mPrevTask?.Run();

            base.Run();

            GL.glBindFramebuffer(GL.GL_FRAMEBUFFER, GetTargetFbo());

            GL.glViewport(0, 0, (int)mParentWidth, (int)mParentHeight);
            GL.glScissor(0, 0, (int)mParentWidth, (int)mParentHeight);
            // Call GlRenderTask's draw logic directly, bypassing GlComposeTask.Run()
            RunDraw();
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

        public override void Run()
        {
            base.Run();

            ref var vp = ref GetViewport();
            var width = mSrcFbo!.width;
            var height = mSrcFbo.height;
            if (width <= 0 || height <= 0) return;

            // Desktop GL path: read from target FBO (parent), not src FBO
            GL.glBindFramebuffer(GL.GL_READ_FRAMEBUFFER, GetTargetFbo());
            GL.glBindFramebuffer(GL.GL_DRAW_FRAMEBUFFER, mDstCopyFbo!.resolvedFbo);
            GL.glViewport(0, 0, (int)mDstCopyFbo.width, (int)mDstCopyFbo.height);
            GL.glScissor(0, 0, (int)mDstCopyFbo.width, (int)mDstCopyFbo.height);
            GL.glBlitFramebuffer(vp.min.x, vp.min.y, vp.max.x, vp.max.y, 0, 0, vp.Sw(), vp.Sh(), GL.GL_COLOR_BUFFER_BIT, GL.GL_LINEAR);

            GL.glBindFramebuffer(GL.GL_FRAMEBUFFER, GetTargetFbo());
            GL.glViewport(0, 0, (int)mParentWidth, (int)mParentHeight);
            GL.glScissor(0, 0, (int)mParentWidth, (int)mParentHeight);

            GL.glDisable(GL.GL_DEPTH_TEST);
            GL.glBlendFunc(GL.GL_ONE, GL.GL_ZERO);
            RunDraw(); // calls GlRenderTask's draw logic directly, bypassing GlComposeTask.Run()
            GL.glBlendFunc(GL.GL_ONE, GL.GL_ONE_MINUS_SRC_ALPHA);
            GL.glEnable(GL.GL_DEPTH_TEST);
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

        public override void Run()
        {
            GL.glEnable(GL.GL_STENCIL_TEST);
            GL.glColorMask(0, 0, 0, 0);
            // draw clip path as normal stencil mask
            GL.glStencilFuncSeparate(GL.GL_FRONT, GL.GL_ALWAYS, 0x1, 0xFF);
            GL.glStencilOpSeparate(GL.GL_FRONT, GL.GL_KEEP, GL.GL_KEEP, GL.GL_INCR_WRAP);

            GL.glStencilFuncSeparate(GL.GL_BACK, GL.GL_ALWAYS, 0x1, 0xFF);
            GL.glStencilOpSeparate(GL.GL_BACK, GL.GL_KEEP, GL.GL_KEEP, GL.GL_DECR_WRAP);

            mClipTask.Run();

            // draw clip mask
            GL.glDepthMask(1);
            GL.glStencilFunc(GL.GL_EQUAL, 0x0, 0xFF);
            GL.glStencilOp(GL.GL_REPLACE, GL.GL_KEEP, GL.GL_REPLACE);

            mMaskTask.Run();

            GL.glColorMask(1, 1, 1, 1);
            GL.glDepthMask(0);
            GL.glDisable(GL.GL_STENCIL_TEST);
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

        public override void Run()
        {
            var width = mCopyRegion.Sw();
            var height = mCopyRegion.Sh();
            if (width <= 0 || height <= 0) return;
            var x = mCopyRegion.Sx();
            var y = mCopyRegion.Sy();
            var fboW = (int)mDstFbo!.width;
            var fboH = (int)mDstFbo.height;
            if (fboW <= 0 || fboH <= 0) return;

            GL.glBindFramebuffer(GL.GL_READ_FRAMEBUFFER, mDstFbo.fbo);
            GL.glBindFramebuffer(GL.GL_DRAW_FRAMEBUFFER, mDstCopyFbo!.resolvedFbo);

            // Desktop GL path
            GL.glViewport(0, 0, width, height);
            GL.glScissor(0, 0, width, height);
            GL.glBlitFramebuffer(x, y, x + width, y + height, 0, 0, width, height, GL.GL_COLOR_BUFFER_BIT, GL.GL_LINEAR);

            GL.glBindFramebuffer(GL.GL_FRAMEBUFFER, mDstFbo.fbo);
            var dstVp = mDstFbo.viewport;
            GL.glViewport(0, 0, dstVp.Sw(), dstVp.Sh());

            GL.glBlendFunc(GL.GL_ONE, GL.GL_ZERO);
            base.Run();
            GL.glBlendFunc(GL.GL_ONE, GL.GL_ONE_MINUS_SRC_ALPHA);
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

        public override void Run()
        {
            mComposeTask.Run();

            ref var vp = ref GetViewport();
            var width = mDstFbo!.width;
            var height = mDstFbo.height;
            if (width <= 0 || height <= 0) return;

            GL.glBindFramebuffer(GL.GL_READ_FRAMEBUFFER, mDstFbo.fbo);
            GL.glBindFramebuffer(GL.GL_DRAW_FRAMEBUFFER, mDstCopyFbo!.resolvedFbo);

            // Desktop GL path
            var dstVp = mDstFbo.viewport;
            GL.glViewport(0, 0, dstVp.Sw(), dstVp.Sh());
            GL.glScissor(0, 0, dstVp.Sw(), dstVp.Sh());
            GL.glBlitFramebuffer(vp.min.x, vp.min.y, vp.max.x, vp.max.y, 0, 0, vp.Sw(), vp.Sh(), GL.GL_COLOR_BUFFER_BIT, GL.GL_LINEAR);

            GL.glBindFramebuffer(GL.GL_FRAMEBUFFER, mDstFbo.fbo);

            GL.glEnable(GL.GL_STENCIL_TEST);
            GL.glColorMask(0, 0, 0, 0);
            GL.glStencilFuncSeparate(GL.GL_FRONT, GL.GL_ALWAYS, 0x0, 0xFF);
            GL.glStencilOpSeparate(GL.GL_FRONT, GL.GL_KEEP, GL.GL_KEEP, GL.GL_INCR_WRAP);

            GL.glStencilFuncSeparate(GL.GL_BACK, GL.GL_ALWAYS, 0x0, 0xFF);
            GL.glStencilOpSeparate(GL.GL_BACK, GL.GL_KEEP, GL.GL_KEEP, GL.GL_DECR_WRAP);

            mStencilTask.Run();

            GL.glColorMask(1, 1, 1, 1);
            GL.glStencilFunc(GL.GL_NOTEQUAL, 0x0, 0xFF);
            GL.glStencilOp(GL.GL_REPLACE, GL.GL_KEEP, GL.GL_REPLACE);

            GL.glBlendFunc(GL.GL_ONE, GL.GL_ZERO);

            base.Run();

            GL.glDisable(GL.GL_STENCIL_TEST);
            GL.glBlendFunc(GL.GL_ONE, GL.GL_ONE_MINUS_SRC_ALPHA);
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

        public override void Run()
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
            var horzSrcTextureLoc = programHorz.GetUniformLocation("uSrcTexture\0"u8);
            var vertSrcTextureLoc = programVert.GetUniformLocation("uSrcTexture\0"u8);

            GL.glViewport(0, 0, width, height);
            GL.glScissor(0, 0, width, height);
            // make a full copy of dst to intermediate buffers
            GL.glBindFramebuffer(GL.GL_READ_FRAMEBUFFER, mDstFbo.fbo);
            GL.glBindFramebuffer(GL.GL_DRAW_FRAMEBUFFER, mDstCopyFbo0.resolvedFbo);
            GL.glBlitFramebuffer(0, 0, width, height, 0, 0, width, height, GL.GL_COLOR_BUFFER_BIT, GL.GL_NEAREST);
            GL.glBindFramebuffer(GL.GL_FRAMEBUFFER, mDstFbo.fbo);

            GL.glDisable(GL.GL_BLEND);
            if (effect!.direction == 0)
            {
                GL.glBindFramebuffer(GL.GL_READ_FRAMEBUFFER, mDstFbo.fbo);
                GL.glBindFramebuffer(GL.GL_DRAW_FRAMEBUFFER, mDstCopyFbo1.resolvedFbo);
                GL.glBlitFramebuffer(0, 0, width, height, 0, 0, width, height, GL.GL_COLOR_BUFFER_BIT, GL.GL_NEAREST);
                // horizontal blur
                GL.glBindFramebuffer(GL.GL_FRAMEBUFFER, mDstCopyFbo1.resolvedFbo);
                horzTask.SetViewport(vp);
                horzTask.AddBindResource(new GlBindingResource(0, dstCopyTexId0, horzSrcTextureLoc));
                horzTask.Run();
                // vertical blur
                GL.glBindFramebuffer(GL.GL_FRAMEBUFFER, mDstFbo.fbo);
                vertTask.SetViewport(vp);
                vertTask.AddBindResource(new GlBindingResource(0, dstCopyTexId1, vertSrcTextureLoc));
                vertTask.Run();
            }
            else if (effect.direction == 1) // horizontal
            {
                horzTask.SetViewport(vp);
                horzTask.AddBindResource(new GlBindingResource(0, dstCopyTexId0, horzSrcTextureLoc));
                horzTask.Run();
            }
            else if (effect.direction == 2) // vertical
            {
                vertTask.SetViewport(vp);
                vertTask.AddBindResource(new GlBindingResource(0, dstCopyTexId0, vertSrcTextureLoc));
                vertTask.Run();
            }
            GL.glEnable(GL.GL_BLEND);
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

        public override void Run()
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
            var horzSrcTextureLoc = programHorz.GetUniformLocation("uSrcTexture\0"u8);
            var vertSrcTextureLoc = programVert.GetUniformLocation("uSrcTexture\0"u8);

            var srcTextureLoc = GetProgram()!.GetUniformLocation("uSrcTexture\0"u8);
            var blrTextureLoc = GetProgram()!.GetUniformLocation("uBlrTexture\0"u8);
            AddBindResource(new GlBindingResource(0, dstCopyTexId0, srcTextureLoc));
            AddBindResource(new GlBindingResource(1, dstCopyTexId1, blrTextureLoc));

            GL.glViewport(0, 0, width, height);
            GL.glScissor(0, 0, width, height);

            // make full copies of dst to intermediate buffers
            GL.glBindFramebuffer(GL.GL_READ_FRAMEBUFFER, mDstFbo.fbo);
            GL.glBindFramebuffer(GL.GL_DRAW_FRAMEBUFFER, mDstCopyFbo0.resolvedFbo);
            GL.glBlitFramebuffer(0, 0, width, height, 0, 0, width, height, GL.GL_COLOR_BUFFER_BIT, GL.GL_NEAREST);
            GL.glBindFramebuffer(GL.GL_READ_FRAMEBUFFER, mDstFbo.fbo);
            GL.glBindFramebuffer(GL.GL_DRAW_FRAMEBUFFER, mDstCopyFbo1.resolvedFbo);
            GL.glBlitFramebuffer(0, 0, width, height, 0, 0, width, height, GL.GL_COLOR_BUFFER_BIT, GL.GL_NEAREST);

            GL.glDisable(GL.GL_BLEND);
            // when sigma is 0, no blur is applied
            if (!TvgMath.Zero(effect!.sigma))
            {
                // horizontal blur
                GL.glBindFramebuffer(GL.GL_FRAMEBUFFER, mDstCopyFbo0.resolvedFbo);
                horzTask.SetViewport(vp);
                horzTask.AddBindResource(new GlBindingResource(0, dstCopyTexId1, horzSrcTextureLoc));
                horzTask.Run();
                // vertical blur
                GL.glBindFramebuffer(GL.GL_FRAMEBUFFER, mDstCopyFbo1.resolvedFbo);
                vertTask.SetViewport(vp);
                vertTask.AddBindResource(new GlBindingResource(0, dstCopyTexId0, vertSrcTextureLoc));
                vertTask.Run();
                // copy original image to intermediate buffer
                GL.glBindFramebuffer(GL.GL_READ_FRAMEBUFFER, mDstFbo.fbo);
                GL.glBindFramebuffer(GL.GL_DRAW_FRAMEBUFFER, mDstCopyFbo0.resolvedFbo);
                GL.glBlitFramebuffer(0, 0, width, height, 0, 0, width, height, GL.GL_COLOR_BUFFER_BIT, GL.GL_NEAREST);
            }
            // run drop shadow effect
            GL.glBindFramebuffer(GL.GL_FRAMEBUFFER, mDstFbo.fbo);
            base.Run();
            GL.glEnable(GL.GL_BLEND);
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

        public override void Run()
        {
            var width = (int)mDstFbo!.width;
            var height = (int)mDstFbo.height;
            // get target handles and pass to shader
            var dstCopyTexId = mDstCopyFbo!.colorTex;
            var srcTextureLoc = GetProgram()!.GetUniformLocation("uSrcTexture\0"u8);
            AddBindResource(new GlBindingResource(0, dstCopyTexId, srcTextureLoc));

            GL.glViewport(0, 0, width, height);
            GL.glScissor(0, 0, width, height);
            // make full copy of dst to intermediate buffer
            GL.glBindFramebuffer(GL.GL_READ_FRAMEBUFFER, mDstFbo.fbo);
            GL.glBindFramebuffer(GL.GL_DRAW_FRAMEBUFFER, mDstCopyFbo.resolvedFbo);
            GL.glBlitFramebuffer(0, 0, width, height, 0, 0, width, height, GL.GL_COLOR_BUFFER_BIT, GL.GL_NEAREST);
            GL.glBindFramebuffer(GL.GL_FRAMEBUFFER, mDstFbo.fbo);

            // run transform
            GL.glDisable(GL.GL_BLEND);
            base.Run();
            GL.glEnable(GL.GL_BLEND);
        }
    }
}
