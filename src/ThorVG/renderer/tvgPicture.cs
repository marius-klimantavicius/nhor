// Ported from ThorVG/src/renderer/tvgPicture.h and tvgPicture.cpp

using System;
using System.Collections.Generic;

namespace ThorVG
{
    /// <summary>Iterator over Picture's single child. Mirrors C++ PictureIterator.</summary>
    public class PictureIterator : Iterator
    {
        private readonly Paint? _paint;
        private Paint? _ptr;

        public PictureIterator(Paint? p) { _paint = p; }

        public override Paint? Next()
        {
            if (_ptr == null) _ptr = _paint;
            else _ptr = null;
            return _ptr;
        }

        public override uint Count() => _paint != null ? 1u : 0u;

        public override void Begin()
        {
            _ptr = null;
        }
    }

    /// <summary>
    /// An image that can be loaded from various formats.
    /// Mirrors C++ tvg::Picture / PictureImpl.
    /// </summary>
    public class Picture : Paint
    {
        internal ImageLoader? loader;
        internal Paint? vector;             // vector picture uses
        internal RenderSurface? bitmap;     // bitmap picture uses
        internal AssetResolver? resolver;
        internal Point origin;
        internal float w, h;
        internal FilterMethod filter = FilterMethod.Bilinear;
        internal bool resizing;

        protected Picture() { }

        public static Picture Gen() => new Picture();
        public override Type PaintType() => Type.Picture;

        // --- Public API (mirrors thorvg.h Picture) ---

        public Result Load(string? filename)
        {
            if (filename == null) return Result.InvalidArguments;
            if (vector != null || bitmap != null) return Result.InsufficientCondition;

            bool invalid;
            var loader = (ImageLoader?)LoaderMgr.Loader(filename, out invalid);
            if (loader == null)
            {
                if (invalid) return Result.InvalidArguments;
                return Result.NonSupport;
            }
            return LoadImpl(loader);
        }

        public Result Load(byte[] data, uint size, string? mimeType, string? rpath = null, bool copy = false)
        {
            if (data == null || size <= 0) return Result.InvalidArguments;
            if (vector != null || bitmap != null) return Result.InsufficientCondition;
            var loader = (ImageLoader?)LoaderMgr.Loader(data, size, mimeType, rpath, copy);
            if (loader == null) return Result.NonSupport;
            return LoadImpl(loader);
        }

        public Result Load(uint[] data, uint w, uint h, ColorSpace cs, bool copy = false)
        {
            if (data == null || w <= 0 || h <= 0 || cs == ColorSpace.Unknown) return Result.InvalidArguments;
            if (vector != null || bitmap != null) return Result.InsufficientCondition;

            var loader = (ImageLoader?)LoaderMgr.Loader(data, w, h, cs, copy);
            if (loader == null) return Result.FailedAllocation;

            return LoadImpl(loader);
        }

        public Result Resolver(Func<Paint, string, object?, bool>? func, object? data = null)
        {
            if (loader != null) return Result.InsufficientCondition;

            if (func == null)
            {
                this.resolver = null;
                return Result.Success;
            }

            if (this.resolver == null) this.resolver = new AssetResolver();
            this.resolver.func = func;
            this.resolver.data = data;
            return Result.Success;
        }

        public Result SetSize(float w, float h)
        {
            this.w = w;
            this.h = h;
            resizing = true;
            return Result.Success;
        }

        public Result GetSize(out float w, out float h)
        {
            if (loader == null)
            {
                w = h = 0;
                return Result.InsufficientCondition;
            }
            w = this.w;
            h = this.h;
            return Result.Success;
        }

        public Result SetOrigin(float x, float y)
        {
            origin = new Point(x, y);
            pImpl.Mark(RenderUpdateFlag.Transform);
            return Result.Success;
        }

        public Result GetOrigin(out float x, out float y)
        {
            x = origin.x;
            y = origin.y;
            return Result.Success;
        }

        public Result SetFilter(FilterMethod method)
        {
            if (method != filter)
            {
                pImpl.Mark(RenderUpdateFlag.Image);
                filter = method;
            }
            return Result.Success;
        }

        public Paint? FindPaint(uint id)
        {
            Paint? ret = null;

            var accessor = Accessor.Gen();
            accessor.Set(this, (Paint paint, object? data) =>
            {
                if (paint.id == (uint)data!)
                {
                    ret = paint;
                    return false;
                }
                return true;
            }, id);

            return ret;
        }

        public uint[]? Data(out uint w, out uint h)
        {
            // Try it, if not loaded yet.
            InternalLoad();

            if (loader != null)
            {
                w = (uint)loader.w;
                h = (uint)loader.h;
            }
            else
            {
                w = 0;
                h = 0;
            }
            if (bitmap != null) return bitmap.data;
            return null;
        }

        // --- Rendering infrastructure (mirrors C++ PictureImpl) ---

        internal bool PaintSkip(RenderUpdateFlag flag)
        {
            if (flag == RenderUpdateFlag.None) return true;
            return false;
        }

        internal bool PaintUpdate(RenderMethod renderer, in Matrix transform, List<object?> clips, byte opacity, RenderUpdateFlag flag, bool clipper)
        {
            InternalLoad();

            var pivot = new Point(-origin.x * w, -origin.y * h);

            if (bitmap != null)
            {
                // Overriding Transformation by the desired image size
                var sx = w / loader!.w;
                var sy = h / loader.h;
                var scale = sx < sy ? sx : sy;
                var m = TvgMath.Multiply(transform, new Matrix(scale, 0, pivot.x, 0, scale, pivot.y, 0, 0, 1));
                pImpl.rd = renderer.Prepare(bitmap, pImpl.rd, m, clips, opacity, filter, flag);
            }
            else if (vector != null)
            {
                if (resizing)
                {
                    loader?.Resize(vector, w, h);
                    resizing = false;
                }
                NeedComposition(opacity);
                vector.pImpl.SetBlend(pImpl.blendMethod); // propagate blend method to nested vector scene
                var tm = transform;
                TvgMath.TranslateR(ref tm, pivot);
                return vector.pImpl.Update(renderer, tm, clips, opacity, flag, false) != null;
            }
            return true;
        }

        internal bool PaintRender(RenderMethod renderer, CompositionFlag flag)
        {
            var ret = true;

            if (bitmap != null)
            {
                renderer.Blend(pImpl.blendMethod);
                return renderer.RenderImage(pImpl.rd);
            }
            else if (vector != null)
            {
                RenderCompositor? cmp = null;
                if (pImpl.cmpFlag != CompositionFlag.Invalid)
                {
                    cmp = renderer.Target(PaintBounds(), renderer.ColorSpaceValue(), pImpl.cmpFlag);
                    renderer.BeginComposite(cmp, MaskMethod.None, 255);
                }
                ret = vector.pImpl.Render(renderer);
                if (cmp != null) renderer.EndComposite(cmp);
            }
            return ret;
        }

        /// <summary>
        /// Compute geometric bounds. Mirrors C++ PictureImpl::bounds(Point*, Matrix, bool).
        /// </summary>
        internal bool GeometricBounds(Span<Point> pt4, in Matrix m, bool obb)
        {
            pt4[0] = TvgMath.Transform(new Point(0.0f, 0.0f), m);
            pt4[1] = TvgMath.Transform(new Point(w, 0.0f), m);
            pt4[2] = TvgMath.Transform(new Point(w, h), m);
            pt4[3] = TvgMath.Transform(new Point(0.0f, h), m);
            return true;
        }

        internal RenderRegion PaintBounds()
        {
            if (vector != null) return vector.pImpl.Bounds();
            if (pImpl.renderer != null) return pImpl.renderer.Region(pImpl.rd);
            return default;
        }

        internal bool Intersects(in RenderRegion region)
        {
            if (pImpl.renderer == null) return false;
            InternalLoad();
            if (pImpl.rd != null) return pImpl.renderer.IntersectsImage(pImpl.rd, region);
            else if (vector != null)
            {
                // Cast to Scene to check intersections
                if (vector is Scene scene) return SceneIntersects(scene, region);
            }
            return false;
        }

        private static bool SceneIntersects(Scene scene, in RenderRegion region)
        {
            return scene.Intersects(region);
        }

        internal Paint DuplicatePicture(Paint? ret)
        {
            if (ret != null)
            {
                TvgCommon.TVGERR("RENDERER", "TODO: duplicate()");
            }

            InternalLoad();

            var picture = Gen();

            if (vector != null)
            {
                picture.vector = vector.Duplicate();
                picture.vector.pImpl.parent = picture;
            }

            if (loader != null)
            {
                picture.loader = loader;
                ++picture.loader.sharing;
                picture.pImpl.Mark(RenderUpdateFlag.Image);
            }

            picture.bitmap = bitmap;
            picture.origin = origin;
            picture.w = w;
            picture.h = h;
            picture.filter = filter;
            picture.resizing = resizing;

            return picture;
        }

        internal Iterator GetPictureIterator()
        {
            InternalLoad();
            return new PictureIterator(vector);
        }

        // --- Virtual dispatch overrides ---
        internal override Paint DuplicatePaintVirt(Paint? ret) => DuplicatePicture(ret);
        internal override Iterator? GetIteratorVirt() => GetPictureIterator();
        internal override bool PaintSkipVirt(RenderUpdateFlag flag) => PaintSkip(flag);
        internal override bool PaintUpdateVirt(RenderMethod renderer, in Matrix transform, List<object?> clips, byte opacity, RenderUpdateFlag flag, bool clipper) => PaintUpdate(renderer, transform, clips, opacity, flag, clipper);
        internal override bool PaintRenderVirt(RenderMethod renderer, CompositionFlag flag) => PaintRender(renderer, flag);
        internal override RenderRegion PaintBoundsVirt() => PaintBounds();
        internal override bool GeometricBoundsVirt(Span<Point> pt4, in Matrix m, bool obb) => GeometricBounds(pt4, m, obb);
        internal override bool IntersectsVirt(in RenderRegion region) => Intersects(region);

        // --- Private helpers ---

        private void InternalLoad()
        {
            if (loader != null)
            {
                if (vector != null)
                {
                    loader.Sync();
                }
                else if ((vector = loader.GetPaint()) != null)
                {
                    vector.Ref();
                    vector.pImpl.parent = this;
                    if (w != loader.w || h != loader.h)
                    {
                        if (!resizing)
                        {
                            w = loader.w;
                            h = loader.h;
                        }
                        loader.Resize(vector, w, h);
                        resizing = false;
                    }
                }
                else if (bitmap == null)
                {
                    bitmap = loader.Bitmap();
                }
            }
        }

        private void NeedComposition(byte opacity)
        {
            pImpl.cmpFlag = CompositionFlag.Invalid; // must clear after the rendering

            // In this case, paint(scene) would try composition itself.
            if (opacity < 255) return;

            // Composition test
            var method = pImpl.GetMask(out var target);
            if (target == null || target.pImpl.opacity == 255 || target.pImpl.opacity == 0) return;
            pImpl.Mark(CompositionFlag.Opacity);
        }

        private Result LoadImpl(ImageLoader loader)
        {
            // Same resource has been loaded.
            if (this.loader == loader)
            {
                this.loader.sharing--; // make it sure the reference counting.
                return Result.Success;
            }
            else if (this.loader != null)
            {
                LoaderMgr.Retrieve(this.loader);
            }

            this.loader = loader;
            loader.Set(resolver);
            if (!loader.Read()) return Result.Unknown;

            this.w = loader.w;
            this.h = loader.h;

            pImpl.Mark(RenderUpdateFlag.All);

            return Result.Success;
        }
    }
}
