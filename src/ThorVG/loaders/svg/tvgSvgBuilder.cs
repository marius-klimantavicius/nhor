// Ported from ThorVG/src/loaders/svg/tvgSvgBuilder.h and tvgSvgBuilder.cpp
// SVG DOM to ThorVG scene conversion.

using System;
using System.Collections.Generic;

namespace ThorVG
{
    /************************************************************************/
    /* ImageMimeTypeEncoding                                                */
    /************************************************************************/

    [Flags]
    internal enum ImageMimeTypeEncoding
    {
        Base64 = 0x1,
        Utf8 = 0x2
    }

    internal readonly struct ImageMimeType
    {
        public readonly string name;
        public readonly ImageMimeTypeEncoding encoding;

        public ImageMimeType(string name, ImageMimeTypeEncoding encoding)
        {
            this.name = name;
            this.encoding = encoding;
        }
    }

    /************************************************************************/
    /* SvgBuilder                                                           */
    /************************************************************************/

    public static class SvgBuilder
    {
        private static readonly ImageMimeType[] _imageMimeTypes = new ImageMimeType[]
        {
            new ImageMimeType("jpeg", ImageMimeTypeEncoding.Base64),
            new ImageMimeType("png", ImageMimeTypeEncoding.Base64),
            new ImageMimeType("webp", ImageMimeTypeEncoding.Base64),
            new ImageMimeType("svg+xml", ImageMimeTypeEncoding.Base64 | ImageMimeTypeEncoding.Utf8),
        };

        /************************************************************************/
        /* Internal Class Implementation                                        */
        /************************************************************************/

        private static bool _isGroupType(SvgNodeType type)
        {
            if (type == SvgNodeType.Doc || type == SvgNodeType.G || type == SvgNodeType.Use ||
                type == SvgNodeType.ClipPath || type == SvgNodeType.Symbol || type == SvgNodeType.Filter) return true;
            return false;
        }

        //According to: https://www.w3.org/TR/SVG11/coords.html#ObjectBoundingBoxUnits (the last paragraph)
        //a stroke width should be ignored for bounding box calculations
        private static unsafe Box _bounds(Paint paint)
        {
            if (paint.Bounds(out var x, out var y, out var w, out var h) == Result.Success)
                return new Box(x, y, w, h);
            return new Box(0, 0, 0, 0);
        }

        private static Box _objectBoundingBox(in Box ratio, in Box bounds)
        {
            return new Box(TvgMath.MultiplyAdd(ratio.x, bounds.w, bounds.x), TvgMath.MultiplyAdd(ratio.y, bounds.h, bounds.y),
                           ratio.w * bounds.w, ratio.h * bounds.h);
        }

        private static bool _validBox(in Box box) => box.w > 0.0f && box.h > 0.0f;

        private static void _transformMultiply(in Matrix mBBox, ref Matrix gradTransf)
        {
            gradTransf.e13 = TvgMath.MultiplyAdd(gradTransf.e13, mBBox.e11, mBBox.e13);
            gradTransf.e12 *= mBBox.e11;
            gradTransf.e11 *= mBBox.e11;

            gradTransf.e23 = TvgMath.MultiplyAdd(gradTransf.e23, mBBox.e22, mBBox.e23);
            gradTransf.e22 *= mBBox.e22;
            gradTransf.e21 *= mBBox.e22;
        }

        private static LinearGradient _applyLinearGradientProperty(SvgStyleGradient g, Box vBox, Box viewport, int opacity)
        {
            var fillGrad = LinearGradient.Gen();
            var isTransform = (g.transform != null);
            ref var finalTransform = ref fillGrad.GetTransform();
            if (isTransform) finalTransform = g.transform!.Value;

            if (g.userSpace)
            {
                fillGrad.Linear(g.linear!.x1 * (g.linear.isX1Percentage ? vBox.w : viewport.w),
                                g.linear.y1 * (g.linear.isY1Percentage ? vBox.h : viewport.h),
                                g.linear.x2 * (g.linear.isX2Percentage ? vBox.w : viewport.w),
                                g.linear.y2 * (g.linear.isY2Percentage ? vBox.h : viewport.h));
            }
            else
            {
                var m = new Matrix(vBox.w, 0, vBox.x, 0, vBox.h, vBox.y, 0, 0, 1);
                if (isTransform) _transformMultiply(m, ref finalTransform);
                else finalTransform = m;
                fillGrad.Linear(g.linear!.x1, g.linear.y1, g.linear.x2, g.linear.y2);
            }
            fillGrad.SetSpread(g.spread);

            //Update the stops
            if (g.stops.Count == 0) return fillGrad;

            var stops = new Fill.ColorStop[g.stops.Count];
            var prevOffset = 0.0f;
            for (int i = 0; i < g.stops.Count; ++i)
            {
                var colorStop = g.stops[i];
                //Use premultiplied color
                stops[i].r = colorStop.r;
                stops[i].g = colorStop.g;
                stops[i].b = colorStop.b;
                stops[i].a = (byte)((colorStop.a * opacity) / 255);
                stops[i].offset = colorStop.offset;
                //check the offset corner cases - refer to: https://svgwg.org/svg2-draft/pservers.html#StopNotes
                if (colorStop.offset < prevOffset) stops[i].offset = prevOffset;
                else if (colorStop.offset > 1) stops[i].offset = 1;
                prevOffset = stops[i].offset;
            }
            fillGrad.SetColorStops(stops, (uint)g.stops.Count);
            return fillGrad;
        }

        private static RadialGradient _applyRadialGradientProperty(SvgStyleGradient g, Box vBox, Box viewport, int opacity)
        {
            var fillGrad = RadialGradient.Gen();
            var isTransform = (g.transform != null);
            ref var finalTransform = ref fillGrad.GetTransform();
            if (isTransform) finalTransform = g.transform!.Value;

            if (g.userSpace)
            {
                //The radius scaling is done according to the Units section:
                //https://www.w3.org/TR/2015/WD-SVG2-20150915/coords.html
                var diag = MathF.Sqrt(TvgMath.MultiplyAdd(vBox.w, vBox.w, vBox.h * vBox.h)) / MathF.Sqrt(2.0f);
                var viewportDiag = MathF.Sqrt(TvgMath.MultiplyAdd(viewport.w, viewport.w, viewport.h * viewport.h)) / MathF.Sqrt(2.0f);
                fillGrad.Radial(g.radial!.cx * (g.radial.isCxPercentage ? vBox.w : viewport.w),
                                g.radial.cy * (g.radial.isCyPercentage ? vBox.h : viewport.h),
                                g.radial.r * (g.radial.isRPercentage ? diag : viewportDiag),
                                g.radial.fx * (g.radial.isFxPercentage ? vBox.w : viewport.w),
                                g.radial.fy * (g.radial.isFyPercentage ? vBox.h : viewport.h),
                                g.radial.fr * (g.radial.isFrPercentage ? diag : viewportDiag));
            }
            else
            {
                var m = new Matrix(vBox.w, 0, vBox.x, 0, vBox.h, vBox.y, 0, 0, 1);
                if (isTransform) _transformMultiply(m, ref finalTransform);
                else finalTransform = m;
                fillGrad.Radial(g.radial!.cx, g.radial.cy, g.radial.r, g.radial.fx, g.radial.fy, g.radial.fr);
            }
            fillGrad.SetSpread(g.spread);

            //Update the stops
            if (g.stops.Count == 0) return fillGrad;

            var stops = new Fill.ColorStop[g.stops.Count];
            var prevOffset = 0.0f;
            for (int i = 0; i < g.stops.Count; ++i)
            {
                var colorStop = g.stops[i];
                //Use premultiplied color
                stops[i].r = colorStop.r;
                stops[i].g = colorStop.g;
                stops[i].b = colorStop.b;
                stops[i].a = (byte)((colorStop.a * opacity) / 255);
                stops[i].offset = colorStop.offset;
                //check the offset corner cases - refer to: https://svgwg.org/svg2-draft/pservers.html#StopNotes
                if (colorStop.offset < prevOffset) stops[i].offset = prevOffset;
                else if (colorStop.offset > 1) stops[i].offset = 1;
                prevOffset = stops[i].offset;
            }
            fillGrad.SetColorStops(stops, (uint)g.stops.Count);
            return fillGrad;
        }

        private static void _appendRect(Shape shape, float x, float y, float w, float h, float rx, float ry)
        {
            var halfW = w * 0.5f;
            var halfH = h * 0.5f;

            //clamping cornerRadius by minimum size
            if (rx > halfW) rx = halfW;
            if (ry > halfH) ry = halfH;

            if (rx == 0 && ry == 0)
            {
                shape.rs.path.cmds.Grow(5);
                shape.rs.path.pts.Grow(4);
                shape.MoveTo(x, y);
                shape.LineTo(x + w, y);
                shape.LineTo(x + w, y + h);
                shape.LineTo(x, y + h);
                shape.Close();
            }
            else
            {
                var hrx = rx * MathConstants.PATH_KAPPA;
                var hry = ry * MathConstants.PATH_KAPPA;

                shape.rs.path.cmds.Grow(10);
                shape.rs.path.pts.Grow(17);
                shape.MoveTo(x + rx, y);
                shape.LineTo(x + w - rx, y);
                shape.CubicTo(x + w - rx + hrx, y, x + w, y + ry - hry, x + w, y + ry);
                shape.LineTo(x + w, y + h - ry);
                shape.CubicTo(x + w, y + h - ry + hry, x + w - rx + hrx, y + h, x + w - rx, y + h);
                shape.LineTo(x + rx, y + h);
                shape.CubicTo(x + rx - hrx, y + h, x, y + h - ry + hry, x, y + h - ry);
                shape.LineTo(x, y + ry);
                shape.CubicTo(x, y + ry - hry, x + rx - hrx, y, x + rx, y);
                shape.Close();
            }
        }

        private static void _appendCircle(Shape shape, float cx, float cy, float rx, float ry)
        {
            if (rx < 0) rx = ry;
            else if (ry < 0) ry = rx;
            if (rx <= 0 || ry <= 0) return;
            var rxKappa = rx * MathConstants.PATH_KAPPA;
            var ryKappa = ry * MathConstants.PATH_KAPPA;

            shape.rs.path.cmds.Grow(6);
            shape.rs.path.pts.Grow(13);
            shape.MoveTo(cx + rx, cy);
            shape.CubicTo(cx + rx, cy + ryKappa, cx + rxKappa, cy + ry, cx, cy + ry);
            shape.CubicTo(cx - rxKappa, cy + ry, cx - rx, cy + ryKappa, cx - rx, cy);
            shape.CubicTo(cx - rx, cy - ryKappa, cx - rxKappa, cy - ry, cx, cy - ry);
            shape.CubicTo(cx + rxKappa, cy - ry, cx + rx, cy - ryKappa, cx + rx, cy);
            shape.Close();
        }

        private static bool _appendClipChild(SvgParserContext ctx, SvgNode node, Shape shape, in Box vBox, string svgPath, out Paint? clipped)
        {
            Matrix? transform = null;

            //The SVG standard allows only for 'use' nodes that point directly to a basic shape.
            if (node.type == SvgNodeType.Use)
            {
                if (node.child.Count != 1)
                {
                    clipped = null;
                    return false;
                }
                var child = node.child[0];
                var finalTransform = TvgMath.Identity();
                if (node.transform != null) finalTransform = node.transform.Value;
                if (node.use.x != 0.0f || node.use.y != 0.0f)
                {
                    finalTransform = TvgMath.Multiply(finalTransform,
                        new Matrix(1, 0, node.use.x, 0, 1, node.use.y, 0, 0, 1));
                }
                if (child.transform != null)
                {
                    finalTransform = TvgMath.Multiply(finalTransform, child.transform.Value);
                }

                transform = TvgMath.IsIdentity(finalTransform) ? null : finalTransform;
                node = child;
            }

            if (!_appendClipShape(ctx, node, shape, vBox, svgPath, transform))
            {
                clipped = null;
                return false;
            }

            // Apply Clip Chaining
            var clipNode = node.style!.clipPath.node;
            if (clipNode != null)
            {
                if (node.style.clipPath.applying)
                {
                    clipped = null;
                    return false;
                }
                return _applyClip(ctx, shape, shape, node, clipNode, vBox, svgPath, out clipped);
            }

            clipped = shape;
            return true;
        }

        private static Matrix _useTransform(SvgNode node)
        {
            var m = node.transform ?? TvgMath.Identity();
            if (node.use.x != 0.0f || node.use.y != 0.0f)
            {
                m = TvgMath.Multiply(m, new Matrix(1, 0, node.use.x, 0, 1, node.use.y, 0, 0, 1));
            }
            return m;
        }

        private static Matrix _compositionTransform(Paint content, SvgNode node, SvgNode compNode, SvgNodeType type)
        {
            var m = TvgMath.Identity();
            var userSpace = type == SvgNodeType.Mask ? compNode.maskNode.maskContentUserSpace : compNode.clip.userSpace;
            //The initial mask transformation ignored according to the SVG standard.
            if (node.type == SvgNodeType.Use)
            {
                if (type != SvgNodeType.Mask || !userSpace) m = _useTransform(node);
            }
            else if (node.transform != null && type != SvgNodeType.Mask)
            {
                m = node.transform.Value;
            }
            if (compNode.transform != null)
            {
                m = TvgMath.Multiply(m, compNode.transform.Value);
            }
            if (!userSpace)
            {
                Box bbox;
                if (node.type == SvgNodeType.Use)
                {
                    var useTransform = _useTransform(node);
                    var prevTransform = content.Transform();
                    if (TvgMath.Inverse(useTransform, out var inv))
                    {
                        content.Transform(TvgMath.Multiply(inv, prevTransform));
                    }
                    bbox = _bounds(content);
                    content.Transform(prevTransform);
                }
                else
                {
                    bbox = _bounds(content);
                }
                m = TvgMath.Multiply(m, new Matrix(bbox.w, 0, bbox.x, 0, bbox.h, bbox.y, 0, 0, 1));
            }
            return m;
        }

        private static bool _clipperUnion(SvgParserContext ctx, Paint content, SvgNode node, SvgNode clipNode, in Box vBox, string svgPath, out Paint? region)
        {
            node.style!.clipPath.applying = true;

            Paint? unionRegion = null;
            Scene? scene = null;
            foreach (var p in clipNode.child)
            {
                var child = Shape.Gen();
                if (!_appendClipChild(ctx, p, child, vBox, svgPath, out var clipped))
                {
                    Paint.Rel(child);
                    continue;
                }
                child.SetFill(255, 255, 255, 255);
                if (unionRegion == null)
                {
                    unionRegion = clipped;
                }
                else
                {
                    if (scene == null)
                    {
                        scene = Scene.Gen();
                        scene.Add(unionRegion);
                        unionRegion = scene;
                    }
                    scene.Add(clipped!);
                }
            }

            node.style.clipPath.applying = false;

            if (unionRegion == null)
            {
                region = null;
                return false;
            }

            unionRegion.Transform(_compositionTransform(content, node, clipNode, SvgNodeType.ClipPath));
            region = unionRegion;
            return true;
        }

        private static bool _applyClip(SvgParserContext ctx, Paint paint, Paint content, SvgNode node, SvgNode clipNode, in Box vBox, string svgPath, out Paint? result)
        {
            if (!_clipperUnion(ctx, content, node, clipNode, vBox, svgPath, out var region))
            {
                result = null;
                return false;
            }

            paint.SetMask(region, MaskMethod.Alpha);

            result = paint;
            var innerClipNode = clipNode.style!.clipPath.node;
            if (innerClipNode != null && !innerClipNode.style!.clipPath.applying)
            {
                innerClipNode.style.clipPath.applying = true;
                var scene = Scene.Gen();
                scene.Add(paint);
                if (!_applyClip(ctx, scene, content, node, innerClipNode, vBox, svgPath, out result))
                {
                    scene.Opacity(0);
                    result = scene;
                }
                innerClipNode.style.clipPath.applying = false;
            }
            return true;
        }

        private static Scene _applyMask(SvgParserContext ctx, Paint content, Scene target, SvgNode node, SvgNode maskNode, in Box vBox, string svgPath, bool wrap)
        {
            node.style!.mask.applying = true;

            var result = target;
            var mask = _sceneBuildHelper(ctx, maskNode, vBox, svgPath, true, 0);
            if (mask != null)
            {
                var maskData = maskNode.maskNode;
                var nodeTransform = node.type == SvgNodeType.Use
                    ? _useTransform(node)
                    : node.transform ?? TvgMath.Identity();
                if (!maskData.maskContentUserSpace)
                {
                    mask.Transform(_compositionTransform(content, node, maskNode, SvgNodeType.Mask));
                }
                else if (!TvgMath.IsIdentity(nodeTransform))
                {
                    mask.Transform(nodeTransform);
                }

                var bbox = _bounds(content);
                var clipper = Shape.Gen();
                if (maskData.userSpace)
                {
                    clipper.AppendRect(maskData.box.x, maskData.box.y, maskData.box.w, maskData.box.h);
                    if (!TvgMath.IsIdentity(nodeTransform)) clipper.Transform(nodeTransform);
                }
                else
                {
                    var box = _objectBoundingBox(maskData.box, bbox);
                    clipper.AppendRect(box.x, box.y, box.w, box.h);
                }
                mask.Clip(clipper);

                if (wrap)
                {
                    var scene = Scene.Gen();
                    scene.Add(target);
                    result = scene;
                }
                result.SetMask(mask, maskData.type == SvgMaskType.Luminance ? MaskMethod.Luma : MaskMethod.Alpha);
            }

            node.style.mask.applying = false;
            return result;
        }

        private static Paint? _applyBlend(Paint? paint, SvgNode node)
        {
            if (paint != null && (node.style!.flags & SvgStyleFlags.BlendMode) != 0)
            {
                paint.SetBlend(node.style.blendMode);
            }
            return paint;
        }

        private static Paint? _applyComposition(SvgParserContext ctx, Paint? paint, SvgNode node, in Box vBox, string svgPath)
        {
            if (paint == null) return null;

            if (node.style!.clipPath.applying || node.style.mask.applying)
            {
                //TVGLOG("SVG", "Multiple composition tried! Check out circular dependency?");
                return paint;
            }

            var clipNode = node.style.clipPath.node;
            var maskNode = node.style.mask.node;

            if (clipNode == null && maskNode == null) return paint;
            if ((clipNode != null && clipNode.child.Count == 0) || (maskNode != null && maskNode.child.Count == 0))
            {
                Paint.Rel(paint);
                return null;
            }

            var scene = Scene.Gen();
            scene.Add(paint);

            if (clipNode != null)
            {
                if (!_applyClip(ctx, scene, paint, node, clipNode, vBox, svgPath, out var clipped))
                {
                    Paint.Rel(scene);
                    return null;
                }
                scene = (Scene)clipped!;
            }

            if (maskNode != null)
            {
                scene = _applyMask(ctx, paint, scene, node, maskNode, vBox, svgPath, clipNode != null);
            }

            return scene;
        }

        private static Paint? _applyFilter(SvgParserContext ctx, Paint paint, SvgNode node, in Box vBox, string svgPath)
        {
            var filterNode = node.style!.filter.node;
            if (filterNode == null || filterNode.child.Count == 0) return paint;
            var filter = filterNode.filter;

            var scene = Scene.Gen();

            var bbox = _bounds(paint);
            var clipBox = filter.filterUserSpace
                ? filter.box
                : _objectBoundingBox(filter.box, bbox);
            var primitiveUserSpace = filter.primitiveUserSpace;
            var sx = paint.Transform().e11;
            var sy = paint.Transform().e22;

            for (int i = 0; i < filterNode.child.Count; ++i)
            {
                var child = filterNode.child[i];
                if (child.type == SvgNodeType.GaussianBlur)
                {
                    var gauss = child.gaussianBlur;

                    var direction = gauss.stdDevX > 0.0f ? (gauss.stdDevY > 0.0f ? 0 : 1) : (gauss.stdDevY > 0.0f ? 2 : -1);
                    if (direction == -1) continue;

                    var stdDevX = gauss.stdDevX;
                    var stdDevY = gauss.stdDevY;
                    if (gauss.hasBox)
                    {
                        var gaussBox = gauss.box;
                        var isPercent = gauss.isPercentage;
                        if (primitiveUserSpace)
                        {
                            if (isPercent[0]) gaussBox.x *= ctx.svgParse!.global.w;
                            if (isPercent[1]) gaussBox.y *= ctx.svgParse!.global.h;
                            if (isPercent[2]) gaussBox.w *= ctx.svgParse!.global.w;
                            if (isPercent[3]) gaussBox.h *= ctx.svgParse!.global.h;
                        }
                        else
                        {
                            stdDevX *= bbox.w;
                            stdDevY *= bbox.h;
                            if (isPercent[0]) gaussBox.x = TvgMath.MultiplyAdd(gauss.box.x, bbox.w, bbox.x);
                            if (isPercent[1]) gaussBox.y = TvgMath.MultiplyAdd(gauss.box.y, bbox.h, bbox.y);
                            if (isPercent[2]) gaussBox.w *= bbox.w;
                            if (isPercent[3]) gaussBox.h *= bbox.h;
                        }
                        clipBox.Intersect(gaussBox);
                    }
                    else if (!primitiveUserSpace)
                    {
                        stdDevX *= bbox.w;
                        stdDevY *= bbox.h;
                    }
                    scene.AddEffect(SceneEffect.GaussianBlur,
                        (double)(1.25f * (direction == 2 ? stdDevY * sy : stdDevX * sx)),
                        direction, gauss.edgeModeWrap ? 1 : 0, 55);
                }
            }

            scene.Add(paint);

            var clip = Shape.Gen();
            clip.AppendRect(clipBox.x, clipBox.y, clipBox.w, clipBox.h);
            scene.Clip(clip);

            return scene;
        }

        private static Paint? _applyProperty(SvgParserContext ctx, SvgNode node, Shape vg, in Box vBox, string svgPath, bool clip)
        {
            var style = node.style!;

            //Clip transformation is applied directly to the path in the _appendClipShape function
            if (node.type == SvgNodeType.Doc || !node.style!.display) return vg;

            //If fill property is nullptr then do nothing
            if (style.fill.paint.none)
            {
                //Do nothing
            }
            else if (style.fill.paint.gradient != null)
            {
                var bBox = style.fill.paint.gradient.userSpace ? vBox : _bounds(vg);
                if (style.fill.paint.gradient.type == SvgGradientType.Linear)
                {
                    vg.SetFill(_applyLinearGradientProperty(style.fill.paint.gradient, bBox, ctx.svgParse!.global, style.fill.opacity));
                }
                else if (style.fill.paint.gradient.type == SvgGradientType.Radial)
                {
                    vg.SetFill(_applyRadialGradientProperty(style.fill.paint.gradient, bBox, ctx.svgParse!.global, style.fill.opacity));
                }
            }
            else if (style.fill.paint.pattern != null)
            {
                var patternPaint = _applyPatternProperty(ctx, vg, node, style.fill.paint.pattern, vBox, svgPath);
                if (patternPaint != null)
                {
                    vg.SetFillRule(style.fill.fillRule);
                    vg.Order(!style.paintOrder);
                    _applyStroke(style, vg, vBox, ctx.svgParse!.global);
                    var patternScene = Scene.Gen();
                    patternScene.Add(patternPaint);
                    patternScene.Add(vg);
                    patternScene.Opacity((byte)style.opacity);
                    if (node.transform != null && !clip) patternScene.Transform(node.transform.Value);
                    Paint? patternResult = _applyFilter(ctx, patternScene, node, vBox, svgPath);
                    patternResult = _applyComposition(ctx, patternResult, node, vBox, svgPath);
                    return _applyBlend(patternResult, node);
                }
            }
            else if (style.fill.paint.url != null)
            {
                //TVGLOG("SVG", "The fill's url not supported.");
            }
            else if (style.fill.paint.curColor)
            {
                //Apply the current style color
                vg.SetFill(style.color.r, style.color.g, style.color.b, (byte)RenderHelper.Multiply((byte)style.fill.opacity, style.color.a));
            }
            else
            {
                //Apply the fill color
                vg.SetFill(style.fill.paint.color.r, style.fill.paint.color.g, style.fill.paint.color.b, (byte)RenderHelper.Multiply((byte)style.fill.opacity, style.fill.paint.color.a));
            }

            vg.SetFillRule(style.fill.fillRule);
            vg.Order(!style.paintOrder);
            vg.Opacity((byte)style.opacity);

            if (node.type == SvgNodeType.G || node.type == SvgNodeType.Use)
            {
                if ((style.flags & SvgStyleFlags.BlendMode) != 0) vg.SetBlend(style.blendMode);
                return vg;
            }

            _applyStroke(style, vg, vBox, ctx.svgParse!.global);

            //apply transform after the local space shape bbox for gradient acquisition
            if (node.transform != null && !clip) vg.Transform(node.transform.Value);

            var p = _applyFilter(ctx, vg, node, vBox, svgPath);
            p = _applyComposition(ctx, p, node, vBox, svgPath);
            return _applyBlend(p, node);
        }

        private static void _applyStroke(SvgStyleProperty style, Shape vg, in Box vBox, in Box viewport)
        {
            vg.StrokeWidth(style.stroke.width);
            vg.StrokeCap(style.stroke.cap);
            vg.StrokeJoin(style.stroke.join);
            vg.StrokeMiterlimit(style.stroke.miterlimit);
            if (style.stroke.dash.array.Count > 0)
            {
                vg.StrokeDash(style.stroke.dash.array.ToArray(), (uint)style.stroke.dash.array.Count, style.stroke.dash.offset);
            }
            else
            {
                vg.StrokeDash(null, 0, style.stroke.dash.offset);
            }

            //If stroke property is nullptr then do nothing
            if (style.stroke.paint.none)
            {
                vg.StrokeWidth(0.0f);
            }
            else if (style.stroke.paint.gradient != null)
            {
                var bBox = style.stroke.paint.gradient.userSpace ? vBox : _bounds(vg);
                if (style.stroke.paint.gradient.type == SvgGradientType.Linear)
                {
                    vg.StrokeFill(_applyLinearGradientProperty(style.stroke.paint.gradient, bBox, viewport, style.stroke.opacity));
                }
                else if (style.stroke.paint.gradient.type == SvgGradientType.Radial)
                {
                    vg.StrokeFill(_applyRadialGradientProperty(style.stroke.paint.gradient, bBox, viewport, style.stroke.opacity));
                }
            }
            else if (style.stroke.paint.url != null)
            {
                //TODO: Apply the color pointed by url
                //TVGLOG("SVG", "The stroke's url not supported.");
            }
            else if (style.stroke.paint.curColor)
            {
                //Apply the current style color
                vg.StrokeFill(style.color.r, style.color.g, style.color.b, (byte)RenderHelper.Multiply((byte)style.stroke.opacity, style.color.a));
            }
            else
            {
                //Apply the stroke color
                vg.StrokeFill(style.stroke.paint.color.r, style.stroke.paint.color.g, style.stroke.paint.color.b, (byte)RenderHelper.Multiply((byte)style.stroke.opacity, style.stroke.paint.color.a));
            }

        }

        private static bool _recognizeShape(SvgNode node, Shape shape)
        {
            switch (node.type)
            {
                case SvgNodeType.Path:
                {
                    if (node.path.path != null)
                    {
                        if (!SvgPath.ToShape(node.path.path, shape.rs.path))
                        {
                            //TVGERR("SVG", "Invalid path information.");
                            return false;
                        }
                    }
                    break;
                }
                case SvgNodeType.Ellipse:
                {
                    _appendCircle(shape, node.ellipse.cx, node.ellipse.cy, node.ellipse.rx, node.ellipse.ry);
                    break;
                }
                case SvgNodeType.Polygon:
                {
                    if (node.polygon.pts.Count < 2) break;
                    var pts = node.polygon.pts;
                    shape.MoveTo(pts[0], pts[1]);
                    for (int i = 2; i + 1 < pts.Count; i += 2)
                    {
                        shape.LineTo(pts[i], pts[i + 1]);
                    }
                    shape.Close();
                    break;
                }
                case SvgNodeType.Polyline:
                {
                    if (node.polyline.pts.Count < 2) break;
                    var pts = node.polyline.pts;
                    shape.MoveTo(pts[0], pts[1]);
                    for (int i = 2; i + 1 < pts.Count; i += 2)
                    {
                        shape.LineTo(pts[i], pts[i + 1]);
                    }
                    break;
                }
                case SvgNodeType.Circle:
                {
                    _appendCircle(shape, node.circle.cx, node.circle.cy, node.circle.r, node.circle.r);
                    break;
                }
                case SvgNodeType.Rect:
                {
                    _appendRect(shape, node.rect.x, node.rect.y, node.rect.w, node.rect.h, node.rect.rx, node.rect.ry);
                    break;
                }
                case SvgNodeType.Line:
                {
                    shape.MoveTo(node.line.x1, node.line.y1);
                    shape.LineTo(node.line.x2, node.line.y2);
                    break;
                }
                default:
                {
                    return false;
                }
            }
            return true;
        }

        private static Paint? _shapeBuildHelper(SvgParserContext ctx, SvgNode node, in Box vBox, string svgPath)
        {
            var shape = Shape.Gen();
            if (!_recognizeShape(node, shape)) { Paint.Rel(shape); return null; }
            return _applyProperty(ctx, node, shape, vBox, svgPath, false);
        }

        private static unsafe bool _appendClipShape(SvgParserContext ctx, SvgNode node, Shape shape, in Box vBox, string svgPath, Matrix? transform)
        {
            uint currentPtsCnt = shape.rs.path.pts.count;

            if (!_recognizeShape(node, shape)) return false;

            //The 'transform' matrix has higher priority than the node->transform, since it already contains it
            Matrix? m = transform ?? (node.transform != null ? node.transform : null);

            if (m != null)
            {
                var mVal = m.Value;
                var ptsCnt = shape.rs.path.pts.count;
                var pts = shape.rs.path.pts.data;
                var p = pts + currentPtsCnt;
                while (currentPtsCnt < ptsCnt)
                {
                    TvgMath.TransformInPlace(ref *p, mVal);
                    ++p;
                    ++currentPtsCnt;
                }
            }

            return true;
        }

        private static bool _isValidImageMimeTypeAndEncoding(string href, ref int pos, out string? mimetype, out ImageMimeTypeEncoding encoding)
        {
            mimetype = null;
            encoding = default;

            const string imagePrefix = "image/";
            if (pos + imagePrefix.Length > href.Length ||
                !href.AsSpan(pos, imagePrefix.Length).Equals(imagePrefix, StringComparison.OrdinalIgnoreCase)) return false;
            pos += imagePrefix.Length;

            //RFC2397 data:[<mediatype>][;base64],<data>
            for (int i = 0; i < _imageMimeTypes.Length; i++)
            {
                var name = _imageMimeTypes[i].name;
                if (pos + name.Length > href.Length ||
                    !href.AsSpan(pos, name.Length).Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
                pos += name.Length;
                mimetype = name;

                while (pos < href.Length && href[pos] != ',')
                {
                    while (pos < href.Length && href[pos] != ';') ++pos;
                    if (pos >= href.Length) return false;
                    ++pos;

                    if ((_imageMimeTypes[i].encoding & ImageMimeTypeEncoding.Base64) != 0)
                    {
                        const string base64Prefix = "base64,";
                        if (pos + base64Prefix.Length <= href.Length &&
                            href.AsSpan(pos, base64Prefix.Length).Equals(base64Prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            pos += base64Prefix.Length;
                            encoding = ImageMimeTypeEncoding.Base64;
                            return true;
                        }
                    }
                    if ((_imageMimeTypes[i].encoding & ImageMimeTypeEncoding.Utf8) != 0)
                    {
                        const string utf8Prefix = "utf8,";
                        if (pos + utf8Prefix.Length <= href.Length &&
                            href.AsSpan(pos, utf8Prefix.Length).Equals(utf8Prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            pos += utf8Prefix.Length;
                            encoding = ImageMimeTypeEncoding.Utf8;
                            return true;
                        }
                    }
                }
                //no encoding defined
                if (pos < href.Length && href[pos] == ',' && (_imageMimeTypes[i].encoding & ImageMimeTypeEncoding.Utf8) != 0)
                {
                    ++pos;
                    encoding = ImageMimeTypeEncoding.Utf8;
                    return true; //allow no encoding defined if utf8 expected
                }
                return false;
            }
            return false;
        }

        private static Paint? _imageBuildHelper(SvgParserContext ctx, SvgNode node, in Box vBox, string svgPath)
        {
            if (string.IsNullOrEmpty(node.image.href)) return null;

            var picture = Picture.Gen();

            var href = node.image.href!;
            int hrefPos = 0;

            if (href.StartsWith("data:", StringComparison.Ordinal))
            {
                hrefPos = "data:".Length;
                if (!_isValidImageMimeTypeAndEncoding(href, ref hrefPos, out var mimeType, out var encoding))
                    { Paint.Rel(picture); return null; }

                var dataStr = href.Substring(hrefPos);
                if (encoding == ImageMimeTypeEncoding.Base64)
                {
                    var decoded = TvgCompressor.B64Decode(dataStr);
                    if (picture.Load(decoded, (uint)decoded.Length, mimeType) != Result.Success)
                    {
                        { Paint.Rel(picture); return null; }
                    }
                    ctx.images.Add(dataStr);
                }
                else
                {
                    var decodedBytes = SvgUtil.URLDecodeBytes(dataStr);
                    if (picture.Load(decodedBytes, (uint)decodedBytes.Length, mimeType) != Result.Success)
                    {
                        { Paint.Rel(picture); return null; }
                    }
                    ctx.images.Add(dataStr);
                }
            }
            else
            {
                var hrefStr = href;
                if (hrefStr.StartsWith("file://", StringComparison.Ordinal))
                    hrefStr = hrefStr.Substring("file://".Length);
                //TODO: protect against recursive svg image loading
                //Temporarily disable embedded svg:
                var dotIdx = hrefStr.LastIndexOf('.');
                if (dotIdx >= 0 && string.Equals(hrefStr.Substring(dotIdx), ".svg", StringComparison.Ordinal))
                {
                    //TVGLOG("SVG", "Embedded svg file is disabled.");
                    { Paint.Rel(picture); return null; }
                }
                var imagePath = hrefStr;
                if (!hrefStr.StartsWith("/", StringComparison.Ordinal))
                {
                    var last = svgPath.LastIndexOf('/');
                    imagePath = (last < 0 ? "" : svgPath.Substring(0, last + 1)) + imagePath;
                }
                if (picture.Load(imagePath) != Result.Success)
                {
                    { Paint.Rel(picture); return null; }
                }
            }

            float w, h;
            Matrix m;
            if (picture.GetSize(out w, out h) == Result.Success && w > 0 && h > 0)
            {
                var sx = node.image.w / w;
                var sy = node.image.h / h;
                m = new Matrix(sx, 0, node.image.x, 0, sy, node.image.y, 0, 0, 1);
            }
            else
            {
                m = TvgMath.Identity();
            }
            if (node.transform != null) m = TvgMath.Multiply(node.transform.Value, m);
            picture.Transform(m);

            var p = _applyFilter(ctx, picture, node, vBox, svgPath);
            p = _applyComposition(ctx, p, node, vBox, svgPath);
            return _applyBlend(p, node);
        }

        private static Matrix _calculateAspectRatioMatrix(AspectRatioAlign align, AspectRatioMeetOrSlice meetOrSlice, float width, float height, in Box box)
        {
            var sx = width / box.w;
            var sy = height / box.h;
            var tvx = box.x * sx;
            var tvy = box.y * sy;

            if (align == AspectRatioAlign.None) return new Matrix(sx, 0, -tvx, 0, sy, -tvy, 0, 0, 1);

            //Scale
            if (meetOrSlice == AspectRatioMeetOrSlice.Meet)
            {
                if (sx < sy) sy = sx;
                else sx = sy;
            }
            else
            {
                if (sx < sy) sx = sy;
                else sy = sx;
            }

            //Align
            tvx = box.x * sx;
            tvy = box.y * sy;
            var tvw = box.w * sx;
            var tvh = box.h * sy;

            switch (align)
            {
                case AspectRatioAlign.XMinYMin:
                    break;
                case AspectRatioAlign.XMidYMin:
                    tvx = TvgMath.MultiplyAdd(width - tvw, -0.5f, tvx);
                    break;
                case AspectRatioAlign.XMaxYMin:
                    tvx -= width - tvw;
                    break;
                case AspectRatioAlign.XMinYMid:
                    tvy = TvgMath.MultiplyAdd(height - tvh, -0.5f, tvy);
                    break;
                case AspectRatioAlign.XMidYMid:
                    tvx = TvgMath.MultiplyAdd(width - tvw, -0.5f, tvx);
                    tvy = TvgMath.MultiplyAdd(height - tvh, -0.5f, tvy);
                    break;
                case AspectRatioAlign.XMaxYMid:
                    tvx -= width - tvw;
                    tvy = TvgMath.MultiplyAdd(height - tvh, -0.5f, tvy);
                    break;
                case AspectRatioAlign.XMinYMax:
                    tvy -= height - tvh;
                    break;
                case AspectRatioAlign.XMidYMax:
                    tvx = TvgMath.MultiplyAdd(width - tvw, -0.5f, tvx);
                    tvy -= height - tvh;
                    break;
                case AspectRatioAlign.XMaxYMax:
                    tvx -= width - tvw;
                    tvy -= height - tvh;
                    break;
                default:
                    break;
            }

            return new Matrix(sx, 0, -tvx, 0, sy, -tvy, 0, 0, 1);
        }

        private static Matrix _symbolTransform(SvgNode node, in Box vBox)
        {
            var symbolNode = node.use.symbol!;
            var symbol = symbolNode.symbol;
            var width = symbol.hasWidth ? symbol.w : vBox.w;
            if (node.use.isWidthSet) width = node.use.w;
            var height = symbol.hasHeight ? symbol.h : vBox.h;
            if (node.use.isHeightSet) height = node.use.h;
            var vw = symbol.hasViewBox ? symbol.vw : width;
            var vh = symbol.hasViewBox ? symbol.vh : height;

            var mViewBox = TvgMath.Identity();
            if ((!TvgMath.Equal(width, vw) || !TvgMath.Equal(height, vh)) && vw > 0 && vh > 0)
            {
                var box = new Box(symbol.vx, symbol.vy, vw, vh);
                mViewBox = _calculateAspectRatioMatrix(symbol.align, symbol.meetOrSlice, width, height, box);
            }
            else if (!TvgMath.Zero(symbol.vx) || !TvgMath.Zero(symbol.vy))
            {
                mViewBox = new Matrix(1, 0, -symbol.vx, 0, 1, -symbol.vy, 0, 0, 1);
            }

            // mSceneTransform = mUseTransform * mSymbolTransform * mViewBox
            var mSceneTransform = mViewBox;
            if (symbolNode.transform != null)
            {
                mSceneTransform = TvgMath.Multiply(symbolNode.transform.Value, mViewBox);
            }
            return TvgMath.Multiply(_useTransform(node), mSceneTransform);
        }

        private static Scene? _useBuildHelper(SvgParserContext ctx, SvgNode node, in Box vBox, string svgPath, int depth)
        {
            var scene = _sceneBuildHelper(ctx, node, vBox, svgPath, false, depth + 1);
            if (scene == null) return null;

            if (node.use.symbol != null)
            {
                var symbol = node.use.symbol.symbol;
                var width = (symbol.hasWidth ? symbol.w : vBox.w);
                if (node.use.isWidthSet) width = node.use.w;
                var height = (symbol.hasHeight ? symbol.h : vBox.h);
                if (node.use.isHeightSet) height = node.use.h;
                if (!node.use.symbol.symbol.overflowVisible)
                {
                    var viewBoxClip = Shape.Gen();
                    viewBoxClip.AppendRect(0, 0, width, height);

                    // mClipTransform = mUseTransform * mSymbolTransform
                    var mClipTransform = _useTransform(node);
                    if (node.use.symbol.transform != null)
                    {
                        mClipTransform = TvgMath.Multiply(mClipTransform, node.use.symbol.transform.Value);
                    }
                    viewBoxClip.Transform(mClipTransform);

                    var clippingLayer = Scene.Gen();
                    clippingLayer.Clip(viewBoxClip);
                    clippingLayer.Add(scene);
                    return clippingLayer;
                }
                return scene;
            }

            return scene;
        }

        private static void _applyTextFill(SvgStyleProperty style, Text text, SvgTextNode textNode, in Box vBox, in Box viewport)
        {
            //If fill property is nullptr then do nothing
            if (style.fill.paint.none)
            {
                //Do nothing
            }
            else if (style.fill.paint.gradient != null)
            {
                var bBox = style.fill.paint.gradient.userSpace ? vBox : _bounds(text);
                if (style.fill.paint.gradient.type == SvgGradientType.Linear)
                {
                    text.shape.SetFill(_applyLinearGradientProperty(style.fill.paint.gradient, bBox, viewport, style.fill.opacity));
                }
                else if (style.fill.paint.gradient.type == SvgGradientType.Radial)
                {
                    text.shape.SetFill(_applyRadialGradientProperty(style.fill.paint.gradient, bBox, viewport, style.fill.opacity));
                }
            }
            else if (style.fill.paint.url != null)
            {
                //TODO: Apply the color pointed by url
                //TVGLOG("SVG", "The fill's url not supported.");
            }
            else
            {
                var color = style.fill.paint.curColor ? style.color : style.fill.paint.color;
                text.SetFill(color.r, color.g, color.b);
                if (style.fontWeight >= SvgFontWeight.Weight600) text.SetOutline(textNode.fontSize * .03f, color.r, color.g, color.b);
                text.Opacity((byte)style.fill.opacity);
            }
        }

        private static string? _processText(string? text, SvgXmlSpace space)
        {
            if (text == null) return null;

            var len = text.Length;
            var processed = new char[len + 1];
            int dst = 0;

            if (space == SvgXmlSpace.Preserve)
            {
                for (int i = 0; i < text.Length; i++)
                {
                    var c = text[i];
                    if (c == '\n' || c == '\t' || c == '\r') processed[dst++] = ' ';
                    else processed[dst++] = c;
                }
                return new string(processed, 0, dst);
            }
            else
            {
                var spaceFound = false;
                int src = 0;
                // skip leading whitespace
                while (src < text.Length && SvgUtil.IsWhiteSpace(text[src])) src++;

                while (src < text.Length)
                {
                    if (SvgUtil.IsWhiteSpace(text[src]))
                    {
                        if (!spaceFound)
                        {
                            processed[dst++] = ' ';
                            spaceFound = true;
                        }
                    }
                    else
                    {
                        processed[dst++] = text[src];
                        spaceFound = false;
                    }
                    src++;
                }
                // trim trailing whitespace
                while (dst > 0 && SvgUtil.IsWhiteSpace(processed[dst - 1])) dst--;
                return new string(processed, 0, dst);
            }
        }

        private static SvgBaseline _effectiveBaseline(SvgStyleProperty style)
            => style.alignmentBaseline != SvgBaseline.Auto ? style.alignmentBaseline : style.dominantBaseline;

        private static float _applySpacing(Text text, float letterSpacing, float wordSpacing)
        {
            if (letterSpacing == 0 && wordSpacing == 0) return 1;
            var value = text.GetText(); float advance = 0; uint gaps = 0, spaces = 0;
            if (value == null) return 1;
            for (int i = 0; i < value.Length;)
            {
                bool space = value[i] == ' ';
                if (text.GetMetrics(value.Substring(i), out var metrics, out int next) != Result.Success || next <= 0) return 1;
                i += next;
                if (i < value.Length) { advance += metrics.advance; ++gaps; if (space) ++spaces; }
            }
            if (advance <= 0) return 1;
            float scale = 1 + TvgMath.MultiplyAdd(letterSpacing, gaps, wordSpacing * spaces) / advance;
            if (scale < 0) scale = 0;
            text.SetSpacing(scale, 1);
            return scale;
        }

        private static void _applyTextBaseline(Text text, SvgBaseline baseline, ref Matrix transform)
        {
            if (baseline == SvgBaseline.Auto || baseline == SvgBaseline.Alphabetic) return;
            if (text.GetMetrics(out var tm) != Result.Success) return;

            var shift = 0.0f;
            switch (baseline)
            {
                case SvgBaseline.BeforeEdge:
                    shift = tm.ascent;
                    break;
                case SvgBaseline.AfterEdge:
                    shift = tm.descent;
                    break;
                case SvgBaseline.Central:
                    shift = 0.5f * (tm.ascent + tm.descent);
                    break;
                case SvgBaseline.Middle:
                    if (text.GetMetrics("x", out var gm) == Result.Success && gm.max.y > 0.0f) shift = 0.5f * gm.max.y;
                    else shift = 0.27f * tm.ascent;
                    break;
                case SvgBaseline.Hanging:
                    shift = 0.8f * tm.ascent;
                    break;
                case SvgBaseline.Mathematical:
                    shift = 0.5f * tm.ascent;
                    break;
                default:
                    return;
            }

            TvgMath.TranslateR(ref transform, new Point(0.0f, shift));
            text.Transform(transform);
        }

        private static Text? _buildText(SvgTextNode textNode, SvgXmlSpace xmlSpace, Matrix? transform, SvgBaseline baseline)
        {
            if (textNode.text == null) return null;

            var text = Text.Gen();

            //TODO: handle def values of font and size as used in a system?
            var size = textNode.fontSize * 0.75f; //1 pt = 1/72; 1 in = 96 px; -> 72/96 = 0.75
            if (text.SetFont(textNode.fontFamily) != Result.Success)
            {
                text.SetFont(null); //fallback to any available font
            }
            text.SetFontSize(size);

            var processedText = _processText(textNode.text, xmlSpace);
            text.SetText(processedText);

            text.GetMetrics(out var metrics);
            var textTransform = transform ?? TvgMath.Identity();
            TvgMath.TranslateR(ref textTransform, new Point(textNode.x + textNode.dx, textNode.y + textNode.dy - metrics.ascent));
            text.Transform(textTransform);

            _applyTextBaseline(text, baseline, ref textTransform);

            return text;
        }

        private static void _updatePos(Text text, SvgTextNode textNode, float anchor, float spacingScale, ref Point textPos)
        {
            var advance = 0.0f;
            var value = text.GetText();
            if (value != null)
            {
                for (int i = 0; i < value.Length;)
                {
                    if (text.GetMetrics(value.Substring(i), out var metrics, out var length) != Result.Success || length <= 0) break;
                    advance += metrics.advance;
                    i += length;
                }
            }
            textPos.x = TvgMath.MultiplyAdd((1.0f - anchor) * advance, spacingScale, textNode.x + textNode.dx);
            textPos.y = textNode.y + textNode.dy;
        }

        private static bool _hasPositionedTspan(SvgNode node, int depth)
        {
            if (depth > 2192) return true;
            foreach (var child in node.child)
            {
                if (child.type != SvgNodeType.Tspan) continue;
                if (child.text.text != null) return true;
                if (_hasPositionedTspan(child, depth + 1)) return true;
            }
            return false;
        }

        private static void _buildTspanScene(SvgParserContext ctx, SvgNode node, Scene scene, in Box vBox, string svgPath, int depth, ref Point textPos)
        {
            if (depth > 2192) return;
            foreach (var child in node.child)
            {
                if (child.type != SvgNodeType.Tspan) continue;

                var textNode = new SvgTextNode
                {
                    text = child.text.text,
                    fontFamily = child.text.fontFamily,
                    x = child.text.x,
                    y = child.text.y,
                    dx = child.text.dx,
                    dy = child.text.dy,
                    fontSize = child.text.fontSize
                };

                if (textNode.text != null)
                {
                    var xmlSpace = child.xmlSpace;
                    for (var n = child.parent; n != null; n = n.parent)
                    {
                        if (textNode.fontSize <= 0.0f) textNode.fontSize = n.text.fontSize;
                        if (textNode.fontFamily == null) textNode.fontFamily = n.text.fontFamily;
                        if (xmlSpace == SvgXmlSpace.None) xmlSpace = n.xmlSpace;
                        if (n.type == SvgNodeType.Text) break;
                    }
                    if (xmlSpace == SvgXmlSpace.None) xmlSpace = SvgXmlSpace.Default;

                    if (textNode.x == float.MaxValue) textNode.x = textPos.x;
                    if (textNode.y == float.MaxValue) textNode.y = textPos.y;

                    var text = _buildText(textNode, xmlSpace, null, _effectiveBaseline(child.style!));
                    if (text != null)
                    {
                        text.SetAlign(child.style!.textAnchor, 0.0f);
                        float spacingScale = _applySpacing(text, child.style.letterSpacing, child.style.wordSpacing);
                        _updatePos(text, textNode, child.style.textAnchor, spacingScale, ref textPos);
                        _applyTextFill(child.style!, text, textNode, vBox, ctx.svgParse!.global);
                        Paint? paint = _applyFilter(ctx, text, child, vBox, svgPath);
                        paint = _applyComposition(ctx, paint, child, vBox, svgPath);
                        paint = _applyBlend(paint, child);
                        if (paint != null) scene.Add(paint);
                    }
                }
                _buildTspanScene(ctx, child, scene, vBox, svgPath, depth + 1, ref textPos);
            }
        }

        private static Paint? _textBuildHelper(SvgParserContext ctx, SvgNode node, in Box vBox, string svgPath)
        {
            var xmlSpace = node.xmlSpace;
            for (var n = node.parent; xmlSpace == SvgXmlSpace.None && n != null; n = n.parent)
                xmlSpace = n.xmlSpace;
            if (xmlSpace == SvgXmlSpace.None) xmlSpace = SvgXmlSpace.Default;

            if (!_hasPositionedTspan(node, 0))
            {
                var text = _buildText(node.text, xmlSpace, node.transform, _effectiveBaseline(node.style!));
                if (text == null) return null;
                text.SetAlign(node.style!.textAnchor, 0.0f);
                _applySpacing(text, node.style.letterSpacing, node.style.wordSpacing);
                _applyTextFill(node.style!, text, node.text, vBox, ctx.svgParse!.global);
                var p = _applyFilter(ctx, text, node, vBox, svgPath);
                p = _applyComposition(ctx, p, node, vBox, svgPath);
                return _applyBlend(p, node);
            }

            var scene = Scene.Gen();
            if (node.transform != null) scene.Transform(node.transform.Value);
            var textPos = new Point(node.text.x, node.text.y);

            if (_buildText(node.text, xmlSpace, null, _effectiveBaseline(node.style!)) is { } baseText)
            {
                baseText.SetAlign(node.style!.textAnchor, 0.0f);
                float spacingScale = _applySpacing(baseText, node.style.letterSpacing, node.style.wordSpacing);
                _updatePos(baseText, node.text, node.style.textAnchor, spacingScale, ref textPos);
                _applyTextFill(node.style!, baseText, node.text, vBox, ctx.svgParse!.global);
                scene.Add(baseText);
            }

            _buildTspanScene(ctx, node, scene, vBox, svgPath, 0, ref textPos);

            var paint = _applyFilter(ctx, scene, node, vBox, svgPath);
            paint = _applyComposition(ctx, paint, node, vBox, svgPath);
            return _applyBlend(paint, node);
        }

        private static Scene? _sceneBuildHelper(SvgParserContext ctx, SvgNode node, in Box vBox, string svgPath, bool mask, int depth)
        {
            /* Exception handling: Prevent invalid SVG data input.
               The size is the arbitrary value, we need an experimental size. */
            if (depth > 2192)
            {
                //TVGERR("SVG", "Infinite recursive call - stopped after %d calls! Svg file may be incorrectly formatted.", depth);
                return null;
            }

            if (!_isGroupType(node.type) && !mask) return null;

            var scene = Scene.Gen();
            // For a Symbol node, the viewBox transformation has to be applied first - see _symbolTransform()
            if (!mask && node.type != SvgNodeType.Symbol)
            {
                if (node.type == SvgNodeType.Use)
                {
                    scene.Transform(node.use.symbol != null ? _symbolTransform(node, vBox) : _useTransform(node));
                }
                else if (node.transform != null)
                {
                    scene.Transform(node.transform.Value);
                }
            }

            if (!node.style!.display || node.style.opacity == 0) return scene;

            foreach (var child in node.child)
            {
                if (child.type == SvgNodeType.ClipPath || child.type == SvgNodeType.Filter || child.type == SvgNodeType.Pattern) continue;
                Paint? paint = null;
                if (_isGroupType(child.type))
                {
                    if (child.type == SvgNodeType.Use)
                    {
                        paint = _useBuildHelper(ctx, child, vBox, svgPath, depth + 1);
                    }
                    else if (!(child.type == SvgNodeType.Symbol && node.type != SvgNodeType.Use))
                    {
                        paint = _sceneBuildHelper(ctx, child, vBox, svgPath, false, depth + 1);
                    }
                }
                else
                {
                    if (child.type == SvgNodeType.Image) paint = _imageBuildHelper(ctx, child, vBox, svgPath);
                    else if (child.type == SvgNodeType.Text) paint = _textBuildHelper(ctx, child, vBox, svgPath);
                    else if (child.type != SvgNodeType.Mask) paint = _shapeBuildHelper(ctx, child, vBox, svgPath);
                }
                if (paint != null)
                {
                    if (child.id != null)
                    {
                        paint.id = (uint)TvgCompressor.Djb2EncodeNative(child.id);
                        if (ctx.accessible)
                        {
                            ctx.access.Add(new AccessorEntity { id = paint.id, paint = paint, name = child.id });
                        }
                    }
                    scene.Add(paint);
                }
            }
            scene.Opacity((byte)node.style.opacity);

            var p = _applyFilter(ctx, scene, node, vBox, svgPath);
            p = _applyComposition(ctx, p, node, vBox, svgPath);
            return (Scene?)_applyBlend(p, node);
        }

        private static Paint? _buildPatternChild(SvgParserContext ctx, SvgNode child, in Box vBox, string svgPath)
        {
            if (child.type == SvgNodeType.ClipPath || child.type == SvgNodeType.Filter || child.type == SvgNodeType.Pattern) return null;
            if (_isGroupType(child.type))
            {
                if (child.type == SvgNodeType.Use) return _useBuildHelper(ctx, child, vBox, svgPath, 0);
                if (child.type != SvgNodeType.Symbol) return _sceneBuildHelper(ctx, child, vBox, svgPath, false, 0);
                return null;
            }
            if (child.type == SvgNodeType.Image) return _imageBuildHelper(ctx, child, vBox, svgPath);
            if (child.type == SvgNodeType.Text) return _textBuildHelper(ctx, child, vBox, svgPath);
            if (child.type == SvgNodeType.Mask) return null;
            return _shapeBuildHelper(ctx, child, vBox, svgPath);
        }

        private static Paint? _buildBaseTile(SvgParserContext ctx, SvgNode patternNode, in Box vBox, string svgPath)
        {
            Paint? paint = null;
            Scene? tileScene = null;
            foreach (var patternChild in patternNode.child)
            {
                var child = _buildPatternChild(ctx, patternChild, vBox, svgPath);
                if (child == null) continue;
                if (paint == null && tileScene == null)
                {
                    paint = child;
                    continue;
                }
                if (tileScene == null)
                {
                    tileScene = Scene.Gen();
                    tileScene.Add(paint!);
                    paint = null;
                }
                tileScene.Add(child);
            }
            return tileScene ?? paint;
        }

        private static bool _patternCellRect(SvgPatternNode pattern, in Box bbox, out Box cell)
        {
            cell = pattern.patternUserSpace ? pattern.box : _objectBoundingBox(pattern.box, bbox);
            return _validBox(cell);
        }

        private static Matrix _patternContentTransform(SvgPatternNode pattern, in Box bbox, in Box cell)
        {
            if (pattern.hasViewBox)
            {
                var sx = cell.w / pattern.vbox.w;
                var sy = cell.h / pattern.vbox.h;
                return new Matrix(sx, 0, -pattern.vbox.x * sx, 0, sy, -pattern.vbox.y * sy, 0, 0, 1);
            }
            if (!pattern.contentUserSpace) return new Matrix(bbox.w, 0, 0, 0, bbox.h, 0, 0, 0, 1);
            return TvgMath.Identity();
        }

        private static Box _transformBounds(in Box bounds, in Matrix matrix)
        {
            var lt = TvgMath.Transform(new Point(bounds.x, bounds.y), matrix);
            var lb = TvgMath.Transform(new Point(bounds.x, bounds.y + bounds.h), matrix);
            var rt = TvgMath.Transform(new Point(bounds.x + bounds.w, bounds.y), matrix);
            var rb = TvgMath.Transform(new Point(bounds.x + bounds.w, bounds.y + bounds.h), matrix);
            var minX = MathF.Min(MathF.Min(lt.x, lb.x), MathF.Min(rt.x, rb.x));
            var minY = MathF.Min(MathF.Min(lt.y, lb.y), MathF.Min(rt.y, rb.y));
            var maxX = MathF.Max(MathF.Max(lt.x, lb.x), MathF.Max(rt.x, rb.x));
            var maxY = MathF.Max(MathF.Max(lt.y, lb.y), MathF.Max(rt.y, rb.y));
            return new Box(minX, minY, maxX - minX, maxY - minY);
        }

        private static void _patternTileGrid(in Box cell, in Box bbox, Matrix? transform,
                                             out float startX, out float startY, out int cols, out int rows)
        {
            var box = bbox;
            if (transform.HasValue && TvgMath.Inverse(transform.Value, out var inverse)) box = _transformBounds(bbox, inverse);
            startX = TvgMath.MultiplyAdd(MathF.Floor((box.x - cell.x) / cell.w), cell.w, cell.x);
            startY = TvgMath.MultiplyAdd(MathF.Floor((box.y - cell.y) / cell.h), cell.h, cell.y);
            cols = (int)MathF.Ceiling((box.x + box.w - startX) / cell.w);
            rows = (int)MathF.Ceiling((box.y + box.h - startY) / cell.h);
        }

        private static Paint? _applyPatternProperty(SvgParserContext ctx, Shape vg, SvgNode node, SvgNode patternNode,
                                                    in Box vBox, string svgPath)
        {
            if (patternNode.child.Count == 0) return null;
            var pattern = patternNode.pattern;
            if (pattern.applying) return null;

            var bbox = _bounds(vg);
            if (!_validBox(bbox) || !_patternCellRect(pattern, bbox, out var cell)) return null;

            _patternTileGrid(cell, bbox, pattern.transform, out var startX, out var startY, out var cols, out var rows);
            var contentTransform = _patternContentTransform(pattern, bbox, cell);
            pattern.applying = true;

            var baseTile = _buildBaseTile(ctx, patternNode, vBox, svgPath);
            var tilesScene = Scene.Gen();
            if (baseTile != null)
            {
                for (int row = 0; row < rows; ++row)
                {
                    for (int col = 0; col < cols; ++col)
                    {
                        var copy = baseTile.Duplicate();
                        if (copy == null) continue;
                        var tileTransform = TvgMath.Multiply(
                            new Matrix(1, 0, TvgMath.MultiplyAdd(col, cell.w, startX), 0, 1, TvgMath.MultiplyAdd(row, cell.h, startY), 0, 0, 1),
                            contentTransform);
                        if (pattern.transform.HasValue) tileTransform = TvgMath.Multiply(pattern.transform.Value, tileTransform);
                        copy.Transform(TvgMath.Multiply(tileTransform, copy.Transform()));
                        tilesScene.Add(copy);
                    }
                }
                Paint.Rel(baseTile);
            }

            var clipper = Shape.Gen();
            if (!_recognizeShape(node, clipper))
            {
                pattern.applying = false;
                Paint.Rel(tilesScene);
                Paint.Rel(clipper);
                return null;
            }
            tilesScene.Clip(clipper);
            pattern.applying = false;
            return tilesScene;
        }

        private static void _updateInvalidViewSize(Scene scene, ref Box vBox, ref float w, ref float h, SvgViewFlag viewFlag)
        {
            var useW = (viewFlag & SvgViewFlag.Width) != 0;
            var useH = (viewFlag & SvgViewFlag.Height) != 0;
            var bbox = _bounds(scene);

            if (!useW && !useH)
            {
                vBox = bbox;
            }
            else
            {
                vBox.w = useW ? w : bbox.w;
                vBox.h = useH ? h : bbox.h;
            }

            //the size would have 1x1 or percentage values.
            if (!useW) w *= vBox.w;
            if (!useH) h *= vBox.h;
        }

        private static void _loadFonts(List<FontFace> fonts)
        {
            if (fonts.Count == 0) return;

            foreach (var p in fonts)
            {
                if (p.name == null || p.src == null) continue;
                int shift = 0;
                string? type = null;
                int limit = Math.Min(p.srcLen, 40);
                for (int i = 0; i + 10 <= limit; ++i)
                {
                    if (p.src.AsSpan(i, 10).SequenceEqual("ttf;base64"))
                    {
                        shift = i + 11;
                        type = "ttf";
                        break;
                    }
                    if (p.src.AsSpan(i, 10).SequenceEqual("otf;base64"))
                    {
                        shift = i + 11;
                        type = "otf";
                        break;
                    }
                }
                if (type == null || shift > p.srcLen) continue;
                var encodedData = p.src.Substring(shift, p.srcLen - shift);
                var decoded = TvgCompressor.B64Decode(encodedData);
                p.decoded = decoded;

                if (Text.LoadFont(p.name!, decoded, (uint)decoded.Length, type) != Result.Success)
                {
                    //TVGERR("SVG", "Error while loading the ttf font named \"%s\".", p.name);
                }
            }
        }

        /************************************************************************/
        /* External Class Implementation                                        */
        /************************************************************************/

        private static bool _hasUserSpaceGradients(SvgParserContext ctx)
        {
            foreach (var g in ctx.gradients)
            {
                if (g.userSpace) return true;
            }
            if (ctx.def != null)
            {
                foreach (var g in ctx.def.defs.gradients)
                {
                    if (g.userSpace) return true;
                }
            }
            return false;
        }

        public static Scene? SvgSceneBuild(SvgParserContext ctx, Box vBox, float w, float h, AspectRatioAlign align, AspectRatioMeetOrSlice meetOrSlice, string svgPath, SvgViewFlag viewFlag)
        {
            //TODO: aspect ratio is valid only if viewBox was set

            if (ctx.doc == null || ctx.doc.type != SvgNodeType.Doc) return null;

            _loadFonts(ctx.fonts);

            var docNode = _sceneBuildHelper(ctx, ctx.doc, vBox, svgPath, false, 0);
            if (docNode == null) return null;

            if ((viewFlag & SvgViewFlag.Viewbox) == 0)
            {
                var prevW = vBox.w;
                var prevH = vBox.h;
                _updateInvalidViewSize(docNode, ref vBox, ref w, ref h, viewFlag);
                if ((!TvgMath.Equal(vBox.w, prevW) || !TvgMath.Equal(vBox.h, prevH)) && _hasUserSpaceGradients(ctx))
                {
                    Paint.Rel(docNode);
                    ctx.images.Clear();
                    docNode = _sceneBuildHelper(ctx, ctx.doc, vBox, svgPath, false, 0);
                    if (docNode == null) return null;
                }
            }

            if (!TvgMath.Equal(w, vBox.w) || !TvgMath.Equal(h, vBox.h))
            {
                var m = _calculateAspectRatioMatrix(align, meetOrSlice, w, h, vBox);
                docNode.Transform(m);
            }
            else if (!TvgMath.Zero(vBox.x) || !TvgMath.Zero(vBox.y))
            {
                docNode.Translate(-vBox.x, -vBox.y);
            }

            var viewBoxClip = Shape.Gen();
            viewBoxClip.AppendRect(0, 0, w, h);

            var clippingLayer = Scene.Gen();
            clippingLayer.Clip(viewBoxClip);
            clippingLayer.Add(docNode);

            ctx.doc.doc.vbox = vBox;
            ctx.doc.doc.w = w;
            ctx.doc.doc.h = h;

            var root = Scene.Gen();
            root.Add(clippingLayer);

            return root;
        }

        /************************************************************************/
        /* Public helper methods (backwards compatible with stub API)            */
        /************************************************************************/

        /// <summary>
        /// Build shape path data from an SVG node into a RenderPath.
        /// This processes the geometry of individual shape nodes.
        /// </summary>
        public static bool BuildShape(SvgNode node, RenderPath path)
        {
            switch (node.type)
            {
                case SvgNodeType.Path:
                    if (node.path.path != null)
                        return SvgPath.ToShape(node.path.path, path);
                    return false;

                case SvgNodeType.Rect:
                    AppendRect(path, node.rect.x, node.rect.y, node.rect.w, node.rect.h, node.rect.rx, node.rect.ry);
                    return true;

                case SvgNodeType.Circle:
                    AppendCircle(path, node.circle.cx, node.circle.cy, node.circle.r, node.circle.r);
                    return true;

                case SvgNodeType.Ellipse:
                    AppendCircle(path, node.ellipse.cx, node.ellipse.cy, node.ellipse.rx, node.ellipse.ry);
                    return true;

                case SvgNodeType.Line:
                    path.MoveTo(new Point(node.line.x1, node.line.y1));
                    path.LineTo(new Point(node.line.x2, node.line.y2));
                    return true;

                case SvgNodeType.Polygon:
                    if (node.polygon.pts.Count >= 4)
                    {
                        path.MoveTo(new Point(node.polygon.pts[0], node.polygon.pts[1]));
                        for (int i = 2; i < node.polygon.pts.Count - 1; i += 2)
                            path.LineTo(new Point(node.polygon.pts[i], node.polygon.pts[i + 1]));
                        path.Close();
                        return true;
                    }
                    return false;

                case SvgNodeType.Polyline:
                    if (node.polyline.pts.Count >= 4)
                    {
                        path.MoveTo(new Point(node.polyline.pts[0], node.polyline.pts[1]));
                        for (int i = 2; i < node.polyline.pts.Count - 1; i += 2)
                            path.LineTo(new Point(node.polyline.pts[i], node.polyline.pts[i + 1]));
                        return true;
                    }
                    return false;

                default:
                    return false;
            }
        }

        private static void AppendRect(RenderPath path, float x, float y, float w, float h, float rx, float ry)
        {
            var halfW = w * 0.5f;
            var halfH = h * 0.5f;

            if (rx > halfW) rx = halfW;
            if (ry > halfH) ry = halfH;

            if (rx == 0 && ry == 0)
            {
                path.MoveTo(new Point(x, y));
                path.LineTo(new Point(x + w, y));
                path.LineTo(new Point(x + w, y + h));
                path.LineTo(new Point(x, y + h));
                path.Close();
            }
            else
            {
                var hrx = rx * MathConstants.PATH_KAPPA;
                var hry = ry * MathConstants.PATH_KAPPA;

                path.MoveTo(new Point(x + rx, y));
                path.LineTo(new Point(x + w - rx, y));
                path.CubicTo(new Point(x + w - rx + hrx, y), new Point(x + w, y + ry - hry), new Point(x + w, y + ry));
                path.LineTo(new Point(x + w, y + h - ry));
                path.CubicTo(new Point(x + w, y + h - ry + hry), new Point(x + w - rx + hrx, y + h), new Point(x + w - rx, y + h));
                path.LineTo(new Point(x + rx, y + h));
                path.CubicTo(new Point(x + rx - hrx, y + h), new Point(x, y + h - ry + hry), new Point(x, y + h - ry));
                path.LineTo(new Point(x, y + ry));
                path.CubicTo(new Point(x, y + ry - hry), new Point(x + rx - hrx, y), new Point(x + rx, y));
                path.Close();
            }
        }

        private static void AppendCircle(RenderPath path, float cx, float cy, float rx, float ry)
        {
            if (rx < 0) rx = ry;
            else if (ry < 0) ry = rx;
            if (rx <= 0 || ry <= 0) return;
            var rxKappa = rx * MathConstants.PATH_KAPPA;
            var ryKappa = ry * MathConstants.PATH_KAPPA;

            path.MoveTo(new Point(cx + rx, cy));
            path.CubicTo(new Point(cx + rx, cy + ryKappa), new Point(cx + rxKappa, cy + ry), new Point(cx, cy + ry));
            path.CubicTo(new Point(cx - rxKappa, cy + ry), new Point(cx - rx, cy + ryKappa), new Point(cx - rx, cy));
            path.CubicTo(new Point(cx - rx, cy - ryKappa), new Point(cx - rxKappa, cy - ry), new Point(cx, cy - ry));
            path.CubicTo(new Point(cx + rxKappa, cy - ry), new Point(cx + rx, cy - ryKappa), new Point(cx + rx, cy));
            path.Close();
        }

        /// <summary>
        /// Collect all shape paths from the SVG DOM tree into a flat list of RenderPaths.
        /// This is a simplified version of the C++ scene builder for testing purposes.
        /// </summary>
        public static List<RenderPath> CollectPaths(SvgNode? root)
        {
            var paths = new List<RenderPath>();
            if (root == null) return paths;
            CollectPathsRecursive(root, paths);
            return paths;
        }

        private static void CollectPathsRecursive(SvgNode node, List<RenderPath> paths)
        {
            if (node.style != null && !node.style.display) return;

            if (!_isGroupType(node.type))
            {
                var path = new RenderPath();
                if (BuildShape(node, path))
                {
                    paths.Add(path);
                }
                else
                {
                    path.cmds.Dispose();
                    path.pts.Dispose();
                }
            }

            foreach (var child in node.child)
            {
                CollectPathsRecursive(child, paths);
            }
        }
    }
}
