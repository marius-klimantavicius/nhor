// Ported from ThorVG/src/renderer/tvgPaint.h and tvgPaint.cpp

using System;
using System.Collections.Generic;

namespace ThorVG
{
    /// <summary>Iterator for traversing paint children. Mirrors C++ tvg::Iterator.</summary>
    public abstract class Iterator
    {
        public abstract Paint? Next();
        public abstract uint Count();
        public abstract void Begin();
    }

    /// <summary>Mask composition data. Mirrors C++ tvg::Mask.</summary>
    public class Mask
    {
        public Paint target = null!;
        public Paint source = null!;
        public MaskMethod method;
    }

    /// <summary>
    /// Abstract base for all graphical elements. Mirrors C++ tvg::Paint.
    /// </summary>
    public abstract class Paint
    {
        public PaintImpl pImpl;
        public uint id;

        protected Paint()
        {
            pImpl = new PaintImpl(this);
        }

        // --- Public API (mirrors thorvg.h Paint struct) ---

        public Paint? Parent() => pImpl.parent;
        public Result Visible(bool on) => pImpl.SetVisible(!on);
        public Result Rotate(float degree) => pImpl.Rotate(degree) ? Result.Success : Result.InsufficientCondition;
        public Result Scale(float factor) => pImpl.Scale(factor) ? Result.Success : Result.InsufficientCondition;
        public Result Translate(float x, float y) => pImpl.Translate(x, y) ? Result.Success : Result.InsufficientCondition;

        public Result Transform(in Matrix m)
        {
            return pImpl.Transform(m) ? Result.Success : Result.InsufficientCondition;
        }

        public ref Matrix Transform() => ref pImpl.Transform();

        public Result Opacity(byte o)
        {
            if (pImpl.opacity != o)
            {
                pImpl.opacity = o;
                pImpl.Mark(RenderUpdateFlag.Color);
            }
            return Result.Success;
        }

        public byte Opacity() => pImpl.opacity;

        public Result SetMask(Paint? target, MaskMethod method)
        {
            if (method > MaskMethod.Darken) return Result.InvalidArguments;
            return pImpl.SetMask(target, method);
        }

        public MaskMethod GetMask(out Paint? target) => pImpl.GetMask(out target);

        public Result Clip(Shape? clipper) => pImpl.Clip(clipper);
        public Shape? GetClipper() => pImpl.clipper;

        public bool IsVisible() => !pImpl.hidden;

        public Result SetBlend(BlendMethod method)
        {
            if (method <= BlendMethod.Add || method == BlendMethod.Composition)
            {
                pImpl.SetBlend(method);
                return Result.Success;
            }
            return Result.InvalidArguments;
        }

        public ushort Ref() => pImpl.Ref();
        public ushort Unref(bool free = true) => pImpl.UnrefX(free);
        public ushort RefCnt() => pImpl.refCnt;

        public Paint Duplicate() => pImpl.Duplicate();

        public Result Bounds(out float x, out float y, out float w, out float h)
        {
            Span<Point> pt4 = stackalloc Point[4];
            var pm = pImpl.Ptransform();
            if (pImpl.GeometricBounds(pt4, pm, false))
            {
                var box = new BBox();
                box.Init();
                for (int i = 0; i < 4; ++i)
                {
                    if (pt4[i].x < box.min.x) box.min.x = pt4[i].x;
                    if (pt4[i].x > box.max.x) box.max.x = pt4[i].x;
                    if (pt4[i].y < box.min.y) box.min.y = pt4[i].y;
                    if (pt4[i].y > box.max.y) box.max.y = pt4[i].y;
                }
                x = box.min.x;
                y = box.min.y;
                w = box.max.x - box.min.x;
                h = box.max.y - box.min.y;
                return Result.Success;
            }
            x = y = w = h = 0;
            return Result.InsufficientCondition;
        }

        public Result Bounds(Point[] pts)
        {
            if (pts == null || pts.Length < 4) return Result.InvalidArguments;
            var pm = pImpl.Ptransform();
            if (pImpl.GeometricBounds(pts, pm, true)) return Result.Success;
            return Result.InsufficientCondition;
        }

        public bool Intersects(int x, int y, int w = 1, int h = 1)
        {
            if (w <= 0 || h <= 0) return false;
            return pImpl.Intersects(new RenderRegion(x, y, x + w, y + h));
        }

        public abstract Type PaintType();

        // Virtual dispatch methods replacing PaintType() switch patterns
        internal virtual Paint DuplicatePaintVirt(Paint? ret) => ret ?? Shape.Gen();
        internal virtual Iterator? GetIteratorVirt() => null;
        internal virtual bool PaintSkipVirt(RenderUpdateFlag flag) => false;
        internal virtual bool PaintUpdateVirt(RenderMethod renderer, in Matrix transform, List<object?> clips, byte opacity, RenderUpdateFlag flag, bool clipper) => false;
        internal virtual bool PaintRenderVirt(RenderMethod renderer, CompositionFlag flag) => false;
        internal virtual RenderRegion PaintBoundsVirt() => default;
        internal virtual bool GeometricBoundsVirt(Span<Point> pt4, in Matrix m, bool obb) => false;
        internal virtual bool IntersectsVirt(in RenderRegion region) => false;

        public static void Rel(Paint? paint)
        {
            if (paint != null && paint.RefCnt() <= 0)
            {
                paint.pImpl.Cleanup();
            }
        }
    }

    /// <summary>Internal Paint implementation. Mirrors C++ Paint::Impl.</summary>
    public class PaintImpl
    {
        public Paint paint;
        public Paint? parent;
        public Mask? maskData;
        public Shape? clipper;
        public RenderMethod? renderer;
        public object? rd;

        public struct TransformData
        {
            public Matrix m;
            public float degree;
            public float scale;
            public bool overriding;

            public void Update()
            {
                if (overriding) return;
                m.e11 = 1.0f; m.e12 = 0.0f;
                m.e21 = 0.0f; m.e22 = 1.0f;
                m.e31 = 0.0f; m.e32 = 0.0f; m.e33 = 1.0f;
                TvgMath.Scale(ref m, new Point(scale, scale));
                TvgMath.Rotate(ref m, degree);
            }
        }

        public TransformData tr;
        public RenderUpdateFlag renderFlag = RenderUpdateFlag.None;
        public CompositionFlag cmpFlag = CompositionFlag.Invalid;
        public BlendMethod blendMethod;
        public ushort refCnt;
        public byte ctxFlag;
        public byte opacity;
        public bool hidden;

        public PaintImpl(Paint pnt)
        {
            paint = pnt;
            hidden = false;
            Reset();
        }

        public ushort Ref() => ++refCnt;

        public ushort Unref(bool free = true)
        {
            parent = null;
            return UnrefX(free);
        }

        public ushort UnrefX(bool free)
        {
            if (refCnt > 0) --refCnt;

            if (free && refCnt == 0)
            {
                Cleanup();
                return 0;
            }

            return refCnt;
        }

        /// <summary>
        /// Deterministic cleanup of render resources. Mirrors C++ ~Paint::Impl destructor.
        /// Frees maskData target, unrefs clipper, disposes render data via renderer, unrefs renderer.
        /// </summary>
        internal void Cleanup()
        {
            // Scene-specific: unref children before renderer is disposed.
            // Mirrors C++ ~SceneImpl() destructor which calls clearPaints() + resetEffects(false).
            if (paint is Scene scene)
            {
                foreach (var p in scene.paints)
                {
                    p.pImpl.Unref();
                }
                scene.paints.Clear();
                scene.ResetEffects(false);
            }

            if (maskData != null)
            {
                maskData.target.pImpl.Unref();
                maskData = null;
            }

            if (clipper != null)
            {
                clipper.pImpl.Unref();
                clipper = null;
            }

            if (renderer != null)
            {
                if (rd != null) renderer.Dispose(rd);
                renderer.Unref();
                renderer = null;
                rd = null;
            }
        }

        public void Mark(CompositionFlag flag)
        {
            cmpFlag = (CompositionFlag)((byte)cmpFlag | (byte)flag);
        }

        public bool Marked(CompositionFlag flag)
        {
            return ((byte)cmpFlag & (byte)flag) != 0;
        }

        public bool Marked(RenderUpdateFlag flag)
        {
            return (renderFlag & flag) != 0;
        }

        public void Mark(RenderUpdateFlag flag)
        {
            renderFlag |= flag;
        }

        public bool Transform(in Matrix m)
        {
            tr.m = m;
            tr.overriding = true;
            Mark(RenderUpdateFlag.Transform);
            return true;
        }

        public ref Matrix Transform()
        {
            if ((renderFlag & RenderUpdateFlag.Transform) != 0) tr.Update();
            return ref tr.m;
        }

        public Result Clip(Shape? clp)
        {
            if (clp != null && clp.pImpl.parent != null) return Result.InsufficientCondition;
            if (clipper != null) clipper.pImpl.Unref(clipper != clp);
            clipper = clp;
            if (clp != null)
            {
                clp.Ref();
                clp.pImpl.parent = parent;
            }
            return Result.Success;
        }

        public Result SetMask(Paint? target, MaskMethod method)
        {
            if (target != null && target.pImpl.parent != null) return Result.InsufficientCondition;

            if (maskData != null)
            {
                maskData.target.pImpl.Unref(maskData.target != target);
                maskData = null;
            }

            if (method == MaskMethod.None) return target != null ? Result.InvalidArguments : Result.Success;

            maskData = new Mask();
            target!.Ref();
            maskData.target = target;
            target.pImpl.parent = parent;
            maskData.source = paint;
            maskData.method = method;
            return Result.Success;
        }

        public MaskMethod GetMask(out Paint? target)
        {
            if (maskData != null)
            {
                target = maskData.target;
                return maskData.method;
            }
            target = null;
            return MaskMethod.None;
        }

        public void Reset()
        {
            if (clipper != null)
            {
                clipper.pImpl.Unref();
                clipper = null;
            }

            if (maskData != null)
            {
                maskData.target.pImpl.Unref();
                maskData = null;
            }

            TvgMath.SetIdentity(out tr.m);
            tr.degree = 0.0f;
            tr.scale = 1.0f;
            tr.overriding = false;
            parent = null;
            blendMethod = BlendMethod.Normal;
            renderFlag = RenderUpdateFlag.None;
            ctxFlag = (byte)ContextFlag.Default;
            opacity = 255;
            paint.id = 0;
        }

        public bool Rotate(float degree)
        {
            if (tr.overriding) return false;
            if (TvgMath.Equal(degree, tr.degree)) return true;
            tr.degree = degree;
            Mark(RenderUpdateFlag.Transform);
            return true;
        }

        public bool Scale(float factor)
        {
            if (tr.overriding) return false;
            if (TvgMath.Equal(factor, tr.scale)) return true;
            tr.scale = factor;
            Mark(RenderUpdateFlag.Transform);
            return true;
        }

        public bool Translate(float x, float y)
        {
            if (tr.overriding) return false;
            if (TvgMath.Equal(x, tr.m.e13) && TvgMath.Equal(y, tr.m.e23)) return true;
            tr.m.e13 = x;
            tr.m.e23 = y;
            Mark(RenderUpdateFlag.Transform);
            return true;
        }

        public void SetBlend(BlendMethod method)
        {
            if (blendMethod != method)
            {
                blendMethod = method;
                Mark(RenderUpdateFlag.Blend);
            }
        }

        public Result SetVisible(bool h)
        {
            if (hidden != h)
            {
                hidden = h;
                Damage();
            }
            return Result.Success;
        }

        // Dispatching methods that delegate to the concrete Paint subclass impl
        public Paint Duplicate(Paint? ret = null)
        {
            if (ret != null) ret.SetMask(null, MaskMethod.None);

            ret = paint.DuplicatePaintVirt(ret);

            if (maskData != null) ret.SetMask(maskData.target.Duplicate(), maskData.method);
            if (clipper != null) ret.Clip((Shape?)clipper.Duplicate());

            ret.pImpl.tr = tr;
            ret.pImpl.blendMethod = blendMethod;
            ret.pImpl.opacity = opacity;
            ret.pImpl.hidden = hidden;
            ret.pImpl.Mark(RenderUpdateFlag.All);

            return ret;
        }

        public virtual Iterator? GetIterator()
        {
            return paint.GetIteratorVirt();
        }

        // =====================================================================
        //  Paint dispatch helpers (mirrors C++ PAINT_METHOD macro)
        // =====================================================================

        private bool PaintSkip(RenderUpdateFlag flag) => paint.PaintSkipVirt(flag);

        private bool PaintUpdate(RenderMethod renderer, in Matrix transform, List<object?> clips, byte opacity, RenderUpdateFlag flag, bool clipper)
            => paint.PaintUpdateVirt(renderer, transform, clips, opacity, flag, clipper);

        private bool PaintRender(RenderMethod renderer, CompositionFlag flag) => paint.PaintRenderVirt(renderer, flag);

        internal RenderRegion PaintBounds() => paint.PaintBoundsVirt();

        // =====================================================================
        //  Ptransform — accumulated parent transform (excludes own transform)
        //  Mirrors C++ Paint::Impl::ptransform()
        // =====================================================================

        internal Matrix Ptransform()
        {
            var p = this;
            var tm = TvgMath.Identity();
            while (p.parent != null)
            {
                p = p.parent.pImpl;
                tm = TvgMath.Multiply(p.Transform(), tm);
            }
            return tm;
        }

        // =====================================================================
        //  GeometricBounds — bounds from path data, not from renderer
        //  Mirrors C++ Paint::Impl::bounds(Point*, const Matrix*, bool)
        // =====================================================================

        internal bool GeometricBounds(Span<Point> pt4, in Matrix pm, bool obb)
        {
            var m = TvgMath.Multiply(pm, Transform());
            return paint.GeometricBoundsVirt(pt4, m, obb);
        }

        // =====================================================================
        //  Bounds (public) — renderer-based region (for internal rendering use)
        // =====================================================================

        internal RenderRegion Bounds()
        {
            return PaintBounds();
        }

        // =====================================================================
        //  Intersects — dispatch to concrete type's intersects method
        //  Mirrors C++ Paint::Impl::intersects(const RenderRegion&)
        // =====================================================================

        internal bool Intersects(in RenderRegion region)
        {
            if (renderer == null) return false;
            return paint.IntersectsVirt(region);
        }

        // =====================================================================
        //  _compFastTrack — viewport optimization for simple rect masks/clips
        // =====================================================================

        private static unsafe bool _compFastTrack(RenderMethod renderer, Paint cmpTarget, in Matrix pm, ref RenderRegion before)
        {
            if (cmpTarget is not Shape shape) return false;

            // Trimming likely makes the shape non-rectangular
            if (shape.rs.Trimpath()) return false;

            // Rectangle candidates?
            PathCommand* cmds;
            uint cmdsCnt;
            Point* pts;
            uint ptsCnt;
            shape.GetPath(out cmds, out cmdsCnt, out pts, out ptsCnt);

            // No rectangle format
            if (ptsCnt != 4) return false;

            // No rotation and no skewing
            var tm = TvgMath.Multiply(pm, cmpTarget.pImpl.Transform());

            // Perpendicular Rectangle?
            if (TvgMath.RightAngle(tm) && !TvgMath.Skewed(tm))
            {
                var pt1 = pts[0];
                var pt2 = pts[1];
                var pt3 = pts[2];
                var pt4 = pts[3];

                if ((TvgMath.Equal(pt1.x, pt2.x) && TvgMath.Equal(pt2.y, pt3.y) && TvgMath.Equal(pt3.x, pt4.x) && TvgMath.Equal(pt1.y, pt4.y)) ||
                    (TvgMath.Equal(pt2.x, pt3.x) && TvgMath.Equal(pt1.y, pt2.y) && TvgMath.Equal(pt1.x, pt4.x) && TvgMath.Equal(pt3.y, pt4.y)))
                {
                    var v1 = TvgMath.Transform(pt1, tm);
                    var v2 = TvgMath.Transform(pt3, tm);

                    // sorting
                    if (v1.x > v2.x) { var t = v1.x; v1.x = v2.x; v2.x = t; }
                    if (v1.y > v2.y) { var t = v1.y; v1.y = v2.y; v2.y = t; }

                    var after = new RenderRegion(
                        (int)MathF.Round(v1.x), (int)MathF.Round(v1.y),
                        (int)MathF.Round(v2.x), (int)MathF.Round(v2.y));

                    if (after.max.x < after.min.x) after.max.x = after.min.x;
                    if (after.max.y < after.min.y) after.max.y = after.min.y;

                    after.IntersectWith(before);
                    renderer.Viewport(after);
                    return true;
                }
            }

            return _clipRect(renderer, pts, tm, ref before);
        }

        private static unsafe bool _clipRect(RenderMethod renderer, Point* pts, in Matrix m, ref RenderRegion before)
        {
            // Transform corners
            var c0 = TvgMath.Transform(pts[0], m);
            var c1 = TvgMath.Transform(pts[1], m);
            var c2 = TvgMath.Transform(pts[2], m);
            var c3 = TvgMath.Transform(pts[3], m);

            // Check if the clipper is a superset of the current viewport(before) region
            bool PointInConvexQuad(in Point p, in Point q0, in Point q1, in Point q2, in Point q3)
            {
                static float Sign(in Point p1, in Point p2, in Point p3)
                {
                    return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
                }
                var b1 = Sign(p, q0, q1) < 0.0f;
                var b2 = Sign(p, q1, q2) < 0.0f;
                var b3 = Sign(p, q2, q3) < 0.0f;
                var b4 = Sign(p, q3, q0) < 0.0f;
                return (b1 == b2) && (b2 == b3) && (b3 == b4);
            }

            if (!PointInConvexQuad(new Point(before.min.x, before.min.y), c0, c1, c2, c3)) return false;
            if (!PointInConvexQuad(new Point(before.max.x, before.min.y), c0, c1, c2, c3)) return false;
            if (!PointInConvexQuad(new Point(before.max.x, before.max.y), c0, c1, c2, c3)) return false;
            if (!PointInConvexQuad(new Point(before.min.x, before.max.y), c0, c1, c2, c3)) return false;

            // same viewport
            return true;
        }

        // =====================================================================
        //  Update — orchestrating update of paint tree
        //  Mirrors C++ Paint::Impl::update()
        // =====================================================================

        internal unsafe object? Update(RenderMethod renderer, in Matrix pm, List<object?> clips, byte opacity, RenderUpdateFlag flag, bool clipper = false)
        {
            if (PaintSkip(flag | renderFlag)) return rd;

            cmpFlag = CompositionFlag.Invalid; // must clear after the rendering

            if (this.renderer != renderer)
            {
                if (this.renderer != null) TvgCommon.TVGERR("RENDERER", "paint's renderer has been changed!");
                renderer.Ref();
                this.renderer = renderer;
            }

            if ((renderFlag & RenderUpdateFlag.Transform) != 0) tr.Update();

            // 1. Composition Pre Processing
            object? trd = null;
            var viewport = default(RenderRegion);
            var compFastTrack = false;

            if (maskData != null)
            {
                var target = maskData.target;
                var method = maskData.method;
                target.pImpl.ctxFlag &= unchecked((byte)~(byte)ContextFlag.FastTrack); // reset

                if (target.PaintType() == Type.Shape)
                {
                    var shape = (Shape)target;
                    var a = shape.rs.color.a;
                    if (shape.rs.fill == null && shape.pImpl.maskData == null)
                    {
                        if ((method == MaskMethod.Alpha && a == 255 && shape.pImpl.opacity == 255) ||
                            (method == MaskMethod.InvAlpha && (a == 0 || shape.pImpl.opacity == 0)))
                        {
                            viewport = renderer.Viewport();
                            if (_compFastTrack(renderer, target, pm, ref viewport))
                            {
                                target.pImpl.ctxFlag |= (byte)ContextFlag.FastTrack;
                                compFastTrack = true;
                            }
                        }
                    }
                }
                if (!compFastTrack)
                {
                    trd = target.pImpl.Update(renderer, pm, clips, 255, flag, false);
                }
            }

            // 2. Clipping
            if (this.clipper != null)
            {
                var pclip = this.clipper.pImpl;
                pclip.ctxFlag &= unchecked((byte)~(byte)ContextFlag.FastTrack); // reset
                viewport = renderer.Viewport();
                if (pclip.clipper == null && ((Shape)this.clipper).rs.StrokeWidth() == 0.0f &&
                    _compFastTrack(renderer, this.clipper, pm, ref viewport))
                {
                    pclip.ctxFlag |= (byte)ContextFlag.FastTrack;
                    compFastTrack = true;
                }
                else
                {
                    Mark(RenderUpdateFlag.Clip);
                    trd = pclip.Update(renderer, pm, clips, 255, flag, true);
                    clips.Add(trd);
                }
            }

            // 3. Main Update
            var combinedOpacity = RenderHelper.Multiply(opacity, this.opacity);
            var m = TvgMath.Multiply(pm, tr.m);
            PaintUpdate(renderer, m, clips, combinedOpacity, flag | renderFlag, clipper);

            // 4. Composition Post Processing
            if (compFastTrack) renderer.Viewport(viewport);
            else if (this.clipper != null) clips.RemoveAt(clips.Count - 1);

            renderFlag = RenderUpdateFlag.None;

            return rd;
        }

        // =====================================================================
        //  Render — orchestrating render of paint tree
        //  Mirrors C++ Paint::Impl::render()
        // =====================================================================

        internal bool Render(RenderMethod renderer, CompositionFlag flag = CompositionFlag.Invalid)
        {
            if (hidden || opacity == 0) return true;

            RenderCompositor? cmp = null;

            // OPTIMIZE: bounds(renderer) calls could dismiss the parallelization
            if (maskData != null && (maskData.target.pImpl.ctxFlag & (byte)ContextFlag.FastTrack) == 0)
            {
                var region = PaintBounds();

                var mData = maskData;
                while (mData != null)
                {
                    if (RenderHelper.MaskRegionMerging(mData.method))
                        region.AddWith(mData.target.pImpl.Bounds());
                    if (region.Invalid()) return true;
                    mData = mData.target.pImpl.maskData;
                }
                cmp = renderer.Target(region, RenderHelper.MaskToColorSpace(renderer, maskData.method), CompositionFlag.Masking);
                if (renderer.BeginComposite(cmp, MaskMethod.None, 255))
                {
                    maskData.target.pImpl.Render(renderer);
                }
            }

            if (cmp != null) renderer.BeginComposite(cmp, maskData!.method, maskData.target.pImpl.opacity);

            bool ret = PaintRender(renderer, flag);

            if (cmp != null) renderer.EndComposite(cmp);

            return ret;
        }

        // =====================================================================
        //  Damage
        // =====================================================================

        internal void Damage(in RenderRegion vport)
        {
            if (renderer != null) renderer.Damage(rd, vport);
        }

        internal void Damage()
        {
            if (renderer != null) renderer.Damage(rd, Bounds());
        }
    }
}
