// Ported from ThorVG/src/renderer/sw_engine/tvgSwRasterTexmap.h

using System;
using System.Runtime.CompilerServices;
using static ThorVG.SwHelper;

namespace ThorVG
{
    public struct Vertex
    {
        public Point pt;
        public Point uv;
    }

    public struct Polygon
    {
        public Vertex v0, v1, v2;
    }

    public static unsafe partial class SwRaster
    {
        // One scan converter belongs to each rasterTexmapPolygon call. A blend
        // callback may render another image before the current polygon finishes.
        private struct TexmapState
        {
            public float dudx, dvdx;
            public float dxdya, dxdyb, dudya, dvdya;
            public float xa, xb, ua, va;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int _modf(float v)
        {
            return 255 - (((int)(v * 256.0f)) & 255);
        }

        private static byte _feathering(int iru, int irv, int ar, int ab, int sw, int sh)
        {
            if (irv == 1)
            {
                if (iru == 1) return (byte)(255 - MULTIPLY(ar, ab));
                else if (iru == sw) return (byte)MULTIPLY(ar, 255 - ab);
                return (byte)(255 - ab);
            }
            else if (irv == sh)
            {
                if (iru == 1) return (byte)MULTIPLY(255 - ar, ab);
                else if (iru == sw) return (byte)MULTIPLY(ar, ab);
                return (byte)ab;
            }
            else
            {
                if (iru == 1) return (byte)(255 - ar);
                else if (iru == sw) return (byte)ar;
            }
            return 255;
        }

        private static bool _rasterMaskedPolygonImageSegment(ref TexmapState state, SwSurface surface, SwImage image, in RenderRegion bbox, int yStart, int yEnd, byte opacity, bool needAA)
        {
            // Not implemented in C++ either
            return false;
        }

        private static void _rasterBlendingPolygonImageSegment(ref TexmapState state, SwSurface surface, SwImage image, in RenderRegion bbox, int yStart, int yEnd, byte opacity, bool needAA)
        {
            if (surface.channelSize == sizeof(byte)) return;

            var localDudx = state.dudx; var localDvdx = state.dvdx;
            var localDxdya = state.dxdya; var localDxdyb = state.dxdyb;
            var localDudya = state.dudya; var localDvdya = state.dvdya;
            var localXa = state.xa; var localXb = state.xb;
            var localUa = state.ua; var localVa = state.va;
            var sbuf = image.buf32;
            var dbuf = surface.buf32;
            var sw = (int)image.w;
            var sh = (int)image.h;

            if (yStart < bbox.min.y) yStart = bbox.min.y;
            if (yEnd > bbox.max.y) yEnd = bbox.max.y;

            int y = yStart;
            while (y < yEnd)
            {
                int x1 = Math.Max((int)localXa, bbox.min.x);
                int x2 = Math.Min((int)localXb, bbox.max.x);

                if ((x2 - x1) >= 1 && (x1 < bbox.max.x) && (x2 > bbox.min.x))
                {
                    float dx = 1 - (localXa - x1);
                    float u = TvgMath.MultiplyAdd(dx, localDudx, localUa);
                    float v = TvgMath.MultiplyAdd(dx, localDvdx, localVa);
                    var buf = dbuf + ((y * surface.stride) + x1);
                    int x = x1;

                    while (x++ < x2)
                    {
                        int uu = (int)u;
                        int vv = (int)v;
                        if ((uint)uu >= image.w || (uint)vv >= image.h) continue;

                        int ar = _modf(u);
                        int ab = _modf(v);
                        int iru = uu + 1;
                        int irv = vv + 1;

                        int px = (int)*(sbuf + (vv * image.stride) + uu);
                        if (image.filter == FilterMethod.Bilinear)
                        {
                            if (iru < sw) { int px2 = (int)*(sbuf + (vv * image.stride) + iru); px = (int)INTERPOLATE((uint)px, (uint)px2, (byte)ar); }
                            if (irv < sh) { int px2 = (int)*(sbuf + (irv * image.stride) + uu); if (iru < sw) { int px3 = (int)*(sbuf + (irv * image.stride) + iru); px2 = (int)INTERPOLATE((uint)px2, (uint)px3, (byte)ar); } px = (int)INTERPOLATE((uint)px, (uint)px2, (byte)ab); }
                        }

                        if (needAA) { var feather = _feathering(iru, irv, ar, ab, sw, sh); if (feather < 255) px = (int)ALPHA_BLEND((uint)px, feather); }

                        *buf = INTERPOLATE(surface.blender!(surface, rasterUnpremultiply((uint)px), *buf), *buf, (byte)MULTIPLY(opacity, A((uint)px)));
                        ++buf;
                        u += localDudx;
                        v += localDvdx;
                    }
                }

                localXa += localDxdya;
                localXb += localDxdyb;
                localUa += localDudya;
                localVa += localDvdya;
                ++y;
            }
            state.xa = localXa; state.xb = localXb; state.ua = localUa; state.va = localVa;
        }

        private static void _rasterPolygonImageSegment32(ref TexmapState state, SwSurface surface, SwImage image, in RenderRegion bbox, int yStart, int yEnd, byte opacity, bool matting, bool needAA)
        {
            var localDudx = state.dudx; var localDvdx = state.dvdx;
            var localDxdya = state.dxdya; var localDxdyb = state.dxdyb;
            var localDudya = state.dudya; var localDvdya = state.dvdya;
            var localXa = state.xa; var localXb = state.xb;
            var localUa = state.ua; var localVa = state.va;
            var sbuf = image.buf32;
            var dbuf = surface.buf32;
            var sw = (int)image.w;
            var sh = (int)image.h;
            var fullOpacity = (opacity == 255);

            var csize = matting ? surface.compositor!.image.channelSize : (byte)0;
            var alpha = matting ? surface.Alpha(surface.compositor!.method) : null;

            if (yStart < bbox.min.y) yStart = bbox.min.y;
            if (yEnd > bbox.max.y) yEnd = bbox.max.y;

            int y = yStart;
            while (y < yEnd)
            {
                int x1 = Math.Max((int)localXa, bbox.min.x);
                int x2 = Math.Min((int)localXb, bbox.max.x);

                if ((x2 - x1) >= 1 && (x1 < bbox.max.x) && (x2 > bbox.min.x))
                {
                    float dx = 1 - (localXa - x1);
                    float u = TvgMath.MultiplyAdd(dx, localDudx, localUa);
                    float v = TvgMath.MultiplyAdd(dx, localDvdx, localVa);
                    var buf = dbuf + ((y * surface.stride) + x1);
                    int x = x1;

                    byte* cmp = null;
                    if (matting) cmp = &surface.compositor!.image.buf8[(y * surface.compositor.image.stride + x1) * csize];

                    while (x++ < x2)
                    {
                        int uu = (int)u;
                        int vv = (int)v;
                        if ((uint)uu >= image.w || (uint)vv >= image.h) continue;

                        int ar = _modf(u);
                        int ab = _modf(v);
                        int iru = uu + 1;
                        int irv = vv + 1;

                        int px = (int)*(sbuf + (vv * image.stride) + uu);
                        if (image.filter == FilterMethod.Bilinear)
                        {
                            if (iru < sw) { int px2 = (int)*(sbuf + (vv * image.stride) + iru); px = (int)INTERPOLATE((uint)px, (uint)px2, (byte)ar); }
                            if (irv < sh) { int px2 = (int)*(sbuf + (irv * image.stride) + uu); if (iru < sw) { int px3 = (int)*(sbuf + (irv * image.stride) + iru); px2 = (int)INTERPOLATE((uint)px2, (uint)px3, (byte)ar); } px = (int)INTERPOLATE((uint)px, (uint)px2, (byte)ab); }
                        }

                        uint src;
                        if (matting)
                        {
                            var a = alpha!(cmp);
                            src = fullOpacity ? ALPHA_BLEND((uint)px, a) : ALPHA_BLEND((uint)px, (uint)MULTIPLY(opacity, a));
                            cmp += csize;
                        }
                        else
                        {
                            src = fullOpacity ? (uint)px : ALPHA_BLEND((uint)px, opacity);
                        }

                        if (needAA) { var feather = _feathering(iru, irv, ar, ab, sw, sh); if (feather < 255) src = ALPHA_BLEND(src, feather); }

                        *buf = src + ALPHA_BLEND(*buf, IA(src));
                        ++buf;
                        u += localDudx;
                        v += localDvdx;
                    }
                }

                localXa += localDxdya;
                localXb += localDxdyb;
                localUa += localDudya;
                localVa += localDvdya;
                ++y;
            }
            state.xa = localXa; state.xb = localXb; state.ua = localUa; state.va = localVa;
        }

        private static void _rasterPolygonImageSegment8(ref TexmapState state, SwSurface surface, SwImage image, in RenderRegion bbox, int yStart, int yEnd, byte opacity, bool needAA)
        {
            var localDudx = state.dudx; var localDvdx = state.dvdx;
            var localDxdya = state.dxdya; var localDxdyb = state.dxdyb;
            var localDudya = state.dudya; var localDvdya = state.dvdya;
            var localXa = state.xa; var localXb = state.xb;
            var localUa = state.ua; var localVa = state.va;
            var sbuf = image.buf32;
            var dbuf = surface.buf8;

            if (yStart < bbox.min.y) yStart = bbox.min.y;
            if (yEnd > bbox.max.y) yEnd = bbox.max.y;

            int y = yStart;
            while (y < yEnd)
            {
                int x1 = Math.Max((int)localXa, bbox.min.x);
                int x2 = Math.Min((int)localXb, bbox.max.x);

                if ((x2 - x1) >= 1 && (x1 < bbox.max.x) && (x2 > bbox.min.x))
                {
                    float dx = 1 - (localXa - x1);
                    float u = TvgMath.MultiplyAdd(dx, localDudx, localUa);
                    float v = TvgMath.MultiplyAdd(dx, localDvdx, localVa);
                    var buf = dbuf + ((y * surface.stride) + x1);
                    int x = x1;

                    while (x++ < x2)
                    {
                        var uu = (int)u;
                        var vv = (int)v;
                        if ((uint)uu >= image.w || (uint)vv >= image.h) continue;

                        var px = A(*(sbuf + (vv * image.stride) + uu));
                        *buf = (byte)MULTIPLY(px, opacity);
                        ++buf;
                        u += localDudx;
                        v += localDvdx;
                    }
                }

                localXa += localDxdya;
                localXb += localDxdyb;
                localUa += localDudya;
                localVa += localDvdya;
                ++y;
            }
            state.xa = localXa; state.xb = localXb; state.ua = localUa; state.va = localVa;
        }

        private static void _rasterPolygonImageSegment(ref TexmapState state, SwSurface surface, SwImage image, in RenderRegion bbox, int yStart, int yEnd, byte opacity, bool matting, bool needAA)
        {
            if (surface.channelSize == sizeof(uint)) _rasterPolygonImageSegment32(ref state, surface, image, bbox, yStart, yEnd, opacity, matting, needAA);
            else if (surface.channelSize == sizeof(byte)) _rasterPolygonImageSegment8(ref state, surface, image, bbox, yStart, yEnd, opacity, needAA);
        }

        private static void _rasterPolygonImage(ref TexmapState state, SwSurface surface, SwImage image, in RenderRegion bbox, ref Polygon polygon, byte opacity, bool needAA)
        {
            Span<float> xArr = stackalloc float[] { polygon.v0.pt.x, polygon.v1.pt.x, polygon.v2.pt.x };
            Span<float> yArr = stackalloc float[] { polygon.v0.pt.y, polygon.v1.pt.y, polygon.v2.pt.y };
            Span<float> uArr = stackalloc float[] { polygon.v0.uv.x, polygon.v1.uv.x, polygon.v2.uv.x };
            Span<float> vArr = stackalloc float[] { polygon.v0.uv.y, polygon.v1.uv.y, polygon.v2.uv.y };

            Span<float> dxdy = stackalloc float[3];
            dxdy.Clear();
            var upper = false;

            // Sort the vertices in ascending Y order
            if (yArr[0] > yArr[1]) { (xArr[0], xArr[1]) = (xArr[1], xArr[0]); (yArr[0], yArr[1]) = (yArr[1], yArr[0]); (uArr[0], uArr[1]) = (uArr[1], uArr[0]); (vArr[0], vArr[1]) = (vArr[1], vArr[0]); }
            if (yArr[0] > yArr[2]) { (xArr[0], xArr[2]) = (xArr[2], xArr[0]); (yArr[0], yArr[2]) = (yArr[2], yArr[0]); (uArr[0], uArr[2]) = (uArr[2], uArr[0]); (vArr[0], vArr[2]) = (vArr[2], vArr[0]); }
            if (yArr[1] > yArr[2]) { (xArr[1], xArr[2]) = (xArr[2], xArr[1]); (yArr[1], yArr[2]) = (yArr[2], yArr[1]); (uArr[1], uArr[2]) = (uArr[2], uArr[1]); (vArr[1], vArr[2]) = (vArr[2], vArr[1]); }

            Span<int> yi = stackalloc int[] { (int)yArr[0], (int)yArr[1], (int)yArr[2] };

            if ((yi[0] == yi[1] && yi[0] == yi[2]) || ((int)xArr[0] == (int)xArr[1] && (int)xArr[0] == (int)xArr[2])) return;

            var denom = TvgMath.MultiplyAdd(xArr[2] - xArr[0], yArr[1] - yArr[0], -((xArr[1] - xArr[0]) * (yArr[2] - yArr[0])));
            if (TvgMath.Zero(denom)) return;

            denom = 1 / denom;
            state.dudx = TvgMath.MultiplyAdd(uArr[2] - uArr[0], yArr[1] - yArr[0], -((uArr[1] - uArr[0]) * (yArr[2] - yArr[0]))) * denom;
            state.dvdx = TvgMath.MultiplyAdd(vArr[2] - vArr[0], yArr[1] - yArr[0], -((vArr[1] - vArr[0]) * (yArr[2] - yArr[0]))) * denom;
            var dudy = TvgMath.MultiplyAdd(uArr[1] - uArr[0], xArr[2] - xArr[0], -((uArr[2] - uArr[0]) * (xArr[1] - xArr[0]))) * denom;
            var dvdy = TvgMath.MultiplyAdd(vArr[1] - vArr[0], xArr[2] - xArr[0], -((vArr[2] - vArr[0]) * (xArr[1] - xArr[0]))) * denom;

            if (yArr[1] > yArr[0]) dxdy[0] = (xArr[1] - xArr[0]) / (yArr[1] - yArr[0]);
            if (yArr[2] > yArr[0]) dxdy[1] = (xArr[2] - xArr[0]) / (yArr[2] - yArr[0]);
            if (yArr[2] > yArr[1]) dxdy[2] = (xArr[2] - xArr[1]) / (yArr[2] - yArr[1]);

            var side = (dxdy[1] > dxdy[0]);
            if (TvgMath.Equal(yArr[0], yArr[1])) side = xArr[0] > xArr[1];
            if (TvgMath.Equal(yArr[1], yArr[2])) side = xArr[2] > xArr[1];

            var compositing = _compositing(surface);
            var blending = _blending(surface);

            if (!side)
            {
                state.dxdya = dxdy[1];
                state.dudya = TvgMath.MultiplyAdd(state.dxdya, state.dudx, dudy);
                state.dvdya = TvgMath.MultiplyAdd(state.dxdya, state.dvdx, dvdy);

                var dy = 1.0f - (yArr[0] - yi[0]);
                state.xa = TvgMath.MultiplyAdd(dy, state.dxdya, xArr[0]);
                state.ua = TvgMath.MultiplyAdd(dy, state.dudya, uArr[0]);
                state.va = TvgMath.MultiplyAdd(dy, state.dvdya, vArr[0]);

                if (yi[0] < yi[1])
                {
                    var off_y = yArr[0] < bbox.min.y ? (bbox.min.y - yArr[0]) : 0;
                    state.xa = TvgMath.MultiplyAdd(off_y, state.dxdya, state.xa); state.ua = TvgMath.MultiplyAdd(off_y, state.dudya, state.ua); state.va = TvgMath.MultiplyAdd(off_y, state.dvdya, state.va);
                    state.dxdyb = dxdy[0];
                    state.xb = TvgMath.MultiplyAdd(off_y, state.dxdyb, TvgMath.MultiplyAdd(dy, state.dxdyb, xArr[0]));

                    if (compositing) { if (_matting(surface)) _rasterPolygonImageSegment(ref state, surface, image, bbox, yi[0], yi[1], opacity, true, needAA); else _rasterMaskedPolygonImageSegment(ref state, surface, image, bbox, yi[0], yi[1], opacity, needAA); }
                    else if (blending) _rasterBlendingPolygonImageSegment(ref state, surface, image, bbox, yi[0], yi[1], opacity, needAA);
                    else _rasterPolygonImageSegment(ref state, surface, image, bbox, yi[0], yi[1], opacity, false, needAA);
                    upper = true;
                }
                if (yi[1] < yi[2])
                {
                    var off_y = yArr[1] < bbox.min.y ? (bbox.min.y - yArr[1]) : 0;
                    if (!upper) { state.xa = TvgMath.MultiplyAdd(off_y, state.dxdya, state.xa); state.ua = TvgMath.MultiplyAdd(off_y, state.dudya, state.ua); state.va = TvgMath.MultiplyAdd(off_y, state.dvdya, state.va); }
                    state.dxdyb = dxdy[2];
                    state.xb = TvgMath.MultiplyAdd(off_y, state.dxdyb, TvgMath.MultiplyAdd(1 - (yArr[1] - yi[1]), state.dxdyb, xArr[1]));

                    if (compositing) { if (_matting(surface)) _rasterPolygonImageSegment(ref state, surface, image, bbox, yi[1], yi[2], opacity, true, needAA); else _rasterMaskedPolygonImageSegment(ref state, surface, image, bbox, yi[1], yi[2], opacity, needAA); }
                    else if (blending) _rasterBlendingPolygonImageSegment(ref state, surface, image, bbox, yi[1], yi[2], opacity, needAA);
                    else _rasterPolygonImageSegment(ref state, surface, image, bbox, yi[1], yi[2], opacity, false, needAA);
                }
            }
            else
            {
                state.dxdyb = dxdy[1];
                var dy = 1.0f - (yArr[0] - yi[0]);
                state.xb = TvgMath.MultiplyAdd(dy, state.dxdyb, xArr[0]);

                if (yi[0] < yi[1])
                {
                    var off_y = yArr[0] < bbox.min.y ? (bbox.min.y - yArr[0]) : 0;
                    state.xb = TvgMath.MultiplyAdd(off_y, state.dxdyb, state.xb);

                    state.dxdya = dxdy[0];
                    state.dudya = TvgMath.MultiplyAdd(state.dxdya, state.dudx, dudy);
                    state.dvdya = TvgMath.MultiplyAdd(state.dxdya, state.dvdx, dvdy);

                    state.xa = TvgMath.MultiplyAdd(off_y, state.dxdya, TvgMath.MultiplyAdd(dy, state.dxdya, xArr[0]));
                    state.ua = TvgMath.MultiplyAdd(off_y, state.dudya, TvgMath.MultiplyAdd(dy, state.dudya, uArr[0]));
                    state.va = TvgMath.MultiplyAdd(off_y, state.dvdya, TvgMath.MultiplyAdd(dy, state.dvdya, vArr[0]));

                    if (compositing) { if (_matting(surface)) _rasterPolygonImageSegment(ref state, surface, image, bbox, yi[0], yi[1], opacity, true, needAA); else _rasterMaskedPolygonImageSegment(ref state, surface, image, bbox, yi[0], yi[1], opacity, needAA); }
                    else if (blending) _rasterBlendingPolygonImageSegment(ref state, surface, image, bbox, yi[0], yi[1], opacity, needAA);
                    else _rasterPolygonImageSegment(ref state, surface, image, bbox, yi[0], yi[1], opacity, false, needAA);
                    upper = true;
                }
                if (yi[1] < yi[2])
                {
                    var off_y = yArr[1] < bbox.min.y ? (bbox.min.y - yArr[1]) : 0;
                    if (!upper) state.xb = TvgMath.MultiplyAdd(off_y, state.dxdyb, state.xb);

                    state.dxdya = dxdy[2];
                    state.dudya = TvgMath.MultiplyAdd(state.dxdya, state.dudx, dudy);
                    state.dvdya = TvgMath.MultiplyAdd(state.dxdya, state.dvdx, dvdy);
                    dy = 1 - (yArr[1] - yi[1]);
                    state.xa = TvgMath.MultiplyAdd(off_y, state.dxdya, TvgMath.MultiplyAdd(dy, state.dxdya, xArr[1]));
                    state.ua = TvgMath.MultiplyAdd(off_y, state.dudya, TvgMath.MultiplyAdd(dy, state.dudya, uArr[1]));
                    state.va = TvgMath.MultiplyAdd(off_y, state.dvdya, TvgMath.MultiplyAdd(dy, state.dvdya, vArr[1]));

                    if (compositing) { if (_matting(surface)) _rasterPolygonImageSegment(ref state, surface, image, bbox, yi[1], yi[2], opacity, true, needAA); else _rasterMaskedPolygonImageSegment(ref state, surface, image, bbox, yi[1], yi[2], opacity, needAA); }
                    else if (blending) _rasterBlendingPolygonImageSegment(ref state, surface, image, bbox, yi[1], yi[2], opacity, needAA);
                    else _rasterPolygonImageSegment(ref state, surface, image, bbox, yi[1], yi[2], opacity, false, needAA);
                }
            }
        }

        public static bool rasterTexmapPolygon(SwSurface surface, SwImage image, in Matrix transform, in RenderRegion bbox, byte opacity)
        {
            Span<Vertex> vertices = stackalloc Vertex[4];
            vertices[0] = new Vertex { pt = new Point(0, 0), uv = new Point(0, 0) };
            vertices[1] = new Vertex { pt = new Point(image.w, 0), uv = new Point(image.w, 0) };
            vertices[2] = new Vertex { pt = new Point(image.w, image.h), uv = new Point(image.w, image.h) };
            vertices[3] = new Vertex { pt = new Point(0, image.h), uv = new Point(0, image.h) };

            for (int i = 0; i < 4; i++)
            {
                vertices[i].pt = TvgMath.Transform(vertices[i].pt, transform);
            }

            var needAA = TvgMath.RightAngle(transform) ? false : true;

            var state = default(TexmapState);
            Polygon polygon;

            // Draw the first polygon
            polygon.v0 = vertices[0];
            polygon.v1 = vertices[1];
            polygon.v2 = vertices[3];
            _rasterPolygonImage(ref state, surface, image, bbox, ref polygon, opacity, needAA);

            // Draw the second polygon
            polygon.v0 = vertices[1];
            polygon.v1 = vertices[2];
            polygon.v2 = vertices[3];
            _rasterPolygonImage(ref state, surface, image, bbox, ref polygon, opacity, needAA);

            return true;
        }
    }
}
