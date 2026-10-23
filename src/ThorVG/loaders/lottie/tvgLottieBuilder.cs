// Ported from ThorVG/src/loaders/lottie/tvgLottieBuilder.h and tvgLottieBuilder.cpp

using System;
using System.Collections.Generic;

namespace ThorVG
{
    public enum RenderFragment : byte { ByNone = 0, ByFill, ByStroke }

    public struct RenderRepeater
    {
        public int cnt;
        public Matrix transform;
        public float offset;
        public Point position;
        public Point anchor;
        public Point scale;
        public float rotation;
        public byte startOpacity;
        public byte endOpacity;
        public bool inorder;
    }

    public class RenderText : IDisposable
    {
        public Point cursor;
        public int line, space, idx;
        public float lineSpace, totalLineSpace;
        public int pIdx;        // current native UTF-8 byte index
        public string text;     // the full text string
        internal readonly byte[] utf8;
        public int nChars;
        public float scale;
        public Scene textScene;
        public Scene lineScene;
        public float capScale, firstMargin;
        public LottieTextFollowPath? follow;
        public bool perpendicular;

        public RenderText(LottieText text, TextDocument doc)
        {
            this.text = doc.text ?? "";
            utf8 = doc.Utf8;
            pIdx = 0;
            nChars = utf8.Length;
            scale = doc.size;
            textScene = Scene.Gen();
            lineScene = Scene.Gen();
        }

        public void Dispose()
        {
            Paint.Rel(textScene);
            Paint.Rel(lineScene);
            textScene = null!;
            lineScene = null!;
        }
    }

    public class RenderContext : IInlistNode<RenderContext>, IDisposable
    {
        public RenderContext? Prev { get; set; }
        public RenderContext? Next { get; set; }

        public Shape? propagator;
        public Shape? merging;
        public int beginIdx;
        public List<RenderRepeater> repeaters = new();
        public Matrix? transform;
        public LottieModifier? modifiers;
        public RenderFragment fragment = RenderFragment.ByNone;
        public bool reqFragment;

        public RenderContext() { }

        public void Init(Shape propagator)
        {
            Reset();
            propagator.ResetFull();
            propagator.Ref();
            this.propagator = propagator;
        }

        public void Init(RenderContext rhs, Shape propagator, bool mergeable = false)
        {
            Reset();
            if (mergeable) merging = rhs.merging;
            propagator.Ref();
            this.propagator = propagator;
            repeaters.AddRange(rhs.repeaters);
            fragment = rhs.fragment;

            LottieModifier? tail = null;
            for (var m = rhs.modifiers; m != null; m = m.next)
            {
                LottieModifier clone = m switch
                {
                    LottieRoundnessModifier r => new LottieRoundnessModifier(r.r),
                    LottieOffsetModifier o => new LottieOffsetModifier(o.offset, o.miterLimit, o.join),
                    LottiePuckerBloatModifier p => new LottiePuckerBloatModifier(p.amount),
                    LottieZigZagModifier z => new LottieZigZagModifier(z.amp, z.freq, z.point),
                    _ => throw new InvalidOperationException("Unknown Lottie modifier")
                };
                if (tail == null) modifiers = clone;
                else tail.next = clone;
                tail = clone;
            }

            if (rhs.transform.HasValue)
            {
                transform = rhs.transform.Value;
            }
        }

        public void Reset()
        {
            propagator = null;
            merging = null;
            beginIdx = 0;
            repeaters.Clear();
            transform = null;
            modifiers = null;
            fragment = RenderFragment.ByNone;
            reqFragment = false;
            Prev = null;
            Next = null;
        }

        public void Update(LottieModifier next)
        {
            next.next = modifiers;
            modifiers = next;
        }

        public void Dispose()
        {
            var owner = propagator;
            propagator = null;
            owner?.Unref(false);
            merging = null;
            transform = null;
            modifiers = null;
            repeaters.Clear();
        }
    }

    public class LottieBuilder : IDisposable
    {
        private LottieExpressions? exps;
        public LottieTween tween = new();
        public AssetResolver? resolver;
        public Action<LottieAudioResolver, object?>? audioResolver;
        public object? audioResolverData;
        public float volume = 1.0f;

        // Object pools for per-frame allocations
        private Stack<RenderContext> contextPool = new();
        private Stack<Inlist<RenderContext>> contextListPool = new();

        private RenderContext AcquireContext(Shape propagator)
        {
            if (contextPool.TryPop(out var ctx))
            {
                ctx.Init(propagator);
                return ctx;
            }
            ctx = new RenderContext();
            ctx.Init(propagator);
            return ctx;
        }

        private RenderContext AcquireContext(RenderContext rhs, Shape propagator, bool mergeable = false)
        {
            if (contextPool.TryPop(out var ctx))
            {
                ctx.Init(rhs, propagator, mergeable);
                return ctx;
            }
            ctx = new RenderContext();
            ctx.Init(rhs, propagator, mergeable);
            return ctx;
        }

        private void ReleaseContext(RenderContext ctx)
        {
            ctx.Dispose();
            contextPool.Push(ctx);
        }

        private Inlist<RenderContext> AcquireContextList()
        {
            if (contextListPool.TryPop(out var list))
            {
                return list;
            }
            return new Inlist<RenderContext>();
        }

        private void ReleaseContextList(Inlist<RenderContext> list)
        {
            list.Free();
            contextListPool.Push(list);
        }

        public LottieBuilder()
        {
            exps = LottieExpressions.Instance();
        }

        ~LottieBuilder() => Dispose();

        public void Dispose()
        {
            var expressions = System.Threading.Interlocked.Exchange(ref exps, null);
            if (expressions == null) return;
            LottieExpressions.Retrieve(expressions);
            tween.Dispose();
            contextPool.Clear();
            contextListPool.Clear();
            GC.SuppressFinalize(this);
        }

        public bool Expressions()
        {
            return exps != null;
        }

        // --- Static helpers ---

        private static void Dimension3D(LottieTransform transform, float frameNo, ref Matrix m, float angle, LottieTween tween, LottieExpressions? exps)
        {
            var x = TvgMath.Deg2Rad(transform.ddd!.rx.Evaluate(frameNo, tween, exps));
            var y = TvgMath.Deg2Rad(transform.ddd.ry.Evaluate(frameNo, tween, exps));
            var z = TvgMath.MultiplyAdd(transform.rotation.Evaluate(frameNo, tween, exps), TvgMath.Deg2Rad(1.0f), angle);

            var sx = MathF.Sin(x); var sy = MathF.Sin(y); var sz = MathF.Sin(z);
            var cx = MathF.Cos(x); var cy = MathF.Cos(y); var cz = MathF.Cos(z);

            var ri00 = cy * cz;
            var ri01 = -cy * sz;
            var ri10 = TvgMath.MultiplyAdd(sx * sy, cz, cx * sz);
            var ri11 = TvgMath.MultiplyAdd(-sx * sy, sz, cx * cz);

            var o = transform.ddd.orient.Evaluate(frameNo, tween, exps);
            if (o.x == 0.0f && o.y == 0.0f && o.z == 0.0f)
            {
                m.e11 = ri00;
                m.e12 = ri01;
                m.e21 = ri10;
                m.e22 = ri11;
                return;
            }

            var ri02 = sy;
            var ri12 = -sx * cy;

            var ox = TvgMath.Deg2Rad(o.x);
            var oy = TvgMath.Deg2Rad(o.y);
            var oz = TvgMath.Deg2Rad(o.z);

            var sox = MathF.Sin(ox); var soy = MathF.Sin(oy); var soz = MathF.Sin(oz);
            var cox = MathF.Cos(ox); var coy = MathF.Cos(oy); var coz = MathF.Cos(oz);

            var ro00 = coy * coz;
            var ro01 = -coy * soz;
            var ro10 = TvgMath.MultiplyAdd(sox * soy, coz, cox * soz);
            var ro11 = TvgMath.MultiplyAdd(-sox * soy, soz, cox * coz);
            var ro20 = TvgMath.MultiplyAdd(-cox * soy, coz, sox * soz);
            var ro21 = TvgMath.MultiplyAdd(cox * soy, soz, sox * coz);

            m.e11 = TvgMath.MultiplyAdd(ri02, ro20, TvgMath.MultiplyAdd(ri00, ro00, ri01 * ro10));
            m.e12 = TvgMath.MultiplyAdd(ri02, ro21, TvgMath.MultiplyAdd(ri00, ro01, ri01 * ro11));
            m.e21 = TvgMath.MultiplyAdd(ri12, ro20, TvgMath.MultiplyAdd(ri10, ro00, ri11 * ro10));
            m.e22 = TvgMath.MultiplyAdd(ri12, ro21, TvgMath.MultiplyAdd(ri10, ro01, ri11 * ro11));
        }

        private static void Rotate(LottieTransform transform, float frameNo, ref Matrix m, float angle, LottieTween tween, LottieExpressions? exps)
        {
            if (transform.ddd != null)
            {
                Dimension3D(transform, frameNo, ref m, angle, tween, exps);
            }
            else
            {
                var degree = transform.rotation.Evaluate(frameNo, tween, exps) + angle;
                if (degree == 0.0f) return;
                var radian = TvgMath.Deg2Rad(degree);
                m.e11 = MathF.Cos(radian);
                m.e12 = -MathF.Sin(radian);
                m.e21 = MathF.Sin(radian);
                m.e22 = MathF.Cos(radian);
            }
        }

        private static void Skew(ref Matrix m, float angleDeg, float axisDeg)
        {
            var angle = -TvgMath.Deg2Rad(angleDeg);
            var tanVal = MathF.Tan(angle);

            axisDeg = axisDeg % 180.0f;
            if (MathF.Abs(axisDeg) < 0.01f || MathF.Abs(axisDeg - 180.0f) < 0.01f || MathF.Abs(axisDeg + 180.0f) < 0.01f)
            {
                float cosVal = MathF.Cos(TvgMath.Deg2Rad(axisDeg));
                var B = cosVal * cosVal * tanVal;
                m.e12 = TvgMath.MultiplyAdd(B, m.e11, m.e12);
                m.e22 = TvgMath.MultiplyAdd(B, m.e21, m.e22);
                return;
            }
            else if (MathF.Abs(axisDeg - 90.0f) < 0.01f || MathF.Abs(axisDeg + 90.0f) < 0.01f)
            {
                float sinVal = -MathF.Sin(TvgMath.Deg2Rad(axisDeg));
                var C = sinVal * sinVal * tanVal;
                m.e11 = TvgMath.MultiplyAdd(-C, m.e12, m.e11);
                m.e21 = TvgMath.MultiplyAdd(-C, m.e22, m.e21);
                return;
            }

            var axis = -TvgMath.Deg2Rad(axisDeg);
            var cosV = MathF.Cos(axis);
            var sinV = MathF.Sin(axis);
            var oneMinusA = TvgMath.MultiplyAdd(-(sinV * cosV), tanVal, 1.0f);
            var onePlusA = TvgMath.MultiplyAdd(sinV * cosV, tanVal, 1.0f);
            var Bv = cosV * cosV * tanVal;
            var Cv = sinV * sinV * tanVal;

            var e11 = m.e11;
            var e21 = m.e21;
            m.e11 = TvgMath.MultiplyAdd(oneMinusA, e11, -Cv * m.e12);
            m.e12 = TvgMath.MultiplyAdd(Bv, e11, onePlusA * m.e12);
            m.e21 = TvgMath.MultiplyAdd(oneMinusA, e21, -Cv * m.e22);
            m.e22 = TvgMath.MultiplyAdd(Bv, e21, onePlusA * m.e22);
        }

        private static bool UpdateTransformMatrix(LottieTransform? transform, float frameNo, ref Matrix matrix, out byte opacity, bool autoOrient, LottieTween tween, LottieExpressions? exps)
        {
            TvgMath.SetIdentity(out matrix);

            if (transform == null)
            {
                opacity = 255;
                return false;
            }

            if (transform.coords != null)
                TvgMath.Translate(ref matrix, new Point(transform.coords.x.Evaluate(frameNo, tween, exps), transform.coords.y.Evaluate(frameNo, tween, exps)));
            else
                TvgMath.Translate(ref matrix, transform.position.EvaluateForRendering(frameNo, tween, exps));

            var angle = autoOrient ? transform.position.GetAngle(frameNo, tween) : 0.0f;
            Rotate(transform, frameNo, ref matrix, angle, tween, exps);

            var skewAngle = transform.skewAngle.Evaluate(frameNo, tween, exps);
            if (skewAngle != 0.0f)
            {
                skewAngle = skewAngle % 180.0f;
                if (MathF.Abs(skewAngle - 90.0f) < 0.01f || MathF.Abs(skewAngle + 90.0f) < 0.01f)
                {
                    opacity = 0;
                    return false;
                }
                Skew(ref matrix, skewAngle, transform.skewAxis.Evaluate(frameNo, exps));
            }

            var scale = transform.scale.Evaluate(frameNo, tween, exps);
            TvgMath.ScaleR(ref matrix, TvgMath.PointMul(scale, 0.01f));

            // Lottie specific anchor transform.
            TvgMath.TranslateR(ref matrix, TvgMath.PointNeg(transform.anchor.Evaluate(frameNo, tween, exps)));

            // invisible just in case.
            if (scale.x == 0.0f || scale.y == 0.0f) opacity = 0;
            else opacity = transform.opacity.Evaluate(frameNo, tween, exps);

            return true;
        }

        private static void UpdateStroke(LottieStroke stroke, float frameNo, RenderContext ctx, LottieTween tween, LottieExpressions? exps)
        {
            ctx.propagator!.StrokeWidth(stroke.width.Evaluate(frameNo, tween, exps));
            ctx.propagator.StrokeCap(stroke.cap);
            ctx.propagator.StrokeJoin(stroke.join);
            ctx.propagator.StrokeMiterlimit(stroke.miterLimit);

            if (stroke.dashattr != null)
            {
                var dashes = new float[stroke.dashattr.values.Count];
                for (int i = 0; i < stroke.dashattr.values.Count; ++i)
                    dashes[i] = stroke.dashattr.values[i].Evaluate(frameNo, tween, exps);
                ctx.propagator.StrokeDash(dashes, (uint)dashes.Length, stroke.dashattr.offset.Evaluate(frameNo, tween, exps));
            }
            else
            {
                ctx.propagator.StrokeDash(null, 0);
            }
        }

        private static bool Draw(LottieGroup parent, LottieShape? shape, RenderContext ctx)
        {
            if (ctx.merging != null) return false;

            if (shape != null)
            {
                ctx.merging = shape.renderPooler.Pooling();
                ctx.propagator!.pImpl.Duplicate(ctx.merging);
            }
            else
            {
                ctx.merging = (Shape)ctx.propagator!.Duplicate();
            }

            parent.scene!.Add(ctx.merging);
            return true;
        }

        private static unsafe void Repeat(LottieGroup parent, Shape path, LottieRenderPooler<Shape> pooler, RenderContext ctx)
        {
            var propagators = new List<Shape> { ctx.propagator! };
            var shapes = new List<Shape>();

            for (int ri = ctx.repeaters.Count - 1; ri >= 0; --ri)
            {
                var repeater = ctx.repeaters[ri];
                shapes.Clear();

                for (int i = 0; i < repeater.cnt; ++i)
                {
                    var multiplier = repeater.offset + (float)i;
                    for (int pi = 0; pi < propagators.Count; ++pi)
                    {
                        var p = propagators[pi];
                        var shape = pooler.Pooling();
                        shape.Ref(); // prevent pooler from returning the same shape
                        p.pImpl.Duplicate(shape);
                        shape.rs.path.cmds.Clear();
                        shape.rs.path.cmds.Push(path.rs.path.cmds);
                        shape.rs.path.pts.Clear();
                        shape.rs.path.pts.Push(path.rs.path.pts);
                        var opacity = LottieDataHelper.LerpByte(repeater.startOpacity, repeater.endOpacity, (float)(i + 1) / repeater.cnt);
                        shape.Opacity(RenderHelper.Multiply(shape.Opacity(), opacity));

                        var m = TvgMath.Identity();
                        TvgMath.Translate(ref m, LottieModifierHelpers.ScaleAdd(repeater.anchor, repeater.position, multiplier));
                        TvgMath.Scale(ref m, new Point(MathF.Pow(repeater.scale.x * 0.01f, multiplier), MathF.Pow(repeater.scale.y * 0.01f, multiplier)));
                        TvgMath.Rotate(ref m, repeater.rotation * multiplier);
                        TvgMath.TranslateR(ref m, TvgMath.PointNeg(repeater.anchor));

                        Matrix inv;
                        TvgMath.Inverse(repeater.transform, out inv);
                        shape.Transform(TvgMath.Multiply(TvgMath.Multiply(repeater.transform, m), TvgMath.Multiply(inv, shape.Transform())));
                        shapes.Add(shape);
                    }
                }

                propagators.Clear();

                // push repeat shapes in order
                if (repeater.inorder)
                {
                    foreach (var s in shapes)
                    {
                        parent.scene!.Add(s);
                        s.Unref();
                        propagators.Add(s);
                    }
                }
                else if (shapes.Count > 0)
                {
                    for (int si = shapes.Count - 1; si >= 0; --si)
                    {
                        parent.scene!.Add(shapes[si]);
                        shapes[si].Unref();
                        propagators.Add(shapes[si]);
                    }
                }
                shapes.Clear();
            }
        }

        private static unsafe void Close(ref Array<Point> pts, Point p, bool round)
        {
            if (round && TvgMath.Zero(TvgMath.PointSub(pts.Last(), pts[pts.count - 2]))) pts[pts.count - 2] = p;
            pts.Last() = p;
        }

        // --- Instance methods ---

        private void UpdateTransformLayer(LottieLayer? layer, float frameNo)
        {
            if (layer == null) return;
            if (tween.active) layer.cacheFrameNo = -1.0f;
            else
            {
                if (TvgMath.Equal(layer.cacheFrameNo, frameNo)) return;
                layer.cacheFrameNo = frameNo;
            }

            var transform = layer.transform;
            var parent = layer.parent;

            if (parent != null) UpdateTransformLayer(parent, frameNo);

            var matrix = layer.cacheMatrix;

            UpdateTransformMatrix(transform, frameNo, ref matrix, out layer.cacheOpacity, layer.autoOrient, tween, exps);

            if (parent != null) matrix = TvgMath.Multiply(parent.cacheMatrix, matrix);

            layer.cacheMatrix = matrix;
        }

        private void UpdateTransform(LottieGroup parent, int childIdx, float frameNo, Inlist<RenderContext> contexts, RenderContext ctx)
        {
            var transform = parent.children[childIdx] as LottieTransform;
            if (transform == null) return;

            Matrix m = default;
            byte opacity;

            if (parent.Mergeable())
            {
                if (ctx.transform.HasValue)
                {
                    UpdateTransformMatrix(transform, frameNo, ref m, out opacity, false, tween, exps);
                    m = TvgMath.Multiply(ctx.transform.Value, m);
                    ctx.transform = m;
                }
                else
                {
                    m = default;
                    UpdateTransformMatrix(transform, frameNo, ref m, out opacity, false, tween, exps);
                    ctx.transform = m;
                }
                return;
            }

            ctx.merging = null;
            m = default;

            if (!UpdateTransformMatrix(transform, frameNo, ref m, out opacity, false, tween, exps)) return;

            ctx.propagator!.Transform(TvgMath.Multiply(ctx.propagator.Transform(), m));
            ctx.propagator.Opacity(RenderHelper.Multiply(opacity, ctx.propagator.pImpl.opacity));

            // FIXME: preserve the stroke width. too workaround, need a better design.
            if (ctx.propagator.rs.StrokeWidth() > 0.0f)
            {
                var denominator = MathF.Sqrt(TvgMath.MultiplyAdd(m.e11, m.e11, m.e12 * m.e12));
                if (denominator > 1.0f) ctx.propagator.StrokeWidth(ctx.propagator.GetStrokeWidth() / denominator);
            }

            // FIXME: compensate gradient fills when the propagator enters a group scope.
            if (ctx.fragment != RenderFragment.ByNone)
            {
                if (TvgMath.Inverse(m, out var im))
                {
                    ref var rs = ref ctx.propagator.rs;
                    if (rs.fill != null) rs.fill.SetTransform(TvgMath.Multiply(rs.fill.GetTransform(), im));
                    if (rs.stroke?.fill != null) rs.stroke.fill.SetTransform(TvgMath.Multiply(rs.stroke.fill.GetTransform(), im));
                }
            }
        }

        private void UpdateGroup(LottieGroup parent, int childIdx, float frameNo, Inlist<RenderContext> pcontexts, RenderContext ctx)
        {
            var group = (LottieGroup)parent.children[childIdx];

            if (!group.visible) return;

            // prepare render data

            // special tune: sharing the context if the blending is compatible
            // propagate the blending to its parent(layer) if possible. this potentially helps performance if the layer has mattes/maskings.
            if (group.blendMethod == BlendMethod.Normal || group.blendMethod == parent.blendMethod)
            {
                group.scene = parent.scene;
            }
            else if (parent.blendMethod == BlendMethod.Normal && parent.children.Count == 1)
            {
                group.scene = parent.scene;
                group.scene!.SetBlend(group.blendMethod);
            }
            else
            {
                group.scene = Scene.Gen();
                parent.scene!.Add(group.scene);
                group.scene.SetBlend(group.blendMethod);
            }

            group.reqFragment |= ctx.reqFragment;

            // generate a merging shape to consolidate partial shapes into a single entity
            if (group.Mergeable()) Draw(group, null, ctx);

            var contexts = AcquireContextList();
            var propagator = group.Mergeable() ? ctx.propagator! : (Shape)ctx.propagator!.pImpl.Duplicate(group.renderPooler.Pooling());
            try
            {
                contexts.Back(AcquireContext(ctx, propagator, group.Mergeable()));
                UpdateChildren(group, frameNo, contexts);
            }
            finally { ReleaseContextList(contexts); }
        }

        private bool Fragmented(LottieGroup parent, int childIdx, Inlist<RenderContext> contexts, RenderContext ctx, RenderFragment fragment)
        {
            if (ctx.fragment != RenderFragment.ByNone) return true;
            if (!ctx.reqFragment) return false;

            contexts.Back(AcquireContext(ctx, (Shape)ctx.propagator!.pImpl.Duplicate(parent.renderPooler.Pooling())));

            contexts.Tail!.beginIdx = childIdx - 1;
            ctx.fragment = fragment;

            return false;
        }

        private bool UpdateSolidStroke(LottieGroup parent, int childIdx, float frameNo, Inlist<RenderContext> contexts, RenderContext ctx)
        {
            var stroke = (LottieSolidStroke)parent.children[childIdx];
            if (Fragmented(parent, childIdx, contexts, ctx, RenderFragment.ByStroke)) return false;

            var opacity = stroke.opacity.Evaluate(frameNo, tween, exps);
            if (opacity == 0) return false;

            ctx.merging = null;
            var color = stroke.color.Evaluate(frameNo, tween, exps);
            ctx.propagator!.StrokeFill((byte)color.r, (byte)color.g, (byte)color.b, opacity);
            UpdateStroke(stroke.stroke, frameNo, ctx, tween, exps);

            return false;
        }

        private bool UpdateGradientStroke(LottieGroup parent, int childIdx, float frameNo, Inlist<RenderContext> contexts, RenderContext ctx)
        {
            var stroke = (LottieGradientStroke)parent.children[childIdx];
            if (Fragmented(parent, childIdx, contexts, ctx, RenderFragment.ByStroke)) return false;

            var opacity = stroke.opacity.Evaluate(frameNo, tween, exps);
            if (opacity == 0 && !stroke.opaque) return false;

            ctx.merging = null;
            var fill = stroke.CreateFill(frameNo, opacity, tween, exps);
            if (fill != null) ctx.propagator!.StrokeFill(fill);
            UpdateStroke(stroke.stroke, frameNo, ctx, tween, exps);

            return false;
        }

        private bool UpdateSolidFill(LottieGroup parent, int childIdx, float frameNo, Inlist<RenderContext> contexts, RenderContext ctx)
        {
            var fill = (LottieSolidFill)parent.children[childIdx];
            var opacity = fill.opacity.Evaluate(frameNo, tween, exps);

            // interrupted by fully opaque, stop the current rendering
            if (ctx.fragment == RenderFragment.ByFill && opacity == 255) return true;
            if (opacity == 0) return false;

            if (Fragmented(parent, childIdx, contexts, ctx, RenderFragment.ByFill)) return false;

            ctx.merging = null;
            var color = fill.color.Evaluate(frameNo, tween, exps);
            ctx.propagator!.SetFill((byte)color.r, (byte)color.g, (byte)color.b, opacity);
            ctx.propagator.SetFillRule(fill.rule);

            if (ctx.propagator.GetStrokeWidth() > 0) ctx.propagator.Order(true);

            return false;
        }

        private bool UpdateGradientFill(LottieGroup parent, int childIdx, float frameNo, Inlist<RenderContext> contexts, RenderContext ctx)
        {
            var fill = (LottieGradientFill)parent.children[childIdx];
            var opacity = fill.opacity.Evaluate(frameNo, tween, exps);

            // interrupted by fully opaque, stop the current rendering
            if (ctx.fragment == RenderFragment.ByFill && fill.opaque && opacity == 255) return true;

            if (Fragmented(parent, childIdx, contexts, ctx, RenderFragment.ByFill)) return false;

            ctx.merging = null;

            var fillVal = fill.CreateFill(frameNo, opacity, tween, exps);
            if (fillVal != null) ctx.propagator!.SetFill(fillVal);
            ctx.propagator!.SetFillRule(fill.rule);

            if (ctx.propagator.GetStrokeWidth() > 0) ctx.propagator.Order(true);

            return false;
        }

        private unsafe void AppendRect(LottieRect rect, Shape shape, Point pos, Point size, float r, bool clockwise, RenderContext ctx)
        {
            ref var path = ref shape.rs.path;
            var cnt = path.pts.count;

            if (ctx.modifiers != null)
            {
                var temp = rect.renderPooler.Pooling();
                temp.ResetShape();
                temp.AppendRect(pos.x, pos.y, size.x, size.y, r, r, clockwise);
                ctx.modifiers.Rect(temp.rs.path, shape.rs.path, pos, size, r, clockwise);
            }
            else shape.AppendRect(pos.x, pos.y, size.x, size.y, r, r, clockwise);

            if (ctx.transform.HasValue)
            {
                var t = ctx.transform.Value;
                for (var i = cnt; i < path.pts.count; ++i)
                {
                    TvgMath.TransformInPlace(ref path.pts[i], t);
                }
            }
        }

        private void UpdateRect(LottieGroup parent, int childIdx, float frameNo, Inlist<RenderContext> contexts, RenderContext ctx)
        {
            var rect = (LottieRect)parent.children[childIdx];
            var size = rect.size.Evaluate(frameNo, tween, exps);
            var pos = TvgMath.PointSub(rect.position.EvaluateForRendering(frameNo, tween, exps), TvgMath.PointMul(size, 0.5f));
            var r = MathF.Min(rect.radius.Evaluate(frameNo, tween, exps), MathF.Min(size.x * 0.5f, size.y * 0.5f));

            if (ctx.repeaters.Count == 0)
            {
                Draw(parent, rect, ctx);
                AppendRect(rect, ctx.merging!, pos, size, r, rect.clockwise, ctx);
            }
            else
            {
                var shape = rect.renderPooler.Pooling();
                shape.Ref();
                try
                {
                    shape.ResetShape();
                    AppendRect(rect, shape, pos, size, r, rect.clockwise, ctx);
                    Repeat(parent, shape, rect.renderPooler, ctx);
                }
                finally { shape.Unref(); }
            }
        }

        private unsafe void AppendCircle(LottieEllipse ellipse, Shape shape, Point center, Point radius, bool clockwise, RenderContext ctx)
        {
            ref var path = ref shape.rs.path;
            var cnt = path.pts.count;

            if (ctx.modifiers != null)
            {
                var temp = ellipse.renderPooler.Pooling();
                temp.ResetShape();
                temp.AppendCircle(center.x, center.y, radius.x, radius.y, clockwise);
                ctx.modifiers.Ellipse(temp.rs.path, shape.rs.path, center, radius, clockwise);
            }
            else shape.AppendCircle(center.x, center.y, radius.x, radius.y, clockwise);

            if (ctx.transform.HasValue)
            {
                var t = ctx.transform.Value;
                for (var i = cnt; i < path.pts.count; ++i)
                {
                    TvgMath.TransformInPlace(ref path.pts[i], t);
                }
            }
        }

        private void UpdateEllipse(LottieGroup parent, int childIdx, float frameNo, Inlist<RenderContext> contexts, RenderContext ctx)
        {
            var ellipse = (LottieEllipse)parent.children[childIdx];
            var pos = ellipse.position.EvaluateForRendering(frameNo, tween, exps);
            var size = TvgMath.PointMul(ellipse.size.Evaluate(frameNo, tween, exps), 0.5f);

            if (ctx.repeaters.Count == 0)
            {
                Draw(parent, ellipse, ctx);
                AppendCircle(ellipse, ctx.merging!, pos, size, ellipse.clockwise, ctx);
            }
            else
            {
                var shape = ellipse.renderPooler.Pooling();
                shape.Ref();
                try
                {
                    shape.ResetShape();
                    AppendCircle(ellipse, shape, pos, size, ellipse.clockwise, ctx);
                    Repeat(parent, shape, ellipse.renderPooler, ctx);
                }
                finally { shape.Unref(); }
            }
        }

        private unsafe void UpdatePath(LottieGroup parent, int childIdx, float frameNo, Inlist<RenderContext> contexts, RenderContext ctx)
        {
            var path = (LottiePath)parent.children[childIdx];

            if (ctx.repeaters.Count == 0)
            {
                Draw(parent, path, ctx);
                Matrix* t = null;
                Matrix tVal;
                if (ctx.transform.HasValue)
                {
                    tVal = ctx.transform.Value;
                    t = &tVal;
                }
                path.pathset.Evaluate(frameNo, ctx.merging!.rs.path, t, tween, exps, ctx.modifiers);
                ctx.merging.pImpl.Mark(RenderUpdateFlag.Path);
            }
            else
            {
                var shape = path.renderPooler.Pooling();
                shape.Ref();
                try
                {
                    shape.ResetShape();
                    Matrix* t = null;
                    Matrix tVal;
                    if (ctx.transform.HasValue)
                    {
                        tVal = ctx.transform.Value;
                        t = &tVal;
                    }
                    path.pathset.Evaluate(frameNo, shape.rs.path, t, tween, exps, ctx.modifiers);
                    Repeat(parent, shape, path.renderPooler, ctx);
                }
                finally { shape.Unref(); }
            }
        }

        private unsafe void UpdateStar(LottiePolyStar star, float frameNo, Matrix? transformM, Shape merging, RenderContext ctx, LottieTween tween, LottieExpressions? exps)
        {
            const float POLYSTAR_MAGIC_NUMBER = 0.47829f / 0.28f;

            var ptsCnt = star.ptsCnt.Evaluate(frameNo, tween, exps);
            if (ptsCnt < 1) return;
            var innerRadius = star.innerRadius.Evaluate(frameNo, tween, exps);
            var outerRadius = star.outerRadius.Evaluate(frameNo, tween, exps);
            var innerRoundness = star.innerRoundness.Evaluate(frameNo, tween, exps) * 0.01f;
            var outerRoundness = star.outerRoundness.Evaluate(frameNo, tween, exps) * 0.01f;

            var angle = TvgMath.Deg2Rad(-90.0f);
            var partialPointRadius = 0.0f;
            var anglePerPoint = (2.0f * MathConstants.MATH_PI / ptsCnt);
            var halfAnglePerPoint = anglePerPoint * 0.5f;
            var partialPointAmount = ptsCnt - MathF.Floor(ptsCnt);
            var longSegment = false;
            var numPoints = (int)MathF.Ceiling(ptsCnt) * 2;
            var direction = star.clockwise ? 1.0f : -1.0f;
            var hasRoundness = false;

            Shape shape;
            if (ctx.modifiers != null)
            {
                shape = star.renderPooler.Pooling();
                shape.ResetShape();
            }
            else
            {
                shape = merging;
            }

            float x, y;

            if (!TvgMath.Zero(partialPointAmount))
            {
                angle += halfAnglePerPoint * (1.0f - partialPointAmount) * direction;
            }

            if (!TvgMath.Zero(partialPointAmount))
            {
                partialPointRadius = innerRadius + partialPointAmount * (outerRadius - innerRadius);
                x = partialPointRadius * MathF.Cos(angle);
                y = partialPointRadius * MathF.Sin(angle);
                angle += anglePerPoint * partialPointAmount * 0.5f * direction;
            }
            else
            {
                x = outerRadius * MathF.Cos(angle);
                y = outerRadius * MathF.Sin(angle);
                angle += halfAnglePerPoint * direction;
            }

            if (TvgMath.Zero(innerRoundness) && TvgMath.Zero(outerRoundness))
            {
                shape.rs.path.pts.Reserve((uint)(numPoints + 2));
                shape.rs.path.cmds.Reserve((uint)(numPoints + 3));
            }
            else
            {
                shape.rs.path.pts.Reserve((uint)(numPoints * 3 + 2));
                shape.rs.path.cmds.Reserve((uint)(numPoints + 3));
                hasRoundness = true;
            }

            var inPt = transformM.HasValue ? TvgMath.Transform(new Point(x, y), transformM.Value) : new Point(x, y);
            shape.MoveTo(inPt.x, inPt.y);

            for (int i = 0; i < numPoints; i++)
            {
                var radius = longSegment ? outerRadius : innerRadius;
                var dTheta = halfAnglePerPoint;
                if (!TvgMath.Zero(partialPointRadius) && i == numPoints - 2)
                {
                    dTheta = anglePerPoint * partialPointAmount * 0.5f;
                }
                if (!TvgMath.Zero(partialPointRadius) && i == numPoints - 1)
                {
                    radius = partialPointRadius;
                }
                var previousX = x;
                var previousY = y;
                x = radius * MathF.Cos(angle);
                y = radius * MathF.Sin(angle);

                if (hasRoundness)
                {
                    var cp1Theta = TvgMath.Atan2(previousY, previousX) - MathConstants.MATH_PI2 * direction;
                    var cp1Dx = MathF.Cos(cp1Theta);
                    var cp1Dy = MathF.Sin(cp1Theta);
                    var cp2Theta = TvgMath.Atan2(y, x) - MathConstants.MATH_PI2 * direction;
                    var cp2Dx = MathF.Cos(cp2Theta);
                    var cp2Dy = MathF.Sin(cp2Theta);

                    var cp1Roundness = longSegment ? innerRoundness : outerRoundness;
                    var cp2Roundness = longSegment ? outerRoundness : innerRoundness;
                    var cp1Radius = longSegment ? innerRadius : outerRadius;
                    var cp2Radius = longSegment ? outerRadius : innerRadius;

                    var cp1x = cp1Radius * cp1Roundness * POLYSTAR_MAGIC_NUMBER * cp1Dx / ptsCnt;
                    var cp1y = cp1Radius * cp1Roundness * POLYSTAR_MAGIC_NUMBER * cp1Dy / ptsCnt;
                    var cp2x = cp2Radius * cp2Roundness * POLYSTAR_MAGIC_NUMBER * cp2Dx / ptsCnt;
                    var cp2y = cp2Radius * cp2Roundness * POLYSTAR_MAGIC_NUMBER * cp2Dy / ptsCnt;

                    if (!TvgMath.Zero(partialPointAmount) && (i == 0 || i == numPoints - 1))
                    {
                        cp1x *= partialPointAmount;
                        cp1y *= partialPointAmount;
                        cp2x *= partialPointAmount;
                        cp2y *= partialPointAmount;
                    }
                    var in2 = transformM.HasValue ? TvgMath.Transform(new Point(previousX - cp1x, previousY - cp1y), transformM.Value) : new Point(previousX - cp1x, previousY - cp1y);
                    var in3 = transformM.HasValue ? TvgMath.Transform(new Point(x + cp2x, y + cp2y), transformM.Value) : new Point(x + cp2x, y + cp2y);
                    var in4 = transformM.HasValue ? TvgMath.Transform(new Point(x, y), transformM.Value) : new Point(x, y);
                    shape.CubicTo(in2.x, in2.y, in3.x, in3.y, in4.x, in4.y);
                }
                else
                {
                    var inP = transformM.HasValue ? TvgMath.Transform(new Point(x, y), transformM.Value) : new Point(x, y);
                    shape.LineTo(inP.x, inP.y);
                }
                angle += dTheta * direction;
                longSegment = !longSegment;
            }
            // ensure proper shape closure
            Close(ref shape.rs.path.pts, inPt, hasRoundness);
            shape.Close();

            if (ctx.modifiers != null) ctx.modifiers.Polystar(shape.rs.path, merging.rs.path, outerRoundness, hasRoundness);
        }

        private unsafe void UpdatePolygon(LottieGroup parent, LottiePolyStar star, float frameNo, Matrix? transformM, Shape merging, RenderContext ctx, LottieTween tween, LottieExpressions? exps)
        {
            const float POLYGON_MAGIC_NUMBER = 0.25f;

            var ptsCnt = (int)MathF.Floor(star.ptsCnt.Evaluate(frameNo, tween, exps));
            if (ptsCnt < 1) return;
            var radius = star.outerRadius.Evaluate(frameNo, tween, exps);
            var outerRoundness = star.outerRoundness.Evaluate(frameNo, tween, exps) * 0.01f;

            var angle = -MathConstants.MATH_PI2;
            var anglePerPoint = 2.0f * MathConstants.MATH_PI / (float)ptsCnt;
            var direction = star.clockwise ? 1.0f : -1.0f;
            var hasRoundness = !TvgMath.Zero(outerRoundness);
            var x = radius * MathF.Cos(angle);
            var y = radius * MathF.Sin(angle);

            angle += anglePerPoint * direction;

            Shape shape;
            if (ctx.modifiers != null)
            {
                shape = star.renderPooler.Pooling();
                shape.ResetShape();
            }
            else
            {
                shape = merging;
                if (hasRoundness)
                {
                    shape.rs.path.pts.Reserve((uint)(ptsCnt * 3 + 2));
                    shape.rs.path.cmds.Reserve((uint)(ptsCnt + 3));
                }
                else
                {
                    shape.rs.path.pts.Reserve((uint)(ptsCnt + 2));
                    shape.rs.path.cmds.Reserve((uint)(ptsCnt + 3));
                }
            }

            var inPt = transformM.HasValue ? TvgMath.Transform(new Point(x, y), transformM.Value) : new Point(x, y);
            shape.MoveTo(inPt.x, inPt.y);

            var coeff = anglePerPoint * radius * outerRoundness * POLYGON_MAGIC_NUMBER;
            for (int i = 0; i < ptsCnt; i++)
            {
                var previousX = x;
                var previousY = y;
                x = radius * MathF.Cos(angle);
                y = radius * MathF.Sin(angle);

                if (hasRoundness)
                {
                    var cp1Theta = TvgMath.Atan2(previousY, previousX) - MathConstants.MATH_PI2 * direction;
                    var cp1x = coeff * MathF.Cos(cp1Theta);
                    var cp1y = coeff * MathF.Sin(cp1Theta);
                    var cp2Theta = TvgMath.Atan2(y, x) - MathConstants.MATH_PI2 * direction;
                    var cp2x = coeff * MathF.Cos(cp2Theta);
                    var cp2y = coeff * MathF.Sin(cp2Theta);

                    var in2 = transformM.HasValue ? TvgMath.Transform(new Point(previousX - cp1x, previousY - cp1y), transformM.Value) : new Point(previousX - cp1x, previousY - cp1y);
                    var in3 = transformM.HasValue ? TvgMath.Transform(new Point(x + cp2x, y + cp2y), transformM.Value) : new Point(x + cp2x, y + cp2y);
                    var in4 = transformM.HasValue ? TvgMath.Transform(new Point(x, y), transformM.Value) : new Point(x, y);
                    shape.CubicTo(in2.x, in2.y, in3.x, in3.y, in4.x, in4.y);
                }
                else
                {
                    var inP = new Point(x, y);
                    if (transformM.HasValue) inP = TvgMath.Transform(inP, transformM.Value);
                    shape.LineTo(inP.x, inP.y);
                }
                angle += anglePerPoint * direction;
            }
            // ensure proper shape closure
            Close(ref shape.rs.path.pts, inPt, hasRoundness);
            shape.Close();

            if (ctx.modifiers != null) ctx.modifiers.Polystar(shape.rs.path, merging.rs.path, 0.0f, false);
        }

        private void UpdatePolystar(LottieGroup parent, int childIdx, float frameNo, Inlist<RenderContext> contexts, RenderContext ctx)
        {
            var star = (LottiePolyStar)parent.children[childIdx];

            // Optimize: Can we skip the individual coords transform?
            var matrix = TvgMath.Identity();
            TvgMath.Translate(ref matrix, star.position.EvaluateForRendering(frameNo, tween, exps));
            TvgMath.Rotate(ref matrix, star.rotation.Evaluate(frameNo, tween, exps));

            if (ctx.transform.HasValue) matrix = TvgMath.Multiply(ctx.transform.Value, matrix);

            var identity = TvgMath.IsIdentity(matrix);

            if (ctx.repeaters.Count == 0)
            {
                Draw(parent, star, ctx);
                if (star.starType == LottiePolyStar.StarType.Star) UpdateStar(star, frameNo, identity ? null : matrix, ctx.merging!, ctx, tween, exps);
                else UpdatePolygon(parent, star, frameNo, identity ? null : matrix, ctx.merging!, ctx, tween, exps);
                ctx.merging!.pImpl.Mark(RenderUpdateFlag.Path);
            }
            else
            {
                var shape = star.renderPooler.Pooling();
                shape.Ref();
                try
                {
                    shape.ResetShape();
                    if (star.starType == LottiePolyStar.StarType.Star) UpdateStar(star, frameNo, identity ? null : matrix, shape, ctx, tween, exps);
                    else UpdatePolygon(parent, star, frameNo, identity ? null : matrix, shape, ctx, tween, exps);
                    Repeat(parent, shape, star.renderPooler, ctx);
                }
                finally { shape.Unref(); }
            }
        }

        private void UpdateRoundedCorner(LottieGroup parent, int childIdx, float frameNo, Inlist<RenderContext> contexts, RenderContext ctx)
        {
            var roundedCorner = (LottieRoundedCorner)parent.children[childIdx];
            var r = roundedCorner.radius.Evaluate(frameNo, tween, exps);
            if (r < LottieRoundnessModifier.ROUNDNESS_EPSILON) return;
            ctx.Update(new LottieRoundnessModifier(r));
        }

        private void UpdateOffsetPath(LottieGroup parent, int childIdx, float frameNo, Inlist<RenderContext> contexts, RenderContext ctx)
        {
            var offsetObj = (LottieOffsetPath)parent.children[childIdx];
            ctx.Update(new LottieOffsetModifier(offsetObj.offset.Evaluate(frameNo, tween, exps), offsetObj.miterLimit.Evaluate(frameNo, tween, exps), offsetObj.join));
        }

        private void UpdatePuckerBloat(LottieGroup parent, int childIdx, float frameNo, Inlist<RenderContext> contexts, RenderContext ctx)
        {
            var puckerBloat = (LottiePuckerBloat)parent.children[childIdx];
            ctx.Update(new LottiePuckerBloatModifier(puckerBloat.amount.Evaluate(frameNo, tween, exps)));
        }

        private void UpdateZigZag(LottieGroup parent, int childIdx, float frameNo, Inlist<RenderContext> contexts, RenderContext ctx)
        {
            var zigzag = (LottieZigZag)parent.children[childIdx];
            var amplitude = zigzag.amplitude.Evaluate(frameNo, tween, exps);
            if (TvgMath.Zero(amplitude)) return;
            var frequency = zigzag.frequency.Evaluate(frameNo, tween, exps);
            var point = (LottieZigZagModifier.PointType)zigzag.point.Evaluate(frameNo, tween, exps);
            ctx.Update(new LottieZigZagModifier(amplitude, frequency, point));
        }

        private void UpdateRepeater(LottieGroup parent, int childIdx, float frameNo, Inlist<RenderContext> contexts, RenderContext ctx)
        {
            var repeater = (LottieRepeater)parent.children[childIdx];

            var r = new RenderRepeater();
            r.cnt = (int)repeater.copies.Evaluate(frameNo, tween, exps);
            r.transform = ctx.propagator!.Transform();
            r.offset = repeater.offset.Evaluate(frameNo, tween, exps);
            r.position = repeater.position.EvaluateForRendering(frameNo, tween, exps);
            r.anchor = repeater.anchor.Evaluate(frameNo, tween, exps);
            r.scale = repeater.scale.Evaluate(frameNo, tween, exps);
            r.rotation = repeater.rotation.Evaluate(frameNo, tween, exps);
            r.startOpacity = repeater.startOpacity.Evaluate(frameNo, tween, exps);
            r.endOpacity = repeater.endOpacity.Evaluate(frameNo, tween, exps);
            r.inorder = repeater.inorder;
            ctx.repeaters.Add(r);

            ctx.merging = null;
        }

        private void UpdateTrimpath(LottieGroup parent, int childIdx, float frameNo, Inlist<RenderContext> contexts, RenderContext ctx)
        {
            var trimpath = (LottieTrimpath)parent.children[childIdx];

            trimpath.Segment(frameNo, out var begin, out var end, tween, exps);

            if (ctx.propagator!.rs.stroke != null)
            {
                var length = MathF.Abs(begin - end);
                var tmp = begin;
                begin = TvgMath.MultiplyAdd(length, ctx.propagator.rs.stroke.trim.begin, tmp);
                end = TvgMath.MultiplyAdd(length, ctx.propagator.rs.stroke.trim.end, tmp);
            }

            ctx.propagator.Trimpath(begin, end, trimpath.trimType == LottieTrimpath.TrimType.Simultaneous);
            ctx.merging = null;
        }

        private void UpdateChildren(LottieGroup parent, float frameNo, Inlist<RenderContext> contexts)
        {
            contexts.Head!.beginIdx = parent.children.Count - 1;

            while (!contexts.Empty())
            {
                var ctx = contexts.Head!;
                ctx.reqFragment = parent.reqFragment;
                var stop = false;
                for (var childIdx = ctx.beginIdx; childIdx >= 0; --childIdx)
                {
                    var child = parent.children[childIdx];
                    switch (child.type)
                    {
                        case LottieObject.ObjectType.Group:
                            UpdateGroup(parent, childIdx, frameNo, contexts, ctx);
                            break;
                        case LottieObject.ObjectType.Transform:
                            UpdateTransform(parent, childIdx, frameNo, contexts, ctx);
                            break;
                        case LottieObject.ObjectType.SolidFill:
                            stop = UpdateSolidFill(parent, childIdx, frameNo, contexts, ctx);
                            break;
                        case LottieObject.ObjectType.SolidStroke:
                            stop = UpdateSolidStroke(parent, childIdx, frameNo, contexts, ctx);
                            break;
                        case LottieObject.ObjectType.GradientFill:
                            stop = UpdateGradientFill(parent, childIdx, frameNo, contexts, ctx);
                            break;
                        case LottieObject.ObjectType.GradientStroke:
                            stop = UpdateGradientStroke(parent, childIdx, frameNo, contexts, ctx);
                            break;
                        case LottieObject.ObjectType.Rect:
                            UpdateRect(parent, childIdx, frameNo, contexts, ctx);
                            break;
                        case LottieObject.ObjectType.Ellipse:
                            UpdateEllipse(parent, childIdx, frameNo, contexts, ctx);
                            break;
                        case LottieObject.ObjectType.Path:
                            UpdatePath(parent, childIdx, frameNo, contexts, ctx);
                            break;
                        case LottieObject.ObjectType.Polystar:
                            UpdatePolystar(parent, childIdx, frameNo, contexts, ctx);
                            break;
                        case LottieObject.ObjectType.Trimpath:
                            UpdateTrimpath(parent, childIdx, frameNo, contexts, ctx);
                            break;
                        case LottieObject.ObjectType.Repeater:
                            UpdateRepeater(parent, childIdx, frameNo, contexts, ctx);
                            break;
                        case LottieObject.ObjectType.RoundedCorner:
                            UpdateRoundedCorner(parent, childIdx, frameNo, contexts, ctx);
                            break;
                        case LottieObject.ObjectType.OffsetPath:
                            UpdateOffsetPath(parent, childIdx, frameNo, contexts, ctx);
                            break;
                        case LottieObject.ObjectType.PuckerBloat:
                            UpdatePuckerBloat(parent, childIdx, frameNo, contexts, ctx);
                            break;
                        case LottieObject.ObjectType.ZigZag:
                            UpdateZigZag(parent, childIdx, frameNo, contexts, ctx);
                            break;
                        default: break;
                    }

                    // stop processing for those invisible contents
                    if (stop || ctx.propagator!.Opacity() == 0) break;
                }
                // Remove from the intrusive list, then release to pool
                contexts.Remove(ctx);
                ReleaseContext(ctx);
            }
        }

        private void UpdatePrecomp(LottieComposition comp, LottieLayer precomp, float frameNo)
        {
            if (precomp.children.Count == 0) return;

            frameNo = precomp.Remap(comp, frameNo, exps);

            for (int i = precomp.children.Count - 1; i >= 0; --i)
            {
                var child = (LottieLayer)precomp.children[i];
                if (!child.matteSrc) UpdateLayer(comp, precomp.scene!, child, frameNo);
            }

            // clip the layer viewport
            var clipper = precomp.statical.Pooling(true);
            clipper.Transform(precomp.cacheMatrix);
            precomp.scene!.Clip(clipper);
        }

        private void UpdatePrecomp(LottieComposition comp, LottieLayer precomp, float frameNo, LottieTween tween)
        {
            // record & recover the tweening frame number before remapping
            var record = tween.to;
            tween.to = precomp.Remap(comp, record, exps);

            UpdatePrecomp(comp, precomp, frameNo);

            tween.to = record;
        }

        private void UpdateSolid(LottieLayer layer)
        {
            layer.scene!.Add(layer.statical.Pooling(true));
        }

        private void UpdateImage(LottieGroup layer)
        {
            if (layer.children.Count == 0) return;

            if (layer.children[0] is not LottieImage image) return;
            var picture = image.Get();

            // resolve an image asset if needed
            if (resolver != null && !image.resolved)
            {
                resolver.func?.Invoke(picture, image.bitmap.path ?? "", resolver.data);
                picture.SetSize(image.bitmap.width, image.bitmap.height);
                image.resolved = true;
            }

            // LottieImage can be shared among other layers
            layer.scene!.Add(picture.RefCnt() == 1 ? picture : (Picture)picture.Duplicate());
        }

        private void UpdateURLFont(LottieLayer layer, float frameNo, LottieText text, TextDocument doc)
        {
            // text load
            var paint = Text.Gen();
            if (paint.SetFont(doc.name) != Result.Success)
            {
                string? src;
                if (text.font?.external == true) src = text.font.b64src;
                else src = $"name:{doc.name}";

                if (resolver == null || resolver.func == null || !resolver.func(paint, src, resolver.data))
                {
                    paint.SetFont(null); // fallback to any available font
                }
            }

            // text build - preprocess text
            var textStr = doc.Utf8;
            var buf = new byte[textStr.Length + 1];
            var bIdx = 0;
            var feed = false;
            for (int i = 0; i < textStr.Length; ++i)
            {
                // replace the carriage return and end of text with line feed.
                if (textStr[i] == '\r' || textStr[i] == '\x03')
                {
                    if (i == textStr.Length - 1) break;  // ignore trailing occurrences
                    if (!feed) buf[bIdx] = (byte)'\n';
                    else continue;
                }
                else buf[bIdx] = textStr[i];
                feed = buf[bIdx] == '\n';
                ++bIdx;
            }
            var processedText = TvgUtf8.Decode(buf.AsSpan(0, bIdx));

            var color = doc.color;
            paint.SetFill((byte)color.r, (byte)color.g, (byte)color.b);
            paint.SetFontSize(doc.size * 75.0f); // 1 pt = 1/72; 1 in = 96 px; -> 72/96 = 0.75
            if (text.font != null && text.font.style != null && text.font.style.Contains("Italic")) paint.SetItalic(0.18f);
            paint.SetText(processedText);
            paint.SetLayout(doc.bboxSize.x, doc.bboxSize.y);
            if (doc.bboxSize.x > 0.0f) paint.SetWrapping(TextWrap.Smart);

            // Align to the baseline when unboxed, otherwise to the box top.
            paint.GetMetrics(out var metrics);
            var vpos = doc.bboxPos.y - (doc.bboxSize.y > 0 ? 0 : metrics.ascent);
            paint.Translate(doc.bboxPos.x, vpos);
            paint.SetAlign(doc.justify, 0);

            var hspacing = 1.0f + doc.tracking * doc.size / metrics.ascent;
            var vspacing = (doc.height > 0.0f && paint.Lines() > 1) ? (doc.height / metrics.advance) : 1.0f;
            paint.SetSpacing(hspacing, vspacing);

            layer.scene!.Add(paint);

            // outline
            var strkColor = doc.strokeColor;
            if (doc.strokeWidth > 0.0f)
            {
                paint.SetOutline(doc.strokeWidth, (byte)strkColor.r, (byte)strkColor.g, (byte)strkColor.b);
                paint.shape.Order(doc.strokeBelow);
            }

            // text range
            if (text.ranges.Count == 0) return;

            foreach (var range in text.ranges)
            {
                var f = range.Factor(frameNo, (float)doc.Utf8.Length, 0.0f);
                if (TvgMath.Zero(f)) continue;

                // fill & opacity
                range.Color(frameNo, ref color, ref strkColor, f, tween, exps);
                paint.SetFill((byte)color.r, (byte)color.g, (byte)color.b);
                paint.Opacity(range.style.opacity.Evaluate(frameNo, tween, exps));

                // stroke
                if (range.style.flagStrokeWidth)
                {
                    paint.SetOutline(f * range.style.strokeWidth.Evaluate(frameNo, tween, exps), (byte)strkColor.r, (byte)strkColor.g, (byte)strkColor.b);
                }
            }
        }

        private Shape TextShape(LottieText text, float frameNo, TextDocument doc, LottieGlyph glyph, RenderText ctx)
        {
            ref var transform = ref ctx.lineScene.Transform();
            var shape = text.renderPooler.Pooling();
            shape.ResetShape();

            foreach (var grpObj in glyph.children)
            {
                var group = (LottieGroup)grpObj;
                foreach (var childObj in group.children)
                {
                    unsafe
                    {
                        ((LottiePath)childObj).pathset.Evaluate(frameNo, shape.rs.path, null, tween, exps);
                        shape.pImpl.Mark(RenderUpdateFlag.Path);
                    }
                }
            }
            shape.SetFill((byte)doc.color.r, (byte)doc.color.g, (byte)doc.color.b);
            shape.Translate(ctx.cursor.x - transform.e13, ctx.cursor.y - transform.e23);
            shape.Opacity(255);

            if (doc.strokeWidth > 0.0f)
            {
                shape.StrokeJoin(StrokeJoin.Round);
                shape.StrokeWidth(doc.strokeWidth / ctx.scale);
                shape.StrokeFill((byte)doc.strokeColor.r, (byte)doc.strokeColor.g, (byte)doc.strokeColor.b);
                shape.Order(doc.strokeBelow);
            }
            return shape;
        }

        private bool UpdateTextRange(LottieText text, float frameNo, Shape shape, TextDocument doc, RenderText ctx)
        {
            if (text.ranges.Count == 0) return false;

            ref var transform = ref ctx.lineScene.Transform();
            var scaling = new Point(1.0f, 1.0f);
            var translation = new Point(0, 0);
            var rotation = 0.0f;
            var color = doc.color;
            var strokeColor = doc.strokeColor;
            byte opacity = 255, fillOpacity = 255, strokeOpacity = 255;
            var needGroup = false;

            foreach (var range in text.ranges)
            {
                var basedIdx = ctx.idx;

                if (range.based == LottieTextRange.Based.CharsExcludingSpaces) basedIdx = ctx.idx - ctx.space;
                else if (range.based == LottieTextRange.Based.Words) basedIdx = ctx.line + ctx.space;
                else if (range.based == LottieTextRange.Based.Lines) basedIdx = ctx.line;

                var f = range.Factor(frameNo, (float)ctx.nChars, (float)basedIdx);
                if (TvgMath.Zero(f)) continue;

                needGroup = true;

                // transform
                translation = LottieModifierHelpers.ScaleAdd(translation, range.style.position.EvaluateForRendering(frameNo, tween, exps), f);
                var sc = range.style.scale.Evaluate(frameNo, tween, exps);
                scaling = TvgMath.PointMul(scaling, new Point(
                    TvgMath.MultiplyAdd(f, TvgMath.MultiplyAdd(sc.x, 0.01f, -1.0f), 1.0f),
                    TvgMath.MultiplyAdd(f, TvgMath.MultiplyAdd(sc.y, 0.01f, -1.0f), 1.0f)));
                rotation = TvgMath.MultiplyAdd(f, range.style.rotation.Evaluate(frameNo, tween, exps), rotation);

                // fill & opacity
                opacity = (byte)TvgMath.MultiplyAdd(-f, opacity - range.style.opacity.Evaluate(frameNo, tween, exps), opacity);
                shape.Opacity(opacity);

                range.Color(frameNo, ref color, ref strokeColor, f, tween, exps);
                fillOpacity = (byte)TvgMath.MultiplyAdd(-f, fillOpacity - range.style.fillOpacity.Evaluate(frameNo, tween, exps), fillOpacity);
                shape.SetFill((byte)color.r, (byte)color.g, (byte)color.b, fillOpacity);

                // stroke
                if (range.style.flagStrokeWidth) shape.StrokeWidth(f * range.style.strokeWidth.Evaluate(frameNo, tween, exps) / ctx.scale);
                if (shape.GetStrokeWidth() > 0.0f)
                {
                    strokeOpacity = (byte)TvgMath.MultiplyAdd(-f, strokeOpacity - range.style.strokeOpacity.Evaluate(frameNo, tween, exps), strokeOpacity);
                    shape.StrokeFill((byte)strokeColor.r, (byte)strokeColor.g, (byte)strokeColor.b, strokeOpacity);
                    shape.Order(doc.strokeBelow);
                }
                ctx.cursor.x = TvgMath.MultiplyAdd(f, range.style.letterSpace.Evaluate(frameNo, tween, exps), ctx.cursor.x);
                var spacing = f * range.style.lineSpace.Evaluate(frameNo, tween, exps);
                if (spacing > ctx.lineSpace) ctx.lineSpace = spacing;
            }
            // Apply line group transformation just once
            if (ctx.lineScene.Paints().Count == 0 && needGroup)
            {
                TvgMath.SetIdentity(out transform);
                TvgMath.Translate(ref transform, ctx.cursor);

                // center pivoting
                var align = text.alignOp.anchor.Evaluate(frameNo, tween, exps);
                transform.e13 += align.x;
                transform.e23 += align.y;
                TvgMath.Rotate(ref transform, rotation);

                // center pivoting
                var pivot = TvgMath.PointMul(align, -1.0f);
                transform.e13 += TvgMath.MultiplyAdd(pivot.x, transform.e11, pivot.x * transform.e12);
                transform.e23 += TvgMath.MultiplyAdd(pivot.y, transform.e21, pivot.y * transform.e22);

                //world space translation
                transform.e13 += translation.x / ctx.scale;
                transform.e23 += translation.y / ctx.scale;

                ctx.lineScene.Transform(transform);
            }
            var matrix2 = TvgMath.Identity();
            TvgMath.Translate(ref matrix2, TvgMath.PointSub(TvgMath.PointAdd(TvgMath.PointDiv(translation, ctx.scale), ctx.cursor), new Point(transform.e13, transform.e23)));
            TvgMath.Scale(ref matrix2, TvgMath.PointMul(scaling, ctx.capScale));
            shape.Transform(matrix2);

            if (needGroup) ctx.lineScene.Add(shape);

            return needGroup;
        }

        private static void Commit(LottieGlyph glyph, Shape shape, RenderText ctx)
        {
            var matrix = TvgMath.Identity();

            if (ctx.follow != null)
            {
                var angle = 0.0f;
                var width = glyph.width * 0.5f;
                var pos = ctx.follow.Position(ctx.cursor.x + width + ctx.firstMargin, ref angle);
                matrix.e11 = matrix.e22 = ctx.capScale;
                if (ctx.perpendicular) TvgMath.Rotate(ref matrix, TvgMath.Rad2Deg(angle));
                matrix.e13 = TvgMath.MultiplyAdd(-width, matrix.e11, pos.x);
                matrix.e23 = TvgMath.MultiplyAdd(-width, matrix.e21, pos.y);
            }
            else
            {
                matrix.e11 = matrix.e22 = ctx.capScale;
                matrix.e13 = ctx.cursor.x;
                matrix.e23 = ctx.cursor.y;
            }
            shape.Transform(matrix);
            ctx.textScene.Add(shape);
        }

        private static LottieGlyph? SearchGlyph(LottieFont font, ReadOnlySpan<byte> text, int idx, TextDocument doc, out float capScale)
        {
            capScale = 1.0f;
            Span<byte> capCode = stackalloc byte[1];
            scoped ReadOnlySpan<byte> code = text.Slice(idx);
            if (idx < text.Length && text[idx] < 0x80 && doc.caps > 0 && text[idx] >= 'a' && text[idx] <= 'z')
            {
                capCode[0] = (byte)(text[idx] + 'A' - 'a');
                code = capCode;
                if (doc.caps == 2) capScale = 0.7f;
            }

            foreach (var glyph in font.chars)
            {
                if (code.Length >= glyph.len && glyph.Utf8Code.Length >= glyph.len &&
                    code.Slice(0, glyph.len).SequenceEqual(glyph.Utf8Code.AsSpan(0, glyph.len))) return glyph;
            }
            return null;
        }

        private static float NextWordWidth(LottieText text, TextDocument doc, ReadOnlySpan<byte> str, int idx)
        {
            var w = 0.0f;
            while (idx < str.Length && str[idx] != 0 && str[idx] != ' ' && str[idx] != '\r' && str[idx] != '\x03')
            {
                var glyph = SearchGlyph(text.font!, str, idx, doc, out var capScale);
                if (glyph != null)
                {
                    w = TvgMath.MultiplyAdd(glyph.width + doc.tracking, capScale, w);
                    idx += glyph.len;
                }
                else ++idx;
            }
            return w;
        }

        private void UpdateLocalFont(LottieLayer layer, float frameNo, LottieText text, TextDocument doc)
        {
            using var ctx = new RenderText(text, doc);
            ctx.follow = (text.follow != null && (uint)text.follow.maskIdx < layer.masks.Count) ? text.follow : null;
            ctx.firstMargin = ctx.follow != null ? ctx.follow.Prepare(layer.masks[ctx.follow.maskIdx], frameNo, ctx.scale, tween, exps) : 0.0f;
            ctx.perpendicular = ctx.follow != null && ctx.follow.perpendicular.Evaluate(frameNo, tween, exps) != 0;
            var lineWrapped = false;

            // text string
            while (true)
            {
                byte currentChar = ctx.pIdx < ctx.utf8.Length ? ctx.utf8[ctx.pIdx] : (byte)0;

                // new line of the cursor position
                if (lineWrapped || currentChar == 13 || currentChar == 3 || currentChar == '\0')
                {
                    // text layout position
                    var ascent = text.font!.ascent * ctx.scale;
                    if (ascent > doc.bboxSize.y) ascent = doc.bboxSize.y;

                    // horizontal alignment
                    var layout = new Point(doc.bboxPos.x, doc.bboxPos.y + ascent - doc.shift);
                    layout.x = TvgMath.MultiplyAdd(doc.justify, TvgMath.MultiplyAdd(-ctx.cursor.x, ctx.scale, doc.bboxSize.x), layout.x);

                    // new text group, single scene based on text-grouping
                    ctx.textScene.Add(ctx.lineScene);
                    ctx.textScene.Translate(layout.x, layout.y);
                    ctx.textScene.Scale(ctx.scale);
                    layer.scene!.Add(ctx.textScene);

                    ctx.lineScene = Scene.Gen();
                    ctx.lineScene.Translate(ctx.cursor.x, ctx.cursor.y);

                    if (currentChar == '\0')
                    {
                        ctx.textScene = null!;
                        break;
                    }
                    if (!lineWrapped) ++ctx.pIdx;

                    ctx.totalLineSpace += ctx.lineSpace;
                    ctx.lineSpace = 0.0f;
                    lineWrapped = false;

                    // new text group, single scene for each line
                    ctx.textScene = Scene.Gen();
                    ctx.cursor = new Point(0.0f, TvgMath.MultiplyAdd(++ctx.line, doc.height, ctx.totalLineSpace) / ctx.scale);
                    continue;
                }
                var glyph = SearchGlyph(text.font!, ctx.utf8, ctx.pIdx, doc, out ctx.capScale);
                var advance = glyph != null ? (glyph.width + doc.tracking) * ctx.capScale : 0.0f;

                if (currentChar == ' ')
                {
                    if (doc.bboxSize.x > 0.0f && (ctx.cursor.x + advance + NextWordWidth(text, doc, ctx.utf8, ctx.pIdx + 1)) * ctx.scale >= doc.bboxSize.x)
                    {
                        ++ctx.pIdx;
                        lineWrapped = true;
                        continue;
                    }
                    ++ctx.space;
                    // new text group, single scene for each word
                    if (text.alignOp.group == LottieText.AlignOption.Group.Word)
                    {
                        ctx.textScene.Add(ctx.lineScene);
                        ctx.lineScene = Scene.Gen();
                        ctx.lineScene.Translate(ctx.cursor.x, ctx.cursor.y);
                    }
                }
                // draw matched glyphs
                if (glyph != null)
                {
                    if (doc.bboxSize.x > 0.0f && ctx.cursor.x > 0.0f && (ctx.cursor.x + advance) * ctx.scale > doc.bboxSize.x)
                    {
                        lineWrapped = true;
                        continue;
                    }
                    // new text group, single scene for each characters
                    if (text.alignOp.group == LottieText.AlignOption.Group.Chars || text.alignOp.group == LottieText.AlignOption.Group.All)
                    {
                        ctx.textScene.Add(ctx.lineScene);
                        ctx.lineScene = Scene.Gen();
                        ctx.lineScene.Translate(ctx.cursor.x, ctx.cursor.y);
                    }
                    var shape = TextShape(text, frameNo, doc, glyph, ctx);
                    if (!UpdateTextRange(text, frameNo, shape, doc, ctx)) Commit(glyph, shape, ctx);
                    ctx.cursor.x += advance;
                    ctx.pIdx += glyph.len;
                    ctx.idx += glyph.len;
                } else
                {
                    ++ctx.pIdx;
                    ++ctx.idx;
                }
            }
        }

        private void UpdateText(LottieLayer layer, float frameNo)
        {
            if (layer.children.Count == 0) return;

            var text = (LottieText)layer.children[0];
            var doc = text.doc.Evaluate(frameNo, exps);
            if (doc.text == null) return;

            if (text.font != null && text.font.origin == LottieFont.Origin.Local && text.font.chars.Count > 0)
            {
                UpdateLocalFont(layer, frameNo, text, doc);
            }
            else
            {
                UpdateURLFont(layer, frameNo, text, doc);
            }
        }

        private void UpdateMasks(LottieLayer layer, float frameNo)
        {
            if (layer.masks.Count == 0) return;

            // Introduce an intermediate scene for embracing matte + masking or precomp clipping + masking replaced by clipping
            if (layer.matteTarget != null || layer.layerType == LottieLayer.LayerType.Precomp)
            {
                var scene = Scene.Gen();
                scene.Add(layer.scene!);
                layer.scene = scene;
            }

            Shape? pShape = null;
            MaskMethod pMethod = MaskMethod.None;
            byte pOpacity = 0;

            foreach (var mask in layer.masks)
            {
                if (mask.method == MaskMethod.None) continue;

                var method = mask.method;
                var opacity = mask.opacity.Evaluate(frameNo);
                var expand = mask.expand.Evaluate(frameNo);

                // the first mask
                if (pShape == null)
                {
                    pShape = layer.renderPooler.Pooling();
                    pShape.ResetFull();
                    var compMethod = (method == MaskMethod.Subtract || method == MaskMethod.InvAlpha) ? MaskMethod.InvAlpha : MaskMethod.Alpha;
                    // Cheaper. Replace the masking with a clipper
                    if (!layer.effect && layer.masks.Count == 1 && compMethod == MaskMethod.Alpha)
                    {
                        layer.scene!.Opacity(RenderHelper.Multiply(layer.scene.Opacity(), opacity));
                        layer.scene.Clip(pShape);
                    }
                    else
                    {
                        layer.scene!.SetMask(pShape, compMethod);
                    }
                }
                // Chain mask composition
                else if (pMethod != method || pOpacity != opacity || (method != MaskMethod.Subtract && method != MaskMethod.Difference))
                {
                    var shape = layer.renderPooler.Pooling();
                    shape.ResetFull();
                    pShape.SetMask(shape, method);
                    pShape = shape;
                }

                pShape.SetFill(255, 255, 255, opacity);
                pShape.Transform(layer.cacheMatrix);

                // Default Masking
                unsafe
                {
                    if (expand == 0.0f)
                    {
                        mask.pathset.Evaluate(frameNo, pShape.rs.path, null, tween, exps);
                    }
                    // Masking with Expansion (Offset)
                    else
                    {
                        var offset = new LottieOffsetModifier(expand);
                        mask.pathset.Evaluate(frameNo, pShape.rs.path, null, tween, exps, offset);
                    }
                }
                pOpacity = opacity;
                pMethod = method;
            }
        }

        private bool UpdateMatte(LottieComposition comp, float frameNo, LottieLayer layer)
        {
            var target = layer.matteTarget;
            if (target == null || target.layerType == LottieLayer.LayerType.Null) return true;

            UpdateLayer(comp, null, target, frameNo);

            if (target.scene != null)
            {
                layer.scene!.SetMask(target.scene, layer.matteType);
            }
            else if (layer.matteType == MaskMethod.Alpha || layer.matteType == MaskMethod.Luma)
            {
                // matte target does not exist. alpha blending definitely brings an invisible result
                Paint.Rel(layer.scene);
                layer.scene = null;
                return false;
            }
            return true;
        }

        private void UpdateStrokeEffect(LottieLayer layer, LottieFxStroke effect, float frameNo)
        {
            if (layer.masks.Count == 0) return;

            var shape = layer.renderPooler.Pooling();
            shape.ResetShape();

            unsafe
            {
                // FIXME: all mask
                if (effect.allMask.Evaluate(frameNo) != 0)
                {
                    foreach (var mask in layer.masks)
                    {
                        mask.pathset.Evaluate(frameNo, shape.rs.path, null, tween, exps);
                    }
                }
                // A specific mask
                else
                {
                    var idx = effect.mask.Evaluate(frameNo) - 1;
                    if (idx < 0 || idx >= layer.masks.Count) return;
                    layer.masks[idx].pathset.Evaluate(frameNo, shape.rs.path, null, tween, exps);
                }
            }

            shape.Transform(layer.cacheMatrix);
            shape.Trimpath(effect.begin.Evaluate(frameNo) * 0.01f, effect.end.Evaluate(frameNo) * 0.01f);
            shape.StrokeFill(255, 255, 255, (byte)(effect.opacity.Evaluate(frameNo) * 255.0f));
            shape.StrokeJoin(StrokeJoin.Round);
            shape.StrokeCap(StrokeCap.Round);

            var size = effect.size.Evaluate(frameNo) * 2.0f;
            shape.StrokeWidth(size);

            // fill the color to the layer shapes if any
            var color = effect.color.Evaluate(frameNo);
            if (color.r != 255 || color.g != 255 || color.b != 255)
            {
                var accessor = Accessor.Gen();
                var isStrokeLayer = layer.layerType == LottieLayer.LayerType.Shape;
                accessor.Set(layer.scene!, (Paint paint, object? data) =>
                {
                    if (paint.PaintType() == Type.Shape)
                    {
                        var s = (Shape)paint;
                        if (isStrokeLayer)
                        {
                            s.StrokeWidth(size);
                            s.StrokeFill((byte)color.r, (byte)color.g, (byte)color.b, 255);
                        }
                        s.SetFill((byte)color.r, (byte)color.g, (byte)color.b, 255);
                    }
                    return true;
                }, null);
            }

            layer.scene!.SetMask(shape, MaskMethod.Alpha);
        }

        private void UpdateEffect(LottieLayer layer, float frameNo, byte quality)
        {
            const float BLUR_TO_SIGMA = 0.3f;

            if (layer.effects.Count == 0) return;

            foreach (var effect in layer.effects)
            {
                if (!effect.enable) continue;
                switch (effect.type)
                {
                    case LottieEffect.EffectType.Tint:
                    {
                        var fx = (LottieFxTint)effect;
                        var black = fx.black.Evaluate(frameNo);
                        var white = fx.white.Evaluate(frameNo);
                        layer.scene!.AddEffect(SceneEffect.Tint, black.r, black.g, black.b, white.r, white.g, white.b, (double)fx.intensity.Evaluate(frameNo));
                        break;
                    }
                    case LottieEffect.EffectType.Fill:
                    {
                        var fx = (LottieFxFill)effect;
                        var clr = fx.color.Evaluate(frameNo);
                        layer.scene!.AddEffect(SceneEffect.Fill, clr.r, clr.g, clr.b, (int)(255.0f * fx.opacity.Evaluate(frameNo)));
                        break;
                    }
                    case LottieEffect.EffectType.Stroke:
                    {
                        var fx = (LottieFxStroke)effect;
                        UpdateStrokeEffect(layer, fx, frameNo);
                        break;
                    }
                    case LottieEffect.EffectType.Tritone:
                    {
                        var fx = (LottieFxTritone)effect;
                        var dark = fx.dark.Evaluate(frameNo);
                        var midtone = fx.midtone.Evaluate(frameNo);
                        var bright = fx.bright.Evaluate(frameNo);
                        layer.scene!.AddEffect(SceneEffect.Tritone, dark.r, dark.g, dark.b, midtone.r, midtone.g, midtone.b, bright.r, bright.g, bright.b, (int)fx.blend.Evaluate(frameNo));
                        break;
                    }
                    case LottieEffect.EffectType.DropShadow:
                    {
                        var fx = (LottieFxDropShadow)effect;
                        var clr = fx.color.Evaluate(frameNo);
                        var op = Math.Min(255, (int)fx.opacity.Evaluate(frameNo));
                        layer.scene!.AddEffect(SceneEffect.DropShadow, clr.r, clr.g, clr.b, op, (double)fx.angle.Evaluate(frameNo), (double)fx.distance.Evaluate(frameNo), (double)(fx.blurness.Evaluate(frameNo) * BLUR_TO_SIGMA), (int)quality);
                        break;
                    }
                    case LottieEffect.EffectType.GaussianBlur:
                    {
                        var fx = (LottieFxGaussianBlur)effect;
                        layer.scene!.AddEffect(SceneEffect.GaussianBlur, (double)(fx.blurness.Evaluate(frameNo) * BLUR_TO_SIGMA), fx.direction.Evaluate(frameNo) - 1, fx.wrap.Evaluate(frameNo), (int)quality);
                        break;
                    }
                    default: break;
                }
            }
        }

        private void UpdateAudio(LottieComposition comp, LottieLayer layer, float frameNo)
        {
            if (layer.children.Count == 0 || layer.children[0] is not LottieAudio asset) return;
            var ctrl = layer.Audio();
            var active = frameNo >= layer.inFrame && frameNo < layer.outFrame;
            var position = layer.Remap(comp, frameNo, exps);
            if (position < 0.0f) active = false;
            var value = active ? volume * ctrl.volume.Evaluate(frameNo, tween, exps) : 100.0f;
            var changed = active != ctrl.prevActive || (active && !TvgMath.Equal(value, ctrl.prevVolume));
            if (changed)
            {
                var info = new LottieAudioResolver
                {
                    src = asset.data,
                    mimeType = asset.mimeType,
                    size = asset.size,
                    embedded = asset.size > 0,
                    volume = value,
                    active = active,
                    offset = active ? position / comp.frameRate : 0.0f
                };
                audioResolver?.Invoke(info, audioResolverData);
            }
            ctrl.prevActive = active;
            ctrl.prevVolume = value;
        }

        private void UpdateLayer(LottieComposition comp, Scene? scene, LottieLayer layer, float frameNo)
        {
            if (layer.layerType == LottieLayer.LayerType.Audio)
            {
                if (audioResolver != null) UpdateAudio(comp, layer, frameNo);
                return;
            }

            layer.scene = null;

            // visibility
            if (frameNo < layer.inFrame || frameNo >= layer.outFrame) return;

            UpdateTransformLayer(layer, frameNo);

            // full transparent scene. no need to perform
            if (layer.layerType != LottieLayer.LayerType.Null && layer.cacheOpacity == 0) return;

            // Prepare render data
            layer.scene = Scene.Gen();
            layer.scene.id = (uint)layer.id;

            // ignore opacity when Null layer?
            if (layer.layerType != LottieLayer.LayerType.Null) layer.scene.Opacity(layer.cacheOpacity);

            layer.scene.Transform(layer.cacheMatrix);

            if (!UpdateMatte(comp, frameNo, layer)) return;

            layer.scene.SetBlend(layer.blendMethod);

            switch (layer.layerType)
            {
                case LottieLayer.LayerType.Precomp:
                {
                    if (!tween.active) UpdatePrecomp(comp, layer, frameNo);
                    else UpdatePrecomp(comp, layer, frameNo, tween);
                    break;
                }
                case LottieLayer.LayerType.Solid:
                {
                    UpdateSolid(layer);
                    break;
                }
                case LottieLayer.LayerType.Image:
                {
                    UpdateImage(layer);
                    break;
                }
                case LottieLayer.LayerType.Text:
                {
                    UpdateText(layer, frameNo);
                    break;
                }
                default:
                {
                    if (layer.children.Count > 0)
                    {
                        var contexts = AcquireContextList();
                        try
                        {
                            contexts.Back(AcquireContext(layer.renderPooler.Pooling()));
                            UpdateChildren(layer, frameNo, contexts);
                        }
                        finally { ReleaseContextList(contexts); }
                    }
                    break;
                }
            }

            UpdateMasks(layer, frameNo);

            UpdateEffect(layer, frameNo, comp.quality);

            scene?.Add(layer.scene);
        }

        // --- Build methods ---

        private static void BuildReference(LottieComposition comp, LottieLayer layer)
        {
            foreach (var asset in comp.assets)
            {
                if (layer.rid != asset.id) continue;
                if (layer.layerType == LottieLayer.LayerType.Precomp)
                {
                    if (asset.type != LottieObject.ObjectType.Composition || asset is not LottieRootLayer assetLayer)
                    {
                        TvgCommon.TVGERR("LOTTIE", "Precomp layer references a non-composition asset.");
                        break;
                    }
                    if (BuildComposition(comp, assetLayer))
                    {
                        layer.children = new List<LottieObject>(assetLayer.children);
                        layer.reqFragment = assetLayer.reqFragment;
                        layer.effect |= assetLayer.effect;
                    }
                }
                else if (layer.layerType == LottieLayer.LayerType.Image || layer.layerType == LottieLayer.LayerType.Audio)
                {
                    layer.children.Add(asset);
                }
                break;
            }
        }

        private static void BuildHierarchy(LottieGroup parent, LottieLayer child)
        {
            if (child.pix == -1) return;

            if (child.matteTarget != null && child.pix == child.matteTarget.ix)
            {
                child.parent = child.matteTarget;
                return;
            }

            for (int i = 0; i < parent.children.Count; ++i)
            {
                var par = (LottieLayer)parent.children[i];
                if (ReferenceEquals(child, par)) continue;
                if (child.pix == par.ix)
                {
                    child.parent = par;
                    break;
                }
                if (par.matteTarget != null && par.matteTarget.ix == child.pix)
                {
                    child.parent = par.matteTarget;
                    break;
                }
            }
        }

        private static void AttachFont(LottieComposition comp, LottieLayer parent)
        {
            if (parent.children.Count > 0 && parent.children[0] is LottieText text && text.type == LottieObject.ObjectType.Text)
            {
                var doc = text.doc.Evaluate(0);
                if (doc.name != null)
                {
                    foreach (var font in comp.fonts)
                    {
                        if (TvgStr.Equal(font.name, doc.name)) { text.font = font; break; }
                    }
                }
                return;
            }
            // Match the target's empty invalid text layer before rendering.
            parent.children.Clear();
        }

        private static bool BuildComposition(LottieComposition comp, LottieRootLayer parent)
        {
            if (parent.children.Count == 0) return false;
            if (parent.buildDone) return true;
            parent.buildDone = true;

            for (int i = 0; i < parent.children.Count; ++i)
            {
                var child = (LottieLayer)parent.children[i];

                // attach the precomp layer.
                if (child.rid != 0) BuildReference(comp, child);

                if (child.matteType != MaskMethod.None)
                {
                    // no index of the matte layer is provided: the layer above is used as the matte source
                    if (child.mix == -1)
                    {
                        if (i > 0)
                        {
                            child.matteTarget = (LottieLayer)parent.children[i - 1];
                        }
                    }
                    // matte layer is specified by an index.
                    else child.matteTarget = parent.LayerByIdx(child.mix);
                }

                if (child.matteTarget != null)
                {
                    child.matteTarget.matteSrc = true;
                    // parenting
                    BuildHierarchy(parent, child.matteTarget);
                    // precomp referencing
                    if (child.matteTarget.rid != 0) BuildReference(comp, child.matteTarget);
                }
                BuildHierarchy(parent, child);

                // attach the necessary font data
                if (child.layerType == LottieLayer.LayerType.Text) AttachFont(comp, child);
            }
            return true;
        }

        // --- Public API ---

        public void Build(LottieComposition comp)
        {
            if (comp.root == null) return;

            comp.root.scene = Scene.Gen();

            BuildComposition(comp, comp.root);

            // viewport clip
            var clip = Shape.Gen();
            clip.AppendRect(0, 0, comp.w, comp.h);
            comp.root.scene.Clip(clip);

            // turn off partial rendering for children
            comp.root.scene.fsize = new Point(comp.w, comp.h);
            comp.root.scene.fixedScene = true;
        }

        public bool Update(LottieComposition comp, float frameNo)
        {
            if (comp.root == null || comp.root.children.Count == 0) return false;

            comp.Clamp(ref frameNo);

            if (tween.active)
            {
                var tf = tween.to;
                comp.Clamp(ref tf);
                tween.to = tf;
            }

            if (exps != null && comp.expressions) exps.Update(comp.TimeAtFrame(frameNo));

            // update children layers
            for (int i = comp.root.children.Count - 1; i >= 0; --i)
            {
                var layer = (LottieLayer)comp.root.children[i];
                if (!layer.matteSrc) UpdateLayer(comp, comp.root.scene!, layer, frameNo);
            }

            return true;
        }
    }
}
