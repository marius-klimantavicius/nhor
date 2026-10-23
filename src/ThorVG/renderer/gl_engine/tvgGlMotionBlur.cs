// Ported from pinned ThorVG/src/renderer/gpu_engine/gl/tvgGlEffect.cpp.
using System;
using System.Collections.Generic;

namespace ThorVG
{
    internal class GlMotionBlur
    {
        public Point offset;
        public float samples;
    }

    public unsafe partial class GlEffect
    {
        private void UpdateMotionBlur(RenderEffectMotionBlur effect, in Matrix transform)
        {
            if (!effect.Update(transform, out var offset, out int samples)) return;
            var blur = effect.rd as GlMotionBlur ?? new GlMotionBlur();
            blur.offset = new Point(offset.x, -offset.y);
            blur.samples = samples;
            effect.rd = blur;
        }

        private bool RegionMotionBlur(RenderEffectMotionBlur effect)
        {
            var blur = (GlMotionBlur)effect.rd!;
            int x = (int)MathF.Ceiling(MathF.Abs(blur.offset.x) * .5f), y = (int)MathF.Ceiling(MathF.Abs(blur.offset.y) * .5f);
            effect.extend = new RenderRegion(-x, -y, x, y);
            return true;
        }

        private GlRenderTask RenderMotionBlur(RenderEffectMotionBlur effect, GlRenderTarget target, List<GlRenderTargetPool> pool, in RenderRegion vp, uint vertexOffset, uint indexOffset)
        {
            pMotionBlur ??= new GlProgram(GlShaderSrc.EFFECT_VERTEX, MOTION_BLUR_SHADER);
            var copy = pool[0].GetRenderTarget(vp);
            var blur = (GlMotionBlur)effect.rd!;
            // alignas(16) GlMotionBlur has three floats and one padding float.
            float* data = stackalloc float[4] { blur.offset.x, blur.offset.y, blur.samples, 0 };
            uint parameters = gpuBuffer.Push(data, 4 * sizeof(float), true);
            float* viewport = stackalloc float[4] { 0, 0, vp.Sw(), vp.Sh() };
            uint viewportOffset = gpuBuffer.Push(viewport, 4 * sizeof(float), true);
            // The existing C# color-transform task is the target's general
            // single-pass GlEffectTask: copy source FBO, bind texture, replace output.
            var task = new GlEffectColorTransformTask(pMotionBlur, target, copy);
            task.SetViewport(new RenderRegion(0, 0, vp.Sw(), vp.Sh()));
            task.AddBindResource(new GlBindingResource(0, pMotionBlur.GetUniformBlockIndex(GlShaderUniformBlock.Params), gpuBuffer.GetBufferId(), parameters, 4 * sizeof(float)));
            task.AddBindResource(new GlBindingResource(1, pMotionBlur.GetUniformBlockIndex(GlShaderUniformBlock.Viewport), gpuBuffer.GetBufferId(), viewportOffset, 4 * sizeof(float)));
            task.AddVertexLayout(new GlVertexLayout { index = 0, size = 2, stride = 2 * sizeof(float), offset = vertexOffset, arrayBufferId = gpuBuffer.GetBufferId() });
            task.SetDrawRange(indexOffset, 6);
            return task;
        }

        // Exact pinned EFFECT_MOTIONBLUR shader, with only C# string syntax adapted.
        private const string MOTION_BLUR_SHADER = @"
uniform sampler2D uSrcTexture;
layout(std140) uniform Params {
    vec2 offset;
    float samples;
} uParams;
layout(std140) uniform Viewport {
    vec4 vp;
} uViewport;
out vec4 FragColor;
void main()
{
    vec2 invTextureSize = 1.0 / vec2(textureSize(uSrcTexture, 0));
    vec2 maxCoord = uViewport.vp.zw - vec2(0.5);
    int samples = int(uParams.samples);
    float invSamples = 1.0 / float(samples);
    vec4 color = vec4(0.0);
    for (int i = 0; i < samples; ++i) {
        float t = (float(i) + 0.5) * invSamples - 0.5;
        vec2 p = clamp(gl_FragCoord.xy + uParams.offset * t, vec2(0.5), maxCoord);
        color += textureLod(uSrcTexture, p * invTextureSize, 0.0);
    }
    FragColor = color * invSamples;
}
";
    }
}
