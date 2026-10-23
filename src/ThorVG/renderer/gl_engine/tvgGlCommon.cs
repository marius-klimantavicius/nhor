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

    public class GlGeometryBuffer : IDisposable
    {
        public Array<float> vertex;
        public Array<uint> index;

        ~GlGeometryBuffer() => ReleaseNativeStorage();

        public void Dispose()
        {
            ReleaseNativeStorage();
            GC.SuppressFinalize(this);
        }

        private void ReleaseNativeStorage()
        {
            vertex.Dispose();
            index.Dispose();
        }

        public void Clear()
        {
            vertex.Clear();
            index.Clear();
        }
    }

    public unsafe class GlGeometry : IDisposable
    {
        ~GlGeometry() => ReleaseNativeStorage();

        public void Dispose()
        {
            ReleaseNativeStorage();
            GC.SuppressFinalize(this);
        }

        private void ReleaseNativeStorage()
        {
            // A finalizer can also observe partially initialized fields after
            // a failed managed allocation. Each owning array resets on free.
            fill?.Dispose();
            stroke?.Dispose();
            if (optPath != null) { optPath.cmds.Dispose(); optPath.pts.Dispose(); }
            if (optStrokePath != null) { optStrokePath.cmds.Dispose(); optStrokePath.pts.Dispose(); }
        }

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
            optPathSkipFill = false;
            optStrokePath.Clear();
            var strokeWidth = rshape.StrokeWidth();
            RenderPath? localOut = float.IsFinite(strokeWidth) && !TvgMath.Zero(strokeWidth) ? optStrokePath : null;
            if (rshape.Trimpath())
            {
                var trimmedPath = new RenderPath();
                try
                {
                    if (rshape.stroke!.trim.Trim(rshape.path, trimmedPath))
                        GpuCommon.GpuOptimize(trimmedPath, optPath, localOut, matrix, out optPathThin, out optPathSkipFill);
                    else optPath.Clear();
                }
                finally
                {
                    trimmedPath.cmds.Dispose();
                    trimmedPath.pts.Dispose();
                }
            }
            else
            {
                GpuCommon.GpuOptimize(rshape.path, optPath, localOut, matrix, out optPathThin, out optPathSkipFill);
            }
        }

        public bool TesselateShape(RenderShape rshape, ref float opacityMultiplier)
        {
            fill.Clear();
            fillBounds = default;
            fillWorld = true;
            convex = false;
            opacityMultiplier = 1;

            if (optPathSkipFill) return false;

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
            stroker.Run(path, true);
            strokeBounds = stroker.Bounds();
            return true;
        }

        public bool TesselateStroke(RenderShape rshape)
        {
            stroke.Clear();
            strokeBounds = default;
            strokeRenderWidth = 0.0f;

            var strokeWidth = rshape.StrokeWidth();
            if (!float.IsFinite(strokeWidth) || TvgMath.Zero(strokeWidth)) return false;
            var qualityScale = TvgMath.Scaling(matrix);
            if (!float.IsFinite(qualityScale) || TvgMath.Zero(qualityScale)) return false;
            strokeRenderWidth = strokeWidth * qualityScale;
            if (!float.IsFinite(strokeRenderWidth)) return false;

            var stroker = new Stroker(stroke, strokeWidth, rshape.StrokeCap(), rshape.StrokeJoin(), rshape.StrokeMiterlimit(), qualityScale);
            var dashedPath = new RenderPath();
            try
            {
                if (GpuCommon.GpuStrokeDash(rshape, dashedPath, null)) stroker.Run(dashedPath);
                else stroker.Run(optStrokePath);
            }
            finally
            {
                dashedPath.cmds.Dispose();
                dashedPath.pts.Dispose();
            }
            strokeBounds = stroker.Bounds();
            strokeDirect = stroker.overlapFree;
            return true;
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

            AppendImageVertex(leftTop, 0f, 0f);
            AppendImageVertex(leftBottom, 0f, 1f);
            AppendImageVertex(rightTop, 1f, 0f);
            AppendImageVertex(rightBottom, 1f, 1f);

            fill.index.Push(0);
            fill.index.Push(1);
            fill.index.Push(2);

            fill.index.Push(2);
            fill.index.Push(1);
            fill.index.Push(3);

            fillBounds = GpuCommon.GpuTransformBounds(new RenderRegion(0, 0, (int)image.w, (int)image.h), matrix);
        }

        private void AppendImageVertex(Point pt, float u, float v)
        {
            fill.vertex.Push(pt.x);
            fill.vertex.Push(pt.y);
            fill.vertex.Push(u);
            fill.vertex.Push(v);
        }

        public bool Drawable(RenderUpdateFlag flag)
        {
            if (flag == RenderUpdateFlag.None) return false;
            var buffer = ((flag & RenderUpdateFlag.Stroke) != 0 || (flag & RenderUpdateFlag.GradientStroke) != 0) ? stroke : fill;
            return !buffer.index.Empty();
        }

        public void Draw(GlRenderTask task, GlStageBuffer gpuBuffer, RenderUpdateFlag flag)
        {
            var buffer = ((flag & RenderUpdateFlag.Stroke) != 0 || (flag & RenderUpdateFlag.GradientStroke) != 0) ? stroke : fill;
            var vertexOffset = gpuBuffer.Push(buffer.vertex.data, buffer.vertex.count * sizeof(float));
            var indexOffset = gpuBuffer.PushIndex(buffer.index.data, buffer.index.count * sizeof(uint));

            if ((flag & RenderUpdateFlag.Image) != 0)
            {
                // image has two attributes: [pos, uv]
                task.AddVertexLayout(new GlVertexLayout { index = 0, size = 2, stride = 4 * sizeof(float), offset = vertexOffset, arrayBufferId = gpuBuffer.GetBufferId() });
                task.AddVertexLayout(new GlVertexLayout { index = 1, size = 2, stride = 4 * sizeof(float), offset = vertexOffset + 2 * sizeof(float), arrayBufferId = gpuBuffer.GetBufferId() });
            }
            else
            {
                task.AddVertexLayout(new GlVertexLayout { index = 0, size = 2, stride = 2 * sizeof(float), offset = vertexOffset, arrayBufferId = gpuBuffer.GetBufferId() });
            }
            task.SetDrawRange(indexOffset, buffer.index.count);
        }

        public GlStencilMode GetStencilMode(RenderUpdateFlag flag)
        {
            if ((flag & (RenderUpdateFlag.Stroke | RenderUpdateFlag.GradientStroke)) != 0)
                return strokeDirect ? GlStencilMode.None : GlStencilMode.Stroke;
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
                var fillR = fillWorld ? fillBounds : GpuCommon.GpuTransformBounds(fillBounds, matrix);
                if (fillR.Valid())
                {
                    bounds = fillR;
                    hasBounds = true;
                }
            }

            if (!stroke.index.Empty())
            {
                var strokeR = GpuCommon.GpuTransformBounds(strokeBounds, matrix);
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

        public GlGeometryBuffer fill = new GlGeometryBuffer();
        public GlGeometryBuffer stroke = new GlGeometryBuffer();
        public Matrix matrix;
        public RenderRegion viewport;
        public RenderRegion fillBounds;
        public RenderRegion strokeBounds;
        public FillRule fillRule = FillRule.NonZero;
        public RenderPath optPath = new RenderPath();
        public RenderPath optStrokePath = new RenderPath();
        public float strokeRenderWidth;
        public bool strokeDirect;
        private Matrix cachedInverseMatrix;
        private bool inverseMatrixDirty = true;
        public bool fillWorld;
        public bool optPathThin;
        public bool optPathSkipFill;
        public bool convex;
    }

    public class GlShape
    {
        public RenderShape? rshape;
        public float viewWd;
        public float viewHt;
        public uint opacity;
        public float multiplier = 1.0f;
        public RenderUpdateFlag deferredFlags;
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

    public unsafe class GlIntersector
    {
        public bool IsPointInImage(Point p, GlGeometryBuffer mesh, in Matrix tr)
        {
            for (uint i = 0; i < mesh.index.count; i += 3)
            {
                var p0 = TvgMath.Transform(new Point(mesh.vertex[mesh.index[i + 0] * 4 + 0], mesh.vertex[mesh.index[i + 0] * 4 + 1]), tr);
                var p1 = TvgMath.Transform(new Point(mesh.vertex[mesh.index[i + 1] * 4 + 0], mesh.vertex[mesh.index[i + 1] * 4 + 1]), tr);
                var p2 = TvgMath.Transform(new Point(mesh.vertex[mesh.index[i + 2] * 4 + 0], mesh.vertex[mesh.index[i + 2] * 4 + 1]), tr);
                if (GpuCommon.GpuPointInTriangle(p, p0, p1, p2)) return true;
            }
            return false;
        }

        public bool IsPointInTris(Point p, GlGeometryBuffer mesh)
        {
            for (uint i = 0; i < mesh.index.count; i += 3)
            {
                var p0 = new Point(mesh.vertex[mesh.index[i + 0] * 2 + 0], mesh.vertex[mesh.index[i + 0] * 2 + 1]);
                var p1 = new Point(mesh.vertex[mesh.index[i + 1] * 2 + 0], mesh.vertex[mesh.index[i + 1] * 2 + 1]);
                var p2 = new Point(mesh.vertex[mesh.index[i + 2] * 2 + 0], mesh.vertex[mesh.index[i + 2] * 2 + 1]);
                if (GpuCommon.GpuPointInTriangle(p, p0, p1, p2)) return true;
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
            for (int i = 0; i < clips.Count; ++i)
            {
                if (clips[i] is not GlShape clip) continue;
                var g = clip.geometry;
                if (clip.validFill)
                {
                    var p = g.fillWorld ? pt : TvgMath.Transform(pt, g.InverseMatrix());
                    if (!g.fillBounds.Inside(p) || !GpuCommon.GpuPointInFillMesh(p, g.fill.vertex.data, g.fill.index.data, g.fill.index.count, g.fillRule)) return false;
                }
                else if (clip.validStroke)
                {
                    var p = TvgMath.Transform(pt, g.InverseMatrix());
                    if (!g.strokeBounds.Inside(p) || !GpuCommon.GpuPointInAnyMesh(p, g.stroke.vertex.data, g.stroke.index.data, g.stroke.index.count)) return false;
                }
            }
            return true;
        }

        public bool IntersectShape(RenderRegion region, GlShape? shape)
        {
            if (shape == null) return false;
            var g = shape.geometry;
            bool fill = shape.validFill && !g.fill.index.Empty() && shape.rshape != null && (shape.rshape.fill != null || shape.rshape.color.a > 0);
            bool stroke = shape.validStroke && !g.stroke.index.Empty();
            if (!fill && !stroke) return false;
            var inverse = g.InverseMatrix();
            int width = region.Sw(), height = region.Sh();
            for (int y = 0; y < height; ++y)
            {
                int py = y % 2 == 0 ? y : height - y - height % 2;
                for (int x = 0; x < width; ++x)
                {
                    var pt = new Point(x + region.min.x, py + region.min.y);
                    bool hit = false;
                    if (fill)
                    {
                        var p = g.fillWorld ? pt : TvgMath.Transform(pt, inverse);
                        hit = g.fillBounds.Inside(p) && GpuCommon.GpuPointInFillMesh(p, g.fill.vertex.data, g.fill.index.data, g.fill.index.count, g.fillRule);
                    }
                    if (!hit && stroke)
                    {
                        var p = TvgMath.Transform(pt, inverse);
                        hit = g.strokeBounds.Inside(p) && GpuCommon.GpuPointInAnyMesh(p, g.stroke.vertex.data, g.stroke.index.data, g.stroke.index.count);
                    }
                    if (hit && IntersectClips(pt, ref shape.clips)) return true;
                }
            }
            return false;
        }

        public bool IntersectImage(RenderRegion region, GlShape? image)
        {
            if (image == null || image.geometry.fill.index.count < 6) return false;
            var mesh = image.geometry.fill;
            Span<Point> triangles = stackalloc Point[6];
            for (uint i = 0; i < 6; ++i)
            {
                uint index = mesh.index[i] * 4;
                triangles[(int)i] = new Point(mesh.vertex[index], mesh.vertex[index + 1]);
            }
            int width = region.Sw(), height = region.Sh();
            for (int y = 0; y < height; ++y)
            {
                int py = y % 2 == 0 ? y : height - y - height % 2;
                for (int x = 0; x < width; ++x)
                {
                    var p = new Point(x + region.min.x, py + region.min.y);
                    if (GpuCommon.GpuPointInQuad(p, triangles) && IntersectClips(p, ref image.clips)) return true;
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

    public unsafe struct GlConicGradientBlock
    {
        public fixed float nStops[4];
        public fixed float stopPoints[GlConstants.MAX_GRADIENT_STOPS];
        public fixed float stopColors[4 * GlConstants.MAX_GRADIENT_STOPS];
        public const int PackedSize = 4 + 5 * GlConstants.MAX_GRADIENT_STOPS;
        public void PackInto(float* data)
        {
            int index = 0;
            for (int i = 0; i < 4; ++i) data[index++] = nStops[i];
            for (int i = 0; i < GlConstants.MAX_GRADIENT_STOPS; ++i) data[index++] = stopPoints[i];
            for (int i = 0; i < 4 * GlConstants.MAX_GRADIENT_STOPS; ++i) data[index++] = stopColors[i];
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
