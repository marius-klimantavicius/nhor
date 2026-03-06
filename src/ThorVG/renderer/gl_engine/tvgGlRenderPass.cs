// Ported from ThorVG/src/renderer/gl_engine/tvgGlRenderPass.h and tvgGlRenderPass.cpp

using System.Collections.Generic;

namespace ThorVG
{
    public class GlRenderPass
    {
        private GlRenderTarget? mFbo;
        private List<GlRenderTask> mTasks = new List<GlRenderTask>();
        private int mDrawDepth;
        private Matrix mViewMatrix;

        public GlRenderPass(GlRenderTarget? fbo)
        {
            mFbo = fbo;
            mDrawDepth = 0;
            mViewMatrix = TvgMath.Identity();
            if (mFbo != null) mViewMatrix = ComputeViewMatrix(mFbo.viewport);
        }

        public GlRenderPass(GlRenderPass other)
        {
            mFbo = other.mFbo;
            mViewMatrix = other.mViewMatrix;
            mTasks.AddRange(other.mTasks);
            other.mTasks.Clear();
            mDrawDepth = other.mDrawDepth;
        }

        public bool IsEmpty() => mFbo == null;

        public void AddRenderTask(GlRenderTask task)
        {
            mTasks.Add(task);
        }

        public uint GetFboId() => mFbo!.fbo;
        public uint GetTextureId() => mFbo!.colorTex;
        public ref RenderRegion GetViewport() => ref mFbo!.viewport;
        public uint GetFboWidth() => mFbo!.width;
        public uint GetFboHeight() => mFbo!.height;
        public ref Matrix GetViewMatrix() => ref mViewMatrix;

        public T EndRenderPass<T>(GlProgram? program, uint targetFbo) where T : GlComposeTask
        {
            var maxDepth = mDrawDepth + 1;

            for (int i = 0; i < mTasks.Count; i++)
            {
                mTasks[i].NormalizeDrawDepth(maxDepth);
            }

            T task;
            if (typeof(T) == typeof(GlBlitTask))
            {
                task = (T)(object)new GlBlitTask(program, targetFbo, mFbo, mTasks);
            }
            else if (typeof(T) == typeof(GlDrawBlitTask))
            {
                task = (T)(object)new GlDrawBlitTask(program, targetFbo, mFbo, mTasks);
            }
            else if (typeof(T) == typeof(GlSceneBlendTask))
            {
                task = (T)(object)new GlSceneBlendTask(program, targetFbo, mFbo, mTasks);
            }
            else
            {
                task = (T)(object)new GlComposeTask(program, targetFbo, mFbo, mTasks);
            }

            mTasks.Clear();
            task.SetRenderSize((uint)mFbo!.viewport.Sw(), (uint)mFbo.viewport.Sh());
            return task;
        }

        public int NextDrawDepth() { return ++mDrawDepth; }
        public void SetDrawDepth(int depth) { mDrawDepth = depth; }
        public GlRenderTarget? GetFbo() { return mFbo; }

        private static Matrix ComputeViewMatrix(in RenderRegion vp)
        {
            var postMatrix = new Matrix(1.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 1.0f);
            TvgMath.Translate(ref postMatrix, new Point(-vp.Sx(), -vp.Sy()));

            var mvp = new Matrix(1.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 1.0f);
            mvp.e11 = 2f / vp.Sw();
            mvp.e22 = -2f / vp.Sh();
            mvp.e13 = -1f;
            mvp.e23 = 1f;
            return TvgMath.Multiply(mvp, postMatrix);
        }
    }
}
