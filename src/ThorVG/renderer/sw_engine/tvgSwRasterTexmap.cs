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
        // Shared state for texmap (not thread-safe, matches C++)
        [ThreadStatic] private static float _dudx, _dvdx;
        [ThreadStatic] private static float _dxdya, _dxdyb, _dudya, _dvdya;
        [ThreadStatic] private static float _xa, _xb, _ua, _va;

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

        private static bool _rasterMaskedPolygonImageSegment(SwSurface surface, SwImage image, in RenderRegion bbox, int yStart, int yEnd, byte opacity, bool needAA)
        {
            // Not implemented in C++ either
            return false;
        }

        private static void _rasterBlendingPolygonImageSegment(SwSurface surface, SwImage image, in RenderRegion bbox, int yStart, int yEnd, byte opacity, bool needAA)
        {
            if (surface.channelSize == sizeof(byte)) return;

            var localDudx = _dudx; var localDvdx = _dvdx;
            var localDxdya = _dxdya; var localDxdyb = _dxdyb;
            var localDudya = _dudya; var localDvdya = _dvdya;
            var localXa = _xa; var localXb = _xb;
            var localUa = _ua; var localVa = _va;
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
                    float u = localUa + dx * localDudx;
                    float v = localVa + dx * localDvdx;
                    var buf = dbuf + ((y * surface.stride) + x1);
                    int x = x1;

                    while (x++ < x2)
                    {
                        int uu = (int)u;
                        int vv = (int)v;
                        if ((uint)uu >= image.w || (uint)vv >= image.h) { u += localDudx; v += localDvdx; ++buf; continue; }

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

                        *buf = INTERPOLATE(surface.blender!(rasterUnpremultiply((uint)px), *buf), *buf, (byte)MULTIPLY(opacity, A((uint)px)));
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
            _xa = localXa; _xb = localXb; _ua = localUa; _va = localVa;
        }

        private static void _rasterPolygonImageSegment32(SwSurface surface, SwImage image, in RenderRegion bbox, int yStart, int yEnd, byte opacity, bool matting, bool needAA)
        {
            var localDudx = _dudx; var localDvdx = _dvdx;
            var localDxdya = _dxdya; var localDxdyb = _dxdyb;
            var localDudya = _dudya; var localDvdya = _dvdya;
            var localXa = _xa; var localXb = _xb;
            var localUa = _ua; var localVa = _va;
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
                    float u = localUa + dx * localDudx;
                    float v = localVa + dx * localDvdx;
                    var buf = dbuf + ((y * surface.stride) + x1);
                    int x = x1;

                    byte* cmp = null;
                    if (matting) cmp = &surface.compositor!.image.buf8[(y * surface.compositor.image.stride + x1) * csize];

                    while (x++ < x2)
                    {
                        int uu = (int)u;
                        int vv = (int)v;
                        if ((uint)uu >= image.w || (uint)vv >= image.h) { u += localDudx; v += localDvdx; ++buf; if (matting) cmp += csize; continue; }

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
            _xa = localXa; _xb = localXb; _ua = localUa; _va = localVa;
        }

        private static void _rasterPolygonImageSegment8(SwSurface surface, SwImage image, in RenderRegion bbox, int yStart, int yEnd, byte opacity, bool needAA)
        {
            var localDudx = _dudx; var localDvdx = _dvdx;
            var localDxdya = _dxdya; var localDxdyb = _dxdyb;
            var localDudya = _dudya; var localDvdya = _dvdya;
            var localXa = _xa; var localXb = _xb;
            var localUa = _ua; var localVa = _va;
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
                    float u = localUa + dx * localDudx;
                    float v = localVa + dx * localDvdx;
                    var buf = dbuf + ((y * surface.stride) + x1);
                    int x = x1;

                    while (x++ < x2)
                    {
                        var uu = (int)u;
                        var vv = (int)v;
                        if ((uint)uu >= image.w || (uint)vv >= image.h) { u += localDudx; v += localDvdx; ++buf; continue; }

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
            _xa = localXa; _xb = localXb; _ua = localUa; _va = localVa;
        }

        private static void _rasterPolygonImageSegment(SwSurface surface, SwImage image, in RenderRegion bbox, int yStart, int yEnd, byte opacity, bool matting, bool needAA)
        {
            if (surface.channelSize == sizeof(uint)) _rasterPolygonImageSegment32(surface, image, bbox, yStart, yEnd, opacity, matting, needAA);
            else if (surface.channelSize == sizeof(byte)) _rasterPolygonImageSegment8(surface, image, bbox, yStart, yEnd, opacity, needAA);
        }

        private static void _rasterPolygonImage(SwSurface surface, SwImage image, in RenderRegion bbox, ref Polygon polygon, byte opacity, bool needAA)
        {
            Span<float> xArr = stackalloc float[] { polygon.v0.pt.x, polygon.v1.pt.x, polygon.v2.pt.x };
            Span<float> yArr = stackalloc float[] { polygon.v0.pt.y, polygon.v1.pt.y, polygon.v2.pt.y };
            Span<float> uArr = stackalloc float[] { polygon.v0.uv.x, polygon.v1.uv.x, polygon.v2.uv.x };
            Span<float> vArr = stackalloc float[] { polygon.v0.uv.y, polygon.v1.uv.y, polygon.v2.uv.y };

            Span<float> dxdy = stackalloc float[3];
            var upper = false;

            // Sort the vertices in ascending Y order
            if (yArr[0] > yArr[1]) { (xArr[0], xArr[1]) = (xArr[1], xArr[0]); (yArr[0], yArr[1]) = (yArr[1], yArr[0]); (uArr[0], uArr[1]) = (uArr[1], uArr[0]); (vArr[0], vArr[1]) = (vArr[1], vArr[0]); }
            if (yArr[0] > yArr[2]) { (xArr[0], xArr[2]) = (xArr[2], xArr[0]); (yArr[0], yArr[2]) = (yArr[2], yArr[0]); (uArr[0], uArr[2]) = (uArr[2], uArr[0]); (vArr[0], vArr[2]) = (vArr[2], vArr[0]); }
            if (yArr[1] > yArr[2]) { (xArr[1], xArr[2]) = (xArr[2], xArr[1]); (yArr[1], yArr[2]) = (yArr[2], yArr[1]); (uArr[1], uArr[2]) = (uArr[2], uArr[1]); (vArr[1], vArr[2]) = (vArr[2], vArr[1]); }

            Span<int> yi = stackalloc int[] { (int)yArr[0], (int)yArr[1], (int)yArr[2] };

            if ((yi[0] == yi[1] && yi[0] == yi[2]) || ((int)xArr[0] == (int)xArr[1] && (int)xArr[0] == (int)xArr[2])) return;

            var denom = ((xArr[2] - xArr[0]) * (yArr[1] - yArr[0]) - (xArr[1] - xArr[0]) * (yArr[2] - yArr[0]));
            if (TvgMath.Zero(denom)) return;

            denom = 1 / denom;
            _dudx = ((uArr[2] - uArr[0]) * (yArr[1] - yArr[0]) - (uArr[1] - uArr[0]) * (yArr[2] - yArr[0])) * denom;
            _dvdx = ((vArr[2] - vArr[0]) * (yArr[1] - yArr[0]) - (vArr[1] - vArr[0]) * (yArr[2] - yArr[0])) * denom;
            var dudy = ((uArr[1] - uArr[0]) * (xArr[2] - xArr[0]) - (uArr[2] - uArr[0]) * (xArr[1] - xArr[0])) * denom;
            var dvdy = ((vArr[1] - vArr[0]) * (xArr[2] - xArr[0]) - (vArr[2] - vArr[0]) * (xArr[1] - xArr[0])) * denom;

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
                _dxdya = dxdy[1];
                _dudya = _dxdya * _dudx + dudy;
                _dvdya = _dxdya * _dvdx + dvdy;

                var dy = 1.0f - (yArr[0] - yi[0]);
                _xa = xArr[0] + dy * _dxdya;
                _ua = uArr[0] + dy * _dudya;
                _va = vArr[0] + dy * _dvdya;

                if (yi[0] < yi[1])
                {
                    var off_y = yArr[0] < bbox.min.y ? (bbox.min.y - yArr[0]) : 0;
                    _xa += off_y * _dxdya; _ua += off_y * _dudya; _va += off_y * _dvdya;
                    _dxdyb = dxdy[0];
                    _xb = xArr[0] + dy * _dxdyb + off_y * _dxdyb;

                    if (compositing) { if (_matting(surface)) _rasterPolygonImageSegment(surface, image, bbox, yi[0], yi[1], opacity, true, needAA); else _rasterMaskedPolygonImageSegment(surface, image, bbox, yi[0], yi[1], opacity, needAA); }
                    else if (blending) _rasterBlendingPolygonImageSegment(surface, image, bbox, yi[0], yi[1], opacity, needAA);
                    else _rasterPolygonImageSegment(surface, image, bbox, yi[0], yi[1], opacity, false, needAA);
                    upper = true;
                }
                if (yi[1] < yi[2])
                {
                    var off_y = yArr[1] < bbox.min.y ? (bbox.min.y - yArr[1]) : 0;
                    if (!upper) { _xa += off_y * _dxdya; _ua += off_y * _dudya; _va += off_y * _dvdya; }
                    _dxdyb = dxdy[2];
                    _xb = xArr[1] + (1 - (yArr[1] - yi[1])) * _dxdyb + off_y * _dxdyb;

                    if (compositing) { if (_matting(surface)) _rasterPolygonImageSegment(surface, image, bbox, yi[1], yi[2], opacity, true, needAA); else _rasterMaskedPolygonImageSegment(surface, image, bbox, yi[1], yi[2], opacity, needAA); }
                    else if (blending) _rasterBlendingPolygonImageSegment(surface, image, bbox, yi[1], yi[2], opacity, needAA);
                    else _rasterPolygonImageSegment(surface, image, bbox, yi[1], yi[2], opacity, false, needAA);
                }
            }
            else
            {
                _dxdyb = dxdy[1];
                var dy = 1.0f - (yArr[0] - yi[0]);
                _xb = xArr[0] + dy * _dxdyb;

                if (yi[0] < yi[1])
                {
                    var off_y = yArr[0] < bbox.min.y ? (bbox.min.y - yArr[0]) : 0;
                    _xb += off_y * _dxdyb;

                    _dxdya = dxdy[0];
                    _dudya = _dxdya * _dudx + dudy;
                    _dvdya = _dxdya * _dvdx + dvdy;

                    _xa = xArr[0] + dy * _dxdya + off_y * _dxdya;
                    _ua = uArr[0] + dy * _dudya + off_y * _dudya;
                    _va = vArr[0] + dy * _dvdya + off_y * _dvdya;

                    if (compositing) { if (_matting(surface)) _rasterPolygonImageSegment(surface, image, bbox, yi[0], yi[1], opacity, true, needAA); else _rasterMaskedPolygonImageSegment(surface, image, bbox, yi[0], yi[1], opacity, needAA); }
                    else if (blending) _rasterBlendingPolygonImageSegment(surface, image, bbox, yi[0], yi[1], opacity, needAA);
                    else _rasterPolygonImageSegment(surface, image, bbox, yi[0], yi[1], opacity, false, needAA);
                    upper = true;
                }
                if (yi[1] < yi[2])
                {
                    var off_y = yArr[1] < bbox.min.y ? (bbox.min.y - yArr[1]) : 0;
                    if (!upper) _xb += off_y * _dxdyb;

                    _dxdya = dxdy[2];
                    _dudya = _dxdya * _dudx + dudy;
                    _dvdya = _dxdya * _dvdx + dvdy;
                    dy = 1 - (yArr[1] - yi[1]);
                    _xa = xArr[1] + dy * _dxdya + off_y * _dxdya;
                    _ua = uArr[1] + dy * _dudya + off_y * _dudya;
                    _va = vArr[1] + dy * _dvdya + off_y * _dvdya;

                    if (compositing) { if (_matting(surface)) _rasterPolygonImageSegment(surface, image, bbox, yi[1], yi[2], opacity, true, needAA); else _rasterMaskedPolygonImageSegment(surface, image, bbox, yi[1], yi[2], opacity, needAA); }
                    else if (blending) _rasterBlendingPolygonImageSegment(surface, image, bbox, yi[1], yi[2], opacity, needAA);
                    else _rasterPolygonImageSegment(surface, image, bbox, yi[1], yi[2], opacity, false, needAA);
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

            Polygon polygon;

            // Draw the first polygon
            polygon.v0 = vertices[0];
            polygon.v1 = vertices[1];
            polygon.v2 = vertices[3];
            _rasterPolygonImage(surface, image, bbox, ref polygon, opacity, needAA);

            // Draw the second polygon
            polygon.v0 = vertices[1];
            polygon.v1 = vertices[2];
            polygon.v2 = vertices[3];
            _rasterPolygonImage(surface, image, bbox, ref polygon, opacity, needAA);

            return true;
        }
    }
}
