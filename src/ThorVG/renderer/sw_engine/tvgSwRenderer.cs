// Ported from ThorVG/src/renderer/sw_engine/tvgSwRenderer.cpp

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static ThorVG.SwHelper;
using static ThorVG.SwBlendOps;
using static ThorVG.SwRaster;

namespace ThorVG
{
    // =====================================================================
    //  Task types (synchronous in C# port)
    // =====================================================================

    public class SwTask
    {
        public SwSurface? surface;
        public SwMpool? mpool;
        public RenderRegion clipBox;
        public RenderRegion curBox;
        public RenderRegion prvBox;
        public Matrix transform;
        public ValueList<object?> clips;
        public RenderDirtyRegion? dirtyRegion;
        public RenderUpdateFlag[] flags = new RenderUpdateFlag[2];
        public byte opacity;
        public bool pushed;
        public bool disposed;
        public bool nodirty;
        public bool valid;

        public RenderRegion Bounds()
        {
            Done();
            return curBox;
        }

        public void Invisible()
        {
            curBox.Reset();
            if (!nodirty) dirtyRegion?.Add(prvBox, curBox);
        }

        public bool Ready(bool condition)
        {
            if (condition)
            {
                if ((flags[0] & RenderUpdateFlag.Color) != 0) Invisible();
                flags[1] = flags[0];
                return true;
            }
            flags[0] |= flags[1];
            flags[1] = RenderUpdateFlag.None;
            return false;
        }

        public virtual void Run(uint tid) { }
        public virtual void DisposeTask() { }
        public virtual bool Clip(ref SwRle target) { return false; }
        public void Done() { /* synchronous - no-op */ }
    }

    public class SwShapeTask : SwTask
    {
        public SwShape shape = new SwShape();
        public RenderShape? rshape;
        public bool clipper;

        private bool Antialiasing(float strokeWidth)
        {
            return strokeWidth < 2.0f || (rshape?.stroke?.dashCount > 0) || (rshape?.stroke?.first == true) || (rshape?.Trimpath() == true) || (rshape?.stroke?.color.a < 255);
        }

        public float ValidStrokeWidth(bool clipper)
        {
            if (rshape?.stroke == null || TvgMath.Zero(rshape.stroke.width)) return 0.0f;
            if (!clipper && (rshape.stroke.fill == null && rshape.stroke.color.a == 0)) return 0.0f;
            if (TvgMath.Zero(rshape.stroke.trim.begin - rshape.stroke.trim.end)) return 0.0f;
            return rshape.stroke.width * (float)Math.Sqrt(transform.e11 * transform.e11 + transform.e12 * transform.e12);
        }

        public override bool Clip(ref SwRle target)
        {
            if (shape.hasStrokeRle)
            {
                return SwRleOps.rleClip(ref target, shape.strokeRle);
            }
            if (shape.fastTrack) return SwRleOps.rleClip(ref target, curBox);
            if (shape.hasRle)
            {
                return SwRleOps.rleClip(ref target, shape.rle);
            }
            return false;
        }

        public override void Run(uint tid)
        {
            var strokeWidth = ValidStrokeWidth(clipper);
            var updateShape = (flags[0] & (RenderUpdateFlag.Path | RenderUpdateFlag.Transform | RenderUpdateFlag.Clip)) != 0;
            var updateFill = (flags[0] & (RenderUpdateFlag.Color | RenderUpdateFlag.Gradient)) != 0;

            // Gradient fill parameters depend on the transform, so recompute when transform changes.
            // Also handles shapes that were previously fully clipped (shape.fill == null).
            if (!updateFill && updateShape && rshape!.fill != null)
                updateFill = true;

            // Shape
            if (updateShape)
            {
                SwShapeOps.shapeReset(shape);
                SwLcdSubpixel.ResetLcdRle(shape); // [LCD Subpixel]
                if (rshape!.fill != null || rshape.color.a > 0 || clipper)
                {
                    if (SwShapeOps.shapePrepare(shape, rshape, transform, clipBox, ref curBox, mpool!, tid, clips.Count > 0))
                    {
                        if (!SwShapeOps.shapeGenRle(shape, curBox, mpool!, tid, Antialiasing(strokeWidth))) goto err;
                        // [LCD Subpixel] Generate 3x-resolution RLE for LCD text rendering
                        if (SwLcdSubpixel.IsActive())
                            SwLcdSubpixel.GenerateLcdRle(shape, curBox, mpool!, tid);
                    }
                    else
                    {
                        updateFill = false;
                        curBox.Reset();
                    }
                }
            }
            // Fill
            if (updateFill)
            {
                if (rshape!.fill != null)
                {
                    var ctable = (flags[0] & RenderUpdateFlag.Gradient) != 0;
                    if (shape.fill == null) ctable = true; // first time: need full color table init
                    if (ctable) SwShapeOps.shapeResetFill(shape);
                    if (!SwShapeOps.shapeGenFillColors(shape, rshape.fill, transform, surface!, opacity, ctable)) goto err;
                }
            }
            // Stroke
            if (updateShape || (flags[0] & RenderUpdateFlag.Stroke) != 0)
            {
                if (strokeWidth > 0.0f)
                {
                    SwShapeOps.shapeResetStroke(shape, rshape!, transform, mpool!, tid);
                    if (!SwShapeOps.shapeGenStrokeRle(shape, rshape!, transform, clipBox, ref curBox, mpool!, tid)) goto err;
                    if (rshape!.StrokeFillGradient() is Fill strokeFill)
                    {
                        var ctable = (flags[0] & RenderUpdateFlag.GradientStroke) != 0;
                        if (ctable) SwShapeOps.shapeResetStrokeFill(shape);
                        if (!SwShapeOps.shapeGenStrokeFillColors(shape, strokeFill, transform, surface!, opacity, ctable)) goto err;
                    }
                }
                else
                {
                    SwShapeOps.shapeDelStroke(shape);
                }
            }

            SwShapeOps.shapeDelOutline(shape, mpool!, tid);

            // Clip Path
            foreach (var p in clips)
            {
                if (p is SwTask clipTask)
                {
                    bool clipShapeRle = true, clipStrokeRle = true;
                    if (shape.hasRle) { clipShapeRle = clipTask.Clip(ref shape.rle); }
                    if (shape.hasStrokeRle) { clipStrokeRle = clipTask.Clip(ref shape.strokeRle); }
                    if (!clipShapeRle || !clipStrokeRle) goto err;
                }
            }

            valid = true;
            if (!nodirty) dirtyRegion?.Add(prvBox, curBox);
            return;

        err:
            SwShapeOps.shapeReset(shape);
            SwLcdSubpixel.ResetLcdRle(shape); // [LCD Subpixel]
            if (shape.hasStrokeRle) { SwRleOps.rleReset(ref shape.strokeRle); }
            SwShapeOps.shapeDelOutline(shape, mpool!, tid);
            Invisible();
        }

        public override void DisposeTask()
        {
            SwShapeOps.shapeFree(shape);
        }
    }

    public unsafe class SwImageTask : SwTask
    {
        public SwImage image = new SwImage();
        public RenderSurface? source;

        public override bool Clip(ref SwRle target)
        {
            // Image as ClipPath not supported
            return true;
        }

        public override void Run(uint tid)
        {
            rasterConvertCS(source!, surface!.cs);
            rasterPremultiply(source!);

            image.buf32 = source!.buf32;
            image.w = source.w;
            image.h = source.h;
            image.stride = source.stride;
            image.channelSize = source.channelSize;

            var updateImage = (flags[0] & (RenderUpdateFlag.Image | RenderUpdateFlag.Clip | RenderUpdateFlag.Transform)) != 0;
            var updateColor = (flags[0] & RenderUpdateFlag.Color) != 0;

            if ((updateImage || updateColor) && opacity > 0)
            {
                if (updateImage) SwImageOps.imageReset(image);
                if (image.buf32 == null || image.w == 0 || image.h == 0) goto err;
                if (!SwImageOps.imagePrepare(image, transform, clipBox, ref curBox, mpool!, tid)) goto err;
                valid = true;
                if (clips.Count > 0)
                {
                    if (!SwImageOps.imageGenRle(image, curBox, mpool!, tid, false)) goto err;
                    if (image.hasRle)
                    {
                        SwImageOps.imageDelOutline(image, mpool!, tid);
                        foreach (var p in clips)
                        {
                            if (p is SwTask clipTask)
                            {
                                if (!clipTask.Clip(ref image.rle)) goto err;
                            }
                        }
                        if (!nodirty) dirtyRegion?.Add(prvBox, curBox);
                        return;
                    }
                }
                else
                {
                    SwImageOps.imageFree(image);
                }
            }
            goto end;
        err:
            curBox.Reset();
            SwImageOps.imageReset(image);
        end:
            SwImageOps.imageDelOutline(image, mpool!, tid);
            if (!nodirty) dirtyRegion?.Add(prvBox, curBox);
        }

        public override void DisposeTask()
        {
            SwImageOps.imageFree(image);
        }
    }

    // =====================================================================
    //  SwRenderer
    // =====================================================================

    public unsafe class SwRenderer : RenderMethod
    {
        private SwSurface? surface;
        private SwMpool? mpool;
        private RenderDirtyRegion dirtyRegion = new RenderDirtyRegion();
        private bool fulldraw;
        private List<SwTask> tasks = new List<SwTask>();
        private List<SwSurface> compositors = new List<SwSurface>();

        private static int rendererCnt = -1;

        // Constructor
        public SwRenderer(uint threads = 1, EngineOption op = EngineOption.Default)
        {
            if (rendererCnt == -1)
            {
                SwMemPool.mpoolInit(threads);
                rendererCnt = 0;
            }

            mpool = SwMemPool.mpoolReq();

            if (op == EngineOption.None) dirtyRegion.support = false;

            ++rendererCnt;
        }

        ~SwRenderer()
        {
            ClearCompositors();
            --rendererCnt;
        }

        // --- Clear ---
        public override bool Clear()
        {
            if (surface != null)
            {
                fulldraw = true;
                return rasterClear(surface, 0, 0, surface.w, surface.h);
            }
            return false;
        }

        // --- Sync ---
        public override bool Sync()
        {
            foreach (var task in tasks)
            {
                if (task.disposed) { /* task can be GC'd */ }
                else
                {
                    task.Done();
                    task.pushed = false;
                }
            }
            tasks.Clear();
            return true;
        }

        // --- Target ---
        public bool Target(uint* data, uint stride, uint w, uint h, ColorSpace cs)
        {
            if (data == null || stride == 0 || w == 0 || h == 0 || w > stride) return false;

            ClearCompositors();

            if (surface == null) surface = new SwSurface();

            surface.buf32 = data;
            surface.stride = stride;
            surface.w = w;
            surface.h = h;
            surface.cs = cs;
            surface.channelSize = RenderHelper.ChannelSize(cs);
            surface.premultiplied = true;

            dirtyRegion.Init(w, h);

            fulldraw = true;

            return rasterCompositor(surface);
        }

        // Overload accepting managed array
        public bool Target(uint[] data, uint stride, uint w, uint h, ColorSpace cs)
        {
            if (data == null || data.Length == 0 || stride == 0 || w == 0 || h == 0 || w > stride) return false;

            ClearCompositors();

            if (surface == null) surface = new SwSurface();

            surface.data = data;
            surface.Pin();
            surface.stride = stride;
            surface.w = w;
            surface.h = h;
            surface.cs = cs;
            surface.channelSize = RenderHelper.ChannelSize(cs);
            surface.premultiplied = true;

            dirtyRegion.Init(w, h);

            fulldraw = true;

            return rasterCompositor(surface);
        }

        // --- PreUpdate / PostUpdate ---
        public override bool PreUpdate() => surface != null;
        public override bool PostUpdate() => true;

        // --- PreRender ---
        public override bool PreRender()
        {
            if (surface == null) return false;
            if (fulldraw || dirtyRegion.Deactivated()) return true;

            foreach (var task in tasks) task.Done();

            dirtyRegion.Commit();

            for (int idx = 0; idx < RenderDirtyRegion.PARTITIONING; ++idx)
            {
                var regions = dirtyRegion.Get(idx);
                // regions is Array<RenderRegion> - iterate if data is available
            }

            return true;
        }

        // --- PostRender ---
        public override bool PostRender()
        {
            if (surface!.cs == ColorSpace.ABGR8888S || surface.cs == ColorSpace.ARGB8888S)
            {
                rasterUnpremultiply(surface);
            }

            dirtyRegion.Clear();
            fulldraw = false;

            return true;
        }

        // --- ClearCompositors ---
        private void ClearCompositors()
        {
            foreach (var cmp in compositors)
            {
                if (cmp.compositor != null)
                {
                    if (cmp.compositor.image.buf32 != null)
                    {
                        NativeMemory.Free(cmp.compositor.image.buf32);
                        cmp.compositor.image.buf32 = null;
                    }
                }
            }
            compositors.Clear();
        }

        // --- Damage ---
        public override void Damage(object? rd, in RenderRegion region)
        {
            var task = rd as SwTask;
            if (dirtyRegion.Deactivated() || (task != null && task.opacity == 0)) return;
            dirtyRegion.Add(region);
        }

        // --- Partial ---
        public override bool Partial(bool disable)
        {
            return dirtyRegion.Deactivate(disable);
        }

        // --- RenderImage ---
        public override bool RenderImage(object? data)
        {
            var task = data as SwImageTask;
            if (task == null) return false;
            task.Done();

            if (task.valid)
            {
                // full scene rendering
                RasterImage(surface!, task.image, task.transform, task.curBox, task.opacity);
            }
            task.prvBox = task.curBox;
            return true;
        }

        private bool RasterImage(SwSurface surface, SwImage image, in Matrix transform, in RenderRegion bbox, byte opacity)
        {
            if (bbox.Sw() <= 0 || bbox.Sh() <= 0 || bbox.X() >= surface.w || bbox.Y() >= surface.h) return true;

            // RLE Image
            if (image.hasRle)
            {
                if (image.rle.Invalid()) return true;
                if (image.direct) return rasterDirectRleImage(surface, image, bbox, opacity);
                else if (image.scaled) return rasterScaledRleImage(surface, image, transform, bbox, opacity);
                else
                {
                    var cmp = Request(sizeof(uint), false);
                    cmp.compositor!.method = MaskMethod.None;
                    cmp.compositor.valid = true;
                    cmp.compositor.image.rle = image.rle;
                    cmp.compositor.image.hasRle = true;
                    rasterClear(cmp, bbox.X(), bbox.Y(), bbox.W(), bbox.H());
                    rasterTexmapPolygon(cmp, image, transform, bbox, 255);
                    return rasterDirectRleImage(surface, cmp.compositor.image, bbox, opacity);
                }
            }
            // Whole Image
            else
            {
                if (image.direct) return rasterDirectImage(surface, image, bbox, opacity);
                else if (image.scaled) return rasterScaledImage(surface, image, transform, bbox, opacity);
                else return rasterTexmapPolygon(surface, image, transform, bbox, opacity);
            }
        }

        // --- RenderShape ---
        public override bool RenderShape(object? data)
        {
            var task = data as SwShapeTask;
            if (task == null) return false;
            task.Done();

            if (task.valid)
            {
                // Local lambdas for fill and stroke rendering
                void fillShape(SwShapeTask t, SwSurface sfc, in RenderRegion bbox)
                {
                    if (t.rshape!.fill != null)
                    {
                        rasterGradientShape(sfc, t.shape, bbox, t.rshape.fill, t.opacity);
                    }
                    else
                    {
                        t.rshape.FillColor(out var r, out var g, out var b, out var a);
                        a = (byte)MULTIPLY(t.opacity, a);
                        if (a > 0)
                        {
                            var c = new RenderColor(r, g, b, a);
                            // [LCD Subpixel] Use per-channel LCD blending for text shapes
                            if (t.shape.hasLcdRle)
                                SwLcdSubpixel.RasterLcdShape(sfc, t.shape, bbox, c);
                            else
                                rasterShape(sfc, t.shape, bbox, c);
                        }
                    }
                }

                void strokeShape(SwShapeTask t, SwSurface sfc, in RenderRegion bbox)
                {
                    if (t.rshape!.StrokeFillGradient() is Fill strokeFill)
                    {
                        rasterGradientStroke(sfc, t.shape, bbox, strokeFill, t.opacity);
                    }
                    else
                    {
                        if (t.rshape.StrokeFill(out var r, out var g, out var b, out var a))
                        {
                            a = (byte)MULTIPLY(t.opacity, a);
                            if (a > 0)
                            {
                                var c = new RenderColor(r, g, b, a);
                                rasterStroke(sfc, t.shape, bbox, c);
                            }
                        }
                    }
                }

                // Determine draw order based on strokeFirst()
                if (task.rshape!.StrokeFirst())
                {
                    strokeShape(task, surface!, task.curBox);
                    fillShape(task, surface!, task.shape.bbox);
                }
                else
                {
                    fillShape(task, surface!, task.shape.bbox);
                    strokeShape(task, surface!, task.curBox);
                }
            }
            task.prvBox = task.curBox;
            return true;
        }

        // --- Blend ---
        public override bool Blend(BlendMethod method)
        {
            if (surface!.blendMethod == method) return true;
            surface.blendMethod = method;

            surface.blender = method switch
            {
                BlendMethod.Multiply => opBlendMultiply,
                BlendMethod.Screen => opBlendScreen,
                BlendMethod.Overlay => opBlendOverlay,
                BlendMethod.Darken => opBlendDarken,
                BlendMethod.Lighten => opBlendLighten,
                BlendMethod.ColorDodge => opBlendColorDodge,
                BlendMethod.ColorBurn => opBlendColorBurn,
                BlendMethod.HardLight => opBlendHardLight,
                BlendMethod.SoftLight => opBlendSoftLight,
                BlendMethod.Difference => opBlendDifference,
                BlendMethod.Exclusion => opBlendExclusion,
                BlendMethod.Hue => opBlendHue,
                BlendMethod.Saturation => opBlendSaturation,
                BlendMethod.Color => opBlendColor,
                BlendMethod.Luminosity => opBlendLuminosity,
                BlendMethod.Add => opBlendAdd,
                _ => null
            };
            return true;
        }

        // --- Region ---
        public override RenderRegion Region(object? data)
        {
            if (data is SwTask task) return task.Bounds();
            return default;
        }

        // --- Composition ---
        public override bool BeginComposite(RenderCompositor? cmp, MaskMethod method, byte opacity)
        {
            if (cmp == null) return false;
            var p = (SwCompositor)cmp;

            p.method = method;
            p.opacity = opacity;

            if (p.method != MaskMethod.None)
            {
                surface = p.recoverSfc;
                surface!.compositor = p;
            }

            return true;
        }

        public override RenderSurface? MainSurface() => surface;

        private SwSurface Request(int channelSize, bool square)
        {
            uint w, h;

            if (square)
            {
                w = h = Math.Max(surface!.w, surface.h);
            }
            else
            {
                w = surface!.w;
                h = surface.h;
            }

            SwSurface? cmp = null;

            // Use cached data
            foreach (var cur in compositors)
            {
                if (cur.compositor != null && cur.compositor.valid && cur.compositor.image.channelSize == channelSize)
                {
                    if (w == cur.w && h == cur.h)
                    {
                        cmp = cur;
                        break;
                    }
                }
            }

            // New Composition
            if (cmp == null)
            {
                cmp = new SwSurface(surface);
                cmp.compositor = new SwCompositor();
                // Allocate native memory for the compositor image
                var bufSize = (nuint)(channelSize * w * h);
                cmp.compositor.image.buf32 = (uint*)NativeMemory.AllocZeroed(bufSize);
                cmp.w = cmp.compositor.image.w = w;
                cmp.h = cmp.compositor.image.h = h;
                cmp.stride = cmp.compositor.image.stride = w;
                cmp.compositor.image.direct = true;
                cmp.compositor.valid = true;
                cmp.channelSize = cmp.compositor.image.channelSize = (byte)channelSize;

                compositors.Add(cmp);
            }

            // Sync
            cmp.buf32 = cmp.compositor!.image.buf32;

            return cmp;
        }

        public override RenderCompositor? Target(in RenderRegion region, ColorSpace cs, CompositionFlag flags)
        {
            var surfBox = new RenderRegion(0, 0, (int)surface!.w, (int)surface.h);
            var bbox = RenderRegion.Intersect(region, surfBox);
            if (bbox.Sw() <= 0 || bbox.Sh() <= 0) return null;

            var cmp = Request(RenderHelper.ChannelSize(cs), (flags & CompositionFlag.PostProcessing) != 0);
            cmp.compositor!.recoverSfc = surface;
            cmp.compositor.recoverCmp = surface.compositor;
            cmp.compositor.valid = false;
            cmp.compositor.bbox = bbox;

            rasterClear(cmp, bbox.X(), bbox.Y(), bbox.W(), bbox.H());

            // Switch render target
            surface = cmp;

            return cmp.compositor;
        }

        public override bool EndComposite(RenderCompositor? cmp)
        {
            if (cmp == null) return false;

            var p = (SwCompositor)cmp;

            // Recover Context
            surface = p.recoverSfc;
            surface!.compositor = p.recoverCmp;

            if (p.valid) return true;
            p.valid = true;

            // Default is alpha blending
            if (p.method == MaskMethod.None)
            {
                return rasterDirectImage(surface, p.image, p.bbox, p.opacity);
            }

            return true;
        }

        // --- Effects ---
        public override void Prepare(RenderEffect effect, in Matrix transform)
        {
            switch (effect.type)
            {
                case SceneEffect.GaussianBlur:
                    SwPostEffect.effectGaussianBlurUpdate((RenderEffectGaussianBlur)effect, transform);
                    break;
                case SceneEffect.DropShadow:
                    SwPostEffect.effectDropShadowUpdate((RenderEffectDropShadow)effect, transform);
                    break;
                case SceneEffect.Fill:
                    SwPostEffect.effectFillUpdate((RenderEffectFill)effect);
                    break;
                case SceneEffect.Tint:
                    SwPostEffect.effectTintUpdate((RenderEffectTint)effect);
                    break;
                case SceneEffect.Tritone:
                    SwPostEffect.effectTritoneUpdate((RenderEffectTritone)effect);
                    break;
            }
        }

        public override bool Region(RenderEffect effect)
        {
            return effect.type switch
            {
                SceneEffect.GaussianBlur => SwPostEffect.effectGaussianBlurRegion((RenderEffectGaussianBlur)effect),
                SceneEffect.DropShadow => SwPostEffect.effectDropShadowRegion((RenderEffectDropShadow)effect),
                _ => false
            };
        }

        public override bool Render(RenderCompositor? cmp, RenderEffect effect, bool direct)
        {
            var p = (SwCompositor)cmp!;

            if (p.image.channelSize != sizeof(uint)) return false;
            if (p.recoverSfc!.channelSize != sizeof(uint)) direct = false;

            return effect.type switch
            {
                SceneEffect.GaussianBlur => SwPostEffect.effectGaussianBlur(p, Request(surface!.channelSize, true), (RenderEffectGaussianBlur)effect),
                SceneEffect.DropShadow => DropShadowRender(p, (RenderEffectDropShadow)effect, direct),
                SceneEffect.Fill => SwPostEffect.effectFill(p, (RenderEffectFill)effect, direct),
                SceneEffect.Tint => SwPostEffect.effectTint(p, (RenderEffectTint)effect, direct),
                SceneEffect.Tritone => SwPostEffect.effectTritone(p, (RenderEffectTritone)effect, direct),
                _ => false
            };
        }

        private bool DropShadowRender(SwCompositor p, RenderEffectDropShadow effect, bool direct)
        {
            var cmp1 = Request(surface!.channelSize, true);
            cmp1.compositor!.valid = false;
            var cmp2 = Request(surface.channelSize, true);
            var surfaces = new SwSurface[] { cmp1, cmp2 };
            var ret = SwPostEffect.effectDropShadow(p, surfaces, effect, direct);
            cmp1.compositor.valid = true;
            return ret;
        }

        public override void Dispose(RenderEffect effect)
        {
            effect.rd = null;
        }

        // --- Bounds ---
        public override bool Bounds(object? data, Point[] pt4, in Matrix m)
        {
            if (data == null) return false;

            var task = data as SwShapeTask;
            if (task == null) return false;
            task.Done();

            return SwShapeOps.shapeStrokeBBox(task.shape, task.rshape!, pt4, m, task.mpool!);
        }

        // --- Intersects ---
        public override bool IntersectsShape(object? data, in RenderRegion region)
        {
            var task = data as SwShapeTask;
            if (task == null) return false;
            task.Done();

            if (!task.valid || !task.Bounds().Intersected(region)) return false;
            if (task.shape.hasStrokeRle)
            {
                if (SwRleOps.rleIntersect(task.shape.strokeRle, region)) return true;
            }
            if (task.shape.hasRle)
            {
                return SwRleOps.rleIntersect(task.shape.rle, region);
            }
            return task.shape.fastTrack;
        }

        public override bool IntersectsImage(object? data, in RenderRegion region)
        {
            var task = data as SwImageTask;
            if (task == null) return false;
            task.Done();

            if (!task.valid || !task.Bounds().Intersected(region)) return false;

            // AABB/OBB SAT test for rotated images
            var rad = TvgMath.Radian(task.transform);
            if (rad > 0.0f && rad < MathConstants.MATH_PI)
            {
                Span<Point> aabb = stackalloc Point[4];
                aabb[0] = new Point(region.min.x, region.min.y);
                aabb[1] = new Point(region.max.x, region.min.y);
                aabb[2] = new Point(region.max.x, region.max.y);
                aabb[3] = new Point(region.min.x, region.max.y);

                Span<Point> obb = stackalloc Point[4];
                obb[0] = TvgMath.Transform(new Point(0.0f, 0.0f), task.transform);
                obb[1] = TvgMath.Transform(new Point(task.image.w, 0.0f), task.transform);
                obb[2] = TvgMath.Transform(new Point(task.image.w, task.image.h), task.transform);
                obb[3] = TvgMath.Transform(new Point(0.0f, task.image.h), task.transform);

                for (int i = 0; i < 4; ++i)
                {
                    Point edge;
                    if (i < 2)
                    {
                        var a1 = aabb[(i + 1) % 4];
                        var a2 = aabb[i];
                        edge = new Point(a1.x - a2.x, a1.y - a2.y);
                    }
                    else
                    {
                        var o1 = obb[(i - 2 + 1) % 4];
                        var o2 = obb[i - 2];
                        edge = new Point(o1.x - o2.x, o1.y - o2.y);
                    }
                    TvgMath.Normalize(ref edge);

                    float minA = TvgMath.Dot(aabb[0], edge), maxA = minA;
                    for (int j = 1; j < 4; ++j)
                    {
                        float proj = TvgMath.Dot(aabb[j], edge);
                        if (proj < minA) minA = proj;
                        if (proj > maxA) maxA = proj;
                    }
                    float minB = TvgMath.Dot(obb[0], edge), maxB = minB;
                    for (int j = 1; j < 4; ++j)
                    {
                        float proj = TvgMath.Dot(obb[j], edge);
                        if (proj < minB) minB = proj;
                        if (proj > maxB) maxB = proj;
                    }
                    if (maxA < minB || maxB < minA) return false;
                }
            }

            if (task.image.hasRle)
            {
                return SwRleOps.rleIntersect(task.image.rle, region);
            }
            return true;
        }

        // --- ColorSpace ---
        public override ColorSpace ColorSpaceValue()
        {
            return surface?.cs ?? ColorSpace.Unknown;
        }

        // --- Dispose (data) ---
        public override void Dispose(object? data)
        {
            var task = data as SwTask;
            if (task == null) return;
            task.Done();
            task.DisposeTask();

            if (task.pushed) task.disposed = true;
        }

        // --- Prepare ---

        private object PrepareCommon(SwTask task, in Matrix transform, ref ValueList<object?> clips, byte opacity, RenderUpdateFlag flags, bool ready)
        {
            if (task.disposed) return task;

            task.surface = surface;
            task.mpool = mpool;
            task.clipBox = RenderRegion.Intersect(vport, new RenderRegion(0, 0, (int)surface!.w, (int)surface.h));
            task.transform = transform;
            task.clips = clips;
            task.dirtyRegion = dirtyRegion;
            task.opacity = opacity;
            task.nodirty = dirtyRegion.Deactivated();
            task.flags[0] = flags;
            task.valid = false;

            if (!task.pushed)
            {
                task.pushed = true;
                tasks.Add(task);
            }

            // Guarantee composition targets get ready
            foreach (var p in clips)
            {
                (p as SwTask)?.Done();
            }

            if (task.Ready(ready)) return task;

            // Execute synchronously (no task scheduler)
            if (flags != 0) task.Run(0);

            return task;
        }

        public override object? Prepare(RenderSurface surfaceData, object? data, in Matrix transform, ref ValueList<object?> clips, byte opacity, FilterMethod filter, RenderUpdateFlag flags)
        {
            var task = data as SwImageTask;
            task?.Done();
            if (task == null)
            {
                task = new SwImageTask();
                task.source = surfaceData;
            }
            task.image.filter = filter;
            return PrepareCommon(task, transform, ref clips, opacity, flags, opacity == 0);
        }

        public override object? Prepare(RenderShape rshape, object? data, in Matrix transform, ref ValueList<object?> clips, byte opacity, RenderUpdateFlag flags, bool clipper)
        {
            var task = data as SwShapeTask;
            task?.Done();
            if (task == null)
            {
                task = new SwShapeTask();
                task.rshape = rshape;
            }

            task.clipper = clipper;

            return PrepareCommon(task, transform, ref clips, opacity, flags, opacity == 0 && !clipper);
        }

        // --- Static management ---
        public static bool Term()
        {
            if (rendererCnt > 0) return false;

            SwMemPool.mpoolTerm();
            rendererCnt = -1;

            return true;
        }

        public static SwRenderer Gen(uint threads = 1, EngineOption op = EngineOption.Default)
        {
            return new SwRenderer(threads, op);
        }

    }
}

