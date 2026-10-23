// Ported from ThorVG/src/renderer/sw_engine/tvgSwRenderer.cpp

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static ThorVG.SwHelper;
using static ThorVG.SwBlendOp;
using static ThorVG.SwRaster;

namespace ThorVG
{
    // =====================================================================
    //  CPU tasks use the same scheduler and per-worker pools as native ThorVG.
    // =====================================================================

    public class SwTask
    {
        private readonly LoaderTask task;

        public SwTask() { task = new LoaderTask(Run); }
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
            valid = false;
            if (!nodirty) dirtyRegion?.Add(prvBox, default);
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
        public void Done() => task.Done();
        public void Request() => TaskScheduler.Request(task);
        public bool Complete()
        {
            prvBox = valid ? curBox : default;
            nodirty = false;
            return true;
        }
    }

    public class SwShapeTask : SwTask
    {
        public SwShape shape = new SwShape();
        public RenderShape? rshape;
        public bool clipper;
        public bool antiAlias = true;
        internal bool lcdSubpixel;

        private bool Antialiasing(float strokeWidth)
        {
            if (!antiAlias) return false;
            return strokeWidth < 2.0f || (rshape?.stroke?.dashCount > 0) || (rshape?.stroke?.first == true) || (rshape?.Trimpath() == true) || (rshape?.stroke?.color.a < 255);
        }

        public float ValidStrokeWidth(bool clipper)
        {
            if (rshape?.stroke == null || TvgMath.Zero(rshape.stroke.width)) return 0.0f;
            if (!clipper && (rshape.stroke.fill == null && rshape.stroke.color.a == 0)) return 0.0f;
            if (TvgMath.Zero(rshape.stroke.trim.begin - rshape.stroke.trim.end)) return 0.0f;
            return rshape.stroke.width * (float)Math.Sqrt(TvgMath.MultiplyAdd(transform.e11, transform.e11, transform.e12 * transform.e12));
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
            var updateFill = (flags[0] & (RenderUpdateFlag.Color | RenderUpdateFlag.Gradient | RenderUpdateFlag.Transform)) != 0;

            // Shape
            if (updateShape)
            {
                SwShapeOps.shapeReset(shape);
                SwLcdSubpixel.ResetLcdRle(shape); // [LCD Subpixel]
                if (rshape!.fill != null || rshape.color.a > 0 || clipper)
                {
                    if (SwShapeOps.shapePrepare(shape, rshape, transform, clipBox, ref curBox, mpool!, tid, clips.Count > 0) &&
                        SwShapeOps.shapeGenRle(shape, curBox, mpool!, tid, Antialiasing(strokeWidth)))
                    {
                        // [LCD Subpixel] Generate 3x-resolution RLE for LCD text rendering
                        if (lcdSubpixel)
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
                if (!SwShapeOps.shapeGenFillColors(ref shape.fill, rshape!.fill, transform, surface!, opacity, (flags[0] & RenderUpdateFlag.Gradient) != 0)) goto err;
            }
            // Stroke
            if (strokeWidth > 0.0f)
            {
                var updateStroke = updateShape || (flags[0] & RenderUpdateFlag.Stroke) != 0;
                if (updateStroke)
                {
                    if (!SwShapeOps.shapeGenStrokeRle(shape, rshape!, transform, clipBox, ref curBox, mpool!, tid, antiAlias)) goto err;
                }
                var ctable = (flags[0] & RenderUpdateFlag.GradientStroke) != 0;
                if (ctable || (flags[0] & RenderUpdateFlag.Transform) != 0)
                {
                    var strokeFill = rshape!.StrokeFillGradient();
                    if (!SwShapeOps.shapeGenFillColors(ref shape.stroke!.fill, strokeFill, transform, surface!, opacity, ctable)) goto err;
                }
            }
            else SwShapeOps.shapeDelStroke(shape);

            SwShapeOps.shapeDelOutline(shape);

            // Clip Path
            foreach (var p in clips)
            {
                if (p is SwTask clipTask)
                {
                    var clipShapeRle = clipTask.Clip(ref shape.rle);
                    var clipStrokeRle = clipTask.Clip(ref shape.strokeRle);
                    if (!clipShapeRle && !clipStrokeRle) goto err;
                }
            }

            valid = true;
            if (!nodirty) dirtyRegion?.Add(prvBox, curBox);
            return;

        err:
            SwShapeOps.shapeReset(shape);
            SwLcdSubpixel.ResetLcdRle(shape); // [LCD Subpixel]
            if (shape.hasStrokeRle) { SwRleOps.rleReset(ref shape.strokeRle); }
            Invisible();
        }

        public override void DisposeTask()
        {
            SwShapeOps.shapeFree(shape);
            clips.Clear();
        }
    }

    public unsafe class SwImageTask : SwTask
    {
        public SwImage image = new SwImage();
        public RenderSurface? source;

        public override bool Clip(ref SwRle target)
        {
            // The target explicitly leaves image clippers unsupported and
            // passes their target through (tvgSwRenderer.cpp SwImageTask::clip).
            TvgCommon.TVGERR("SW_ENGINE", "Image is used as ClipPath?");
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
            image.alphaIgnored = source.alphaIgnored;

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
            if (!nodirty) dirtyRegion?.Add(prvBox, curBox);
        }

        public override void DisposeTask()
        {
            SwImageOps.imageFree(image);
            clips.Clear();
        }
    }

    // =====================================================================
    //  SwRenderer
    // =====================================================================

    public unsafe class SwRenderer : RenderMethod, ISwTargetResult, IDisposable
    {
        private SwSurface? surface;
        private SwMpool? mpool;
        private RenderDirtyRegion dirtyRegion = new RenderDirtyRegion();
        private bool fulldraw;
        private bool rendered;
        private bool antiAlias = true;
        private List<SwTask> tasks = new List<SwTask>();
        private List<SwSurface> compositors = new List<SwSurface>();

        private static int rendererCnt = -1;
        private static readonly object rendererMtx = new object();
        private int disposed;

        // Constructor
        public SwRenderer(uint threads = 1, EngineOption op = EngineOption.Default)
        {
            lock (rendererMtx)
            {
                // Managed canvases can outlive an Init/Term cycle until GC.
                // A later scheduler may have more workers than the old pool.
                SwMemPool.mpoolInit(threads);
                if (rendererCnt == -1)
                {
                    rendererCnt = 0;
                }
                ++rendererCnt;
            }

            mpool = SwMemPool.mpoolReq();

            dirtyRegion.support = op == EngineOption.Default || (op & EngineOption.SmartRender) != 0;
            antiAlias = op == EngineOption.Default || (op & EngineOption.Aliased) == 0;

        }

        ~SwRenderer()
        {
            try { Dispose(); }
            catch (Exception exception) { TvgCommon.TVGERR("RENDERER", "CPU finalizer: {0}", exception.Message); }
        }

        protected override void ReleaseResources() => Dispose();

        public void Dispose()
        {
            if (System.Threading.Interlocked.Exchange(ref disposed, 1) != 0) return;
            try { Sync(); }
            finally
            {
                ClearCompositors();
                surface?.Dispose();
                surface = null;
                dirtyRegion.Dispose();
                lock (rendererMtx) --rendererCnt;
                GC.SuppressFinalize(this);
            }
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
                task.Done();
                if (task.disposed) task.DisposeTask();
                else
                {
                    task.pushed = false;
                }
            }
            tasks.Clear();
            if (rendered)
            {
                dirtyRegion.Clear();
                fulldraw = false;
                rendered = false;
            }
            return true;
        }

        // --- Target ---
        public bool Target(uint* data, uint stride, uint w, uint h, ColorSpace cs)
            => TargetResult(data, stride, w, h, cs) == Result.Success;

        public Result TargetResult(uint* data, uint stride, uint w, uint h, ColorSpace cs)
        {
            if (data == null || stride == 0 || w == 0 || h == 0 || w > stride) return Result.InvalidArguments;
            if (cs != ColorSpace.ABGR8888 && cs != ColorSpace.ABGR8888S && cs != ColorSpace.ARGB8888 && cs != ColorSpace.ARGB8888S) return Result.NonSupport;

            ClearCompositors();

            if (surface == null) surface = new SwSurface();

            surface.Unpin();
            surface.data = null;
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
            => TargetResult(data, stride, w, h, cs) == Result.Success;

        public Result TargetResult(uint[] data, uint stride, uint w, uint h, ColorSpace cs)
        {
            if (data == null || stride == 0 || w == 0 || h == 0 || w > stride ||
                (ulong)data.LongLength < (ulong)stride * h)
            {
                return Result.InvalidArguments;
            }

            if (cs != ColorSpace.ABGR8888 && cs != ColorSpace.ABGR8888S && cs != ColorSpace.ARGB8888 && cs != ColorSpace.ARGB8888S) return Result.NonSupport;

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
                ref var regions = ref dirtyRegion.Get(idx);
                for (uint i = 0; i < regions.count; ++i)
                {
                    var region = regions.data[i];
                    rasterClear(surface, region.X(), region.Y(), region.W(), region.H());
                }
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

            rendered = true;
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

        public override uint DirtyRegions() => fulldraw || dirtyRegion.Deactivated() ? 1u : dirtyRegion.Count();

        public override Result DirtyRegions(Span<Region> regions)
        {
            if (regions.IsEmpty) return Result.InvalidArguments;
            if (fulldraw || dirtyRegion.Deactivated())
            {
                regions[0] = new Region(0, 0, (int)surface!.w, (int)surface.h);
                return Result.Success;
            }
            int output = 0;
            for (int idx = 0; idx < RenderDirtyRegion.PARTITIONING; ++idx)
            {
                ref var partition = ref dirtyRegion.Get(idx);
                for (uint i = 0; i < partition.count; ++i)
                {
                    var region = partition.data[i];
                    regions[output++] = new Region(region.min.x, region.min.y, region.max.x, region.max.y);
                    if (output == regions.Length) return Result.Success;
                }
            }
            return Result.Success;
        }

        // --- RenderImage ---
        public override bool RenderImage(object? data)
        {
            var task = data as SwImageTask;
            if (task == null) return false;
            task.Done();

            if (task.valid)
            {
                if (fulldraw || task.nodirty || task.pushed || dirtyRegion.Deactivated())
                {
                    RasterImage(surface!, task.image, task.transform, task.curBox, task.opacity);
                }
                else if (task.curBox.Valid())
                {
                    for (int idx = 0; idx < RenderDirtyRegion.PARTITIONING; ++idx)
                    {
                        if (!dirtyRegion.Partition(idx).Intersected(task.curBox)) continue;
                        ref var regions = ref dirtyRegion.Get(idx);
                        for (uint i = 0; i < regions.count; ++i)
                        {
                            ref var region = ref regions[i];
                            if (task.curBox.max.x <= region.min.x) break;
                            if (task.curBox.Intersected(region))
                                RasterImage(surface!, task.image, task.transform, RenderRegion.Intersect(task.curBox, region), task.opacity);
                        }
                    }
                }
            }
            return task.Complete();
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

                if (fulldraw || task.nodirty || task.pushed || dirtyRegion.Deactivated())
                {
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
                else if (task.curBox.Valid())
                {
                    for (int idx = 0; idx < RenderDirtyRegion.PARTITIONING; ++idx)
                    {
                        if (!dirtyRegion.Partition(idx).Intersected(task.curBox)) continue;
                        ref var regions = ref dirtyRegion.Get(idx);
                        for (uint i = 0; i < regions.count; ++i)
                        {
                            ref var region = ref regions[i];
                            if (task.curBox.max.x <= region.min.x) break;
                            if (task.rshape!.StrokeFirst())
                            {
                                if (task.rshape.stroke != null && task.curBox.Intersected(region))
                                    strokeShape(task, surface!, RenderRegion.Intersect(task.curBox, region));
                                if (task.shape.bbox.Intersected(region))
                                    fillShape(task, surface!, RenderRegion.Intersect(task.shape.bbox, region));
                            }
                            else
                            {
                                if (task.shape.bbox.Intersected(region))
                                    fillShape(task, surface!, RenderRegion.Intersect(task.shape.bbox, region));
                                if (task.rshape.stroke != null && task.curBox.Intersected(region))
                                    strokeShape(task, surface!, RenderRegion.Intersect(task.curBox, region));
                            }
                        }
                    }
                }
            }
            return task.Complete();
        }

        // --- Blend ---
        public override bool Blend(BlendMethod method)
        {
            if (surface!.blendMethod == method) return true;
            surface.blendMethod = method;

            surface.blender = method switch
            {
                BlendMethod.Multiply => BlendMultiply,
                BlendMethod.Screen => BlendScreen,
                BlendMethod.Overlay => BlendOverlay,
                BlendMethod.Darken => BlendDarken,
                BlendMethod.Lighten => BlendLighten,
                BlendMethod.ColorDodge => BlendColorDodge,
                BlendMethod.ColorBurn => BlendColorBurn,
                BlendMethod.HardLight => BlendHardLight,
                BlendMethod.SoftLight => BlendSoftLight,
                BlendMethod.Difference => BlendDifference,
                BlendMethod.Exclusion => BlendExclusion,
                BlendMethod.Hue => BlendHue,
                BlendMethod.Saturation => BlendSaturation,
                BlendMethod.Color => BlendColor,
                BlendMethod.Luminosity => BlendLuminosity,
                BlendMethod.Add => BlendAdd,
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
                case SceneEffect.MotionBlur:
                    SwPostEffect.effectMotionBlurUpdate((RenderEffectMotionBlur)effect, transform);
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
                SceneEffect.MotionBlur => SwPostEffect.effectMotionBlurRegion((RenderEffectMotionBlur)effect),
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
                SceneEffect.MotionBlur => SwPostEffect.effectMotionBlur(p, Request(surface!.channelSize, true), (RenderEffectMotionBlur)effect),
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
            => Bounds(data, pt4.AsSpan(), m);

        public override bool Bounds(object? data, Span<Point> pt4, in Matrix m)
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
            // A rectangle can reuse allocated (now empty) RLE storage from a
            // previous curved path. Native short-circuits its fast-track first.
            return task.shape.fastTrack ||
                (task.shape.hasRle && SwRleOps.rleIntersect(task.shape.rle, region));
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
            if (task.pushed) task.disposed = true;
            else { task.Done(); task.DisposeTask(); }
        }

        // --- Prepare ---

        private object PrepareCommon(SwTask task, in Matrix transform, ref ValueList<object?> clips, byte opacity, RenderUpdateFlag flags, bool ready)
        {
            if (task.disposed) return task;

            task.surface = surface;
            task.mpool = mpool;
            task.clipBox = RenderRegion.Intersect(vport, new RenderRegion(0, 0, (int)surface!.w, (int)surface.h));
            task.transform = transform;
            // Traversal pops/clears its list before deferred workers finish.
            task.clips.CopyFrom(ref clips);
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

            if (task.Ready(ready)) return task;
            if (flags != 0)
            {
                foreach (var p in clips) (p as SwTask)?.Done();
                task.Request();
            }

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
            task.antiAlias = antiAlias;
            // Text preparation runs on the caller; execution may run on a worker.
            var lcdSubpixel = SwLcdSubpixel.IsActive();
            if (task.lcdSubpixel != lcdSubpixel) flags |= RenderUpdateFlag.Path;
            task.lcdSubpixel = lcdSubpixel;

            return PrepareCommon(task, transform, ref clips, opacity, flags, opacity == 0 && !clipper);
        }

        // --- Static management ---
        public static bool Term()
        {
            lock (rendererMtx)
            {
                if (rendererCnt > 0) return false;
                SwMemPool.mpoolTerm();
                rendererCnt = -1;
                return true;
            }
        }

        public static SwRenderer Gen(uint threads = 1, EngineOption op = EngineOption.Default)
        {
            return new SwRenderer(threads, op);
        }

    }
}
