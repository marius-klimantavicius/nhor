// Ported from ThorVG/src/renderer/tvgText.h and tvgText.cpp

using System;
using System.Collections.Generic;
using System.Threading;

namespace ThorVG
{
    /// <summary>
    /// Text rendering paint. Mirrors C++ tvg::Text / TextImpl.
    /// </summary>
    public class Text : Paint
    {
        internal Shape shape;           // text shape
        internal FontLoader? loader;
        internal FontMetrics fm = new FontMetrics { hinting = true };
        internal string? utf8;
        internal uint utf8len;
        internal float outlineWidth;
        internal float italicShear;
        internal bool updated;
        internal bool lcdSubpixel;     // [LCD Subpixel] per-text opt-in
        private bool preparedLcdSubpixel;

        protected Text()
        {
            shape = Shape.Gen();
            shape.pImpl.parent = this;
            shape.StrokeJoin(StrokeJoin.Round);
        }

        public static Text Gen() => new Text();
        public override Type PaintType() => Type.Text;

        internal void CleanupText()
        {
            if (loader != null)
            {
                loader.Release(fm);
                LoaderMgr.Retrieve(loader);
                loader = null;
            }
            Paint.Rel(shape);
            utf8 = null;
        }

        // --- Public API (mirrors thorvg.h Text) ---

        public Result SetText(string? text)
        {
            this.utf8 = TvgStr.Duplicate(text);
            this.utf8len = utf8 != null ? (uint)TvgUtf8.ByteCount(utf8) : 0;
            updated = true;
            pImpl.Mark(RenderUpdateFlag.Path);
            return Result.Success;
        }

        /// <summary>Select the local TrueType bytecode hinter. Enabled by default;
        /// disabling it uses the unhinted outlines supplied by upstream ThorVG.</summary>
        public Result SetHinting(bool enabled)
        {
            if (fm.hinting == enabled) return Result.Success;
            fm.hinting = enabled;
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
                this.loader.DecrementSharing();
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

        public Result SetItalic(float shear = 0.18f)
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

        /// <summary>
        /// [LCD Subpixel] Enable or disable LCD subpixel rendering for this text.
        /// When enabled (or when <see cref="SwLcdSubpixel.Enabled"/> is true globally),
        /// text is rasterized at 3x horizontal resolution and blended per-channel
        /// for sharper perceived quality on LCD displays.
        /// </summary>
        public Result SetSubpixelRendering(bool enabled)
        {
            if (lcdSubpixel == enabled) return Result.Success;
            lcdSubpixel = enabled;
            updated = true;
            pImpl.Mark(RenderUpdateFlag.Path);
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
            return GetMetrics(ch, out metrics, out _);
        }

        public Result GetMetrics(string? ch, out GlyphMetrics metrics, out int next)
        {
            metrics = default;
            next = 0;
            if (loader == null || fm.fontSize <= 0.0f) return Result.InsufficientCondition;
            if (ch != null && loader.GlyphMetrics(fm, ch, out metrics, out next)) return Result.Success;
            return Result.InvalidArguments;
        }

        public string? GetText() => utf8;

        public uint Lines()
        {
            if (InternalLoad()) return fm.lines;
            return 0;
        }

        /// <summary>
        /// Returns the advance-based width and height of the shaped text.
        /// Unlike Paint.Bounds(), this returns the total advance width (where the
        /// cursor would end up after the last character), not the visual bounding box.
        /// </summary>
        public Result GetTextSize(out float w, out float h)
        {
            w = h = 0;
            if (!InternalLoad()) return Result.InsufficientCondition;
            w = fm.size.x / fm.scale;
            h = fm.size.y / fm.scale;
            return Result.Success;
        }

        /// <summary>Static font loading from file path. Mirrors C++ Text::load(filename).</summary>
        public static Result LoadFont(string? filename)
        {
            if (filename == null) return Result.InvalidArguments;

            var ops = new LoaderOps(Type.Text, Ownership.Transfer);
            return LoaderMgr.Loader(filename, ops, out Result result) != null ? Result.Success : result;
        }

        /// <summary>Static font loading from memory. Mirrors C++ Text::load(name, data, size, mimeType, copy).</summary>
        public static Result LoadFont(string name, byte[]? data, uint size, string? mimeType = "ttf", bool copy = false)
            => LoadFont(name, data, size, mimeType, copy ? Ownership.Copy : Ownership.Borrow);

        internal static Result LoadFont(string name, byte[]? data, uint size, string? mimeType, Ownership owner)
        {
            if (name == null || (size == 0 && data != null)) return Result.InvalidArguments;

            // unload font
            if (data == null)
            {
                if (LoaderMgr.Retrieve(LoaderMgr.Font(name))) return Result.Success;
                return Result.InsufficientCondition;
            }

            if (LoaderMgr.Loader(name, data, size, mimeType, new LoaderOps(Type.Text, owner)) == null) return Result.NonSupport;
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
            return flag == RenderUpdateFlag.None && preparedLcdSubpixel == (SwLcdSubpixel.Enabled || lcdSubpixel);
        }

        internal bool PaintUpdate(RenderMethod renderer, in Matrix transform, ref ValueList<object?> clips, byte opacity, RenderUpdateFlag flag, bool clipper)
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
                else if (fill.GetFillType() == Type.RadialGradient)
                {
                    var rg = (RadialGradient)fill;
                    rg.center = new Point(rg.center.x * scale, rg.center.y * scale);
                    rg.r *= scale;
                    rg.focal = new Point(rg.focal.x * scale, rg.focal.y * scale);
                    rg.fr *= scale;
                }
                else if (fill.GetFillType() == Type.ConicGradient)
                {
                    var conic = (ConicGradient)fill;
                    conic.center = new Point(conic.center.x * scale, conic.center.y * scale);
                }
            }

            if (outlineWidth > 0.0f && pImpl.Marked(RenderUpdateFlag.Stroke))
            {
                shape.StrokeWidth(outlineWidth * scale);
            }

            // Capture eligibility on the caller; SwRenderer stores it per task.
            var lcd = SwLcdSubpixel.Enabled || lcdSubpixel;
            if (preparedLcdSubpixel != lcd) flag |= RenderUpdateFlag.Path;
            var previousContext = SwLcdSubpixel._textContext;
            try
            {
                SwLcdSubpixel._textContext = lcd;
                shape.pImpl.Update(renderer, transform, ref clips, opacity, flag, false);
                preparedLcdSubpixel = lcd;
            }
            finally { SwLcdSubpixel._textContext = previousContext; }
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

        internal bool Intersects(in RenderRegion region, bool visibleOnly)
        {
            if (!InternalLoad()) return false;
            return shape.Intersects(region, false);
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
                text.loader.IncrementSharing();
                loader.Copy(fm, text.fm);
            }

            text.utf8 = utf8 != null ? TvgStr.Duplicate(utf8) : null;
            text.utf8len = utf8len;
            text.italicShear = italicShear;
            text.outlineWidth = outlineWidth;
            text.lcdSubpixel = lcdSubpixel; // [LCD Subpixel]
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
        internal override bool PaintUpdateVirt(RenderMethod renderer, in Matrix transform, ref ValueList<object?> clips, byte opacity, RenderUpdateFlag flag, bool clipper) => PaintUpdate(renderer, transform, ref clips, opacity, flag, clipper);
        internal override bool PaintRenderVirt(RenderMethod renderer, CompositionFlag flag) => PaintRender(renderer, flag);
        internal override RenderRegion PaintBoundsVirt() => TextBounds();
        internal override bool GeometricBoundsVirt(Span<Point> pt4, in Matrix m, bool obb) => GeometricBounds(pt4, m, obb);
        internal override bool IntersectsVirt(in RenderRegion region, bool visibleOnly) => Intersects(region, visibleOnly);

        // --- Private helpers ---

        private bool InternalLoad()
        {
            if (loader == null) return false;
            if (updated)
            {
                if (loader.Get(fm, utf8, utf8len, shape.rs.path))
                {
                    loader.Transform(shape, fm, italicShear);
                }
                updated = false;
            }
            return true;
        }
    }
}
