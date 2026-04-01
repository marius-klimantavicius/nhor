// Ported from ThorVG/src/renderer/gl_engine/tvgGlRenderer.h and tvgGlRenderer.cpp

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ThorVG
{
    public unsafe class GlRenderer : RenderMethod
    {
        /************************************************************************/
        /* RenderTypes enum                                                     */
        /************************************************************************/

        public enum RenderTypes
        {
            RT_Color = 0,
            RT_LinGradient,
            RT_RadGradient,
            RT_Image,
            RT_MaskAlpha,
            RT_MaskAlphaInv,
            RT_MaskLuma,
            RT_MaskLumaInv,
            RT_MaskAdd,
            RT_MaskSub,
            RT_MaskIntersect,
            RT_MaskDifference,
            RT_MaskLighten,
            RT_MaskDarken,
            RT_Stencil,
            RT_Blit,
            // blends (image)
            RT_Blend_Image_Normal,
            RT_Blend_Image_Multiply,
            RT_Blend_Image_Screen,
            RT_Blend_Image_Overlay,
            RT_Blend_Image_Darken,
            RT_Blend_Image_Lighten,
            RT_Blend_Image_ColorDodge,
            RT_Blend_Image_ColorBurn,
            RT_Blend_Image_HardLight,
            RT_Blend_Image_SoftLight,
            RT_Blend_Image_Difference,
            RT_Blend_Image_Exclusion,
            RT_Blend_Image_Hue,
            RT_Blend_Image_Saturation,
            RT_Blend_Image_Color,
            RT_Blend_Image_Luminosity,
            RT_Blend_Image_Add,
            // blends (scene)
            RT_Blend_Scene_Normal,
            RT_Blend_Scene_Multiply,
            RT_Blend_Scene_Screen,
            RT_Blend_Scene_Overlay,
            RT_Blend_Scene_Darken,
            RT_Blend_Scene_Lighten,
            RT_Blend_Scene_ColorDodge,
            RT_Blend_Scene_ColorBurn,
            RT_Blend_Scene_HardLight,
            RT_Blend_Scene_SoftLight,
            RT_Blend_Scene_Difference,
            RT_Blend_Scene_Exclusion,
            RT_Blend_Scene_Hue,
            RT_Blend_Scene_Saturation,
            RT_Blend_Scene_Color,
            RT_Blend_Scene_Luminosity,
            RT_Blend_Scene_Add,
            // shape blends (solid)
            RT_ShapeBlend_Solid_Normal,
            RT_ShapeBlend_Solid_Multiply,
            RT_ShapeBlend_Solid_Screen,
            RT_ShapeBlend_Solid_Overlay,
            RT_ShapeBlend_Solid_Darken,
            RT_ShapeBlend_Solid_Lighten,
            RT_ShapeBlend_Solid_ColorDodge,
            RT_ShapeBlend_Solid_ColorBurn,
            RT_ShapeBlend_Solid_HardLight,
            RT_ShapeBlend_Solid_SoftLight,
            RT_ShapeBlend_Solid_Difference,
            RT_ShapeBlend_Solid_Exclusion,
            RT_ShapeBlend_Solid_Hue,
            RT_ShapeBlend_Solid_Saturation,
            RT_ShapeBlend_Solid_Color,
            RT_ShapeBlend_Solid_Luminosity,
            RT_ShapeBlend_Solid_Add,
            // shape blends (linear gradient)
            RT_ShapeBlend_Linear_Normal,
            RT_ShapeBlend_Linear_Multiply,
            RT_ShapeBlend_Linear_Screen,
            RT_ShapeBlend_Linear_Overlay,
            RT_ShapeBlend_Linear_Darken,
            RT_ShapeBlend_Linear_Lighten,
            RT_ShapeBlend_Linear_ColorDodge,
            RT_ShapeBlend_Linear_ColorBurn,
            RT_ShapeBlend_Linear_HardLight,
            RT_ShapeBlend_Linear_SoftLight,
            RT_ShapeBlend_Linear_Difference,
            RT_ShapeBlend_Linear_Exclusion,
            RT_ShapeBlend_Linear_Hue,
            RT_ShapeBlend_Linear_Saturation,
            RT_ShapeBlend_Linear_Color,
            RT_ShapeBlend_Linear_Luminosity,
            RT_ShapeBlend_Linear_Add,
            // shape blends (radial gradient)
            RT_ShapeBlend_Radial_Normal,
            RT_ShapeBlend_Radial_Multiply,
            RT_ShapeBlend_Radial_Screen,
            RT_ShapeBlend_Radial_Overlay,
            RT_ShapeBlend_Radial_Darken,
            RT_ShapeBlend_Radial_Lighten,
            RT_ShapeBlend_Radial_ColorDodge,
            RT_ShapeBlend_Radial_ColorBurn,
            RT_ShapeBlend_Radial_HardLight,
            RT_ShapeBlend_Radial_SoftLight,
            RT_ShapeBlend_Radial_Difference,
            RT_ShapeBlend_Radial_Exclusion,
            RT_ShapeBlend_Radial_Hue,
            RT_ShapeBlend_Radial_Saturation,
            RT_ShapeBlend_Radial_Color,
            RT_ShapeBlend_Radial_Luminosity,
            RT_ShapeBlend_Radial_Add,
            RT_None
        }

        /************************************************************************/
        /* Private types                                                        */
        /************************************************************************/

        private enum BlendSource { Image, Scene, Solid, LinearGradient, RadialGradient }

        private const float NOISE_LEVEL = 0.5f;

        /************************************************************************/
        /* Static state                                                         */
        /************************************************************************/

        private static int _rendererCnt = -1;
        private static readonly object _rendererMtx = new object();

        /************************************************************************/
        /* Instance state                                                       */
        /************************************************************************/

        private nint mDisplay;
        private nint mSurface;
        private nint mContext;

        private RenderSurface surface = new RenderSurface();
        private int mTargetFboId;
        internal GlStageBuffer mGpuBuffer = new GlStageBuffer();
        private GlRenderTarget mRootTarget = new GlRenderTarget();
        private GlEffect mEffect;
        internal List<GlProgram?> mPrograms = new List<GlProgram?>();

        private List<GlRenderTargetPool> mComposePool = new List<GlRenderTargetPool>();
        private List<GlRenderTargetPool> mBlendPool = new List<GlRenderTargetPool>();
        private List<GlRenderPass> mRenderPassStack = new List<GlRenderPass>();
        private List<GlCompositor> mComposeStack = new List<GlCompositor>();
        private TextureMgr mTextures = new TextureMgr();
        private GlSolidBatch mSolidBatch = new GlSolidBatch();

        // Disposed resources. They should be released on synced call.
        private List<uint> mDisposedTextures = new List<uint>();
        private Key mDisposedKey = new Key();

        private BlendMethod mBlendMethod = BlendMethod.Normal;
        private bool mClearBuffer;

        /************************************************************************/
        /* Constructor / Destructor                                             */
        /************************************************************************/

        private GlRenderer()
        {
            mEffect = new GlEffect(mGpuBuffer);
        }

        ~GlRenderer()
        {
            if (mContext != 0) CurrentContext();
            Flush();
            mTextures.Clear();

            mPrograms.Clear();

            lock (_rendererMtx)
            {
                --_rendererCnt;
            }
        }

        /************************************************************************/
        /* Internal Class Implementation                                        */
        /************************************************************************/

        private void DisposeTexture(uint texId)
        {
            if (texId == 0) return;
            using var lk = new ScopedLock(mDisposedKey);
            mDisposedTextures.Add(texId);
        }

        private void ClearDisposes()
        {
            if (mDisposedTextures.Count > 0)
            {
                fixed (uint* ptr = CollectionsMarshal.AsSpan(mDisposedTextures))
                {
                    GL.glDeleteTextures(mDisposedTextures.Count, ptr);
                }
                mDisposedTextures.Clear();
            }

            mRenderPassStack.Clear();
            mSolidBatch.Clear();
        }

        private void Flush()
        {
            ClearDisposes();

            mRootTarget.Reset();
            mComposePool.Clear();
            mBlendPool.Clear();
            mComposeStack.Clear();
        }

        private bool CurrentContext()
        {
            // In the C# port we do not manage EGL/WGL contexts.
            // Application-managed context is assumed current.
            TvgCommon.TVGLOG("GL_ENGINE", "Maybe missing currentContext()?");
            return true;
        }

        private void InitShaders()
        {
            var count = (int)RenderTypes.RT_None;
            mPrograms.Capacity = count;

            var linearGradientFragShader = string.Concat(
                GlShaderSrc.STR_GRADIENT_FRAG_COMMON_VARIABLES,
                GlShaderSrc.STR_LINEAR_GRADIENT_VARIABLES,
                GlShaderSrc.STR_GRADIENT_FRAG_COMMON_FUNCTIONS,
                GlShaderSrc.STR_LINEAR_GRADIENT_FUNCTIONS,
                GlShaderSrc.STR_LINEAR_GRADIENT_MAIN);

            var radialGradientFragShader = string.Concat(
                GlShaderSrc.STR_GRADIENT_FRAG_COMMON_VARIABLES,
                GlShaderSrc.STR_RADIAL_GRADIENT_VARIABLES,
                GlShaderSrc.STR_GRADIENT_FRAG_COMMON_FUNCTIONS,
                GlShaderSrc.STR_RADIAL_GRADIENT_FUNCTIONS,
                GlShaderSrc.STR_RADIAL_GRADIENT_MAIN);

            mPrograms.Add(new GlProgram(GlShaderSrc.COLOR_VERT_SHADER, GlShaderSrc.COLOR_FRAG_SHADER));
            mPrograms.Add(new GlProgram(GlShaderSrc.GRADIENT_VERT_SHADER, linearGradientFragShader));
            mPrograms.Add(new GlProgram(GlShaderSrc.GRADIENT_VERT_SHADER, radialGradientFragShader));
            mPrograms.Add(new GlProgram(GlShaderSrc.IMAGE_VERT_SHADER, GlShaderSrc.IMAGE_FRAG_SHADER));

            // compose Renderer
            mPrograms.Add(new GlProgram(GlShaderSrc.MASK_VERT_SHADER, GlShaderSrc.MASK_ALPHA_FRAG_SHADER));
            mPrograms.Add(new GlProgram(GlShaderSrc.MASK_VERT_SHADER, GlShaderSrc.MASK_INV_ALPHA_FRAG_SHADER));
            mPrograms.Add(new GlProgram(GlShaderSrc.MASK_VERT_SHADER, GlShaderSrc.MASK_LUMA_FRAG_SHADER));
            mPrograms.Add(new GlProgram(GlShaderSrc.MASK_VERT_SHADER, GlShaderSrc.MASK_INV_LUMA_FRAG_SHADER));
            mPrograms.Add(new GlProgram(GlShaderSrc.MASK_VERT_SHADER, GlShaderSrc.MASK_ADD_FRAG_SHADER));
            mPrograms.Add(new GlProgram(GlShaderSrc.MASK_VERT_SHADER, GlShaderSrc.MASK_SUB_FRAG_SHADER));
            mPrograms.Add(new GlProgram(GlShaderSrc.MASK_VERT_SHADER, GlShaderSrc.MASK_INTERSECT_FRAG_SHADER));
            mPrograms.Add(new GlProgram(GlShaderSrc.MASK_VERT_SHADER, GlShaderSrc.MASK_DIFF_FRAG_SHADER));
            mPrograms.Add(new GlProgram(GlShaderSrc.MASK_VERT_SHADER, GlShaderSrc.MASK_LIGHTEN_FRAG_SHADER));
            mPrograms.Add(new GlProgram(GlShaderSrc.MASK_VERT_SHADER, GlShaderSrc.MASK_DARKEN_FRAG_SHADER));

            // stencil Renderer
            mPrograms.Add(new GlProgram(GlShaderSrc.STENCIL_VERT_SHADER, GlShaderSrc.STENCIL_FRAG_SHADER));

            // blit Renderer
            mPrograms.Add(new GlProgram(GlShaderSrc.BLIT_VERT_SHADER, GlShaderSrc.BLIT_FRAG_SHADER));

            // blend programs: image (17) + scene (17) + shape solid (17) + shape linear (17) + shape radial (17) = 85
            for (int i = 0; i < 85; ++i)
            {
                mPrograms.Add(null);
            }
        }

        private static RenderRegion ViewportRegion(in RenderRegion vp, in RenderRegion bbox)
        {
            var x = bbox.Sx() - vp.Sx();
            var y = bbox.Sy() - vp.Sy();
            var w = bbox.Sw();
            var h = bbox.Sh();
            var yGl = vp.Sh() - y - h;

            return new RenderRegion(x, yGl, x + w, yGl + h);
        }

        private GlRenderTask CreatePrimitiveTask(RenderTypes type, BlendSource source, in RenderRegion viewRegion, out GlRenderTarget? dstCopyFbo)
        {
            dstCopyFbo = null;

            if (mBlendMethod == BlendMethod.Normal) return new GlRenderTask(mPrograms[(int)type]);

            if (mBlendPool.Count == 0) mBlendPool.Add(new GlRenderTargetPool(surface.w, surface.h));
            // Desktop GL path
            dstCopyFbo = mBlendPool[0].GetRenderTarget(viewRegion);
            var program = GetBlendProgram(mBlendMethod, source);
            return new GlDirectBlendTask(program, CurrentPass()!.GetFbo(), dstCopyFbo, viewRegion);
        }

        private GlRenderTask? CreateStencilTask(GlRenderTask task, GlStencilMode stencilMode, int depth)
        {
            if (stencilMode == GlStencilMode.None) return null;

            var stencilTask = new GlRenderTask(mPrograms[(int)RenderTypes.RT_Stencil], task);
            stencilTask.SetDrawDepth(depth);

            return stencilTask;
        }

        private void BindBlendTarget(GlRenderTask task, GlRenderTarget? dstCopyFbo, in RenderRegion viewRegion, uint binding)
        {
            if (dstCopyFbo == null) return;

            // Desktop GL path
            var region = stackalloc float[4];
            region[0] = viewRegion.Sx();
            region[1] = viewRegion.Sy();
            region[2] = dstCopyFbo.width;
            region[3] = dstCopyFbo.height;

            task.AddBindResource(new GlBindingResource(
                binding,
                task.GetProgram()!.GetUniformBlockIndex("BlendRegion\0"u8),
                mGpuBuffer.GetBufferId(),
                mGpuBuffer.Push(region, 4 * sizeof(float), true),
                4 * sizeof(float)));
            task.AddBindResource(new GlBindingResource(0, dstCopyFbo.colorTex, task.GetProgram()!.GetUniformLocation("uDstTexture\0"u8)));
        }

        private void DrawPrimitive(GlShape sdata, in RGBA c, RenderUpdateFlag flag, int depth)
        {
            var blendShape = (mBlendMethod != BlendMethod.Normal);
            var vp = CurrentPass()!.GetViewport();
            var bbox = blendShape ? sdata.geometry.GetBounds() : sdata.geometry.viewport;

            bbox.IntersectWith(vp);
            if (bbox.Invalid()) return;

            var viewRegion = ViewportRegion(vp, bbox);
            var stencilMode = sdata.geometry.GetStencilMode(flag);

            if (!blendShape && stencilMode == GlStencilMode.None && sdata.clips.Count == 0)
            {
                mSolidBatch.Draw(this, sdata, c, depth, viewRegion);
                return;
            }

            if (sdata.clips.Count > 0) mSolidBatch.Clear();

            GlRenderTarget? dstCopyFbo;
            var task = CreatePrimitiveTask(RenderTypes.RT_Color, BlendSource.Solid, viewRegion, out dstCopyFbo);

            task.SetViewMatrix(CurrentPass()!.GetViewMatrix());
            task.SetDrawDepth(depth);

            if (!sdata.geometry.Draw(task, mGpuBuffer, flag))
            {
                return;
            }

            var a = RenderHelper.Multiply(c.a, (byte)sdata.opacity);
            if ((flag & RenderUpdateFlag.Stroke) != 0)
            {
                var strokeWidth = sdata.geometry.strokeRenderWidth;
                if (strokeWidth < GlConstants.MIN_GL_STROKE_WIDTH)
                {
                    var alpha = strokeWidth / GlConstants.MIN_GL_STROKE_WIDTH;
                    a = RenderHelper.Multiply(a, (byte)(alpha * 255));
                }
            }
            task.SetVertexColor(c.r / 255f, c.g / 255f, c.b / 255f, a / 255f);
            task.SetViewport(viewRegion);

            var stencilTask = CreateStencilTask(task, stencilMode, depth);
            // Keep BlendRegion on the existing solid-shape blend UBO slot.
            BindBlendTarget(task, dstCopyFbo, viewRegion, 2);

            if (stencilTask != null) CurrentPass()!.AddRenderTask(new GlStencilCoverTask(stencilTask, task, stencilMode));
            else CurrentPass()!.AddRenderTask(task);
        }

        private void DrawPrimitive(GlShape sdata, Fill fill, RenderUpdateFlag flag, int depth)
        {
            var blendShape = (mBlendMethod != BlendMethod.Normal);
            var vp = CurrentPass()!.GetViewport();
            var bbox = blendShape ? sdata.geometry.GetBounds() : sdata.geometry.viewport;
            bbox.IntersectWith(vp);
            if (bbox.Invalid()) return;

            Fill.ColorStop[]? stops;
            var stopCnt = Math.Min(fill.GetColorStops(out stops), (uint)GlConstants.MAX_GRADIENT_STOPS);
            if (stopCnt < 2) return;

            GlRenderTarget? dstCopyFbo;
            var radial = fill.GetFillType() == Type.RadialGradient;
            var viewRegion = ViewportRegion(vp, bbox);

            RenderTypes taskType = RenderTypes.RT_None;
            var blendSource = BlendSource.LinearGradient;

            if (fill.GetFillType() == Type.LinearGradient)
            {
                taskType = RenderTypes.RT_LinGradient;
            }
            else if (radial)
            {
                taskType = RenderTypes.RT_RadGradient;
                blendSource = BlendSource.RadialGradient;
            }
            else return;

            var task = CreatePrimitiveTask(taskType, blendSource, viewRegion, out dstCopyFbo);

            task.SetViewMatrix(CurrentPass()!.GetViewMatrix());
            task.SetDrawDepth(depth);

            if (!sdata.geometry.Draw(task, mGpuBuffer, flag))
            {
                return;
            }

            task.SetViewport(viewRegion);

            var stencilMode = sdata.geometry.GetStencilMode(flag);
            var stencilTask = CreateStencilTask(task, stencilMode, depth);

            // transform buffer (inverse fill-space transform)
            var invMat3 = stackalloc float[(int)GlConstants.GL_MAT3_STD140_SIZE];
            TvgMath.Inverse(fill.GetTransform(), out var inv);
            TvgMath.Inverse(sdata.geometry.matrix, out var invShape);
            inv = TvgMath.Multiply(inv, invShape);
            GlMatrixHelper.GetMatrix3Std140(inv, new Span<float>(invMat3, (int)GlConstants.GL_MAT3_STD140_SIZE));

            var transformOffset = mGpuBuffer.Push(invMat3, GlConstants.GL_MAT3_STD140_BYTES, true);

            task.AddBindResource(new GlBindingResource(
                0,
                task.GetProgram()!.GetUniformBlockIndex("TransformInfo\0"u8),
                mGpuBuffer.GetBufferId(),
                transformOffset,
                GlConstants.GL_MAT3_STD140_BYTES));

            var alpha = sdata.opacity / 255f;

            if ((flag & RenderUpdateFlag.GradientStroke) != 0)
            {
                var strokeWidth = sdata.geometry.strokeRenderWidth;
                if (strokeWidth < GlConstants.MIN_GL_STROKE_WIDTH)
                {
                    alpha = strokeWidth / GlConstants.MIN_GL_STROKE_WIDTH;
                }
            }

            // gradient block
            GlBindingResource gradientBinding = default;
            var loc = task.GetProgram()!.GetUniformBlockIndex("GradientInfo\0"u8);

            if (fill.GetFillType() == Type.LinearGradient)
            {
                var linearFill = (LinearGradient)fill;

                GlLinearGradientBlock gradientBlock = default;

                gradientBlock.nStops[1] = NOISE_LEVEL;
                gradientBlock.nStops[2] = (int)fill.GetSpread() * 1.0f;
                uint nStops = 0;
                for (uint i = 0; i < stopCnt; ++i)
                {
                    if (i > 0 && gradientBlock.stopPoints[nStops - 1] > stops![i].offset) continue;

                    gradientBlock.stopPoints[i] = stops![i].offset;
                    gradientBlock.stopColors[i * 4 + 0] = stops[i].r / 255f;
                    gradientBlock.stopColors[i * 4 + 1] = stops[i].g / 255f;
                    gradientBlock.stopColors[i * 4 + 2] = stops[i].b / 255f;
                    gradientBlock.stopColors[i * 4 + 3] = stops[i].a / 255f * alpha;
                    nStops++;
                }
                gradientBlock.nStops[0] = nStops * 1.0f;

                linearFill.Linear(out var x1, out var y1, out var x2, out var y2);

                gradientBlock.startPos[0] = x1;
                gradientBlock.startPos[1] = y1;
                gradientBlock.stopPos[0] = x2;
                gradientBlock.stopPos[1] = y2;

                var blockData = stackalloc float[GlLinearGradientBlock.PackedSize];
                gradientBlock.PackInto(blockData);
                var gradOffset = mGpuBuffer.Push(blockData, (uint)(GlLinearGradientBlock.PackedSize * sizeof(float)), true);

                gradientBinding = new GlBindingResource(
                    2,
                    loc,
                    mGpuBuffer.GetBufferId(),
                    gradOffset,
                    (uint)(GlLinearGradientBlock.PackedSize * sizeof(float)));
            }
            else
            {
                var radialFill = (RadialGradient)fill;

                GlRadialGradientBlock gradientBlock = default;

                gradientBlock.nStops[1] = NOISE_LEVEL;
                gradientBlock.nStops[2] = (int)fill.GetSpread() * 1.0f;

                uint nStops = 0;
                for (uint i = 0; i < stopCnt; ++i)
                {
                    if (i > 0 && gradientBlock.stopPoints[nStops - 1] > stops![i].offset) continue;

                    gradientBlock.stopPoints[i] = stops![i].offset;
                    gradientBlock.stopColors[i * 4 + 0] = stops[i].r / 255f;
                    gradientBlock.stopColors[i * 4 + 1] = stops[i].g / 255f;
                    gradientBlock.stopColors[i * 4 + 2] = stops[i].b / 255f;
                    gradientBlock.stopColors[i * 4 + 3] = stops[i].a / 255f * alpha;
                    nStops++;
                }
                gradientBlock.nStops[0] = nStops * 1.0f;

                radialFill.Radial(out var cx, out var cy, out var r, out var fx, out var fy, out var fr);
                radialFill.Correct(ref fx, ref fy, ref fr);

                gradientBlock.centerPos[0] = fx;
                gradientBlock.centerPos[1] = fy;
                gradientBlock.centerPos[2] = cx;
                gradientBlock.centerPos[3] = cy;
                gradientBlock.radius[0] = fr;
                gradientBlock.radius[1] = r;

                var blockData = stackalloc float[GlRadialGradientBlock.PackedSize];
                gradientBlock.PackInto(blockData);
                var gradOffset = mGpuBuffer.Push(blockData, (uint)(GlRadialGradientBlock.PackedSize * sizeof(float)), true);

                gradientBinding = new GlBindingResource(
                    2,
                    loc,
                    mGpuBuffer.GetBufferId(),
                    gradOffset,
                    (uint)(GlRadialGradientBlock.PackedSize * sizeof(float)));
            }

            task.AddBindResource(gradientBinding);

            // TransformInfo uses slot 0 and GradientInfo uses slot 2, so BlendRegion moves to 3.
            BindBlendTarget(task, dstCopyFbo, viewRegion, 3);

            if (stencilTask != null)
            {
                CurrentPass()!.AddRenderTask(new GlStencilCoverTask(stencilTask, task, stencilMode));
            }
            else
            {
                CurrentPass()!.AddRenderTask(task);
            }
        }

        private void DrawClip(ref ValueList<object?> clips)
        {
            var identityVertex = stackalloc float[] { -1f, 1f, -1f, -1f, 1f, 1f, 1f, -1f };
            var identityIndex = stackalloc uint[] { 0, 1, 2, 2, 1, 3 };

            var identityVertexOffset = mGpuBuffer.Push(identityVertex, (uint)(8 * sizeof(float)));
            var identityIndexOffset = mGpuBuffer.PushIndex(identityIndex, (uint)(6 * sizeof(uint)));

            var clipDepths = clips.Count <= 64 ? stackalloc int[clips.Count] : new int[clips.Count];

            for (int i = clips.Count - 1; i >= 0; i--)
            {
                clipDepths[i] = CurrentPass()!.NextDrawDepth();
            }

            var vport = CurrentPass()!.GetViewport();
            var viewMatrix = CurrentPass()!.GetViewMatrix();

            for (int i = 0; i < clips.Count; ++i)
            {
                var sdata = (GlShape?)clips[i];
                if (sdata == null) continue;

                var clipTask = new GlRenderTask(mPrograms[(int)RenderTypes.RT_Stencil]);
                clipTask.SetDrawDepth(clipDepths[i]);
                clipTask.SetViewMatrix(viewMatrix);

                var flag = (sdata.geometry.stroke.vertex.count > 0) ? RenderUpdateFlag.Stroke : RenderUpdateFlag.Path;
                sdata.geometry.Draw(clipTask, mGpuBuffer, flag);

                var bboxClip = sdata.geometry.viewport;
                bboxClip.IntersectWith(vport);

                var cx = bboxClip.Sx() - vport.Sx();
                var cy = vport.Sh() - (bboxClip.Sy() - vport.Sy()) - bboxClip.Sh();
                clipTask.SetViewport(new RenderRegion(cx, cy, cx + bboxClip.Sw(), cy + bboxClip.Sh()));

                var maskTask = new GlRenderTask(mPrograms[(int)RenderTypes.RT_Stencil]);

                maskTask.SetDrawDepth(clipDepths[i]);
                maskTask.AddVertexLayout(new GlVertexLayout { index = 0, size = 2, stride = 2 * sizeof(float), offset = identityVertexOffset });
                maskTask.SetDrawRange(identityIndexOffset, 6);
                maskTask.SetViewport(new RenderRegion(0, 0, vport.Sw(), vport.Sh()));

                CurrentPass()!.AddRenderTask(new GlClipTask(clipTask, maskTask));
            }
        }

        internal GlRenderPass? CurrentPass()
        {
            if (mRenderPassStack.Count == 0) return null;
            return mRenderPassStack[mRenderPassStack.Count - 1];
        }

        private bool BeginComplexBlending(in RenderRegion passVp, RenderRegion bounds)
        {
            if (passVp.Invalid()) return false;

            bounds.IntersectWith(passVp);
            if (bounds.Invalid()) return false;

            if (mBlendMethod == BlendMethod.Normal) return false;

            if (mBlendPool.Count == 0) mBlendPool.Add(new GlRenderTargetPool(surface.w, surface.h));

            var blendFbo = mBlendPool[0].GetRenderTarget(bounds);

            mRenderPassStack.Add(new GlRenderPass(blendFbo));

            return true;
        }

        private void EndBlendingCompose(GlRenderTask stencilTask)
        {
            var blendPass = mRenderPassStack[mRenderPassStack.Count - 1];
            mRenderPassStack.RemoveAt(mRenderPassStack.Count - 1);

            blendPass.SetDrawDepth(CurrentPass()!.NextDrawDepth());

            var composeTask = blendPass.EndRenderPass<GlComposeTask>(null, CurrentPass()!.GetFboId());

            var blendVp = blendPass.GetViewport();
            if (mBlendPool.Count < 2) mBlendPool.Add(new GlRenderTargetPool(surface.w, surface.h));
            // Desktop GL path
            var dstCopyFbo = mBlendPool[1].GetRenderTarget(blendVp);

            var vpX = blendVp.Sx();
            var vpY = CurrentPass()!.GetViewport().Sh() - blendVp.Sy() - blendVp.Sh();
            stencilTask.SetViewport(new RenderRegion(vpX, vpY, vpX + blendVp.Sw(), vpY + blendVp.Sh()));

            stencilTask.SetDrawDepth(CurrentPass()!.NextDrawDepth());
            stencilTask.SetViewMatrix(CurrentPass()!.GetViewMatrix());

            var program = GetBlendProgram(mBlendMethod, BlendSource.Image);
            var blendTask = new GlComplexBlendTask(program, CurrentPass()!.GetFbo(), dstCopyFbo, stencilTask, composeTask);
            PrepareCmpTask(blendTask, blendVp, blendPass.GetFboWidth(), blendPass.GetFboHeight());
            blendTask.SetDrawDepth(CurrentPass()!.NextDrawDepth());

            // src and dst texture
            blendTask.AddBindResource(new GlBindingResource(1, blendPass.GetFbo()!.colorTex, blendTask.GetProgram()!.GetUniformLocation("uSrcTexture\0"u8)));
            blendTask.AddBindResource(new GlBindingResource(2, dstCopyFbo.colorTex, blendTask.GetProgram()!.GetUniformLocation("uDstTexture\0"u8)));

            CurrentPass()!.AddRenderTask(blendTask);
        }

        private static readonly string[] BlendShaderFuncs =
        {
            GlShaderSrc.NORMAL_BLEND_FRAG,
            GlShaderSrc.MULTIPLY_BLEND_FRAG,
            GlShaderSrc.SCREEN_BLEND_FRAG,
            GlShaderSrc.OVERLAY_BLEND_FRAG,
            GlShaderSrc.DARKEN_BLEND_FRAG,
            GlShaderSrc.LIGHTEN_BLEND_FRAG,
            GlShaderSrc.COLOR_DODGE_BLEND_FRAG,
            GlShaderSrc.COLOR_BURN_BLEND_FRAG,
            GlShaderSrc.HARD_LIGHT_BLEND_FRAG,
            GlShaderSrc.SOFT_LIGHT_BLEND_FRAG,
            GlShaderSrc.DIFFERENCE_BLEND_FRAG,
            GlShaderSrc.EXCLUSION_BLEND_FRAG,
            GlShaderSrc.HUE_BLEND_FRAG,
            GlShaderSrc.SATURATION_BLEND_FRAG,
            GlShaderSrc.COLOR_BLEND_FRAG,
            GlShaderSrc.LUMINOSITY_BLEND_FRAG,
            GlShaderSrc.ADD_BLEND_FRAG,
        };

        private GlProgram? GetBlendProgram(BlendMethod method, BlendSource source)
        {
            var shaderFunc = BlendShaderFuncs;

            int methodInd = (int)method;
            int shaderInd = methodInd;

            switch (source)
            {
                case BlendSource.Scene: shaderInd += (int)RenderTypes.RT_Blend_Scene_Normal; break;
                case BlendSource.Image: shaderInd += (int)RenderTypes.RT_Blend_Image_Normal; break;
                case BlendSource.Solid: shaderInd += (int)RenderTypes.RT_ShapeBlend_Solid_Normal; break;
                case BlendSource.LinearGradient: shaderInd += (int)RenderTypes.RT_ShapeBlend_Linear_Normal; break;
                case BlendSource.RadialGradient: shaderInd += (int)RenderTypes.RT_ShapeBlend_Radial_Normal; break;
            }

            if (mPrograms[shaderInd] != null) return mPrograms[shaderInd];

            var helpers = "";
            if (method == BlendMethod.Hue)
            {
                helpers = GlShaderSrc.BLEND_FRAG_HUE;
            }
            else if (method == BlendMethod.Saturation ||
                     method == BlendMethod.Color ||
                     method == BlendMethod.Luminosity)
            {
                helpers = GlShaderSrc.BLEND_FRAG_LUM;
            }

            string vertShader;
            string fragShader;

            if (source == BlendSource.Scene || source == BlendSource.Image)
            {
                vertShader = GlShaderSrc.BLIT_VERT_SHADER;
                var header = (source == BlendSource.Scene) ? GlShaderSrc.BLEND_SCENE_FRAG_HEADER : GlShaderSrc.BLEND_IMAGE_FRAG_HEADER;
                fragShader = string.Concat(header, helpers, shaderFunc[methodInd]);
                mPrograms[shaderInd] = new GlProgram(vertShader, fragShader);
                return mPrograms[shaderInd];
            }

            vertShader = (source == BlendSource.Solid) ? GlShaderSrc.COLOR_VERT_SHADER : GlShaderSrc.GRADIENT_VERT_SHADER;
            switch (source)
            {
                case BlendSource.Solid:
                    fragShader = string.Concat(
                        GlShaderSrc.BLEND_SHAPE_SOLID_FRAG_HEADER,
                        helpers,
                        shaderFunc[methodInd]);
                    break;
                case BlendSource.LinearGradient:
                    fragShader = string.Concat(
                        GlShaderSrc.STR_GRADIENT_FRAG_COMMON_VARIABLES,
                        GlShaderSrc.STR_LINEAR_GRADIENT_VARIABLES,
                        GlShaderSrc.STR_GRADIENT_FRAG_COMMON_FUNCTIONS,
                        GlShaderSrc.STR_LINEAR_GRADIENT_FUNCTIONS,
                        GlShaderSrc.BLEND_SHAPE_LINEAR_FRAG_HEADER,
                        helpers,
                        shaderFunc[methodInd]);
                    break;
                case BlendSource.RadialGradient:
                    fragShader = string.Concat(
                        GlShaderSrc.STR_GRADIENT_FRAG_COMMON_VARIABLES,
                        GlShaderSrc.STR_RADIAL_GRADIENT_VARIABLES,
                        GlShaderSrc.STR_GRADIENT_FRAG_COMMON_FUNCTIONS,
                        GlShaderSrc.STR_RADIAL_GRADIENT_FUNCTIONS,
                        GlShaderSrc.BLEND_SHAPE_RADIAL_FRAG_HEADER,
                        helpers,
                        shaderFunc[methodInd]);
                    break;
                default:
                    TvgCommon.TVGERR("RENDERER", "Unsupported blend source!");
                    return null;
            }

            mPrograms[shaderInd] = new GlProgram(vertShader, fragShader);
            return mPrograms[shaderInd];
        }

        private void PrepareBlitTask(GlBlitTask task)
        {
            PrepareCmpTask(task, new RenderRegion(0, 0, (int)surface.w, (int)surface.h), surface.w, surface.h);
            task.AddBindResource(new GlBindingResource(0, task.GetColorTexture(), task.GetProgram()!.GetUniformLocation("uSrcTexture\0"u8)));
        }

        private void PrepareCmpTask(GlRenderTask task, in RenderRegion vp, uint cmpWidth, uint cmpHeight)
        {
            var passVp = CurrentPass()!.GetViewport();

            var taskVp = vp;
            taskVp.IntersectWith(passVp);

            var x = taskVp.Sx() - passVp.Sx();
            var y = taskVp.Sy() - passVp.Sy();
            var w = taskVp.Sw();
            var h = taskVp.Sh();

            var rw = (float)passVp.W();
            var rh = (float)passVp.H();

            var l = (float)x;
            var t = rh - y;
            var r = (float)(x + w);
            var b = rh - y - h;

            // map vp ltrp to -1:1
            var left = (l / rw) * 2f - 1f;
            var top = (t / rh) * 2f - 1f;
            var right = (r / rw) * 2f - 1f;
            var bottom = (b / rh) * 2f - 1f;

            var uw = (float)w / (float)cmpWidth;
            var uh = (float)h / (float)cmpHeight;

            var vertices = stackalloc float[16];
            vertices[0] = left;  vertices[1] = top;     vertices[2] = 0f; vertices[3] = uh;
            vertices[4] = left;  vertices[5] = bottom;  vertices[6] = 0f; vertices[7] = 0f;
            vertices[8] = right; vertices[9] = top;     vertices[10] = uw; vertices[11] = uh;
            vertices[12] = right; vertices[13] = bottom; vertices[14] = uw; vertices[15] = 0f;
            var indices = stackalloc uint[] { 0, 1, 2, 2, 1, 3 };
            var vertexOffset = mGpuBuffer.Push(vertices, (uint)(16 * sizeof(float)));
            var indexOffset = mGpuBuffer.PushIndex(indices, (uint)(6 * sizeof(uint)));

            task.AddVertexLayout(new GlVertexLayout { index = 0, size = 2, stride = 4 * sizeof(float), offset = vertexOffset });
            task.AddVertexLayout(new GlVertexLayout { index = 1, size = 2, stride = 4 * sizeof(float), offset = vertexOffset + 2 * sizeof(float) });
            task.SetDrawRange(indexOffset, 6);
            y = passVp.Sh() - y - h;
            task.SetViewport(new RenderRegion(x, y, x + w, y + h));
        }

        private void EndRenderPassInternal(RenderCompositor cmp)
        {
            var glCmp = (GlCompositor)cmp;

            // setup masking and blending render pass configurations
            if ((glCmp.flags & (CompositionFlag.Blending | CompositionFlag.Masking)) == (CompositionFlag.Blending | CompositionFlag.Masking))
            {
                // rearrange render tree
                var selfPass = mRenderPassStack[mRenderPassStack.Count - 1];
                mRenderPassStack.RemoveAt(mRenderPassStack.Count - 1);
                var prevPass = mRenderPassStack[mRenderPassStack.Count - 1];
                mRenderPassStack.RemoveAt(mRenderPassStack.Count - 1);
                var maskPass = mRenderPassStack[mRenderPassStack.Count - 1];
                mRenderPassStack.RemoveAt(mRenderPassStack.Count - 1);
                mRenderPassStack.Add(prevPass);
                mRenderPassStack.Add(maskPass);
                mRenderPassStack.Add(selfPass);
                // setup composition properties
                var prevCompose = mComposeStack[mComposeStack.Count - 1];
                var opacity = glCmp.opacity;
                var blendMethod = glCmp.blendMethod;
                // self scene task must be masked but not blended
                glCmp.method = prevCompose.method;
                glCmp.opacity = 255;
                glCmp.blendMethod = BlendMethod.Normal;
                // prev scene task must be blended but not masked
                prevCompose.method = MaskMethod.None;
                prevCompose.opacity = opacity;
                prevCompose.blendMethod = blendMethod;
            }

            if (cmp.method != MaskMethod.None)
            {
                var selfPass = mRenderPassStack[mRenderPassStack.Count - 1];
                mRenderPassStack.RemoveAt(mRenderPassStack.Count - 1);

                // mask is pushed first
                var maskPass = mRenderPassStack[mRenderPassStack.Count - 1];
                mRenderPassStack.RemoveAt(mRenderPassStack.Count - 1);

                GlProgram? program = null;
                switch (cmp.method)
                {
                    case MaskMethod.Alpha: program = mPrograms[(int)RenderTypes.RT_MaskAlpha]; break;
                    case MaskMethod.InvAlpha: program = mPrograms[(int)RenderTypes.RT_MaskAlphaInv]; break;
                    case MaskMethod.Luma: program = mPrograms[(int)RenderTypes.RT_MaskLuma]; break;
                    case MaskMethod.InvLuma: program = mPrograms[(int)RenderTypes.RT_MaskLumaInv]; break;
                    case MaskMethod.Add: program = mPrograms[(int)RenderTypes.RT_MaskAdd]; break;
                    case MaskMethod.Subtract: program = mPrograms[(int)RenderTypes.RT_MaskSub]; break;
                    case MaskMethod.Intersect: program = mPrograms[(int)RenderTypes.RT_MaskIntersect]; break;
                    case MaskMethod.Difference: program = mPrograms[(int)RenderTypes.RT_MaskDifference]; break;
                    case MaskMethod.Lighten: program = mPrograms[(int)RenderTypes.RT_MaskLighten]; break;
                    case MaskMethod.Darken: program = mPrograms[(int)RenderTypes.RT_MaskDarken]; break;
                    default: break;
                }

                if (program != null && !selfPass.IsEmpty() && !maskPass.IsEmpty())
                {
                    var prevTask = maskPass.EndRenderPass<GlComposeTask>(null, CurrentPass()!.GetFboId());
                    prevTask.SetDrawDepth(CurrentPass()!.NextDrawDepth());
                    prevTask.SetRenderSize(glCmp.bbox.W(), glCmp.bbox.H());
                    prevTask.SetViewport(glCmp.bbox);

                    var composeTask = selfPass.EndRenderPass<GlDrawBlitTask>(program, CurrentPass()!.GetFboId());
                    composeTask.SetRenderSize(glCmp.bbox.W(), glCmp.bbox.H());
                    composeTask.SetPrevTask(prevTask);

                    PrepareCmpTask(composeTask, glCmp.bbox, selfPass.GetFboWidth(), selfPass.GetFboHeight());

                    composeTask.AddBindResource(new GlBindingResource(0, selfPass.GetTextureId(), program.GetUniformLocation("uSrcTexture\0"u8)));
                    composeTask.AddBindResource(new GlBindingResource(1, maskPass.GetTextureId(), program.GetUniformLocation("uMaskTexture\0"u8)));

                    composeTask.SetDrawDepth(CurrentPass()!.NextDrawDepth());
                    composeTask.SetParentSize(CurrentPass()!.GetViewport().W(), CurrentPass()!.GetViewport().H());
                    CurrentPass()!.AddRenderTask(composeTask);
                }
            }
            else if (glCmp.blendMethod != BlendMethod.Normal)
            {
                var renderPass = mRenderPassStack[mRenderPassStack.Count - 1];
                mRenderPassStack.RemoveAt(mRenderPassStack.Count - 1);

                if (!renderPass.IsEmpty())
                {
                    if (mBlendPool.Count < 1) mBlendPool.Add(new GlRenderTargetPool(surface.w, surface.h));
                    if (mBlendPool.Count < 2) mBlendPool.Add(new GlRenderTargetPool(surface.w, surface.h));
                    // Desktop GL path
                    var dstCopyFbo = mBlendPool[1].GetRenderTarget(renderPass.GetViewport());
                    // image info
                    var info = stackalloc uint[4];
                    info[0] = (uint)ColorSpace.ABGR8888; info[1] = 0; info[2] = cmp.opacity; info[3] = 0;

                    var program = GetBlendProgram(glCmp.blendMethod, BlendSource.Scene);
                    var blendTask = renderPass.EndRenderPass<GlSceneBlendTask>(program, CurrentPass()!.GetFboId());
                    blendTask.SetSrcTarget(CurrentPass()!.GetFbo());
                    blendTask.SetDstCopy(dstCopyFbo);
                    blendTask.SetRenderSize(glCmp.bbox.W(), glCmp.bbox.H());
                    PrepareCmpTask(blendTask, glCmp.bbox, renderPass.GetFboWidth(), renderPass.GetFboHeight());
                    blendTask.SetDrawDepth(CurrentPass()!.NextDrawDepth());

                    // info
                    var infoOffset = mGpuBuffer.Push(info, (uint)(4 * sizeof(uint)), true);
                    blendTask.AddBindResource(new GlBindingResource(0, blendTask.GetProgram()!.GetUniformBlockIndex("ColorInfo\0"u8), mGpuBuffer.GetBufferId(), infoOffset, (uint)(4 * sizeof(uint))));
                    // textures
                    blendTask.AddBindResource(new GlBindingResource(0, renderPass.GetTextureId(), blendTask.GetProgram()!.GetUniformLocation("uSrcTexture\0"u8)));
                    blendTask.AddBindResource(new GlBindingResource(1, dstCopyFbo.colorTex, blendTask.GetProgram()!.GetUniformLocation("uDstTexture\0"u8)));
                    blendTask.SetParentSize(CurrentPass()!.GetViewport().W(), CurrentPass()!.GetViewport().H());
                    CurrentPass()!.AddRenderTask(blendTask);
                }
            }
            else
            {
                var renderPass = mRenderPassStack[mRenderPassStack.Count - 1];
                mRenderPassStack.RemoveAt(mRenderPassStack.Count - 1);

                if (!renderPass.IsEmpty())
                {
                    var drawBlitTask = renderPass.EndRenderPass<GlDrawBlitTask>(mPrograms[(int)RenderTypes.RT_Image], CurrentPass()!.GetFboId());
                    drawBlitTask.SetRenderSize(glCmp.bbox.W(), glCmp.bbox.H());
                    PrepareCmpTask(drawBlitTask, glCmp.bbox, renderPass.GetFboWidth(), renderPass.GetFboHeight());
                    drawBlitTask.SetDrawDepth(CurrentPass()!.NextDrawDepth());
                    drawBlitTask.SetViewMatrix(TvgMath.Identity());

                    // image info
                    var info = stackalloc uint[4];
                    info[0] = (uint)ColorSpace.ABGR8888; info[1] = 0; info[2] = cmp.opacity; info[3] = 0;
                    var infoOffset = mGpuBuffer.Push(info, (uint)(4 * sizeof(uint)), true);

                    drawBlitTask.AddBindResource(new GlBindingResource(
                        1,
                        drawBlitTask.GetProgram()!.GetUniformBlockIndex("ColorInfo\0"u8),
                        mGpuBuffer.GetBufferId(),
                        infoOffset,
                        (uint)(4 * sizeof(uint))));

                    // texture id
                    drawBlitTask.AddBindResource(new GlBindingResource(0, renderPass.GetTextureId(), drawBlitTask.GetProgram()!.GetUniformLocation("uTexture\0"u8)));
                    drawBlitTask.SetParentSize(CurrentPass()!.GetViewport().W(), CurrentPass()!.GetViewport().H());
                    CurrentPass()!.AddRenderTask(drawBlitTask);
                }
            }
        }

        /************************************************************************/
        /* Gradient block packing helpers                                       */
        /************************************************************************/
        // Pack methods moved to GlLinearGradientBlock.PackInto / GlRadialGradientBlock.PackInto

        /************************************************************************/
        /* External Class Implementation (RenderMethod overrides)               */
        /************************************************************************/

        public override bool Clear()
        {
            if (mRootTarget.Invalid()) return false;
            mClearBuffer = true;
            return true;
        }

        public bool Target(nint display, nint surfaceHandle, nint context, int id, uint w, uint h, ColorSpace cs)
        {
            // assume the context zero is invalid
            if (context == 0 || w == 0 || h == 0) return false;

            if (mContext != 0)
            {
                CurrentContext();
                if (mContext != context) mTextures.Clear();
            }

            Flush();

            surface.stride = w;
            surface.w = w;
            surface.h = h;
            surface.cs = cs;

            mDisplay = display;
            mSurface = surfaceHandle;
            mContext = context;
            mTargetFboId = id;

            var ret = CurrentContext();

            mRootTarget.viewport = new RenderRegion(0, 0, (int)surface.w, (int)surface.h);
            mRootTarget.Init(surface.w, surface.h, mTargetFboId);

            return ret;
        }

        public override bool Sync()
        {
            // nothing to be done.
            if (mRenderPassStack.Count == 0) return true;

            CurrentContext();

            // Blend function for straight alpha
            GL.glBlendFunc(GL.GL_ONE, GL.GL_ONE_MINUS_SRC_ALPHA);
            GL.glEnable(GL.GL_BLEND);
            GL.glEnable(GL.GL_SCISSOR_TEST);
            GL.glCullFace(GL.GL_FRONT_AND_BACK);
            GL.glFrontFace(GL.GL_CCW);
            GL.glEnable(GL.GL_DEPTH_TEST);
            GL.glDepthFunc(GL.GL_GREATER);

            var task = mRenderPassStack[0].EndRenderPass<GlBlitTask>(mPrograms[(int)RenderTypes.RT_Blit], (uint)mTargetFboId);

            PrepareBlitTask(task);

            task.mClearBuffer = mClearBuffer;
            task.SetTargetViewport(new RenderRegion(0, 0, (int)surface.w, (int)surface.h));

            if (mGpuBuffer.FlushToGPU())
            {
                mGpuBuffer.Bind();
                task.Run();
            }

            mGpuBuffer.Unbind();

            GL.glDisable(GL.GL_SCISSOR_TEST);

            ClearDisposes();

            // Reset clear buffer flag to default (false) after use.
            mClearBuffer = false;

            return true;
        }

        public override bool Bounds(object? data, Point[] pt4, in Matrix m)
        {
            if (data != null)
            {
                var sdata = (GlShape)data;
                if (sdata.validStroke)
                {
                    var bbox = new BBox();
                    bbox.Init();
                    var vertexes = sdata.geometry.stroke.vertex;
                    if (TvgMath.MatrixEqual(m, sdata.geometry.matrix))
                    {
                        // Common AABB path: stroke vertices are already in world space.
                        for (uint i = 0; i < vertexes.count / 2; i++)
                        {
                            var vert = new Point(vertexes[i * 2 + 0], vertexes[i * 2 + 1]);
                            bbox.min = new Point(MathF.Min(bbox.min.x, vert.x), MathF.Min(bbox.min.y, vert.y));
                            bbox.max = new Point(MathF.Max(bbox.max.x, vert.x), MathF.Max(bbox.max.y, vert.y));
                        }
                    }
                    else
                    {
                        // GL stroke vertices are generated in world space.
                        var inverseModel = sdata.geometry.InverseMatrix();

                        for (uint i = 0; i < vertexes.count / 2; i++)
                        {
                            var vert = new Point(vertexes[i * 2 + 0], vertexes[i * 2 + 1]);
                            vert = TvgMath.Transform(vert, TvgMath.Multiply(inverseModel, m));
                            bbox.min = new Point(MathF.Min(bbox.min.x, vert.x), MathF.Min(bbox.min.y, vert.y));
                            bbox.max = new Point(MathF.Max(bbox.max.x, vert.x), MathF.Max(bbox.max.y, vert.y));
                        }
                    }
                    pt4[0] = bbox.min;
                    pt4[1] = new Point(bbox.max.x, bbox.min.y);
                    pt4[2] = bbox.max;
                    pt4[3] = new Point(bbox.min.x, bbox.max.y);
                    return true;
                }
            }
            return false;
        }

        public override RenderRegion Region(object? data)
        {
            if (data == null) return default;
            var shape = (GlShape)data;
            return shape.geometry.GetBounds();
        }

        public override bool PreRender()
        {
            if (mRootTarget.Invalid()) return false;

            CurrentContext();
            if (mPrograms.Count == 0) InitShaders();
            mRenderPassStack.Add(new GlRenderPass((GlRenderTarget?)mRootTarget));

            return true;
        }

        public override bool PostRender()
        {
            return true;
        }

        public override RenderCompositor? Target(in RenderRegion region, ColorSpace cs, CompositionFlag flags)
        {
            var vp = region;
            if (CurrentPass() == null || CurrentPass()!.IsEmpty()) return null;

            vp.IntersectWith(CurrentPass()!.GetViewport());

            mComposeStack.Add(new GlCompositor(vp, flags));
            return mComposeStack[mComposeStack.Count - 1];
        }

        public override bool BeginComposite(RenderCompositor? cmp, MaskMethod method, byte opacity)
        {
            if (cmp == null) return false;

            var glCmp = (GlCompositor)cmp;
            glCmp.method = method;
            glCmp.opacity = opacity;
            glCmp.blendMethod = mBlendMethod;

            var index = mRenderPassStack.Count - 1;
            if (index >= mComposePool.Count) mComposePool.Add(new GlRenderTargetPool(surface.w, surface.h));

            if (glCmp.bbox.Valid()) mRenderPassStack.Add(new GlRenderPass(mComposePool[index].GetRenderTarget(glCmp.bbox)));
            else mRenderPassStack.Add(new GlRenderPass((GlRenderTarget?)null));

            return true;
        }

        public override bool EndComposite(RenderCompositor? cmp)
        {
            if (mComposeStack.Count == 0) return false;
            if (mComposeStack[mComposeStack.Count - 1] != cmp) return false;

            // end current render pass
            var curCmp = mComposeStack[mComposeStack.Count - 1];
            mComposeStack.RemoveAt(mComposeStack.Count - 1);

            EndRenderPassInternal(curCmp);

            return true;
        }

        public override void Prepare(RenderEffect effect, in Matrix transform)
        {
            // we must be sure, that we have intermediate FBOs
            if (mBlendPool.Count < 1) mBlendPool.Add(new GlRenderTargetPool(surface.w, surface.h));
            if (mBlendPool.Count < 2) mBlendPool.Add(new GlRenderTargetPool(surface.w, surface.h));

            mEffect.Update(effect, transform);
        }

        public override bool Region(RenderEffect effect)
        {
            return mEffect.Region(effect);
        }

        public override bool Render(RenderCompositor? cmp, RenderEffect effect, bool direct)
        {
            return mEffect.Render(effect, CurrentPass()!, mBlendPool);
        }

        public override void Dispose(RenderEffect effect)
        {
            effect.rd = null;
        }

        public override ColorSpace ColorSpaceValue()
        {
            return surface.cs;
        }

        public override RenderSurface? MainSurface()
        {
            return surface;
        }

        public override bool Blend(BlendMethod method)
        {
            if (method == mBlendMethod) return true;
            mBlendMethod = (method == BlendMethod.Composition ? BlendMethod.Normal : method);
            return true;
        }

        public override bool RenderImage(object? data)
        {
            var sdata = data as GlShape;
            if (sdata == null) return false;

            if (CurrentPass()!.IsEmpty() || !sdata.validFill) return true;

            var vp = CurrentPass()!.GetViewport();
            var bbox = sdata.geometry.viewport;
            bbox.IntersectWith(vp);
            if (bbox.Invalid()) return true;

            var x = bbox.Sx() - vp.Sx();
            var y = bbox.Sy() - vp.Sy();
            var drawDepth = CurrentPass()!.NextDrawDepth();

            if (sdata.clips.Count > 0) DrawClip(ref sdata.clips);

            var task = new GlRenderTask(mPrograms[(int)RenderTypes.RT_Image]);
            task.SetDrawDepth(drawDepth);

            if (!sdata.geometry.Draw(task, mGpuBuffer, RenderUpdateFlag.Image))
            {
                return true;
            }

            bool complexBlend = BeginComplexBlending(bbox, sdata.geometry.GetBounds());
            if (complexBlend) vp = CurrentPass()!.GetViewport();
            task.SetViewMatrix(CurrentPass()!.GetViewMatrix());

            // image info
            var info = stackalloc uint[4];
            info[0] = (uint)sdata.texColorSpace; info[1] = sdata.texFlipY; info[2] = sdata.opacity; info[3] = 0;
            var infoOffset = mGpuBuffer.Push(info, (uint)(4 * sizeof(uint)), true);

            task.AddBindResource(new GlBindingResource(
                1,
                task.GetProgram()!.GetUniformBlockIndex("ColorInfo\0"u8),
                mGpuBuffer.GetBufferId(),
                infoOffset,
                (uint)(4 * sizeof(uint))));

            // texture id
            task.AddBindResource(new GlBindingResource(0, sdata.texId, task.GetProgram()!.GetUniformLocation("uTexture\0"u8)));

            y = vp.Sh() - y - bbox.Sh();
            var x2 = x + bbox.Sw();
            var y2 = y + bbox.Sh();

            task.SetViewport(new RenderRegion(x, y, x2, y2));

            CurrentPass()!.AddRenderTask(task);

            if (complexBlend)
            {
                var stencilTask = new GlRenderTask(mPrograms[(int)RenderTypes.RT_Stencil]);
                sdata.geometry.Draw(stencilTask, mGpuBuffer, RenderUpdateFlag.Image);
                EndBlendingCompose(stencilTask);
            }

            return true;
        }

        public override bool RenderShape(object? data)
        {
            var sdata = data as GlShape;
            if (sdata == null) return true;
            if (CurrentPass()!.IsEmpty() || (!sdata.validFill && !sdata.validStroke)) return true;

            var bbox = sdata.geometry.viewport;
            bbox.IntersectWith(CurrentPass()!.GetViewport());
            if (bbox.Invalid()) return true;

            int drawDepth1 = 0, drawDepth2 = 0;
            if (sdata.validFill) drawDepth1 = CurrentPass()!.NextDrawDepth();
            if (sdata.validStroke) drawDepth2 = CurrentPass()!.NextDrawDepth();

            if (sdata.clips.Count > 0) DrawClip(ref sdata.clips);

            if (sdata.rshape != null && sdata.rshape.StrokeFirst())
            {
                // stroke first
                if (sdata.validStroke)
                {
                    if (sdata.rshape.StrokeFillGradient() != null)
                        DrawPrimitive(sdata, sdata.rshape.StrokeFillGradient()!, RenderUpdateFlag.GradientStroke, drawDepth2);
                    else if (sdata.rshape.stroke != null && sdata.rshape.stroke.color.a > 0)
                        DrawPrimitive(sdata, sdata.rshape.stroke.color, RenderUpdateFlag.Stroke, drawDepth2);
                }
                // then fill
                if (sdata.validFill)
                {
                    if (sdata.rshape.fill != null)
                        DrawPrimitive(sdata, sdata.rshape.fill, RenderUpdateFlag.Gradient, drawDepth1);
                    else if (sdata.rshape.color.a > 0)
                        DrawPrimitive(sdata, sdata.rshape.color, RenderUpdateFlag.Color, drawDepth1);
                }
            }
            else
            {
                // fill first
                if (sdata.validFill)
                {
                    if (sdata.rshape?.fill != null)
                        DrawPrimitive(sdata, sdata.rshape.fill, RenderUpdateFlag.Gradient, drawDepth1);
                    else if (sdata.rshape != null && sdata.rshape.color.a > 0)
                        DrawPrimitive(sdata, sdata.rshape.color, RenderUpdateFlag.Color, drawDepth1);
                }
                // then stroke
                if (sdata.validStroke)
                {
                    if (sdata.rshape?.StrokeFillGradient() != null)
                        DrawPrimitive(sdata, sdata.rshape.StrokeFillGradient()!, RenderUpdateFlag.GradientStroke, drawDepth2);
                    else if (sdata.rshape?.stroke != null && sdata.rshape.stroke.color.a > 0)
                        DrawPrimitive(sdata, sdata.rshape.stroke.color, RenderUpdateFlag.Stroke, drawDepth2);
                }
            }

            return true;
        }

        public override void Dispose(object? data)
        {
            var sdata = data as GlShape;
            if (sdata == null) return;
            var ownsTexture = sdata.texId != 0 && (sdata.texStamp == mTextures.stamp);
            if (ownsTexture) DisposeTexture(mTextures.Release(sdata.texSource, sdata.texFilter, sdata.texId));
        }

        public override object? Prepare(RenderSurface image, object? data, in Matrix transform, ref ValueList<object?> clips, byte opacity, FilterMethod filter, RenderUpdateFlag flags)
        {
            // TODO: redefine GlImage.
            if (opacity == 0) return data;

            var sdata = data as GlShape;
            if (sdata == null) sdata = new GlShape();

            var cacheStale = sdata.texId != 0 && (sdata.texStamp != mTextures.stamp);
            if (flags == RenderUpdateFlag.None && !cacheStale) return data;

            sdata.validFill = false;

            sdata.viewWd = (float)surface.w;
            sdata.viewHt = (float)surface.h;

            var sourceChanged = !ReferenceEquals(sdata.texSource, image) || (sdata.texFilter != filter);
            if (sdata.texId == 0 || sourceChanged || cacheStale)
            {
                var ownsTexture = sdata.texId != 0 && (sdata.texStamp == mTextures.stamp);
                if (ownsTexture) DisposeTexture(mTextures.Release(sdata.texSource, sdata.texFilter, sdata.texId));
                sdata.texId = mTextures.Retain(image, filter);
                sdata.texSource = image;
                sdata.texFilter = filter;
                sdata.texStamp = mTextures.stamp;
                sdata.geometry = new GlGeometry();
            }

            sdata.texColorSpace = image.cs;
            sdata.texFlipY = 1;
            sdata.opacity = opacity;
            sdata.geometry.SetMatrix(transform);
            sdata.geometry.viewport = vport;
            sdata.geometry.TesselateImage(image);
            sdata.validFill = true;

            if ((flags & RenderUpdateFlag.Clip) != 0)
            {
                sdata.clips.CopyFrom(ref clips);
            }

            return sdata;
        }

        public override object? Prepare(RenderShape rshape, object? data, in Matrix transform, ref ValueList<object?> clips, byte opacity, RenderUpdateFlag flags, bool clipper)
        {
            var sdata = data as GlShape;
            if (sdata == null)
            {
                sdata = new GlShape();
                sdata.rshape = rshape;
                flags = RenderUpdateFlag.All;
            }

            if ((opacity == 0 && !clipper) || flags == RenderUpdateFlag.None) return sdata;

            sdata.viewWd = (float)surface.w;
            sdata.viewHt = (float)surface.h;
            sdata.opacity = opacity;

            if ((flags & RenderUpdateFlag.Path) != 0) sdata.geometry = new GlGeometry();

            sdata.geometry.SetMatrix(transform);
            sdata.geometry.viewport = vport;
            if ((flags & (RenderUpdateFlag.Path | RenderUpdateFlag.Transform)) != 0) sdata.geometry.Prepare(rshape);

            // TODO: Please precisely update tessellation not to update only if the color is changed.
            if ((flags & (RenderUpdateFlag.Color | RenderUpdateFlag.Gradient | RenderUpdateFlag.Transform | RenderUpdateFlag.Path)) != 0)
            {
                sdata.validFill = false;
                float opacityMultiplier = 1.0f;
                if (sdata.geometry.TesselateShape(sdata.rshape!, ref opacityMultiplier))
                {
                    sdata.opacity = (uint)(sdata.opacity * opacityMultiplier);
                    sdata.validFill = true;
                }
            }

            // TODO: Please precisely update tessellation not to update only if the color is changed.
            if ((flags & (RenderUpdateFlag.Color | RenderUpdateFlag.Stroke | RenderUpdateFlag.GradientStroke | RenderUpdateFlag.Transform | RenderUpdateFlag.Path)) != 0)
            {
                sdata.validStroke = false;
                if (sdata.geometry.TesselateStroke(sdata.rshape!)) sdata.validStroke = true;
            }

            if ((flags & RenderUpdateFlag.Clip) != 0)
            {
                sdata.clips.CopyFrom(ref clips);
            }

            return sdata;
        }

        public override bool PreUpdate()
        {
            if (mRootTarget.Invalid()) return false;
            CurrentContext();
            return true;
        }

        public override bool PostUpdate()
        {
            return true;
        }

        public override void Damage(object? rd, in RenderRegion region)
        {
            // TODO
        }

        public override bool Partial(bool disable)
        {
            // TODO
            return false;
        }

        public override bool IntersectsShape(object? data, in RenderRegion region)
        {
            if (data == null) return false;
            var shape = (GlShape)data;
            var bbox = shape.geometry.GetBounds();
            if (region.Intersected(bbox))
            {
                if (region.Contained(bbox)) return true;
                var intersector = new GlIntersector();
                return intersector.IntersectShape(RenderRegion.Intersect(region, bbox), shape);
            }
            return false;
        }

        public override bool IntersectsImage(object? data, in RenderRegion region)
        {
            if (data == null) return false;
            var shape = (GlShape)data;
            var bbox = shape.geometry.GetBounds();
            if (region.Intersected(bbox))
            {
                if (region.Contained(bbox)) return true;
                var intersector = new GlIntersector();
                if (intersector.IntersectImage(RenderRegion.Intersect(region, bbox), shape)) return true;
            }
            return false;
        }

        /************************************************************************/
        /* Static factory / term                                                */
        /************************************************************************/

        public static bool Term()
        {
            lock (_rendererMtx)
            {
                if (_rendererCnt > 0) return false;

                GL.glTerm();

                _rendererCnt = -1;
            }

            return true;
        }

        public static GlRenderer? Gen(uint threads, EngineOption op = EngineOption.Default)
        {
            // initialize engine
            lock (_rendererMtx)
            {
                if (_rendererCnt == -1)
                {
                    if (!GL.glInit())
                    {
                        TvgCommon.TVGERR("GL_ENGINE", "Failed GL initialization!");
                        return null;
                    }
                    _rendererCnt = 0;
                }
                ++_rendererCnt;
            }

            return new GlRenderer();
        }
    }
}
