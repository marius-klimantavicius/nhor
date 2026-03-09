// Ported from ThorVG/src/renderer/tvgScene.h and ThorVG/inc/thorvg.h

using System;
using System.Collections.Generic;

namespace ThorVG
{
    /// <summary>Iterator over Scene children. Mirrors C++ SceneIterator.</summary>
    public class SceneIterator : Iterator
    {
        private readonly List<Paint> _paints;
        private int _index;

        public SceneIterator(List<Paint> paints)
        {
            _paints = paints;
            _index = 0;
        }

        public override Paint? Next()
        {
            if (_index >= _paints.Count) return null;
            return _paints[_index++];
        }

        public override uint Count() => (uint)_paints.Count;

        public override void Begin() { _index = 0; }
    }

    /// <summary>
    /// A scene containing child paint objects. Mirrors C++ tvg::Scene / SceneImpl.
    /// </summary>
    public class Scene : Paint
    {
        internal readonly List<Paint> paints = new List<Paint>();
        internal List<RenderEffect>? effects;

        protected Scene() { }

        public static Scene Gen() => new Scene();
        public override Type PaintType() => Type.Scene;

        public IReadOnlyList<Paint> Paints() => paints;

        public Result Add(Paint target, Paint? at = null)
        {
            if (target == null) return Result.InvalidArguments;
            if (target.pImpl.parent != null) return Result.InsufficientCondition;

            target.Ref();
            target.pImpl.Mark(RenderUpdateFlag.Transform);

            if (at == null)
            {
                paints.Add(target);
            }
            else
            {
                var idx = paints.IndexOf(at);
                if (idx < 0) return Result.InvalidArguments;
                paints.Insert(idx, target);
            }
            target.pImpl.parent = this;
            if (target.pImpl.clipper != null) target.pImpl.clipper.pImpl.parent = this;
            if (target.pImpl.maskData != null) target.pImpl.maskData.target.pImpl.parent = this;
            return Result.Success;
        }

        public Result Remove(Paint? paint = null)
        {
            if (paint == null)
            {
                // Clear all paints with damage logic
                return ClearPaints();
            }
            if (paint.pImpl.parent != this) return Result.InsufficientCondition;
            // When the paint is destroyed, damage will be triggered
            if (paint.pImpl.refCnt > 1) paint.pImpl.Damage();
            paint.pImpl.Unref();
            paints.Remove(paint);
            return Result.Success;
        }

        /// <summary>Clear all paints with partial damage logic. Mirrors C++ SceneImpl::clearPaints().</summary>
        private Result ClearPaints()
        {
            if (paints.Count == 0) return Result.Success;

            // Don't need to damage for children
            var recover = (fixedScene && pImpl.renderer != null) ? pImpl.renderer.Partial(true) : false;
            var partialDmg = !(effects != null || fixedScene || recover);

            foreach (var p in paints)
            {
                // When the paint is destroyed damage will be triggered
                if (p.pImpl.refCnt > 1 && partialDmg) p.pImpl.Damage();
                p.pImpl.Unref();
            }
            paints.Clear();

            if (fixedScene && pImpl.renderer != null) pImpl.renderer.Partial(recover);
            if (effects != null || fixedScene) pImpl.Damage(vport2); // redraw scene full region

            return Result.Success;
        }

        public Result AddEffect(SceneEffect effect, params object[] args)
        {
            if (effect == SceneEffect.Clear)
            {
                return ResetEffects();
            }

            effects ??= new List<RenderEffect>();
            RenderEffect? re = null;
            switch (effect)
            {
                case SceneEffect.GaussianBlur:
                    re = RenderEffectGaussianBlur.Gen(
                        Convert.ToSingle(args[0]), Convert.ToInt32(args[1]),
                        Convert.ToInt32(args[2]), Convert.ToInt32(args[3]));
                    break;
                case SceneEffect.DropShadow:
                    re = RenderEffectDropShadow.Gen(
                        Convert.ToInt32(args[0]), Convert.ToInt32(args[1]),
                        Convert.ToInt32(args[2]), Convert.ToInt32(args[3]),
                        Convert.ToSingle(args[4]), Convert.ToSingle(args[5]),
                        Convert.ToSingle(args[6]), Convert.ToInt32(args[7]));
                    break;
                case SceneEffect.Fill:
                    re = RenderEffectFill.Gen(
                        Convert.ToInt32(args[0]), Convert.ToInt32(args[1]),
                        Convert.ToInt32(args[2]), Convert.ToInt32(args[3]));
                    break;
                case SceneEffect.Tint:
                    re = RenderEffectTint.Gen(
                        Convert.ToInt32(args[0]), Convert.ToInt32(args[1]),
                        Convert.ToInt32(args[2]), Convert.ToInt32(args[3]),
                        Convert.ToInt32(args[4]), Convert.ToInt32(args[5]),
                        Convert.ToDouble(args[6]));
                    break;
                case SceneEffect.Tritone:
                    re = RenderEffectTritone.Gen(
                        Convert.ToInt32(args[0]), Convert.ToInt32(args[1]),
                        Convert.ToInt32(args[2]), Convert.ToInt32(args[3]),
                        Convert.ToInt32(args[4]), Convert.ToInt32(args[5]),
                        Convert.ToInt32(args[6]), Convert.ToInt32(args[7]),
                        Convert.ToInt32(args[8]), Convert.ToInt32(args[9]));
                    break;
            }

            if (re == null) return Result.InvalidArguments;
            effects.Add(re);
            return Result.Success;
        }

        // --- Rendering infrastructure (mirrors C++ SceneImpl) ---

        internal RenderRegion vport2;
        internal bool vdirty;
        internal byte sceneOpacity; // for composition
        internal Point fsize;
        internal bool fixedScene;

        /// <summary>Set the fixed scene size. Mirrors C++ SceneImpl::size(const Point&amp;).</summary>
        internal void SetSize(Point size)
        {
            fsize = size;
            fixedScene = (size.x > 0 && size.y > 0);
        }

        internal bool PaintSkip(RenderUpdateFlag flag)
        {
            return false;
        }

        internal bool PaintUpdate(RenderMethod renderer, in Matrix transform, ref ValueList<object?> clips, byte opacity, RenderUpdateFlag flag, bool clipper)
        {
            if (paints.Count == 0) return true;

            if (NeedComposition(opacity) != 0)
            {
                sceneOpacity = opacity;
                opacity = 255;
            }

            // allow partial rendering?
            var recover = fixedScene ? renderer.Partial(true) : false;

            for (int i = 0; i < paints.Count; ++i)
            {
                paints[i].pImpl.Update(renderer, transform, ref clips, opacity, flag, false);
            }

            // recover the condition
            if (fixedScene) renderer.Partial(recover);

            if (effects != null)
            {
                foreach (var effect in effects)
                {
                    renderer.Prepare(effect, transform);
                }
            }

            // this viewport update is more performant than in bounds(). No idea.
            vport2 = renderer.Viewport();

            if (fixedScene)
            {
                var pt = TvgMath.Transform(fsize, transform);
                var r = new RenderRegion(
                    (int)MathF.Round(transform.e13, MidpointRounding.AwayFromZero), (int)MathF.Round(transform.e23, MidpointRounding.AwayFromZero),
                    (int)MathF.Round(pt.x, MidpointRounding.AwayFromZero), (int)MathF.Round(pt.y, MidpointRounding.AwayFromZero));
                vport2.IntersectWith(r);
            }
            else
            {
                vdirty = true;
            }

            // bounds(renderer) here hinders parallelization
            if (fixedScene || effects != null) pImpl.Damage(vport2);

            return true;
        }

        internal bool PaintRender(RenderMethod renderer, CompositionFlag flag)
        {
            if (paints.Count == 0) return true;

            RenderCompositor? cmp = null;
            // its parent is already in composition mode, maybe parasitize its surface
            var incomposite = ((byte)CompositionFlag.PostProcessing & (byte)flag) != 0 && effects == null;
            var ret = true;

            renderer.Blend(pImpl.blendMethod);

            if (!incomposite && pImpl.cmpFlag != CompositionFlag.Invalid)
            {
                cmp = renderer.Target(PaintBounds(), renderer.ColorSpaceValue(), pImpl.cmpFlag);
                renderer.BeginComposite(cmp, MaskMethod.None, sceneOpacity);
            }

            for (int i = 0; i < paints.Count; ++i)
            {
                ret &= paints[i].pImpl.Render(renderer, pImpl.cmpFlag);
            }

            if (cmp != null)
            {
                // Apply post effects if any.
                if (effects != null)
                {
                    var direct = (effects.Count == 1) && pImpl.Marked(CompositionFlag.PostProcessing);
                    foreach (var effect in effects)
                    {
                        if (effect.valid) renderer.Render(cmp, effect, direct);
                    }
                }
                renderer.EndComposite(cmp);
            }

            return ret;
        }

        /// <summary>
        /// Compute geometric bounds from child paints. Mirrors C++ SceneImpl::bounds(Point*, Matrix, bool).
        /// </summary>
        internal bool GeometricBounds(Span<Point> pt4, in Matrix m, bool obb)
        {
            if (paints.Count == 0) return false;

            var min = new Point(float.MaxValue, float.MaxValue);
            var max = new Point(-float.MaxValue, -float.MaxValue);
            var ret = false;

            Span<Point> tmp = stackalloc Point[4];
            foreach (var paint in paints)
            {
                tmp.Clear();

                // For non-obb, pass the matrix down; for obb, pass identity to get local coords
                if (!paint.pImpl.GeometricBounds(tmp, obb ? TvgMath.Identity() : m, false)) continue;
                // Merge regions
                for (int i = 0; i < 4; ++i)
                {
                    if (tmp[i].x < min.x) min.x = tmp[i].x;
                    if (tmp[i].x > max.x) max.x = tmp[i].x;
                    if (tmp[i].y < min.y) min.y = tmp[i].y;
                    if (tmp[i].y > max.y) max.y = tmp[i].y;
                }
                ret = true;
            }

            pt4[0] = min;
            pt4[1] = new Point(max.x, min.y);
            pt4[2] = max;
            pt4[3] = new Point(min.x, max.y);

            if (obb)
            {
                pt4[0] = TvgMath.Transform(pt4[0], m);
                pt4[1] = TvgMath.Transform(pt4[1], m);
                pt4[2] = TvgMath.Transform(pt4[2], m);
                pt4[3] = TvgMath.Transform(pt4[3], m);
            }

            return ret;
        }

        internal RenderRegion PaintBounds()
        {
            if (paints.Count == 0) return default;
            if (!vdirty) return vport2;
            vdirty = false;

            // Merge regions
            var pRegion = new RenderRegion(int.MaxValue, int.MaxValue, 0, 0);
            foreach (var paint in paints)
            {
                var region = paint.pImpl.Bounds();
                if (region.min.x < pRegion.min.x) pRegion.min.x = region.min.x;
                if (pRegion.max.x < region.max.x) pRegion.max.x = region.max.x;
                if (region.min.y < pRegion.min.y) pRegion.min.y = region.min.y;
                if (pRegion.max.y < region.max.y) pRegion.max.y = region.max.y;
            }

            // Extends the render region if post effects require
            var eRegion = default(RenderRegion);
            if (effects != null)
            {
                foreach (var effect in effects)
                {
                    if (effect.valid && pImpl.renderer!.Region(effect)) eRegion.AddWith(effect.extend);
                }
            }

            pRegion.min.x += eRegion.min.x;
            pRegion.min.y += eRegion.min.y;
            pRegion.max.x += eRegion.max.x;
            pRegion.max.y += eRegion.max.y;

            vport2 = RenderRegion.Intersect(vport2, pRegion);
            return vport2;
        }

        private byte NeedComposition(byte opacity)
        {
            if (opacity == 0 || paints.Count == 0) return 0;

            // post effects, masking, blending may require composition
            if (effects != null) pImpl.Mark(CompositionFlag.PostProcessing);
            if (pImpl.GetMask(out _) != MaskMethod.None) pImpl.Mark(CompositionFlag.Masking);
            if (pImpl.blendMethod != BlendMethod.Normal) pImpl.Mark(CompositionFlag.Blending);

            // Half translucent requires intermediate composition.
            if (opacity == 255) return (byte)pImpl.cmpFlag;

            // Only shape or picture may not require composition.
            if (paints.Count == 1)
            {
                var type = paints[0].PaintType();
                if (type == Type.Shape || type == Type.Picture) return (byte)pImpl.cmpFlag;
            }

            pImpl.Mark(CompositionFlag.Opacity);
            return 1;
        }

        /// <summary>Renderer-based intersection test. Mirrors C++ SceneImpl::intersects().</summary>
        internal bool Intersects(in RenderRegion region)
        {
            if (pImpl.renderer == null) return false;

            if (PaintBounds().Intersected(region))
            {
                foreach (var paint in paints)
                {
                    if (paint.pImpl.Intersects(region)) return true;
                }
            }
            return false;
        }

        internal Paint DuplicateScene(Paint? ret)
        {
            if (ret != null)
            {
                TvgCommon.TVGERR("RENDERER", "TODO: duplicate()");
            }

            var scene = Gen();
            foreach (var p in paints)
            {
                var dup = p.Duplicate();
                dup.pImpl.parent = scene;
                dup.Ref();
                scene.paints.Add(dup);
            }

            // Duplicate effects
            if (effects != null)
            {
                scene.effects = new List<RenderEffect>();
                foreach (var effect in effects)
                {
                    RenderEffect? dupEffect = null;
                    switch (effect.type)
                    {
                        case SceneEffect.GaussianBlur:
                        {
                            var src = (RenderEffectGaussianBlur)effect;
                            dupEffect = new RenderEffectGaussianBlur
                            {
                                sigma = src.sigma,
                                direction = src.direction,
                                border = src.border,
                                quality = src.quality,
                                type = src.type,
                                extend = src.extend
                            };
                            break;
                        }
                        case SceneEffect.DropShadow:
                        {
                            var src = (RenderEffectDropShadow)effect;
                            dupEffect = new RenderEffectDropShadow
                            {
                                color = src.color,
                                angle = src.angle,
                                distance = src.distance,
                                sigma = src.sigma,
                                quality = src.quality,
                                type = src.type,
                                extend = src.extend
                            };
                            break;
                        }
                        case SceneEffect.Fill:
                        {
                            var src = (RenderEffectFill)effect;
                            dupEffect = new RenderEffectFill
                            {
                                color = src.color,
                                type = src.type,
                                extend = src.extend
                            };
                            break;
                        }
                        case SceneEffect.Tint:
                        {
                            var src = (RenderEffectTint)effect;
                            dupEffect = new RenderEffectTint
                            {
                                black = src.black,
                                white = src.white,
                                intensity = src.intensity,
                                type = src.type,
                                extend = src.extend
                            };
                            break;
                        }
                        case SceneEffect.Tritone:
                        {
                            var src = (RenderEffectTritone)effect;
                            dupEffect = new RenderEffectTritone
                            {
                                shadow = src.shadow,
                                midtone = src.midtone,
                                highlight = src.highlight,
                                blender = src.blender,
                                type = src.type,
                                extend = src.extend
                            };
                            break;
                        }
                    }
                    if (dupEffect != null)
                    {
                        dupEffect.rd = null;
                        dupEffect.valid = false;
                        scene.effects.Add(dupEffect);
                    }
                }
            }

            if (fixedScene) scene.SetSize(fsize);

            return scene;
        }

        /// <summary>Reset (dispose and clear) all effects. Mirrors C++ SceneImpl::resetEffects().</summary>
        internal Result ResetEffects(bool damage = true)
        {
            if (effects != null)
            {
                foreach (var effect in effects)
                {
                    if (pImpl.renderer != null) pImpl.renderer.Dispose(effect);
                }
                effects = null;
                if (damage) pImpl.Damage(vport2);
            }
            return Result.Success;
        }

        internal Iterator GetSceneIterator() => new SceneIterator(paints);

        // --- Virtual dispatch overrides ---
        internal override Paint DuplicatePaintVirt(Paint? ret) => DuplicateScene(ret);
        internal override Iterator? GetIteratorVirt() => GetSceneIterator();
        internal override bool PaintSkipVirt(RenderUpdateFlag flag) => PaintSkip(flag);
        internal override bool PaintUpdateVirt(RenderMethod renderer, in Matrix transform, ref ValueList<object?> clips, byte opacity, RenderUpdateFlag flag, bool clipper) => PaintUpdate(renderer, transform, ref clips, opacity, flag, clipper);
        internal override bool PaintRenderVirt(RenderMethod renderer, CompositionFlag flag) => PaintRender(renderer, flag);
        internal override RenderRegion PaintBoundsVirt() => PaintBounds();
        internal override bool GeometricBoundsVirt(Span<Point> pt4, in Matrix m, bool obb) => GeometricBounds(pt4, m, obb);
        internal override bool IntersectsVirt(in RenderRegion region) => Intersects(region);
    }
}
