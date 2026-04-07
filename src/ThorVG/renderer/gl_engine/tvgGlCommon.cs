// Ported from ThorVG/src/renderer/gl_engine/tvgGlCommon.h

using System;
using System.Collections.Generic;

namespace ThorVG
{
    public static class GlConstants
    {
        public const float MIN_GL_STROKE_WIDTH = 1.0f;
        public const float MIN_GL_STROKE_ALPHA = 0.25f;

        public const uint GL_MAT3_STD140_SIZE = 12; // mat3 is 3 vec4 columns in std140
        public const uint GL_MAT3_STD140_BYTES = GL_MAT3_STD140_SIZE * sizeof(float);
        public const int MAX_GRADIENT_STOPS = 16;
    }

    public static class GlMatrixHelper
    {
        /// <summary>
        /// All GPU matrices use column major order.
        /// </summary>
        public static void GetMatrix3(in Matrix mat3, Span<float> matOut)
        {
            matOut[0] = mat3.e11; matOut[3] = mat3.e12; matOut[6] = mat3.e13;
            matOut[1] = mat3.e21; matOut[4] = mat3.e22; matOut[7] = mat3.e23;
            matOut[2] = mat3.e31; matOut[5] = mat3.e32; matOut[8] = mat3.e33;
        }

        /// <summary>
        /// All GPU matrices use column major order. std140 mat3 packs each column into a vec4 stride.
        /// </summary>
        public static void GetMatrix3Std140(in Matrix mat3, Span<float> matOut)
        {
            matOut[0] = mat3.e11; matOut[4] = mat3.e12; matOut[8] = mat3.e13;
            matOut[1] = mat3.e21; matOut[5] = mat3.e22; matOut[9] = mat3.e23;
            matOut[2] = mat3.e31; matOut[6] = mat3.e32; matOut[10] = mat3.e33;
            matOut[3] = 0.0f;     matOut[7] = 0.0f;     matOut[11] = 0.0f;
        }
    }

    public enum GlStencilMode
    {
        None,
        FillNonZero,
        FillEvenOdd,
        Stroke,
    }

    public class GlGeometryBuffer
    {
        public Array<float> vertex;
        public Array<uint> index;

        public void Clear()
        {
            vertex.Clear();
            index.Clear();
        }
    }

    public unsafe class GlGeometry
    {
        public Matrix InverseMatrix()
        {
            if (!inverseMatrixDirty) return cachedInverseMatrix;
            TvgMath.Inverse(matrix, out cachedInverseMatrix);
            inverseMatrixDirty = false;
            return cachedInverseMatrix;
        }

        public void SetMatrix(in Matrix tr) { matrix = tr; inverseMatrixDirty = true; }

        public void Prepare(RenderShape rshape)
        {
            optPathThin = false;
            if (rshape.Trimpath())
            {
                var trimmedPath = new RenderPath();
                if (rshape.stroke!.trim.Trim(rshape.path, trimmedPath))
                {
                    trimmedPath.Optimize(optPath, matrix, out optPathThin);
                }
                else
                {
                    optPath.Clear();
                }
            }
            else
            {
                rshape.path.Optimize(optPath, matrix, out optPathThin);
            }
        }

        public bool TesselateShape(RenderShape rshape, ref float opacityMultiplier)
        {
            fill.Clear();
            fillBounds = default;
            fillWorld = true;
            convex = false;

            if (optPathThin && TvgMath.Zero(rshape.StrokeWidth()))
            {
                if (TesselateThinPath(optPath))
                {
                    stroke.index.MoveTo(ref fill.index);
                    stroke.vertex.MoveTo(ref fill.vertex);
                    fillBounds = strokeBounds;
                    strokeBounds = default;
                    strokeRenderWidth = 0.0f;
                    opacityMultiplier = GlConstants.MIN_GL_STROKE_ALPHA;
                    fillRule = rshape.rule;
                    return true;
                }
                return false;
            }

            // Handle normal shapes with more than 2 points
            var bwTess = new BWTessellator(fill);
            bwTess.Tessellate(optPath);
            fillRule = rshape.rule;
            fillBounds = bwTess.Bounds();
            convex = bwTess.convex;
            opacityMultiplier = 1.0f;
            return true;
        }

        public bool TesselateShape(RenderShape rshape)
        {
            float dummy = 1.0f;
            return TesselateShape(rshape, ref dummy);
        }

        public bool TesselateThinPath(RenderPath path)
        {
            stroke.Clear();
            strokeBounds = default;
            strokeRenderWidth = GlConstants.MIN_GL_STROKE_WIDTH;
            if (path.pts.count < 2) return false;
            var stroker = new Stroker(stroke, GlConstants.MIN_GL_STROKE_WIDTH, StrokeCap.Butt, StrokeJoin.Bevel);
            stroker.Run(path);
            strokeBounds = stroker.Bounds();
            return true;
        }

        public bool TesselateStroke(RenderShape rshape)
        {
            stroke.Clear();
            strokeBounds = default;
            strokeRenderWidth = 0.0f;

            var strokeWidth = 0.0f;
            if (float.IsInfinity(matrix.e11))
            {
                strokeWidth = rshape.StrokeWidth() * TvgMath.Scaling(matrix);
                if (strokeWidth <= GlConstants.MIN_GL_STROKE_WIDTH) strokeWidth = GlConstants.MIN_GL_STROKE_WIDTH;
                strokeWidth = strokeWidth / matrix.e11;
            }
            else
            {
                strokeWidth = rshape.StrokeWidth();
            }
            var strokeWidthWorld = strokeWidth * TvgMath.Scaling(matrix);
            if (!float.IsFinite(strokeWidthWorld)) strokeWidthWorld = strokeWidth;

            if (!TvgMath.Zero(strokeWidthWorld))
            {
                var stroker = new Stroker(stroke, strokeWidthWorld, rshape.StrokeCap(), rshape.StrokeJoin(), rshape.StrokeMiterlimit());
                var dashedPathWorld = new RenderPath();
                if (rshape.StrokeDash(dashedPathWorld, matrix)) stroker.Run(dashedPathWorld);
                else stroker.Run(optPath);
                strokeBounds = stroker.Bounds();
                strokeRenderWidth = strokeWidthWorld;
                return true;
            }
            return false;
        }

        public void TesselateImage(RenderSurface image)
        {
            fill.Clear();
            fillBounds = default;
            fillWorld = true;
            strokeRenderWidth = 0.0f;
            fill.vertex.Reserve(5 * 4);
            fill.index.Reserve(6);

            var leftTop = TvgMath.Transform(new Point(0f, 0f), matrix);
            var leftBottom = TvgMath.Transform(new Point(0f, (float)image.h), matrix);
            var rightTop = TvgMath.Transform(new Point((float)image.w, 0f), matrix);
            var rightBottom = TvgMath.Transform(new Point((float)image.w, (float)image.h), matrix);

            AppendImageVertex(leftTop, 0f, 1f);
            AppendImageVertex(leftBottom, 0f, 0f);
            AppendImageVertex(rightTop, 1f, 1f);
            AppendImageVertex(rightBottom, 1f, 0f);

            fill.index.Push(0);
            fill.index.Push(1);
            fill.index.Push(2);

            fill.index.Push(2);
            fill.index.Push(1);
            fill.index.Push(3);

            fillBounds = TransformBounds(new RenderRegion(0, 0, (int)image.w, (int)image.h), matrix);
        }

        private void AppendImageVertex(Point pt, float u, float v)
        {
            fill.vertex.Push(pt.x);
            fill.vertex.Push(pt.y);
            fill.vertex.Push(u);
            fill.vertex.Push(v);
        }

        public bool Draw(GlRenderTask task, GlStageBuffer gpuBuffer, RenderUpdateFlag flag)
        {
            if (flag == RenderUpdateFlag.None) return false;

            var buffer = ((flag & RenderUpdateFlag.Stroke) != 0 || (flag & RenderUpdateFlag.GradientStroke) != 0) ? stroke : fill;
            if (buffer.index.Empty()) return false;

            var vertexOffset = gpuBuffer.Push(buffer.vertex.data, buffer.vertex.count * sizeof(float));
            var indexOffset = gpuBuffer.PushIndex(buffer.index.data, buffer.index.count * sizeof(uint));

            if ((flag & RenderUpdateFlag.Image) != 0)
            {
                // image has two attributes: [pos, uv]
                task.AddVertexLayout(new GlVertexLayout { index = 0, size = 2, stride = 4 * sizeof(float), offset = vertexOffset });
                task.AddVertexLayout(new GlVertexLayout { index = 1, size = 2, stride = 4 * sizeof(float), offset = vertexOffset + 2 * sizeof(float) });
            }
            else
            {
                task.AddVertexLayout(new GlVertexLayout { index = 0, size = 2, stride = 2 * sizeof(float), offset = vertexOffset });
            }
            task.SetDrawRange(indexOffset, buffer.index.count);
            return true;
        }

        public GlStencilMode GetStencilMode(RenderUpdateFlag flag)
        {
            if ((flag & RenderUpdateFlag.Stroke) != 0) return GlStencilMode.Stroke;
            if ((flag & RenderUpdateFlag.GradientStroke) != 0) return GlStencilMode.Stroke;
            if ((flag & RenderUpdateFlag.Image) != 0) return GlStencilMode.None;

            if (convex) return GlStencilMode.None;
            if (fillRule == FillRule.NonZero) return GlStencilMode.FillNonZero;
            if (fillRule == FillRule.EvenOdd) return GlStencilMode.FillEvenOdd;

            return GlStencilMode.None;
        }

        public RenderRegion GetBounds()
        {
            var bounds = default(RenderRegion);
            var hasBounds = false;

            if (!fill.index.Empty())
            {
                var fillR = fillWorld ? fillBounds : TransformBounds(fillBounds, matrix);
                if (fillR.Valid())
                {
                    bounds = fillR;
                    hasBounds = true;
                }
            }

            if (!stroke.index.Empty())
            {
                var strokeR = strokeBounds;
                if (strokeR.Valid())
                {
                    if (hasBounds) bounds.AddWith(strokeR);
                    else
                    {
                        bounds = strokeR;
                        hasBounds = true;
                    }
                }
            }

            if (hasBounds) return bounds;
            return default;
        }

        private static RenderRegion TransformBounds(in RenderRegion bounds, in Matrix mat)
        {
            if (bounds.Invalid()) return bounds;

            var lt = TvgMath.Transform(new Point(bounds.min.x, bounds.min.y), mat);
            var lb = TvgMath.Transform(new Point(bounds.min.x, bounds.max.y), mat);
            var rt = TvgMath.Transform(new Point(bounds.max.x, bounds.min.y), mat);
            var rb = TvgMath.Transform(new Point(bounds.max.x, bounds.max.y), mat);

            var left = MathF.Min(MathF.Min(lt.x, lb.x), MathF.Min(rt.x, rb.x));
            var top = MathF.Min(MathF.Min(lt.y, lb.y), MathF.Min(rt.y, rb.y));
            var right = MathF.Max(MathF.Max(lt.x, lb.x), MathF.Max(rt.x, rb.x));
            var bottom = MathF.Max(MathF.Max(lt.y, lb.y), MathF.Max(rt.y, rb.y));

            return new RenderRegion((int)MathF.Floor(left), (int)MathF.Floor(top), (int)MathF.Ceiling(right), (int)MathF.Ceiling(bottom));
        }

        public GlGeometryBuffer fill = new GlGeometryBuffer();
        public GlGeometryBuffer stroke = new GlGeometryBuffer();
        public Matrix matrix;
        public RenderRegion viewport;
        public RenderRegion fillBounds;
        public RenderRegion strokeBounds;
        public FillRule fillRule = FillRule.NonZero;
        public RenderPath optPath = new RenderPath();
        public float strokeRenderWidth;
        private Matrix cachedInverseMatrix;
        private bool inverseMatrixDirty = true;
        public bool fillWorld;
        public bool optPathThin;
        public bool convex;
    }

    public class GlShape
    {
        public RenderShape? rshape;
        public float viewWd;
        public float viewHt;
        public uint opacity;
        public uint texId;
        public RenderSurface? texSource;
        public FilterMethod texFilter = FilterMethod.Bilinear;
        public uint texFlipY;
        public ColorSpace texColorSpace = ColorSpace.ABGR8888;
        public GlGeometry geometry = new GlGeometry();
        public ValueList<object?> clips;
        public ushort texStamp;  // Tracks TextureMgr.stamp ownership of texId.
        public bool validFill;
        public bool validStroke;
    }

    public class GlIntersector
    {
        public bool IsPointInTriangle(Point p, Point a, Point b, Point c)
        {
            var d1 = TvgMath.Cross(TvgMath.PointSub(p, a), TvgMath.PointSub(p, b));
            var d2 = TvgMath.Cross(TvgMath.PointSub(p, b), TvgMath.PointSub(p, c));
            var d3 = TvgMath.Cross(TvgMath.PointSub(p, c), TvgMath.PointSub(p, a));
            var hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            var hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);
            return !(hasNeg && hasPos);
        }

        public bool IsPointInImage(Point p, GlGeometryBuffer mesh, in Matrix tr)
        {
            for (uint i = 0; i < mesh.index.count; i += 3)
            {
                var p0 = TvgMath.Transform(new Point(mesh.vertex[mesh.index[i + 0] * 4 + 0], mesh.vertex[mesh.index[i + 0] * 4 + 1]), tr);
                var p1 = TvgMath.Transform(new Point(mesh.vertex[mesh.index[i + 1] * 4 + 0], mesh.vertex[mesh.index[i + 1] * 4 + 1]), tr);
                var p2 = TvgMath.Transform(new Point(mesh.vertex[mesh.index[i + 2] * 4 + 0], mesh.vertex[mesh.index[i + 2] * 4 + 1]), tr);
                if (IsPointInTriangle(p, p0, p1, p2)) return true;
            }
            return false;
        }

        public bool IsPointInTris(Point p, GlGeometryBuffer mesh, in Matrix tr)
        {
            for (uint i = 0; i < mesh.index.count; i += 3)
            {
                var p0 = TvgMath.Transform(new Point(mesh.vertex[mesh.index[i + 0] * 2 + 0], mesh.vertex[mesh.index[i + 0] * 2 + 1]), tr);
                var p1 = TvgMath.Transform(new Point(mesh.vertex[mesh.index[i + 1] * 2 + 0], mesh.vertex[mesh.index[i + 1] * 2 + 1]), tr);
                var p2 = TvgMath.Transform(new Point(mesh.vertex[mesh.index[i + 2] * 2 + 0], mesh.vertex[mesh.index[i + 2] * 2 + 1]), tr);
                if (IsPointInTriangle(p, p0, p1, p2)) return true;
            }
            return false;
        }

        public bool IsPointInMesh(Point p, GlGeometryBuffer mesh, in Matrix tr)
        {
            uint crossings = 0;
            Span<Point> triangle = stackalloc Point[3];
            for (uint i = 0; i < mesh.index.count; i += 3)
            {
                triangle[0] = TvgMath.Transform(new Point(mesh.vertex[mesh.index[i + 0] * 2 + 0], mesh.vertex[mesh.index[i + 0] * 2 + 1]), tr);
                triangle[1] = TvgMath.Transform(new Point(mesh.vertex[mesh.index[i + 1] * 2 + 0], mesh.vertex[mesh.index[i + 1] * 2 + 1]), tr);
                triangle[2] = TvgMath.Transform(new Point(mesh.vertex[mesh.index[i + 2] * 2 + 0], mesh.vertex[mesh.index[i + 2] * 2 + 1]), tr);
                for (int j = 0; j < 3; j++)
                {
                    var p1 = triangle[j];
                    var p2 = triangle[(j + 1) % 3];
                    if (p1.y == p2.y) continue;
                    if (p1.y > p2.y) { var tmp = p1; p1 = p2; p2 = tmp; }
                    if ((p.y > p1.y) && (p.y <= p2.y))
                    {
                        var intersectionX = (p2.x - p1.x) * (p.y - p1.y) / (p2.y - p1.y) + p1.x;
                        if (intersectionX > p.x) crossings++;
                    }
                }
            }
            return (crossings % 2) == 1;
        }

        public bool IntersectClips(Point pt, ref ValueList<object?> clips)
        {
            for (int i = 0; i < clips.Count; i++)
            {
                var clip = (GlShape?)clips[i];
                if (clip == null) continue;
                var id = TvgMath.Identity();
                if (!IsPointInMesh(pt, clip.geometry.fill, clip.geometry.fillWorld ? id : clip.geometry.matrix)) return false;
            }
            return true;
        }

        public bool IntersectShape(RenderRegion region, GlShape? shape)
        {
            if (shape == null || (shape.geometry.fill.index.count == 0 && shape.geometry.stroke.index.count == 0)) return false;
            var sizeX = region.Sw();
            var sizeY = region.Sh();
            var id = TvgMath.Identity();

            for (int y = 0; y <= sizeY; y++)
            {
                for (int x = 0; x <= sizeX; x++)
                {
                    var pt = new Point((float)x + region.min.x, (float)y + region.min.y);
                    if (y % 2 == 1) pt.y = (float)sizeY - y - sizeY % 2 + region.min.y;
                    if (IntersectClips(pt, ref shape.clips))
                    {
                        if (shape.validFill && IsPointInMesh(pt, shape.geometry.fill, shape.geometry.fillWorld ? id : shape.geometry.matrix)) return true;
                        if (shape.validStroke && IsPointInTris(pt, shape.geometry.stroke, id)) return true;
                    }
                }
            }
            return false;
        }

        public bool IntersectImage(RenderRegion region, GlShape? image)
        {
            if (image != null)
            {
                var sizeX = region.Sw();
                var sizeY = region.Sh();
                var id = TvgMath.Identity();
                for (int y = 0; y <= sizeY; y++)
                {
                    for (int x = 0; x <= sizeX; x++)
                    {
                        var pt = new Point((float)x + region.min.x, (float)y + region.min.y);
                        if (y % 2 == 1) pt.y = (float)sizeY - y - sizeY % 2 + region.min.y;
                        if (IntersectClips(pt, ref image.clips) && IsPointInImage(pt, image.geometry.fill, image.geometry.fillWorld ? id : image.geometry.matrix)) return true;
                    }
                }
            }
            return false;
        }
    }

    public unsafe struct GlLinearGradientBlock
    {
        public fixed float nStops[4];
        public fixed float startPos[2];
        public fixed float stopPos[2];
        public fixed float stopPoints[GlConstants.MAX_GRADIENT_STOPS];
        public fixed float stopColors[4 * GlConstants.MAX_GRADIENT_STOPS];

        public const int PackedSize = 4 + 2 + 2 + GlConstants.MAX_GRADIENT_STOPS + 4 * GlConstants.MAX_GRADIENT_STOPS; // 88

        public void PackInto(float* data)
        {
            int idx = 0;
            for (int i = 0; i < 4; i++) data[idx++] = nStops[i];
            for (int i = 0; i < 2; i++) data[idx++] = startPos[i];
            for (int i = 0; i < 2; i++) data[idx++] = stopPos[i];
            for (int i = 0; i < GlConstants.MAX_GRADIENT_STOPS; i++) data[idx++] = stopPoints[i];
            for (int i = 0; i < 4 * GlConstants.MAX_GRADIENT_STOPS; i++) data[idx++] = stopColors[i];
        }
    }

    public unsafe struct GlRadialGradientBlock
    {
        public fixed float nStops[4];
        public fixed float centerPos[4];
        public fixed float radius[2];
        public fixed float stopPoints[GlConstants.MAX_GRADIENT_STOPS];
        public fixed float stopColors[4 * GlConstants.MAX_GRADIENT_STOPS];

        public const int PackedSize = 4 + 4 + 2 + 2 + GlConstants.MAX_GRADIENT_STOPS + 4 * GlConstants.MAX_GRADIENT_STOPS; // 92

        public void PackInto(float* data)
        {
            int idx = 0;
            for (int i = 0; i < 4; i++) data[idx++] = nStops[i];
            for (int i = 0; i < 4; i++) data[idx++] = centerPos[i];
            for (int i = 0; i < 2; i++) data[idx++] = radius[i];
            idx += 2; // padding
            for (int i = 0; i < GlConstants.MAX_GRADIENT_STOPS; i++) data[idx++] = stopPoints[i];
            for (int i = 0; i < 4 * GlConstants.MAX_GRADIENT_STOPS; i++) data[idx++] = stopColors[i];
        }
    }

    public class GlCompositor : RenderCompositor
    {
        public RenderRegion bbox;
        public CompositionFlag flags;
        public BlendMethod blendMethod = BlendMethod.Normal;

        public GlCompositor(in RenderRegion box, CompositionFlag flags)
        {
            bbox = box;
            this.flags = flags;
        }
    }
}
