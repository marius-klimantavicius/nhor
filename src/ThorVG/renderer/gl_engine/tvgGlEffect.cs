// Ported from ThorVG/src/renderer/gl_engine/tvgGlEffect.h and tvgGlEffect.cpp

using System;
using System.Collections.Generic;

namespace ThorVG
{
    /************************************************************************/
    /* Internal data structures for effects                                 */
    /************************************************************************/

    internal class GlGaussianBlur
    {
        public float sigma;
        public float scale;
        public float extend;
        public float dummy0 = 0.0f;
    }

    internal class GlDropShadow : GlGaussianBlur
    {
        public Float4 color;
        public Float2 offset;
    }

    internal class GlEffectParams
    {
        public Float12 @params;
    }

    /************************************************************************/
    /* GlEffect                                                             */
    /************************************************************************/

    public unsafe class GlEffect : IDisposable
    {
        private GlStageBuffer gpuBuffer;

        private GlProgram? pBlurV;
        private GlProgram? pBlurH;
        private GlProgram? pDropShadow;
        private GlProgram? pFill;
        private GlProgram? pTint;
        private GlProgram? pTritone;

        public GlEffect(GlStageBuffer buffer)
        {
            gpuBuffer = buffer;
        }

        ~GlEffect()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                pBlurV?.Dispose();
                pBlurH?.Dispose();
                pDropShadow?.Dispose();
                pFill?.Dispose();
                pTint?.Dispose();
                pTritone?.Dispose();
            }
            pBlurV = null;
            pBlurH = null;
            pDropShadow = null;
            pFill = null;
            pTint = null;
            pTritone = null;
        }

        /************************************************************************/
        /* Public API                                                           */
        /************************************************************************/

        public void Update(RenderEffect effect, in Matrix transform)
        {
            switch (effect.type)
            {
                case SceneEffect.GaussianBlur: UpdateGaussianBlur((RenderEffectGaussianBlur)effect, transform); break;
                case SceneEffect.DropShadow: UpdateDropShadow((RenderEffectDropShadow)effect, transform); break;
                case SceneEffect.Fill: UpdateFill((RenderEffectFill)effect, transform); break;
                case SceneEffect.Tint: UpdateTint((RenderEffectTint)effect, transform); break;
                case SceneEffect.Tritone: UpdateTritone((RenderEffectTritone)effect, transform); break;
                default: break;
            }
        }

        public bool Region(RenderEffect effect)
        {
            switch (effect.type)
            {
                case SceneEffect.GaussianBlur: return RegionGaussianBlur((RenderEffectGaussianBlur)effect);
                case SceneEffect.DropShadow: return RegionDropShadow((RenderEffectDropShadow)effect);
                default: return false;
            }
        }

        public bool Render(RenderEffect effect, GlRenderPass pass, List<GlRenderTargetPool> blendPool)
        {
            if (pass.IsEmpty()) return false;
            ref var vp = ref pass.GetViewport();

            // add render geometry
            var vdata = stackalloc float[] { -1.0f, +1.0f, +1.0f, +1.0f, +1.0f, -1.0f, -1.0f, -1.0f };
            var idata = stackalloc uint[] { 0, 1, 2, 0, 2, 3 };
            var voffset = gpuBuffer.Push(vdata, 8 * sizeof(float));
            var ioffset = gpuBuffer.PushIndex(idata, 6 * sizeof(uint));

            GlRenderTask? output = null;

            if (effect.type == SceneEffect.GaussianBlur)
            {
                output = RenderGaussianBlur((RenderEffectGaussianBlur)effect, pass.GetFbo()!, blendPool, vp, voffset, ioffset);
            }
            else if (effect.type == SceneEffect.DropShadow)
            {
                output = RenderDropShadow((RenderEffectDropShadow)effect, pass.GetFbo()!, blendPool, vp, voffset, ioffset);
            }
            else
            {
                output = RenderColorTransform(effect, pass.GetFbo()!, blendPool, vp, voffset, ioffset);
            }

            if (output == null) return false;

            pass.AddRenderTask(output);
            return true;
        }

        /************************************************************************/
        /* Gaussian Blur                                                        */
        /************************************************************************/

        private bool RegionGaussianBlur(RenderEffectGaussianBlur effect)
        {
            var blur = (GlGaussianBlur?)effect.rd;
            if (blur == null) return false;
            if (effect.direction != 2)
            {
                effect.extend.min.x = (int)(-blur.extend);
                effect.extend.max.x = (int)(+blur.extend);
            }
            if (effect.direction != 1)
            {
                effect.extend.min.y = (int)(-blur.extend);
                effect.extend.max.y = (int)(+blur.extend);
            }
            return true;
        }

        private void UpdateGaussianBlur(RenderEffectGaussianBlur effect, in Matrix transform)
        {
            var blur = (GlGaussianBlur?)effect.rd;
            if (blur == null) blur = new GlGaussianBlur();
            blur.sigma = effect.sigma;
            blur.scale = MathF.Sqrt(transform.e11 * transform.e11 + transform.e12 * transform.e12);
            blur.extend = 2 * blur.sigma * blur.scale;
            effect.rd = blur;
            effect.valid = (blur.extend > 0);
        }

        private GlRenderTask? RenderGaussianBlur(RenderEffectGaussianBlur effect, GlRenderTarget dstFbo, List<GlRenderTargetPool> blendPool, in RenderRegion vp, uint voffset, uint ioffset)
        {
            if (pBlurV == null) pBlurV = new GlProgram(GlShaderSrc.EFFECT_VERTEX, GlShaderSrc.GAUSSIAN_VERTICAL);
            if (pBlurH == null) pBlurH = new GlProgram(GlShaderSrc.EFFECT_VERTEX, GlShaderSrc.GAUSSIAN_HORIZONTAL);

            var dstCopyFbo0 = blendPool[0].GetRenderTarget(vp);
            var dstCopyFbo1 = blendPool[1].GetRenderTarget(vp);

            // add uniform data
            var viewport = stackalloc float[] { vp.min.x, vp.min.y, vp.max.x, vp.max.y };
            var blur = (GlGaussianBlur)effect.rd!;
            var blurData = stackalloc float[] { blur.sigma, blur.scale, blur.extend, blur.dummy0 };
            var blurOffset = gpuBuffer.Push(blurData, 4 * sizeof(float), true);
            var viewportOffset = gpuBuffer.Push(viewport, 4 * sizeof(float), true);

            var task = new GlGaussianBlurTask(dstFbo, dstCopyFbo0, dstCopyFbo1);
            task.effect = effect;
            task.SetViewport(new RenderRegion(0, 0, vp.Sw(), vp.Sh()));

            // horizontal blur task
            task.horzTask = new GlRenderTask(pBlurH);
            task.horzTask.AddBindResource(new GlBindingResource(0, pBlurH.GetUniformBlockIndex("Gaussian\0"u8), gpuBuffer.GetBufferId(), blurOffset, 4 * sizeof(float)));
            task.horzTask.AddBindResource(new GlBindingResource(1, pBlurH.GetUniformBlockIndex("Viewport\0"u8), gpuBuffer.GetBufferId(), viewportOffset, 4 * sizeof(float)));
            task.horzTask.AddVertexLayout(new GlVertexLayout { index = 0, size = 2, stride = 2 * sizeof(float), offset = voffset });
            task.horzTask.SetDrawRange(ioffset, 6);

            // vertical blur task
            task.vertTask = new GlRenderTask(pBlurV);
            task.vertTask.AddBindResource(new GlBindingResource(0, pBlurV.GetUniformBlockIndex("Gaussian\0"u8), gpuBuffer.GetBufferId(), blurOffset, 4 * sizeof(float)));
            task.vertTask.AddBindResource(new GlBindingResource(1, pBlurV.GetUniformBlockIndex("Viewport\0"u8), gpuBuffer.GetBufferId(), viewportOffset, 4 * sizeof(float)));
            task.vertTask.AddVertexLayout(new GlVertexLayout { index = 0, size = 2, stride = 2 * sizeof(float), offset = voffset });
            task.vertTask.SetDrawRange(ioffset, 6);

            return task;
        }

        /************************************************************************/
        /* DropShadow                                                           */
        /************************************************************************/

        private bool RegionDropShadow(RenderEffectDropShadow effect)
        {
            var ds = (GlDropShadow?)effect.rd;
            if (ds == null) return false;
            effect.extend.min.x = (int)(-ds.extend);
            effect.extend.max.x = (int)(+ds.extend);
            effect.extend.min.y = (int)(-ds.extend);
            effect.extend.max.y = (int)(+ds.extend);
            return true;
        }

        private void UpdateDropShadow(RenderEffectDropShadow effect, in Matrix transform)
        {
            var ds = (GlDropShadow?)effect.rd;
            if (ds == null) ds = new GlDropShadow();
            var scale = MathF.Sqrt(transform.e11 * transform.e11 + transform.e12 * transform.e12);
            var radian = TvgMath.Deg2Rad(90.0f - effect.angle) - TvgMath.Radian(transform);
            var offsetX = -effect.distance * MathF.Cos(radian) * scale;
            var offsetY = -effect.distance * MathF.Sin(radian) * scale;

            ds.sigma = effect.sigma;
            ds.scale = scale;
            ds.color[3] = effect.color[3] / 255.0f;
            ds.color[0] = effect.color[0] / 255.0f * ds.color[3];
            ds.color[1] = effect.color[1] / 255.0f * ds.color[3];
            ds.color[2] = effect.color[2] / 255.0f * ds.color[3];
            ds.offset[0] = offsetX;
            ds.offset[1] = offsetY;
            ds.extend = 2 * MathF.Max(effect.sigma * scale + MathF.Abs(offsetX), effect.sigma * scale + MathF.Abs(offsetY));
            effect.rd = ds;
            effect.valid = (ds.extend >= 0);
        }

        private GlRenderTask? RenderDropShadow(RenderEffectDropShadow effect, GlRenderTarget dstFbo, List<GlRenderTargetPool> blendPool, in RenderRegion vp, uint voffset, uint ioffset)
        {
            if (pBlurV == null) pBlurV = new GlProgram(GlShaderSrc.EFFECT_VERTEX, GlShaderSrc.GAUSSIAN_VERTICAL);
            if (pBlurH == null) pBlurH = new GlProgram(GlShaderSrc.EFFECT_VERTEX, GlShaderSrc.GAUSSIAN_HORIZONTAL);
            if (pDropShadow == null) pDropShadow = new GlProgram(GlShaderSrc.EFFECT_VERTEX, GlShaderSrc.EFFECT_DROPSHADOW);

            var dstCopyFbo0 = blendPool[0].GetRenderTarget(vp);
            var dstCopyFbo1 = blendPool[1].GetRenderTarget(vp);

            var viewport = stackalloc float[] { vp.min.x, vp.min.y, vp.max.x, vp.max.y };
            var ds = (GlDropShadow)effect.rd!;
            // Pack GlDropShadow into float array: sigma, scale, extend, dummy0, color[4], offset[2]
            var paramsData = stackalloc float[] { ds.sigma, ds.scale, ds.extend, ds.dummy0, ds.color[0], ds.color[1], ds.color[2], ds.color[3], ds.offset[0], ds.offset[1] };
            var paramsOffset = gpuBuffer.Push(paramsData, 10 * sizeof(float), true);
            var viewportOffset = gpuBuffer.Push(viewport, 4 * sizeof(float), true);

            var task = new GlEffectDropShadowTask(pDropShadow, dstFbo, dstCopyFbo0, dstCopyFbo1);
            task.effect = effect;
            task.SetViewport(new RenderRegion(0, 0, vp.Sw(), vp.Sh()));
            task.AddBindResource(new GlBindingResource(0, pDropShadow.GetUniformBlockIndex("DropShadow\0"u8), gpuBuffer.GetBufferId(), paramsOffset, 10 * sizeof(float)));
            task.AddVertexLayout(new GlVertexLayout { index = 0, size = 2, stride = 2 * sizeof(float), offset = voffset });
            task.SetDrawRange(ioffset, 6);

            // horizontal blur task
            task.horzTask = new GlRenderTask(pBlurH);
            task.horzTask.AddBindResource(new GlBindingResource(0, pBlurH.GetUniformBlockIndex("Gaussian\0"u8), gpuBuffer.GetBufferId(), paramsOffset, 4 * sizeof(float)));
            task.horzTask.AddBindResource(new GlBindingResource(1, pBlurH.GetUniformBlockIndex("Viewport\0"u8), gpuBuffer.GetBufferId(), viewportOffset, 4 * sizeof(float)));
            task.horzTask.AddVertexLayout(new GlVertexLayout { index = 0, size = 2, stride = 2 * sizeof(float), offset = voffset });
            task.horzTask.SetDrawRange(ioffset, 6);

            // vertical blur task
            task.vertTask = new GlRenderTask(pBlurV);
            task.vertTask.AddBindResource(new GlBindingResource(0, pBlurV.GetUniformBlockIndex("Gaussian\0"u8), gpuBuffer.GetBufferId(), paramsOffset, 4 * sizeof(float)));
            task.vertTask.AddBindResource(new GlBindingResource(1, pBlurV.GetUniformBlockIndex("Viewport\0"u8), gpuBuffer.GetBufferId(), viewportOffset, 4 * sizeof(float)));
            task.vertTask.AddVertexLayout(new GlVertexLayout { index = 0, size = 2, stride = 2 * sizeof(float), offset = voffset });
            task.vertTask.SetDrawRange(ioffset, 6);

            return task;
        }

        /************************************************************************/
        /* Color Replacement Effects                                            */
        /************************************************************************/

        private void UpdateFill(RenderEffectFill effect, in Matrix transform)
        {
            var p = (GlEffectParams?)effect.rd;
            if (p == null) p = new GlEffectParams();
            p.@params[0] = effect.color[0] / 255.0f;
            p.@params[1] = effect.color[1] / 255.0f;
            p.@params[2] = effect.color[2] / 255.0f;
            p.@params[3] = effect.color[3] / 255.0f;
            effect.rd = p;
            effect.valid = true;
        }

        private void UpdateTint(RenderEffectTint effect, in Matrix transform)
        {
            effect.valid = (effect.intensity > 0);
            if (!effect.valid) return;

            var p = (GlEffectParams?)effect.rd;
            if (p == null) p = new GlEffectParams();
            p.@params[0] = effect.black[0] / 255.0f;
            p.@params[1] = effect.black[1] / 255.0f;
            p.@params[2] = effect.black[2] / 255.0f;
            p.@params[3] = 0.0f;
            p.@params[4] = effect.white[0] / 255.0f;
            p.@params[5] = effect.white[1] / 255.0f;
            p.@params[6] = effect.white[2] / 255.0f;
            p.@params[7] = 0.0f;
            p.@params[8] = effect.intensity / 255.0f;
            effect.rd = p;
        }

        private void UpdateTritone(RenderEffectTritone effect, in Matrix transform)
        {
            effect.valid = (effect.blender < 255);
            if (!effect.valid) return;

            var p = (GlEffectParams?)effect.rd;
            if (p == null) p = new GlEffectParams();
            p.@params[0] = effect.shadow[0] / 255.0f;
            p.@params[1] = effect.shadow[1] / 255.0f;
            p.@params[2] = effect.shadow[2] / 255.0f;
            p.@params[3] = 0.0f;
            p.@params[4] = effect.midtone[0] / 255.0f;
            p.@params[5] = effect.midtone[1] / 255.0f;
            p.@params[6] = effect.midtone[2] / 255.0f;
            p.@params[7] = 0.0f;
            p.@params[8] = effect.highlight[0] / 255.0f;
            p.@params[9] = effect.highlight[1] / 255.0f;
            p.@params[10] = effect.highlight[2] / 255.0f;
            p.@params[11] = effect.blender / 255.0f;
            effect.rd = p;
        }

        private GlRenderTask? RenderColorTransform(RenderEffect effect, GlRenderTarget dstFbo, List<GlRenderTargetPool> blendPool, in RenderRegion vp, uint voffset, uint ioffset)
        {
            GlProgram? program = null;
            if (effect.type == SceneEffect.Fill)
            {
                if (pFill == null) pFill = new GlProgram(GlShaderSrc.EFFECT_VERTEX, GlShaderSrc.EFFECT_FILL);
                program = pFill;
            }
            else if (effect.type == SceneEffect.Tint)
            {
                if (pTint == null) pTint = new GlProgram(GlShaderSrc.EFFECT_VERTEX, GlShaderSrc.EFFECT_TINT);
                program = pTint;
            }
            else if (effect.type == SceneEffect.Tritone)
            {
                if (pTritone == null) pTritone = new GlProgram(GlShaderSrc.EFFECT_VERTEX, GlShaderSrc.EFFECT_TRITONE);
                program = pTritone;
            }
            else return null;

            var dstCopyFbo = blendPool[0].GetRenderTarget(vp);

            var p = (GlEffectParams)effect.rd!;
            uint paramsOffset;
            fixed (float* pptr = (Span<float>)p.@params) { paramsOffset = gpuBuffer.Push(pptr, 12 * sizeof(float), true); }

            var task = new GlEffectColorTransformTask(program, dstFbo, dstCopyFbo);
            task.SetViewport(new RenderRegion(0, 0, vp.Sw(), vp.Sh()));
            task.AddBindResource(new GlBindingResource(0, program!.GetUniformBlockIndex("Params\0"u8), gpuBuffer.GetBufferId(), paramsOffset, 12 * sizeof(float)));
            task.AddVertexLayout(new GlVertexLayout { index = 0, size = 2, stride = 2 * sizeof(float), offset = voffset });
            task.SetDrawRange(ioffset, 6);

            return task;
        }
    }
}
