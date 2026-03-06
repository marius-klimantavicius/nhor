// Ported from ThorVG/src/renderer/sw_engine/tvgSwFill.cpp

using System;
using System.Runtime.CompilerServices;
using static ThorVG.SwHelper;
using static ThorVG.SwBlendOps;

namespace ThorVG
{
    public static unsafe class SwFillOps
    {
        private const float RADIAL_A_THRESHOLD = 0.0005f;
        private const int FIXPT_BITS = 8;
        private const int FIXPT_SIZE = (1 << FIXPT_BITS);

        // =====================================================================
        //  Internal helpers
        // =====================================================================

        private static void _calculateCoefficients(SwFill fill, uint x, uint y,
            out float b, out float deltaB, out float det, out float deltaDet, out float deltaDeltaDet)
        {
            var radial = fill.radial;

            var rx = (x + 0.5f) * radial.a11 + (y + 0.5f) * radial.a12 + radial.a13 - radial.fx;
            var ry = (x + 0.5f) * radial.a21 + (y + 0.5f) * radial.a22 + radial.a23 - radial.fy;

            b = (radial.dr * radial.fr + rx * radial.dx + ry * radial.dy) * radial.invA;
            deltaB = (radial.a11 * radial.dx + radial.a21 * radial.dy) * radial.invA;

            var rr = rx * rx + ry * ry;
            var deltaRr = 2.0f * (rx * radial.a11 + ry * radial.a21) * radial.invA;
            var deltaDeltaRr = 2.0f * (radial.a11 * radial.a11 + radial.a21 * radial.a21) * radial.invA;

            det = b * b + (rr - radial.fr * radial.fr) * radial.invA;
            deltaDet = 2.0f * b * deltaB + deltaB * deltaB + deltaRr + deltaDeltaRr * 0.5f;
            deltaDeltaDet = 2.0f * deltaB * deltaB + deltaDeltaRr;
        }

        private static uint _estimateAAMargin(Fill fdata)
        {
            const float marginScalingFactor = 800.0f;

            if (fdata.GetFillType() == Type.RadialGradient)
            {
                var rg = (RadialGradient)fdata;
                rg.Radial(out _, out _, out var radius, out _, out _, out _);
                return TvgMath.Zero(radius) ? 0u : (uint)(marginScalingFactor / radius);
            }
            else
            {
                var lg = (LinearGradient)fdata;
                lg.Linear(out var x1, out var y1, out var x2, out var y2);
                var len = TvgMath.PointLength(new Point(x1, y1), new Point(x2, y2));
                return TvgMath.Zero(len) ? 0u : (uint)(marginScalingFactor / len);
            }
        }

        private static void _adjustAAMargin(ref uint iMargin, uint index)
        {
            const float threshold = 0.1f;
            const uint iMarginMax = 40;

            var iThreshold = (uint)(index * threshold);
            if (iMargin > iThreshold) iMargin = iThreshold;
            if (iMargin > iMarginMax) iMargin = iMarginMax;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint _alphaUnblend(uint c)
        {
            var a = (c >> 24);
            if (a == 255 || a == 0) return c;
            var invA = 255.0f / (float)a;
            var c0 = (byte)((float)((c >> 16) & 0xFF) * invA);
            var c1 = (byte)((float)((c >> 8) & 0xFF) * invA);
            var c2 = (byte)((float)(c & 0xFF) * invA);
            return (a << 24) | ((uint)c0 << 16) | ((uint)c1 << 8) | c2;
        }

        private static void _applyAA(SwFill fill, uint begin, uint end)
        {
            if (begin == 0 || end == 0) return;

            var i = (uint)(SwConstants.SW_COLOR_TABLE - end);
            var rgbaEnd = _alphaUnblend(fill.ctable.data[i]);
            var rgbaBegin = _alphaUnblend(fill.ctable.data[begin]);

            var dt = 1.0f / (begin + end + 1.0f);
            float t = dt;
            while (i != begin)
            {
                var dist = (byte)(255 - (int)(255 * t));
                var color = INTERPOLATE(rgbaEnd, rgbaBegin, dist);
                fill.ctable.data[i++] = ALPHA_BLEND(color | 0xff000000u, (color >> 24));

                if (i == SwConstants.SW_COLOR_TABLE) i = 0;
                t += dt;
            }
        }

        private static bool _updateColorTable(SwFill fill, Fill fdata, SwSurface surface, byte opacity)
        {
            if (fill.solid) return true;

            var cnt = fdata.GetColorStops(out var colors);
            if (cnt == 0 || colors == null) return false;

            var pIdx = 0;
            var pColor = colors[pIdx];

            var a = (byte)MULTIPLY(pColor.a, opacity);
            if (a < 255) fill.translucent = true;

            var r = pColor.r;
            var g = pColor.g;
            var b = pColor.b;
            var rgba = surface.join!(r, g, b, a);
            var inc = 1.0f / (float)SwConstants.SW_COLOR_TABLE;
            var pos = 1.5f * inc;
            uint i = 0;

            // If repeat, anti-aliasing must be applied between the last and first colors.
            var repeat = fill.spread == FillSpread.Repeat;
            uint iAABegin = repeat ? _estimateAAMargin(fdata) : 0;
            uint iAAEnd = 0;

            fill.ctable.data[i++] = ALPHA_BLEND(rgba | 0xff000000u, a);

            while (pos <= pColor.offset)
            {
                fill.ctable.data[i] = fill.ctable.data[i - 1];
                ++i;
                pos += inc;
            }

            for (uint j = 0; j < cnt - 1; ++j)
            {
                if (repeat && j == cnt - 2 && iAAEnd == 0)
                {
                    iAAEnd = iAABegin;
                    _adjustAAMargin(ref iAAEnd, (uint)(SwConstants.SW_COLOR_TABLE - i));
                }

                var curr = colors[j];
                var next = colors[j + 1];
                var div = next.offset - curr.offset;
                var delta = div != 0.0f ? (1.0f / div) : 0.0f;
                var a2 = (byte)MULTIPLY(next.a, opacity);

                if (!fill.translucent && a2 < 255) fill.translucent = true;

                var rgba2 = surface.join!(next.r, next.g, next.b, a2);

                while (pos < next.offset && i < SwConstants.SW_COLOR_TABLE)
                {
                    var t = (pos - curr.offset) * delta;
                    var dist = (byte)(255 * t);
                    var dist2 = (byte)(255 - dist);
                    var color = INTERPOLATE(rgba, rgba2, dist2);
                    fill.ctable.data[i] = ALPHA_BLEND(color | 0xff000000u, (color >> 24));
                    ++i;
                    pos += inc;
                }
                rgba = rgba2;
                a = a2;

                if (repeat && j == 0) _adjustAAMargin(ref iAABegin, i - 1);
            }
            rgba = ALPHA_BLEND(rgba | 0xff000000u, a);

            for (; i < SwConstants.SW_COLOR_TABLE; ++i)
            {
                fill.ctable.data[i] = rgba;
            }

            if (repeat) _applyAA(fill, iAABegin, iAAEnd);
            else fill.ctable.data[SwConstants.SW_COLOR_TABLE - 1] = rgba;

            return true;
        }

        private static bool _prepareLinear(SwFill fill, LinearGradient linear, in Matrix pTransform)
        {
            linear.Linear(out var x1, out var y1, out var x2, out var y2);

            fill.linear.dx = x2 - x1;
            fill.linear.dy = y2 - y1;
            var len = fill.linear.dx * fill.linear.dx + fill.linear.dy * fill.linear.dy;

            if (len < MathConstants.FLOAT_EPSILON)
            {
                if (TvgMath.Zero(fill.linear.dx) && TvgMath.Zero(fill.linear.dy))
                {
                    fill.solid = true;
                }
                return true;
            }

            fill.linear.dx /= len;
            fill.linear.dy /= len;
            fill.linear.offset = -fill.linear.dx * x1 - fill.linear.dy * y1;

            var transform = TvgMath.Multiply(pTransform, linear.GetTransform());

            if (!TvgMath.Inverse(transform, out var itransform)) return false;

            fill.linear.offset += fill.linear.dx * itransform.e13 + fill.linear.dy * itransform.e23;

            var dx = fill.linear.dx;
            fill.linear.dx = dx * itransform.e11 + fill.linear.dy * itransform.e21;
            fill.linear.dy = dx * itransform.e12 + fill.linear.dy * itransform.e22;

            return true;
        }

        private static bool _prepareRadial(SwFill fill, RadialGradient radial, in Matrix pTransform)
        {
            radial.Radial(out var cx, out var cy, out var r, out var fx, out var fy, out var fr);
            fill.solid = !radial.Correct(ref fx, ref fy, ref fr);
            if (fill.solid) return true;

            fill.radial.dr = r - fr;
            fill.radial.dx = cx - fx;
            fill.radial.dy = cy - fy;
            fill.radial.fr = fr;
            fill.radial.fx = fx;
            fill.radial.fy = fy;
            fill.radial.a = fill.radial.dr * fill.radial.dr - fill.radial.dx * fill.radial.dx - fill.radial.dy * fill.radial.dy;
            const float precision = 0.01f;
            if (fill.radial.a < precision) fill.radial.a = precision;
            fill.radial.invA = 1.0f / fill.radial.a;

            var transform = TvgMath.Multiply(pTransform, radial.GetTransform());

            if (!TvgMath.Inverse(transform, out var itransform)) return false;

            fill.radial.a11 = itransform.e11;
            fill.radial.a12 = itransform.e12;
            fill.radial.a13 = itransform.e13;
            fill.radial.a21 = itransform.e21;
            fill.radial.a22 = itransform.e22;
            fill.radial.a23 = itransform.e23;

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint _clamp(SwFill fill, int pos)
        {
            switch (fill.spread)
            {
                case FillSpread.Pad:
                    if (pos >= SwConstants.SW_COLOR_TABLE) pos = SwConstants.SW_COLOR_TABLE - 1;
                    else if (pos < 0) pos = 0;
                    break;
                case FillSpread.Repeat:
                    pos = pos % SwConstants.SW_COLOR_TABLE;
                    if (pos < 0) pos = SwConstants.SW_COLOR_TABLE + pos;
                    break;
                case FillSpread.Reflect:
                    var limit = SwConstants.SW_COLOR_TABLE * 2;
                    pos = pos % limit;
                    if (pos < 0) pos = limit + pos;
                    if (pos >= SwConstants.SW_COLOR_TABLE) pos = (limit - pos - 1);
                    break;
            }
            return (uint)pos;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint _fixedPixel(SwFill fill, int pos)
        {
            int i = (pos + (FIXPT_SIZE / 2)) >> FIXPT_BITS;
            return fill.ctable.data[_clamp(fill, i)];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint _pixel(SwFill fill, float pos)
        {
            var i = (int)(pos * (SwConstants.SW_COLOR_TABLE - 1) + 0.5f);
            return fill.ctable.data[_clamp(fill, i)];
        }

        // =====================================================================
        //  fillRadial overloads
        // =====================================================================

        public static void fillRadial(SwFill fill, uint* dst, uint y, uint x, uint len, byte* cmp, SwAlpha alpha, byte csize, byte opacity)
        {
            if (fill.radial.a < RADIAL_A_THRESHOLD)
            {
                var radial = fill.radial;
                var rx = (x + 0.5f) * radial.a11 + (y + 0.5f) * radial.a12 + radial.a13 - radial.fx;
                var ry = (x + 0.5f) * radial.a21 + (y + 0.5f) * radial.a22 + radial.a23 - radial.fy;

                if (opacity == 255)
                {
                    for (uint i = 0; i < len; ++i, ++dst, cmp += csize)
                    {
                        var x0 = 0.5f * (rx * rx + ry * ry - radial.fr * radial.fr) / (radial.dr * radial.fr + rx * radial.dx + ry * radial.dy);
                        *dst = opBlendNormal(_pixel(fill, x0), *dst, alpha(cmp));
                        rx += radial.a11;
                        ry += radial.a21;
                    }
                }
                else
                {
                    for (uint i = 0; i < len; ++i, ++dst, cmp += csize)
                    {
                        var x0 = 0.5f * (rx * rx + ry * ry - radial.fr * radial.fr) / (radial.dr * radial.fr + rx * radial.dx + ry * radial.dy);
                        *dst = opBlendNormal(_pixel(fill, x0), *dst, (byte)MULTIPLY(opacity, alpha(cmp)));
                        rx += radial.a11;
                        ry += radial.a21;
                    }
                }
            }
            else
            {
                _calculateCoefficients(fill, x, y, out var b, out var deltaB, out var det, out var deltaDet, out var deltaDeltaDet);

                if (opacity == 255)
                {
                    for (uint i = 0; i < len; ++i, ++dst, cmp += csize)
                    {
                        *dst = opBlendNormal(_pixel(fill, MathF.Sqrt(det) - b), *dst, alpha(cmp));
                        det += deltaDet;
                        deltaDet += deltaDeltaDet;
                        b += deltaB;
                    }
                }
                else
                {
                    for (uint i = 0; i < len; ++i, ++dst, cmp += csize)
                    {
                        *dst = opBlendNormal(_pixel(fill, MathF.Sqrt(det) - b), *dst, (byte)MULTIPLY(opacity, alpha(cmp)));
                        det += deltaDet;
                        deltaDet += deltaDeltaDet;
                        b += deltaB;
                    }
                }
            }
        }

        public static void fillRadial(SwFill fill, uint* dst, uint y, uint x, uint len, SwBlenderA op, byte a)
        {
            if (fill.radial.a < RADIAL_A_THRESHOLD)
            {
                var radial = fill.radial;
                var rx = (x + 0.5f) * radial.a11 + (y + 0.5f) * radial.a12 + radial.a13 - radial.fx;
                var ry = (x + 0.5f) * radial.a21 + (y + 0.5f) * radial.a22 + radial.a23 - radial.fy;
                for (uint i = 0; i < len; ++i, ++dst)
                {
                    var x0 = 0.5f * (rx * rx + ry * ry - radial.fr * radial.fr) / (radial.dr * radial.fr + rx * radial.dx + ry * radial.dy);
                    *dst = op(_pixel(fill, x0), *dst, a);
                    rx += radial.a11;
                    ry += radial.a21;
                }
            }
            else
            {
                _calculateCoefficients(fill, x, y, out var b, out var deltaB, out var det, out var deltaDet, out var deltaDeltaDet);
                for (uint i = 0; i < len; ++i, ++dst)
                {
                    *dst = op(_pixel(fill, MathF.Sqrt(det) - b), *dst, a);
                    det += deltaDet;
                    deltaDet += deltaDeltaDet;
                    b += deltaB;
                }
            }
        }

        public static void fillRadial(SwFill fill, byte* dst, uint y, uint x, uint len, SwMask maskOp, byte a)
        {
            if (fill.radial.a < RADIAL_A_THRESHOLD)
            {
                var radial = fill.radial;
                var rx = (x + 0.5f) * radial.a11 + (y + 0.5f) * radial.a12 + radial.a13 - radial.fx;
                var ry = (x + 0.5f) * radial.a21 + (y + 0.5f) * radial.a22 + radial.a23 - radial.fy;
                for (uint i = 0; i < len; ++i, ++dst)
                {
                    var x0 = 0.5f * (rx * rx + ry * ry - radial.fr * radial.fr) / (radial.dr * radial.fr + rx * radial.dx + ry * radial.dy);
                    var src = (byte)MULTIPLY(a, A(_pixel(fill, x0)));
                    *dst = maskOp(src, *dst, (byte)~src);
                    rx += radial.a11;
                    ry += radial.a21;
                }
            }
            else
            {
                _calculateCoefficients(fill, x, y, out var b, out var deltaB, out var det, out var deltaDet, out var deltaDeltaDet);
                for (uint i = 0; i < len; ++i, ++dst)
                {
                    var src = (byte)MULTIPLY(a, A(_pixel(fill, MathF.Sqrt(det) - b)));
                    *dst = maskOp(src, *dst, (byte)~src);
                    det += deltaDet;
                    deltaDet += deltaDeltaDet;
                    b += deltaB;
                }
            }
        }

        public static void fillRadial(SwFill fill, byte* dst, uint y, uint x, uint len, byte* cmp, SwMask maskOp, byte a)
        {
            if (fill.radial.a < RADIAL_A_THRESHOLD)
            {
                var radial = fill.radial;
                var rx = (x + 0.5f) * radial.a11 + (y + 0.5f) * radial.a12 + radial.a13 - radial.fx;
                var ry = (x + 0.5f) * radial.a21 + (y + 0.5f) * radial.a22 + radial.a23 - radial.fy;
                for (uint i = 0; i < len; ++i, ++dst, ++cmp)
                {
                    var x0 = 0.5f * (rx * rx + ry * ry - radial.fr * radial.fr) / (radial.dr * radial.fr + rx * radial.dx + ry * radial.dy);
                    var src = (byte)MULTIPLY(A(A(_pixel(fill, x0))), a);
                    var tmp = maskOp(src, *cmp, 0);
                    *dst = (byte)(tmp + MULTIPLY(*dst, (byte)~tmp));
                    rx += radial.a11;
                    ry += radial.a21;
                }
            }
            else
            {
                _calculateCoefficients(fill, x, y, out var b, out var deltaB, out var det, out var deltaDet, out var deltaDeltaDet);
                for (uint i = 0; i < len; ++i, ++dst, ++cmp)
                {
                    var src = (byte)MULTIPLY(A(_pixel(fill, MathF.Sqrt(det))), a);
                    var tmp = maskOp(src, *cmp, 0);
                    *dst = (byte)(tmp + MULTIPLY(*dst, (byte)~tmp));
                    det += deltaDet;
                    deltaDet += deltaDeltaDet;
                    b += deltaB;
                }
            }
        }

        public static void fillRadial(SwFill fill, uint* dst, uint y, uint x, uint len, SwBlenderA op, SwBlender op2, byte a)
        {
            if (fill.radial.a < RADIAL_A_THRESHOLD)
            {
                var radial = fill.radial;
                var rx = (x + 0.5f) * radial.a11 + (y + 0.5f) * radial.a12 + radial.a13 - radial.fx;
                var ry = (x + 0.5f) * radial.a21 + (y + 0.5f) * radial.a22 + radial.a23 - radial.fy;

                if (a == 255)
                {
                    for (uint i = 0; i < len; ++i, ++dst)
                    {
                        var x0 = 0.5f * (rx * rx + ry * ry - radial.fr * radial.fr) / (radial.dr * radial.fr + rx * radial.dx + ry * radial.dy);
                        var tmp = op(_pixel(fill, x0), *dst, 255);
                        *dst = op2(tmp, *dst);
                        rx += radial.a11;
                        ry += radial.a21;
                    }
                }
                else
                {
                    for (uint i = 0; i < len; ++i, ++dst)
                    {
                        var x0 = 0.5f * (rx * rx + ry * ry - radial.fr * radial.fr) / (radial.dr * radial.fr + rx * radial.dx + ry * radial.dy);
                        var tmp = op(_pixel(fill, x0), *dst, 255);
                        var tmp2 = op2(tmp, *dst);
                        *dst = INTERPOLATE(tmp2, *dst, a);
                        rx += radial.a11;
                        ry += radial.a21;
                    }
                }
            }
            else
            {
                _calculateCoefficients(fill, x, y, out var b, out var deltaB, out var det, out var deltaDet, out var deltaDeltaDet);
                if (a == 255)
                {
                    for (uint i = 0; i < len; ++i, ++dst)
                    {
                        var tmp = op(_pixel(fill, MathF.Sqrt(det) - b), *dst, 255);
                        *dst = op2(tmp, *dst);
                        det += deltaDet;
                        deltaDet += deltaDeltaDet;
                        b += deltaB;
                    }
                }
                else
                {
                    for (uint i = 0; i < len; ++i, ++dst)
                    {
                        var tmp = op(_pixel(fill, MathF.Sqrt(det) - b), *dst, 255);
                        var tmp2 = op2(tmp, *dst);
                        *dst = INTERPOLATE(tmp2, *dst, a);
                        det += deltaDet;
                        deltaDet += deltaDeltaDet;
                        b += deltaB;
                    }
                }
            }
        }

        // =====================================================================
        //  fillLinear overloads
        // =====================================================================

        public static void fillLinear(SwFill fill, uint* dst, uint y, uint x, uint len, byte* cmp, SwAlpha alpha, byte csize, byte opacity)
        {
            float rx = x + 0.5f;
            float ry = y + 0.5f;
            float t = (fill.linear.dx * rx + fill.linear.dy * ry + fill.linear.offset) * (SwConstants.SW_COLOR_TABLE - 1);
            float inc2 = fill.linear.dx * (SwConstants.SW_COLOR_TABLE - 1);

            if (opacity == 255)
            {
                if (TvgMath.Zero(inc2))
                {
                    var color = _fixedPixel(fill, (int)(t * FIXPT_SIZE));
                    for (uint i = 0; i < len; ++i, ++dst, cmp += csize)
                    {
                        *dst = opBlendNormal(color, *dst, alpha(cmp));
                    }
                    return;
                }

                var vMax = (float)(int.MaxValue >> (FIXPT_BITS + 1));
                var vMin = -vMax;
                var v = t + (inc2 * len);

                if (v < vMax && v > vMin)
                {
                    var t2 = (int)(t * FIXPT_SIZE);
                    var inc2i = (int)(inc2 * FIXPT_SIZE);
                    for (uint j = 0; j < len; ++j, ++dst, cmp += csize)
                    {
                        *dst = opBlendNormal(_fixedPixel(fill, t2), *dst, alpha(cmp));
                        t2 += inc2i;
                    }
                }
                else
                {
                    uint counter = 0;
                    while (counter++ < len)
                    {
                        *dst = opBlendNormal(_pixel(fill, t / SwConstants.SW_COLOR_TABLE), *dst, alpha(cmp));
                        ++dst;
                        t += inc2;
                        cmp += csize;
                    }
                }
            }
            else
            {
                if (TvgMath.Zero(inc2))
                {
                    var color = _fixedPixel(fill, (int)(t * FIXPT_SIZE));
                    for (uint i = 0; i < len; ++i, ++dst, cmp += csize)
                    {
                        *dst = opBlendNormal(color, *dst, (byte)MULTIPLY(alpha(cmp), opacity));
                    }
                    return;
                }

                var vMax = (float)(int.MaxValue >> (FIXPT_BITS + 1));
                var vMin = -vMax;
                var v = t + (inc2 * len);

                if (v < vMax && v > vMin)
                {
                    var t2 = (int)(t * FIXPT_SIZE);
                    var inc2i = (int)(inc2 * FIXPT_SIZE);
                    for (uint j = 0; j < len; ++j, ++dst, cmp += csize)
                    {
                        *dst = opBlendNormal(_fixedPixel(fill, t2), *dst, (byte)MULTIPLY(alpha(cmp), opacity));
                        t2 += inc2i;
                    }
                }
                else
                {
                    uint counter = 0;
                    while (counter++ < len)
                    {
                        *dst = opBlendNormal(_pixel(fill, t / SwConstants.SW_COLOR_TABLE), *dst, (byte)MULTIPLY(opacity, alpha(cmp)));
                        ++dst;
                        t += inc2;
                        cmp += csize;
                    }
                }
            }
        }

        public static void fillLinear(SwFill fill, byte* dst, uint y, uint x, uint len, SwMask maskOp, byte a)
        {
            float rx = x + 0.5f;
            float ry = y + 0.5f;
            float t = (fill.linear.dx * rx + fill.linear.dy * ry + fill.linear.offset) * (SwConstants.SW_COLOR_TABLE - 1);
            float inc2 = fill.linear.dx * (SwConstants.SW_COLOR_TABLE - 1);

            if (TvgMath.Zero(inc2))
            {
                var src = (byte)MULTIPLY(a, A(_fixedPixel(fill, (int)(t * FIXPT_SIZE))));
                for (uint i = 0; i < len; ++i, ++dst)
                {
                    *dst = maskOp(src, *dst, (byte)~src);
                }
                return;
            }

            var vMax = (float)(int.MaxValue >> (FIXPT_BITS + 1));
            var vMin = -vMax;
            var v = t + (inc2 * len);

            if (v < vMax && v > vMin)
            {
                var t2 = (int)(t * FIXPT_SIZE);
                var inc2i = (int)(inc2 * FIXPT_SIZE);
                for (uint j = 0; j < len; ++j, ++dst)
                {
                    var src = (byte)MULTIPLY(A(_fixedPixel(fill, t2)), a);
                    *dst = maskOp(src, *dst, (byte)~src);
                    t2 += inc2i;
                }
            }
            else
            {
                uint counter = 0;
                while (counter++ < len)
                {
                    var src = (byte)MULTIPLY(A(_pixel(fill, t / SwConstants.SW_COLOR_TABLE)), a);
                    *dst = maskOp(src, *dst, (byte)~src);
                    ++dst;
                    t += inc2;
                }
            }
        }

        public static void fillLinear(SwFill fill, byte* dst, uint y, uint x, uint len, byte* cmp, SwMask maskOp, byte a)
        {
            float rx = x + 0.5f;
            float ry = y + 0.5f;
            float t = (fill.linear.dx * rx + fill.linear.dy * ry + fill.linear.offset) * (SwConstants.SW_COLOR_TABLE - 1);
            float inc2 = fill.linear.dx * (SwConstants.SW_COLOR_TABLE - 1);

            if (TvgMath.Zero(inc2))
            {
                var src = (byte)MULTIPLY(A(_fixedPixel(fill, (int)(t * FIXPT_SIZE))), a);
                for (uint i = 0; i < len; ++i, ++dst, ++cmp)
                {
                    var tmp = maskOp(src, *cmp, 0);
                    *dst = (byte)(tmp + MULTIPLY(*dst, (byte)~tmp));
                }
                return;
            }

            var vMax = (float)(int.MaxValue >> (FIXPT_BITS + 1));
            var vMin = -vMax;
            var v = t + (inc2 * len);

            if (v < vMax && v > vMin)
            {
                var t2 = (int)(t * FIXPT_SIZE);
                var inc2i = (int)(inc2 * FIXPT_SIZE);
                for (uint j = 0; j < len; ++j, ++dst, ++cmp)
                {
                    var src = (byte)MULTIPLY(a, A(_fixedPixel(fill, t2)));
                    var tmp = maskOp(src, *cmp, 0);
                    *dst = (byte)(tmp + MULTIPLY(*dst, (byte)~tmp));
                    t2 += inc2i;
                }
            }
            else
            {
                uint counter = 0;
                while (counter++ < len)
                {
                    var src = (byte)MULTIPLY(A(_pixel(fill, t / SwConstants.SW_COLOR_TABLE)), a);
                    var tmp = maskOp(src, *cmp, 0);
                    *dst = (byte)(tmp + MULTIPLY(*dst, (byte)~tmp));
                    ++dst;
                    ++cmp;
                    t += inc2;
                }
            }
        }

        public static void fillLinear(SwFill fill, uint* dst, uint y, uint x, uint len, SwBlenderA op, byte a)
        {
            float rx = x + 0.5f;
            float ry = y + 0.5f;
            float t = (fill.linear.dx * rx + fill.linear.dy * ry + fill.linear.offset) * (SwConstants.SW_COLOR_TABLE - 1);
            float inc2 = fill.linear.dx * (SwConstants.SW_COLOR_TABLE - 1);

            if (TvgMath.Zero(inc2))
            {
                var color = _fixedPixel(fill, (int)(t * FIXPT_SIZE));
                for (uint i = 0; i < len; ++i, ++dst)
                {
                    *dst = op(color, *dst, a);
                }
                return;
            }

            var vMax = (float)(int.MaxValue >> (FIXPT_BITS + 1));
            var vMin = -vMax;
            var v = t + (inc2 * len);

            if (v < vMax && v > vMin)
            {
                var t2 = (int)(t * FIXPT_SIZE);
                var inc2i = (int)(inc2 * FIXPT_SIZE);
                for (uint j = 0; j < len; ++j, ++dst)
                {
                    *dst = op(_fixedPixel(fill, t2), *dst, a);
                    t2 += inc2i;
                }
            }
            else
            {
                uint counter = 0;
                while (counter++ < len)
                {
                    *dst = op(_pixel(fill, t / SwConstants.SW_COLOR_TABLE), *dst, a);
                    ++dst;
                    t += inc2;
                }
            }
        }

        public static void fillLinear(SwFill fill, uint* dst, uint y, uint x, uint len, SwBlenderA op, SwBlender op2, byte a)
        {
            float rx = x + 0.5f;
            float ry = y + 0.5f;
            float t = (fill.linear.dx * rx + fill.linear.dy * ry + fill.linear.offset) * (SwConstants.SW_COLOR_TABLE - 1);
            float inc2 = fill.linear.dx * (SwConstants.SW_COLOR_TABLE - 1);

            if (TvgMath.Zero(inc2))
            {
                var color = _fixedPixel(fill, (int)(t * FIXPT_SIZE));
                if (a == 255)
                {
                    for (uint i = 0; i < len; ++i, ++dst)
                    {
                        var tmp = op(color, *dst, a);
                        *dst = op2(tmp, *dst);
                    }
                }
                else
                {
                    for (uint i = 0; i < len; ++i, ++dst)
                    {
                        var tmp = op(color, *dst, a);
                        var tmp2 = op2(tmp, *dst);
                        *dst = INTERPOLATE(tmp2, *dst, a);
                    }
                }
                return;
            }

            var vMax = (float)(int.MaxValue >> (FIXPT_BITS + 1));
            var vMin = -vMax;
            var v = t + (inc2 * len);

            if (a == 255)
            {
                if (v < vMax && v > vMin)
                {
                    var t2 = (int)(t * FIXPT_SIZE);
                    var inc2i = (int)(inc2 * FIXPT_SIZE);
                    for (uint j = 0; j < len; ++j, ++dst)
                    {
                        var tmp = op(_fixedPixel(fill, t2), *dst, 255);
                        *dst = op2(tmp, *dst);
                        t2 += inc2i;
                    }
                }
                else
                {
                    uint counter = 0;
                    while (counter++ < len)
                    {
                        var tmp = op(_pixel(fill, t / SwConstants.SW_COLOR_TABLE), *dst, 255);
                        *dst = op2(tmp, *dst);
                        ++dst;
                        t += inc2;
                    }
                }
            }
            else
            {
                if (v < vMax && v > vMin)
                {
                    var t2 = (int)(t * FIXPT_SIZE);
                    var inc2i = (int)(inc2 * FIXPT_SIZE);
                    for (uint j = 0; j < len; ++j, ++dst)
                    {
                        var tmp = op(_fixedPixel(fill, t2), *dst, 255);
                        var tmp2 = op2(tmp, *dst);
                        *dst = INTERPOLATE(tmp2, *dst, a);
                        t2 += inc2i;
                    }
                }
                else
                {
                    uint counter = 0;
                    while (counter++ < len)
                    {
                        var tmp = op(_pixel(fill, t / SwConstants.SW_COLOR_TABLE), *dst, 255);
                        var tmp2 = op2(tmp, *dst);
                        *dst = INTERPOLATE(tmp2, *dst, a);
                        ++dst;
                        t += inc2;
                    }
                }
            }
        }

        // =====================================================================
        //  Generic (monomorphized) overloads — SwBlenderA → IBlenderAOp
        // =====================================================================

        public static void fillLinear<TOp>(SwFill fill, uint* dst, uint y, uint x, uint len, byte a) where TOp : struct, IBlenderAOp
        {
            float rx = x + 0.5f;
            float ry = y + 0.5f;
            float t = (fill.linear.dx * rx + fill.linear.dy * ry + fill.linear.offset) * (SwConstants.SW_COLOR_TABLE - 1);
            float inc2 = fill.linear.dx * (SwConstants.SW_COLOR_TABLE - 1);

            if (TvgMath.Zero(inc2))
            {
                var color = _fixedPixel(fill, (int)(t * FIXPT_SIZE));
                for (uint i = 0; i < len; ++i, ++dst)
                {
                    *dst = TOp.Apply(color, *dst, a);
                }
                return;
            }

            var vMax = (float)(int.MaxValue >> (FIXPT_BITS + 1));
            var vMin = -vMax;
            var v = t + (inc2 * len);

            if (v < vMax && v > vMin)
            {
                var t2 = (int)(t * FIXPT_SIZE);
                var inc2i = (int)(inc2 * FIXPT_SIZE);
                for (uint j = 0; j < len; ++j, ++dst)
                {
                    *dst = TOp.Apply(_fixedPixel(fill, t2), *dst, a);
                    t2 += inc2i;
                }
            }
            else
            {
                uint counter = 0;
                while (counter++ < len)
                {
                    *dst = TOp.Apply(_pixel(fill, t / SwConstants.SW_COLOR_TABLE), *dst, a);
                    ++dst;
                    t += inc2;
                }
            }
        }

        public static void fillRadial<TOp>(SwFill fill, uint* dst, uint y, uint x, uint len, byte a) where TOp : struct, IBlenderAOp
        {
            if (fill.radial.a < RADIAL_A_THRESHOLD)
            {
                var radial = fill.radial;
                var rx = (x + 0.5f) * radial.a11 + (y + 0.5f) * radial.a12 + radial.a13 - radial.fx;
                var ry = (x + 0.5f) * radial.a21 + (y + 0.5f) * radial.a22 + radial.a23 - radial.fy;
                for (uint i = 0; i < len; ++i, ++dst)
                {
                    var x0 = 0.5f * (rx * rx + ry * ry - radial.fr * radial.fr) / (radial.dr * radial.fr + rx * radial.dx + ry * radial.dy);
                    *dst = TOp.Apply(_pixel(fill, x0), *dst, a);
                    rx += radial.a11;
                    ry += radial.a21;
                }
            }
            else
            {
                _calculateCoefficients(fill, x, y, out var b, out var deltaB, out var det, out var deltaDet, out var deltaDeltaDet);
                for (uint i = 0; i < len; ++i, ++dst)
                {
                    *dst = TOp.Apply(_pixel(fill, MathF.Sqrt(det) - b), *dst, a);
                    det += deltaDet;
                    deltaDet += deltaDeltaDet;
                    b += deltaB;
                }
            }
        }

        // =====================================================================
        //  Generic (monomorphized) overloads — SwMask → IMaskOp
        // =====================================================================

        public static void fillLinear<TMask>(SwFill fill, byte* dst, uint y, uint x, uint len, byte a) where TMask : struct, IMaskOp
        {
            float rx = x + 0.5f;
            float ry = y + 0.5f;
            float t = (fill.linear.dx * rx + fill.linear.dy * ry + fill.linear.offset) * (SwConstants.SW_COLOR_TABLE - 1);
            float inc2 = fill.linear.dx * (SwConstants.SW_COLOR_TABLE - 1);

            if (TvgMath.Zero(inc2))
            {
                var src = (byte)MULTIPLY(a, A(_fixedPixel(fill, (int)(t * FIXPT_SIZE))));
                for (uint i = 0; i < len; ++i, ++dst)
                {
                    *dst = TMask.Apply(src, *dst, (byte)~src);
                }
                return;
            }

            var vMax = (float)(int.MaxValue >> (FIXPT_BITS + 1));
            var vMin = -vMax;
            var v = t + (inc2 * len);

            if (v < vMax && v > vMin)
            {
                var t2 = (int)(t * FIXPT_SIZE);
                var inc2i = (int)(inc2 * FIXPT_SIZE);
                for (uint j = 0; j < len; ++j, ++dst)
                {
                    var src = (byte)MULTIPLY(A(_fixedPixel(fill, t2)), a);
                    *dst = TMask.Apply(src, *dst, (byte)~src);
                    t2 += inc2i;
                }
            }
            else
            {
                uint counter = 0;
                while (counter++ < len)
                {
                    var src = (byte)MULTIPLY(A(_pixel(fill, t / SwConstants.SW_COLOR_TABLE)), a);
                    *dst = TMask.Apply(src, *dst, (byte)~src);
                    ++dst;
                    t += inc2;
                }
            }
        }

        public static void fillRadial<TMask>(SwFill fill, byte* dst, uint y, uint x, uint len, byte a) where TMask : struct, IMaskOp
        {
            if (fill.radial.a < RADIAL_A_THRESHOLD)
            {
                var radial = fill.radial;
                var rx = (x + 0.5f) * radial.a11 + (y + 0.5f) * radial.a12 + radial.a13 - radial.fx;
                var ry = (x + 0.5f) * radial.a21 + (y + 0.5f) * radial.a22 + radial.a23 - radial.fy;
                for (uint i = 0; i < len; ++i, ++dst)
                {
                    var x0 = 0.5f * (rx * rx + ry * ry - radial.fr * radial.fr) / (radial.dr * radial.fr + rx * radial.dx + ry * radial.dy);
                    var src = (byte)MULTIPLY(a, A(_pixel(fill, x0)));
                    *dst = TMask.Apply(src, *dst, (byte)~src);
                    rx += radial.a11;
                    ry += radial.a21;
                }
            }
            else
            {
                _calculateCoefficients(fill, x, y, out var b, out var deltaB, out var det, out var deltaDet, out var deltaDeltaDet);
                for (uint i = 0; i < len; ++i, ++dst)
                {
                    var src = (byte)MULTIPLY(a, A(_pixel(fill, MathF.Sqrt(det) - b)));
                    *dst = TMask.Apply(src, *dst, (byte)~src);
                    det += deltaDet;
                    deltaDet += deltaDeltaDet;
                    b += deltaB;
                }
            }
        }

        // =====================================================================
        //  Public API
        // =====================================================================

        public static bool fillGenColorTable(SwFill fill, Fill fdata, in Matrix transform, SwSurface surface, byte opacity, bool ctable)
        {
            if (fill == null) return false;

            fill.spread = fdata.GetSpread();

            if (fdata.GetFillType() == Type.LinearGradient)
            {
                if (!_prepareLinear(fill, (LinearGradient)fdata, transform)) return false;
            }
            else if (fdata.GetFillType() == Type.RadialGradient)
            {
                if (!_prepareRadial(fill, (RadialGradient)fdata, transform)) return false;
            }

            if (ctable) return _updateColorTable(fill, fdata, surface, opacity);
            return true;
        }

        public static Fill.ColorStop? fillFetchSolid(SwFill fill, Fill fdata)
        {
            if (!fill.solid) return null;

            var cnt = fdata.GetColorStops(out var colors);
            if (cnt == 0 || colors == null) return null;

            return colors[cnt - 1];
        }

        public static void fillReset(SwFill fill)
        {
            fill.translucent = false;
            fill.solid = false;
        }

        public static void fillFree(SwFill? fill)
        {
            // No-op in managed C# - GC handles deallocation
        }
    }
}
