// Ported from ThorVG/src/renderer/tvgShape.h and ThorVG/inc/thorvg.h

using System;
using System.Collections.Generic;

namespace ThorVG
{
    /// <summary>
    /// 2D shape with path, stroke, and fill properties.
    /// Mirrors C++ tvg::Shape / ShapeImpl.
    /// </summary>
    public class Shape : Paint
    {
        internal RenderShape rs = new RenderShape();

        protected Shape() { }

        // --- Factory ---
        public static Shape Gen() => new Shape();

        public override Type PaintType() => Type.Shape;

        // --- Path commands ---

        /// <summary>
        /// Public reset: only clears path, NOT fill/stroke/color/rule.
        /// Mirrors C++ Shape::reset() which calls resetPath() only.
        /// </summary>
        public Result ResetShape()
        {
            rs.path.cmds.Clear();
            rs.path.pts.Clear();
            pImpl.Mark(RenderUpdateFlag.Path);
            return Result.Success;
        }

        /// <summary>
        /// Full internal reset (path + color + rule + stroke + fill).
        /// Mirrors C++ ShapeImpl::reset() (the internal version).
        /// </summary>
        internal void ResetFull()
        {
            pImpl.Reset();
            rs.path.cmds.Clear();
            rs.path.pts.Clear();
            rs.color.a = 0;
            rs.rule = FillRule.NonZero;
            rs.stroke = null;
            rs.fill = null;
        }

        public unsafe Result MoveTo(float x, float y)
        {
            rs.path.MoveTo(new Point(x, y));
            // Note: C++ Shape::moveTo does NOT mark Path here (unlike lineTo/cubicTo/close)
            return Result.Success;
        }

        public unsafe Result LineTo(float x, float y)
        {
            rs.path.LineTo(new Point(x, y));
            pImpl.Mark(RenderUpdateFlag.Path);
            return Result.Success;
        }

        public unsafe Result CubicTo(float cx1, float cy1, float cx2, float cy2, float x, float y)
        {
            rs.path.CubicTo(new Point(cx1, cy1), new Point(cx2, cy2), new Point(x, y));
            pImpl.Mark(RenderUpdateFlag.Path);
            return Result.Success;
        }

        public unsafe Result Close()
        {
            rs.path.Close();
            pImpl.Mark(RenderUpdateFlag.Path);
            return Result.Success;
        }

        // --- Append helpers ---
        public unsafe Result AppendRect(float x, float y, float w, float h, float rx = 0, float ry = 0, bool cw = true)
        {
            if (TvgMath.Zero(rx) && TvgMath.Zero(ry))
            {
                rs.path.cmds.Grow(5);
                rs.path.pts.Grow(4);
                var cmds = rs.path.cmds.End();
                var pts = rs.path.pts.End();
                cmds[0] = PathCommand.MoveTo;
                cmds[1] = cmds[2] = cmds[3] = PathCommand.LineTo;
                cmds[4] = PathCommand.Close;
                pts[0] = new Point(x + w, y);
                pts[2] = new Point(x, y + h);
                if (cw) { pts[1] = new Point(x + w, y + h); pts[3] = new Point(x, y); }
                else { pts[1] = new Point(x, y); pts[3] = new Point(x + w, y + h); }
                rs.path.cmds.count += 5;
                rs.path.pts.count += 4;
            }
            else
            {
                var hsize = new Point(w * 0.5f, h * 0.5f);
                if (rx > hsize.x) rx = hsize.x;
                if (ry > hsize.y) ry = hsize.y;
                var hr = new Point(rx * MathConstants.PATH_KAPPA, ry * MathConstants.PATH_KAPPA);

                rs.path.cmds.Grow(10);
                rs.path.pts.Grow(17);
                var cmds = rs.path.cmds.End();
                var pts = rs.path.pts.End();
                cmds[0] = PathCommand.MoveTo;
                cmds[9] = PathCommand.Close;
                pts[0] = new Point(x + w, y + ry);
                if (cw)
                {
                    cmds[1] = cmds[3] = cmds[5] = cmds[7] = PathCommand.LineTo;
                    cmds[2] = cmds[4] = cmds[6] = cmds[8] = PathCommand.CubicTo;
                    pts[1] = new Point(x + w, y + h - ry);
                    pts[2] = new Point(x + w, y + h - ry + hr.y); pts[3] = new Point(x + w - rx + hr.x, y + h); pts[4] = new Point(x + w - rx, y + h);
                    pts[5] = new Point(x + rx, y + h);
                    pts[6] = new Point(x + rx - hr.x, y + h); pts[7] = new Point(x, y + h - ry + hr.y); pts[8] = new Point(x, y + h - ry);
                    pts[9] = new Point(x, y + ry);
                    pts[10] = new Point(x, y + ry - hr.y); pts[11] = new Point(x + rx - hr.x, y); pts[12] = new Point(x + rx, y);
                    pts[13] = new Point(x + w - rx, y);
                    pts[14] = new Point(x + w - rx + hr.x, y); pts[15] = new Point(x + w, y + ry - hr.y); pts[16] = new Point(x + w, y + ry);
                }
                else
                {
                    cmds[1] = cmds[3] = cmds[5] = cmds[7] = PathCommand.CubicTo;
                    cmds[2] = cmds[4] = cmds[6] = cmds[8] = PathCommand.LineTo;
                    pts[1] = new Point(x + w, y + ry - hr.y); pts[2] = new Point(x + w - rx + hr.x, y); pts[3] = new Point(x + w - rx, y);
                    pts[4] = new Point(x + rx, y);
                    pts[5] = new Point(x + rx - hr.x, y); pts[6] = new Point(x, y + ry - hr.y); pts[7] = new Point(x, y + ry);
                    pts[8] = new Point(x, y + h - ry);
                    pts[9] = new Point(x, y + h - ry + hr.y); pts[10] = new Point(x + rx - hr.x, y + h); pts[11] = new Point(x + rx, y + h);
                    pts[12] = new Point(x + w - rx, y + h);
                    pts[13] = new Point(x + w - rx + hr.x, y + h); pts[14] = new Point(x + w, y + h - ry + hr.y); pts[15] = new Point(x + w, y + h - ry);
                    pts[16] = new Point(x + w, y + ry);
                }
                rs.path.cmds.count += 10;
                rs.path.pts.count += 17;
            }
            pImpl.Mark(RenderUpdateFlag.Path);
            return Result.Success;
        }

        public unsafe Result AppendCircle(float cx, float cy, float rx, float ry, bool cw = true)
        {
            var rxk = rx * MathConstants.PATH_KAPPA;
            var ryk = ry * MathConstants.PATH_KAPPA;
            rs.path.cmds.Grow(6);
            var cmds = rs.path.cmds.End();
            cmds[0] = PathCommand.MoveTo;
            cmds[1] = cmds[2] = cmds[3] = cmds[4] = PathCommand.CubicTo;
            cmds[5] = PathCommand.Close;
            rs.path.cmds.count += 6;

            ReadOnlySpan<int> tableCw = stackalloc int[]{0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12};
            ReadOnlySpan<int> tableCcw = stackalloc int[]{0, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 12};
            var idx = cw ? tableCw : tableCcw;

            rs.path.pts.Grow(13);
            var pts = rs.path.pts.End();
            pts[idx[0]] = new Point(cx, cy - ry);
            pts[idx[1]] = new Point(cx + rxk, cy - ry); pts[idx[2]] = new Point(cx + rx, cy - ryk); pts[idx[3]] = new Point(cx + rx, cy);
            pts[idx[4]] = new Point(cx + rx, cy + ryk); pts[idx[5]] = new Point(cx + rxk, cy + ry); pts[idx[6]] = new Point(cx, cy + ry);
            pts[idx[7]] = new Point(cx - rxk, cy + ry); pts[idx[8]] = new Point(cx - rx, cy + ryk); pts[idx[9]] = new Point(cx - rx, cy);
            pts[idx[10]] = new Point(cx - rx, cy - ryk); pts[idx[11]] = new Point(cx - rxk, cy - ry); pts[idx[12]] = new Point(cx, cy - ry);
            rs.path.pts.count += 13;
            pImpl.Mark(RenderUpdateFlag.Path);
            return Result.Success;
        }

        // --- Fill ---
        public Result SetFill(byte r, byte g, byte b, byte a = 255)
        {
            if (rs.fill != null)
            {
                rs.fill = null;
                pImpl.Mark(RenderUpdateFlag.Gradient);
            }
            if (r == rs.color.r && g == rs.color.g && b == rs.color.b && a == rs.color.a) return Result.Success;
            rs.color = new RGBA(r, g, b, a);
            pImpl.Mark(RenderUpdateFlag.Color);
            return Result.Success;
        }

        public Result SetFill(Fill f)
        {
            if (f == null) return Result.InvalidArguments;
            rs.fill = f;
            pImpl.Mark(RenderUpdateFlag.Gradient);
            return Result.Success;
        }

        public Result GetFillColor(out byte r, out byte g, out byte b, out byte a)
        {
            r = rs.color.r; g = rs.color.g; b = rs.color.b; a = rs.color.a;
            return Result.Success;
        }

        public Fill? GetFill() => rs.fill;

        public Result SetFillRule(FillRule rule) { rs.rule = rule; return Result.Success; }
        public FillRule GetFillRule() => rs.rule;

        // --- Stroke ---
        public Result StrokeWidth(float width)
        {
            if (width < 0.0f) width = 0.0f;
            rs.stroke ??= new RenderStroke();
            rs.stroke.width = width;
            pImpl.Mark(RenderUpdateFlag.Stroke);
            return Result.Success;
        }

        public float GetStrokeWidth() => rs.StrokeWidth();

        public Result StrokeFill(byte r, byte g, byte b, byte a = 255)
        {
            rs.stroke ??= new RenderStroke();
            if (rs.stroke.fill != null)
            {
                rs.stroke.fill = null;
                pImpl.Mark(RenderUpdateFlag.GradientStroke);
            }
            rs.stroke.color = new RGBA(r, g, b, a);
            pImpl.Mark(RenderUpdateFlag.Stroke);
            return Result.Success;
        }

        public Result StrokeFill(Fill f)
        {
            if (f == null) return Result.InvalidArguments;
            rs.stroke ??= new RenderStroke();
            rs.stroke.fill = f;
            rs.stroke.color.a = 0;
            pImpl.Mark(RenderUpdateFlag.Stroke | RenderUpdateFlag.GradientStroke);
            return Result.Success;
        }

        public Result GetStrokeFill(out byte r, out byte g, out byte b, out byte a)
        {
            if (rs.stroke == null) { r = g = b = a = 0; return Result.InsufficientCondition; }
            r = rs.stroke.color.r; g = rs.stroke.color.g;
            b = rs.stroke.color.b; a = rs.stroke.color.a;
            return Result.Success;
        }

        public Fill? GetStrokeFillGradient() => rs.StrokeFillGradient();

        public Result StrokeJoin(StrokeJoin join)
        {
            rs.stroke ??= new RenderStroke();
            rs.stroke.join = join;
            pImpl.Mark(RenderUpdateFlag.Stroke);
            return Result.Success;
        }

        public StrokeJoin GetStrokeJoin() => rs.StrokeJoin();

        public Result StrokeCap(StrokeCap cap)
        {
            rs.stroke ??= new RenderStroke();
            rs.stroke.cap = cap;
            pImpl.Mark(RenderUpdateFlag.Stroke);
            return Result.Success;
        }

        public StrokeCap GetStrokeCap() => rs.StrokeCap();

        public Result StrokeMiterlimit(float miterlimit)
        {
            if (miterlimit < 0.0f) return Result.InvalidArguments;
            rs.stroke ??= new RenderStroke();
            rs.stroke.miterlimit = miterlimit;
            pImpl.Mark(RenderUpdateFlag.Stroke);
            return Result.Success;
        }

        public float GetStrokeMiterlimit() => rs.StrokeMiterlimit();

        public Result StrokeDash(float[]? pattern, uint cnt, float offset = 0.0f)
        {
            if (pattern == null && cnt > 0) return Result.InvalidArguments;
            if (pattern != null && cnt == 0) return Result.InvalidArguments;
            rs.stroke ??= new RenderStroke();
            if (cnt > 0 && pattern != null)
            {
                rs.stroke.dashPattern = new float[cnt];
                rs.stroke.dashLength = 0.0f;
                for (uint i = 0; i < cnt; ++i)
                {
                    rs.stroke.dashPattern[i] = pattern[i] < 0.0f ? 0.0f : pattern[i];
                    rs.stroke.dashLength += rs.stroke.dashPattern[i];
                }
            }
            else
            {
                rs.stroke.dashPattern = null;
                rs.stroke.dashLength = 0.0f;
            }
            rs.stroke.dashCount = cnt;
            rs.stroke.dashOffset = offset;
            pImpl.Mark(RenderUpdateFlag.Stroke);
            return Result.Success;
        }

        public Result Trimpath(float begin, float end, bool simultaneous = true)
        {
            if (rs.stroke == null)
            {
                if (begin == 0.0f && end == 1.0f) return Result.Success;
                rs.stroke = new RenderStroke();
            }
            if (TvgMath.Equal(rs.stroke.trim.begin, begin) && TvgMath.Equal(rs.stroke.trim.end, end) && rs.stroke.trim.simultaneous == simultaneous) return Result.Success;
            rs.stroke.trim = new RenderTrimPath { begin = begin, end = end, simultaneous = simultaneous };
            pImpl.Mark(RenderUpdateFlag.Path);
            return Result.Success;
        }

        public Result Order(bool strokeFirst)
        {
            rs.stroke ??= new RenderStroke();
            rs.stroke.first = strokeFirst;
            pImpl.Mark(RenderUpdateFlag.Stroke);
            return Result.Success;
        }

        public unsafe Result AppendPath(PathCommand[]? cmds, uint cmdsCnt, Point[]? pts, uint ptsCnt)
        {
            if (cmds == null || pts == null || cmdsCnt == 0 || ptsCnt == 0) return Result.InvalidArguments;
            rs.path.cmds.Grow(cmdsCnt);
            var dst = rs.path.cmds.End();
            for (uint i = 0; i < cmdsCnt; i++) dst[i] = cmds[i];
            rs.path.cmds.count += cmdsCnt;
            rs.path.pts.Grow(ptsCnt);
            var pdst = rs.path.pts.End();
            for (uint i = 0; i < ptsCnt; i++) pdst[i] = pts[i];
            rs.path.pts.count += ptsCnt;
            pImpl.Mark(RenderUpdateFlag.Path);
            return Result.Success;
        }

        public uint GetStrokeDash(out float[]? pattern, out float offset)
        {
            if (rs.stroke == null || rs.stroke.dashCount == 0)
            {
                pattern = null;
                offset = 0.0f;
                return 0;
            }
            pattern = rs.stroke.dashPattern;
            offset = rs.stroke.dashOffset;
            return rs.stroke.dashCount;
        }

        public unsafe Result GetPath(out PathCommand* cmds, out uint cmdsCnt, out Point* pts, out uint ptsCnt)
        {
            cmds = rs.path.cmds.data;
            cmdsCnt = rs.path.cmds.count;
            pts = rs.path.pts.data;
            ptsCnt = rs.path.pts.count;
            return Result.Success;
        }

        // --- Rendering infrastructure (mirrors C++ ShapeImpl) ---

        internal bool PaintSkip(RenderUpdateFlag flag)
        {
            return flag == RenderUpdateFlag.None;
        }

        internal bool PaintUpdate(RenderMethod renderer, in Matrix transform, ref ValueList<object?> clips, byte opacity, RenderUpdateFlag flag, bool clipper)
        {
            if (NeedComposition(opacity))
            {
                pImpl.opacity = opacity;
                opacity = 255;
            }
            pImpl.rd = renderer.Prepare(rs, pImpl.rd, transform, ref clips, opacity, flag, clipper);
            return true;
        }

        internal bool PaintRender(RenderMethod renderer, CompositionFlag flag)
        {
            if (pImpl.rd == null) return false;

            RenderCompositor? cmp = null;

            renderer.Blend(pImpl.blendMethod);

            if (pImpl.cmpFlag != CompositionFlag.Invalid)
            {
                cmp = renderer.Target(PaintBounds(), renderer.ColorSpaceValue(), pImpl.cmpFlag);
                renderer.BeginComposite(cmp, MaskMethod.None, pImpl.opacity);
            }

            var ret = renderer.RenderShape(pImpl.rd);
            if (cmp != null) renderer.EndComposite(cmp);
            return ret;
        }

        internal RenderRegion PaintBounds()
        {
            return pImpl.renderer!.Region(pImpl.rd);
        }

        /// <summary>
        /// Compute geometric bounds from path data. Mirrors C++ ShapeImpl::bounds(Point*, Matrix, bool).
        /// Falls back to RenderPath.Bounds() when the renderer is not available or doesn't support bounds.
        /// </summary>
        internal unsafe bool GeometricBounds(Span<Point> pt4, in Matrix m, bool obb)
        {
            // Fallback: compute from path data directly
            var box = new BBox();
            box.Init();
            fixed (Matrix* mp = &m)
            {
                if (!rs.path.Bounds(obb ? null : mp, ref box)) return false;
            }
            if (rs.stroke != null)
            {
                // Use geometric mean for feathering (matches C++)
                var sx = (float)Math.Sqrt(m.e11 * m.e11 + m.e21 * m.e21);
                var sy = (float)Math.Sqrt(m.e12 * m.e12 + m.e22 * m.e22);
                var feather = rs.stroke.width * (float)Math.Sqrt(sx * sy);
                box.min.x -= feather * 0.5f;
                box.min.y -= feather * 0.5f;
                box.max.x += feather * 0.5f;
                box.max.y += feather * 0.5f;
            }
            pt4[0] = box.min;
            pt4[1] = new Point(box.max.x, box.min.y);
            pt4[2] = box.max;
            pt4[3] = new Point(box.min.x, box.max.y);

            if (obb)
            {
                pt4[0] = TvgMath.Transform(pt4[0], m);
                pt4[1] = TvgMath.Transform(pt4[1], m);
                pt4[2] = TvgMath.Transform(pt4[2], m);
                pt4[3] = TvgMath.Transform(pt4[3], m);
            }

            return true;
        }

        private bool NeedComposition(byte opacity)
        {
            if (opacity == 0) return false;

            // Shape composition is only necessary when stroking & fill are valid.
            if (rs.stroke == null || rs.stroke.width < MathConstants.FLOAT_EPSILON || (rs.stroke.fill == null && rs.stroke.color.a == 0)) return false;
            if (rs.fill == null && rs.color.a == 0) return false;

            // translucent fill & stroke
            if (opacity < 255)
            {
                pImpl.Mark(CompositionFlag.Opacity);
                return true;
            }

            // Composition test
            var method = pImpl.GetMask(out var target);
            if (target == null) return false;

            if ((target.pImpl.opacity == 255 || target.pImpl.opacity == 0) && target.PaintType() == Type.Shape)
            {
                var shape = (Shape)target;
                if (shape.rs.fill == null)
                {
                    var a = shape.rs.color.a;
                    if (a == 0 || a == 255)
                    {
                        if (method == MaskMethod.Luma || method == MaskMethod.InvLuma)
                        {
                            var r2 = shape.rs.color.r;
                            var g2 = shape.rs.color.g;
                            var b2 = shape.rs.color.b;
                            if ((r2 == 255 && g2 == 255 && b2 == 255) || (r2 == 0 && g2 == 0 && b2 == 0)) return false;
                        }
                        else return false;
                    }
                }
            }

            pImpl.Mark(CompositionFlag.Masking);
            return true;
        }

        // --- Missing methods from C++ ShapeImpl ---

        /// <summary>Renderer-based intersection test. Mirrors C++ ShapeImpl::intersects().</summary>
        internal bool Intersects(in RenderRegion region)
        {
            if (pImpl.rd == null || pImpl.renderer == null) return false;
            return pImpl.renderer.IntersectsShape(pImpl.rd, region);
        }

        /// <summary>Pre-allocate command buffer. Mirrors C++ ShapeImpl::reserveCmd().</summary>
        internal void ReserveCmd(uint cmdCnt)
        {
            rs.path.cmds.Reserve(cmdCnt);
        }

        /// <summary>Pre-allocate point buffer. Mirrors C++ ShapeImpl::reservePts().</summary>
        internal void ReservePts(uint ptsCnt)
        {
            rs.path.pts.Reserve(ptsCnt);
        }

        /// <summary>Get trimpath begin/end values. Mirrors C++ ShapeImpl::trimpath(float*, float*).</summary>
        internal bool GetTrimpath(out float begin, out float end)
        {
            if (rs.stroke != null)
            {
                begin = rs.stroke.trim.begin;
                end = rs.stroke.trim.end;
                return rs.stroke.trim.simultaneous;
            }
            begin = 0.0f;
            end = 1.0f;
            return false;
        }

        /// <summary>Returns null -- shapes have no children. Mirrors C++ ShapeImpl::iterator().</summary>
        internal Iterator? GetShapeIterator() => null;

        // --- Virtual dispatch overrides ---
        internal override Paint DuplicatePaintVirt(Paint? ret) => DuplicateShape(ret);
        internal override Iterator? GetIteratorVirt() => GetShapeIterator();
        internal override bool PaintSkipVirt(RenderUpdateFlag flag) => PaintSkip(flag);
        internal override bool PaintUpdateVirt(RenderMethod renderer, in Matrix transform, ref ValueList<object?> clips, byte opacity, RenderUpdateFlag flag, bool clipper) => PaintUpdate(renderer, transform, ref clips, opacity, flag, clipper);
        internal override bool PaintRenderVirt(RenderMethod renderer, CompositionFlag flag) => PaintRender(renderer, flag);
        internal override RenderRegion PaintBoundsVirt() => PaintBounds();
        internal override bool GeometricBoundsVirt(Span<Point> pt4, in Matrix m, bool obb) => GeometricBounds(pt4, m, obb);
        internal override bool IntersectsVirt(in RenderRegion region) => Intersects(region);

        // --- Internal duplication ---
        internal Paint DuplicateShape(Paint? ret)
        {
            var shape = ret as Shape ?? Gen();

            shape.rs.path.Clear();
            shape.rs.path.cmds.Push(rs.path.cmds);
            shape.rs.path.pts.Push(rs.path.pts);

            shape.rs.fill = rs.fill?.Duplicate();

            if (rs.stroke != null)
            {
                shape.rs.stroke ??= new RenderStroke();
                shape.rs.stroke.CopyFrom(rs.stroke);
            }
            else
            {
                shape.rs.stroke = null;
            }

            shape.rs.color = rs.color;
            shape.rs.rule = rs.rule;
            return shape;
        }
    }
}
