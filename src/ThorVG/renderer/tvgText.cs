// Ported from ThorVG/src/renderer/tvgText.h and tvgText.cpp

using System;
using System.Collections.Generic;

namespace ThorVG
{
    /// <summary>
    /// Text rendering paint. Mirrors C++ tvg::Text / TextImpl.
    /// </summary>
    public class Text : Paint
    {
        internal Shape shape;           // text shape
        internal FontLoader? loader;
        internal FontMetrics fm = new FontMetrics();
        internal string? utf8;
        internal float outlineWidth;
        internal float italicShear;
        internal bool updated;

        protected Text()
        {
            shape = Shape.Gen();
            shape.pImpl.parent = this;
            shape.StrokeJoin(StrokeJoin.Round);
        }

        public static Text Gen() => new Text();
        public override Type PaintType() => Type.Text;

        // --- Public API (mirrors thorvg.h Text) ---

        public Result SetText(string? text)
        {
            this.utf8 = text;
            updated = true;
            pImpl.Mark(RenderUpdateFlag.Path);
            return Result.Success;
        }

        public Result SetFont(string? name)
        {
            var loader = (FontLoader?)(name != null ? LoaderMgr.Font(name) : LoaderMgr.AnyFont());
            if (loader == null) return Result.InsufficientCondition;

            // Same resource has been loaded.
            if (this.loader == loader)
            {
                this.loader.sharing--; // make it sure the reference counting.
                return Result.Success;
            }
            else if (this.loader != null)
            {
                this.loader.Release(fm);
                LoaderMgr.Retrieve(this.loader);
            }
            this.loader = loader;
            updated = true;

            return Result.Success;
        }

        public Result SetFontSize(float fontSize)
        {
            if (fontSize > 0.0f)
            {
                if (fm.fontSize != fontSize)
                {
                    fm.fontSize = fontSize;
                    updated = true;
                }
                return Result.Success;
            }
            return Result.InvalidArguments;
        }

        public Result SetWrapping(TextWrap mode)
        {
            if (fm.wrap == mode) return Result.Success;
            fm.wrap = mode;
            updated = true;
            pImpl.Mark(RenderUpdateFlag.Path);
            return Result.Success;
        }

        public Result SetAlign(float x, float y)
        {
            fm.align = new Point(x, y);
            pImpl.Mark(RenderUpdateFlag.Transform);
            return Result.Success;
        }

        public Result SetLayout(float w, float h)
        {
            fm.box = new Point(w, h);
            updated = true;
            return Result.Success;
        }

        public Result SetFill(byte r, byte g, byte b)
        {
            return shape.SetFill(r, g, b);
        }

        public Result SetOutline(float width, byte r, byte g, byte b)
        {
            outlineWidth = width;
            shape.StrokeFill(r, g, b);
            pImpl.Mark(RenderUpdateFlag.Stroke);
            return Result.Success;
        }

        public Result SetFill(Fill f)
        {
            return shape.SetFill(f);
        }

        public Result SetItalic(float shear)
        {
            if (shear < 0.0f) shear = 0.0f;
            else if (shear > 0.5f) shear = 0.5f;
            italicShear = shear;
            updated = true;
            return Result.Success;
        }

        public Result SetSpacing(float letter, float line)
        {
            if (letter < 0.0f || line < 0.0f) return Result.InvalidArguments;

            fm.spacing = new Point(letter, line);
            updated = true;

            return Result.Success;
        }

        public Result GetMetrics(out TextMetrics metrics)
        {
            metrics = default;
            if (loader == null || fm.fontSize <= 0.0f) return Result.InsufficientCondition;
            loader.Metrics(fm, out metrics);
            return Result.Success;
        }

        public Result GetMetrics(string? ch, out GlyphMetrics metrics)
        {
            metrics = default;
            if (loader == null || fm.fontSize <= 0.0f) return Result.InsufficientCondition;
            if (ch != null && loader.GlyphMetrics(fm, ch, out metrics)) return Result.Success;
            return Result.InvalidArguments;
        }

        public string? GetText() => utf8;

        public uint Lines()
        {
            if (InternalLoad()) return fm.lines;
            return 0;
        }

        /// <summary>Static font loading from file path. Mirrors C++ Text::load(filename).</summary>
        public static Result LoadFont(string? filename)
        {
            if (filename == null) return Result.InvalidArguments;

            bool invalid;
            var loader = LoaderMgr.Loader(filename, out invalid);
            if (loader != null)
            {
                if (loader.sharing > 0) --loader.sharing; // font loading doesn't mean sharing.
                return Result.Success;
            }
            else
            {
                if (invalid) return Result.InvalidArguments;
                else return Result.NonSupport;
            }
        }

        /// <summary>Static font loading from memory. Mirrors C++ Text::load(name, data, size, mimeType, copy).</summary>
        public static Result LoadFont(string name, byte[]? data, uint size, string? mimeType = null, bool copy = false)
        {
            if (name == null || (size == 0 && data != null)) return Result.InvalidArguments;

            // unload font
            if (data == null)
            {
                if (LoaderMgr.Retrieve(LoaderMgr.Font(name))) return Result.Success;
                return Result.InsufficientCondition;
            }

            if (LoaderMgr.Loader(name, data, size, mimeType, copy) == null) return Result.NonSupport;
            return Result.Success;
        }

        /// <summary>Static font unloading. Mirrors C++ Text::unload(filename).</summary>
        public static Result UnloadFont(string? filename)
        {
            if (filename == null) return Result.InvalidArguments;
            if (LoaderMgr.Retrieve(filename)) return Result.Success;
            return Result.InsufficientCondition;
        }

        // --- Rendering infrastructure (mirrors C++ TextImpl) ---

        internal bool PaintSkip(RenderUpdateFlag flag)
        {
            if (flag == RenderUpdateFlag.None) return true;
            return false;
        }

        internal bool PaintUpdate(RenderMethod renderer, in Matrix transform, List<object?> clips, byte opacity, RenderUpdateFlag flag, bool clipper)
        {
            if (!InternalLoad()) return true;

            var scale = fm.scale;

            // transform the gradient coordinates based on the final scaled font.
            var fill = ((Shape)shape).rs.fill;
            if (fill != null && shape.pImpl.Marked(RenderUpdateFlag.Gradient))
            {
                if (fill.GetFillType() == Type.LinearGradient)
                {
                    var lg = (LinearGradient)fill;
                    lg.p1 = new Point(lg.p1.x * scale, lg.p1.y * scale);
                    lg.p2 = new Point(lg.p2.x * scale, lg.p2.y * scale);
                }
                else
                {
                    var rg = (RadialGradient)fill;
                    rg.center = new Point(rg.center.x * scale, rg.center.y * scale);
                    rg.r *= scale;
                    rg.focal = new Point(rg.focal.x * scale, rg.focal.y * scale);
                    rg.fr *= scale;
                }
            }

            if (outlineWidth > 0.0f && pImpl.Marked(RenderUpdateFlag.Stroke))
            {
                shape.StrokeWidth(outlineWidth * scale);
            }

            shape.pImpl.Update(renderer, transform, clips, opacity, flag, false);
            return true;
        }

        internal bool PaintRender(RenderMethod renderer, CompositionFlag flag)
        {
            if (loader == null || fm.engine == null) return true;
            renderer.Blend(pImpl.blendMethod);
            return shape.pImpl.Render(renderer);
        }

        internal RenderRegion TextBounds()
        {
            if (!InternalLoad()) return default;
            return shape.PaintBounds();
        }

        /// <summary>
        /// Compute geometric bounds. Mirrors C++ TextImpl::bounds(Point*, Matrix, bool).
        /// Delegates to shape's Paint::Impl::bounds(pt4, &m, obb).
        /// </summary>
        internal bool GeometricBounds(Span<Point> pt4, in Matrix m, bool obb)
        {
            if (!InternalLoad()) return true;
            return shape.pImpl.GeometricBounds(pt4, m, obb);
        }

        internal bool Intersects(in RenderRegion region)
        {
            if (!InternalLoad()) return false;
            return shape.Intersects(region);
        }

        internal Paint DuplicateText(Paint? ret)
        {
            if (ret != null)
            {
                TvgCommon.TVGERR("RENDERER", "TODO: duplicate()");
            }

            InternalLoad();

            var text = Gen();

            shape.DuplicateShape(text.shape);

            if (loader != null)
            {
                text.loader = loader;
                ++text.loader.sharing;
                loader.Copy(fm, text.fm);
            }

            text.utf8 = utf8 != null ? TvgStr.Duplicate(utf8) : null;
            text.italicShear = italicShear;
            text.outlineWidth = outlineWidth;
            text.updated = true;

            return text;
        }

        internal Iterator? GetTextIterator()
        {
            return null;
        }

        // --- Virtual dispatch overrides ---
        internal override Paint DuplicatePaintVirt(Paint? ret) => DuplicateText(ret);
        internal override Iterator? GetIteratorVirt() => GetTextIterator();
        internal override bool PaintSkipVirt(RenderUpdateFlag flag) => PaintSkip(flag);
        internal override bool PaintUpdateVirt(RenderMethod renderer, in Matrix transform, List<object?> clips, byte opacity, RenderUpdateFlag flag, bool clipper) => PaintUpdate(renderer, transform, clips, opacity, flag, clipper);
        internal override bool PaintRenderVirt(RenderMethod renderer, CompositionFlag flag) => PaintRender(renderer, flag);
        internal override RenderRegion PaintBoundsVirt() => TextBounds();
        internal override bool GeometricBoundsVirt(Span<Point> pt4, in Matrix m, bool obb) => GeometricBounds(pt4, m, obb);
        internal override bool IntersectsVirt(in RenderRegion region) => Intersects(region);

        // --- Private helpers ---

        private bool InternalLoad()
        {
            if (loader == null) return false;
            if (updated)
            {
                if (loader.Get(fm, utf8, shape.rs.path))
                {
                    loader.Transform(shape, fm, italicShear);
                }
                updated = false;
            }
            return true;
        }
    }
}
