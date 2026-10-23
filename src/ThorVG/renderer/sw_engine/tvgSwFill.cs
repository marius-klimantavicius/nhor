// Ported from ThorVG/src/renderer/sw_engine/tvgSwFill.cpp

using System;
using System.Runtime.CompilerServices;
using static ThorVG.SwHelper;
using static ThorVG.SwBlendOps;

namespace ThorVG
{
    public static unsafe partial class SwFillOps
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
            // Match the pinned ARM compiler's contracted operation order; the
            // helper retains separate multiply/add rounding on x86.
            var radial = fill.radial;

            var rx = TvgMath.MultiplyAdd(x + 0.5f, radial.a11, (y + 0.5f) * radial.a12) + radial.a13 - radial.fx;
            var ry = TvgMath.MultiplyAdd(x + 0.5f, radial.a21, (y + 0.5f) * radial.a22) + radial.a23 - radial.fy;

            b = TvgMath.MultiplyAdd(ry, radial.dy, TvgMath.MultiplyAdd(radial.dr, radial.fr, rx * radial.dx)) * radial.invA;
            deltaB = TvgMath.MultiplyAdd(radial.a11, radial.dx, radial.a21 * radial.dy) * radial.invA;

            var rr = TvgMath.MultiplyAdd(rx, rx, ry * ry);
            var twiceDeltaRr = 2.0f * TvgMath.MultiplyAdd(rx, radial.a11, ry * radial.a21);
            var deltaDeltaRr = 2.0f * TvgMath.MultiplyAdd(radial.a11, radial.a11, radial.a21 * radial.a21) * radial.invA;

            det = TvgMath.MultiplyAdd(b, b, TvgMath.MultiplyAdd(-radial.fr, radial.fr, rr) * radial.invA);
            deltaDet = TvgMath.MultiplyAdd(deltaDeltaRr, 0.5f,
                TvgMath.MultiplyAdd(twiceDeltaRr, radial.invA, TvgMath.MultiplyAdd(2.0f * b, deltaB, deltaB * deltaB)));
            deltaDeltaDet = TvgMath.MultiplyAdd(2.0f * deltaB, deltaB, deltaDeltaRr);
        }

        private const uint MARGIN_MAX = 40;

        private static void _adjustAAMargin(ref uint margin, uint count)
        {
            uint threshold = (uint)(count * 0.1f);
            if (margin > threshold) margin = threshold;
            if (margin > MARGIN_MAX) margin = MARGIN_MAX;
        }

        private static uint _alphaUnblend(uint c)
        {
            uint a = c >> 24;
            if (a == 255 || a == 0) return c;
            float invA = 255.0f / a;
            byte c0 = (byte)(((c >> 16) & 255) * invA);
            byte c1 = (byte)(((c >> 8) & 255) * invA);
            byte c2 = (byte)((c & 255) * invA);
            return (a << 24) | ((uint)c0 << 16) | ((uint)c1 << 8) | c2;
        }

        private static void _applyRepeatAA(SwFill fill, Fill.ColorStop[] colors, uint count)
        {
            if (count == 1) return;
            uint beginCount = (uint)MathF.Max(0, MathF.Floor(colors[1].offset * SwConstants.SW_COLOR_TABLE - 0.5f) + 1.0f);
            uint endCount = (uint)MathF.Max(0, MathF.Floor((1.0f - colors[count - 2].offset) * SwConstants.SW_COLOR_TABLE - 0.5f) + 1.0f);
            uint begin = TvgMath.Zero(fill.extent) ? 0 : (uint)(800.0f / fill.extent);
            _adjustAAMargin(ref begin, beginCount);
            uint end = begin;
            _adjustAAMargin(ref end, endCount);
            if (begin == 0 || end == 0) return;
            uint i = SwConstants.SW_COLOR_TABLE - end;
            uint colorEnd = _alphaUnblend(fill.ctable.data[i]);
            uint colorBegin = _alphaUnblend(fill.ctable.data[begin]);
            float step = 1.0f / (begin + end + 1.0f), t = step;
            while (i != begin)
            {
                byte distance = (byte)(255 - (int)(255 * t));
                uint color = INTERPOLATE(colorEnd, colorBegin, distance);
                fill.ctable.data[i++] = ALPHA_BLEND(color | 0xff000000u, color >> 24);
                if (i == SwConstants.SW_COLOR_TABLE) i = 0;
                t += step;
            }
        }

        private static void _genColorTable(SwFill fill, Fill.ColorStop[] colors, uint count, SwSurface surface, byte opacity, uint begin, uint end)
        {
            var first = colors[0];
            byte a = (byte)MULTIPLY(first.a, opacity);
            if (a < 255) fill.translucent = true;
            uint rgba = surface.join!(first.r, first.g, first.b, a);
            float increment = 1.0f / SwConstants.SW_COLOR_TABLE;
            float position = (begin + 0.5f) * increment;
            uint i = begin;
            uint padding = ALPHA_BLEND(rgba | 0xff000000u, a);
            while (i < end && (i == 0 || position <= first.offset))
            {
                fill.ctable.data[i++] = padding;
                position += increment;
            }
            for (uint j = 0; j < count - 1; ++j)
            {
                var current = colors[j];
                var next = colors[j + 1];
                float divisor = next.offset - current.offset;
                float delta = divisor != 0 ? 1.0f / divisor : 0;
                byte a2 = (byte)MULTIPLY(next.a, opacity);
                if (!fill.translucent && a2 < 255) fill.translucent = true;
                uint rgba2 = surface.join!(next.r, next.g, next.b, a2);
                while (i < end && position < next.offset)
                {
                    float t = (position - current.offset) * delta;
                    int distance = (int)(255 * t);
                    uint color = INTERPOLATE(rgba, rgba2, (byte)(255 - distance));
                    fill.ctable.data[i++] = ALPHA_BLEND(color | 0xff000000u, color >> 24);
                    position += increment;
                }
                rgba = rgba2;
                a = a2;
            }
            rgba = ALPHA_BLEND(rgba | 0xff000000u, a);
            for (; i < end; ++i) fill.ctable.data[i] = rgba;
            if (end == SwConstants.SW_COLOR_TABLE) fill.ctable.data[SwConstants.SW_COLOR_TABLE - 1] = rgba;
        }

        private static bool _updateColorTable(SwFill fill, Fill fdata, SwSurface surface, byte opacity)
        {
            if (fill.solid) return true;
            uint count = fdata.GetColorStops(out var colors);
            if (count == 0 || colors == null) return false;
            _genColorTable(fill, colors, count, surface, opacity, 0, SwConstants.SW_COLOR_TABLE);
            if (fill.spread == FillSpread.Repeat && fdata.GetFillType() != Type.ConicGradient) _applyRepeatAA(fill, colors, count);
            return true;
        }

        private static bool _updateAAMargin(SwFill fill, Fill fdata, SwSurface surface, byte opacity)
        {
            uint count = fdata.GetColorStops(out var colors);
            if (count == 0 || colors == null) return false;
            _genColorTable(fill, colors, count, surface, opacity, 0, MARGIN_MAX);
            _genColorTable(fill, colors, count, surface, opacity, SwConstants.SW_COLOR_TABLE - MARGIN_MAX, SwConstants.SW_COLOR_TABLE);
            _applyRepeatAA(fill, colors, count);
            return true;
        }

        private static bool _prepareLinear(SwFill fill, LinearGradient linear, in Matrix pTransform, ref bool extentChanged)
        {
            linear.Linear(out var x1, out var y1, out var x2, out var y2);

            fill.linear.dx = x2 - x1;
            fill.linear.dy = y2 - y1;
            var len = TvgMath.MultiplyAdd(fill.linear.dx, fill.linear.dx, fill.linear.dy * fill.linear.dy);

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
            fill.linear.offset = TvgMath.MultiplyAdd(-fill.linear.dx, x1, -fill.linear.dy * y1);

            var transform = TvgMath.Multiply(pTransform, linear.GetTransform());

            if (!TvgMath.Inverse(transform, out var itransform)) return false;

            fill.linear.offset += TvgMath.MultiplyAdd(fill.linear.dx, itransform.e13, fill.linear.dy * itransform.e23);

            var dx = fill.linear.dx;
            fill.linear.dx = TvgMath.MultiplyAdd(dx, itransform.e11, fill.linear.dy * itransform.e21);
            fill.linear.dy = TvgMath.MultiplyAdd(dx, itransform.e12, fill.linear.dy * itransform.e22);

            float extent = 1.0f / MathF.Sqrt(TvgMath.MultiplyAdd(fill.linear.dx, fill.linear.dx, fill.linear.dy * fill.linear.dy));
            if (!TvgMath.Equal(extent, fill.extent)) { extentChanged = true; fill.extent = extent; }
            return true;
        }

        private static bool _prepareRadial(SwFill fill, RadialGradient radial, in Matrix pTransform, ref bool extentChanged)
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
            fill.radial.a = TvgMath.MultiplyAdd(-fill.radial.dy, fill.radial.dy,
                TvgMath.MultiplyAdd(-fill.radial.dx, fill.radial.dx, fill.radial.dr * fill.radial.dr));
            var precision = MathF.Max(1e-5f, MathF.Max(fill.radial.dr * fill.radial.dr, 1.0f) * 1e-4f);
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

            float sx = TvgMath.MultiplyAdd(transform.e11, transform.e11, transform.e21 * transform.e21);
            float sy = TvgMath.MultiplyAdd(transform.e12, transform.e12, transform.e22 * transform.e22);
            float sxy = TvgMath.MultiplyAdd(transform.e11, transform.e12, transform.e21 * transform.e22);
            float d = MathF.Sqrt(TvgMath.MultiplyAdd(sx - sy, sx - sy, 4.0f * sxy * sxy));
            float extent = r * MathF.Sqrt(0.5f * MathF.Max(0, sx + sy - d));
            if (!TvgMath.Equal(extent, fill.extent)) { extentChanged = true; fill.extent = extent; }
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
            var i = (int)(TvgMath.MultiplyAdd(pos, SwConstants.SW_COLOR_TABLE - 1, 0.5f));
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
                var rx = TvgMath.MultiplyAdd(x + 0.5f, radial.a11, (y + 0.5f) * radial.a12) + radial.a13 - radial.fx;
                var ry = TvgMath.MultiplyAdd(x + 0.5f, radial.a21, (y + 0.5f) * radial.a22) + radial.a23 - radial.fy;

                if (opacity == 255)
                {
                    for (uint i = 0; i < len; ++i, ++dst, cmp += csize)
                    {
                        var x0 = 0.5f * (TvgMath.MultiplyAdd(-radial.fr, radial.fr, TvgMath.MultiplyAdd(rx, rx, ry * ry))) / (TvgMath.MultiplyAdd(ry, radial.dy, TvgMath.MultiplyAdd(radial.dr, radial.fr, rx * radial.dx)));
                        *dst = opBlendNormal(_pixel(fill, x0), *dst, alpha(cmp));
                        rx += radial.a11;
                        ry += radial.a21;
                    }
                }
                else
                {
                    for (uint i = 0; i < len; ++i, ++dst, cmp += csize)
                    {
                        var x0 = 0.5f * (TvgMath.MultiplyAdd(-radial.fr, radial.fr, TvgMath.MultiplyAdd(rx, rx, ry * ry))) / (TvgMath.MultiplyAdd(ry, radial.dy, TvgMath.MultiplyAdd(radial.dr, radial.fr, rx * radial.dx)));
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
                var rx = TvgMath.MultiplyAdd(x + 0.5f, radial.a11, (y + 0.5f) * radial.a12) + radial.a13 - radial.fx;
                var ry = TvgMath.MultiplyAdd(x + 0.5f, radial.a21, (y + 0.5f) * radial.a22) + radial.a23 - radial.fy;
                for (uint i = 0; i < len; ++i, ++dst)
                {
                    var x0 = 0.5f * (TvgMath.MultiplyAdd(-radial.fr, radial.fr, TvgMath.MultiplyAdd(rx, rx, ry * ry))) / (TvgMath.MultiplyAdd(ry, radial.dy, TvgMath.MultiplyAdd(radial.dr, radial.fr, rx * radial.dx)));
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
                var rx = TvgMath.MultiplyAdd(x + 0.5f, radial.a11, (y + 0.5f) * radial.a12) + radial.a13 - radial.fx;
                var ry = TvgMath.MultiplyAdd(x + 0.5f, radial.a21, (y + 0.5f) * radial.a22) + radial.a23 - radial.fy;
                for (uint i = 0; i < len; ++i, ++dst)
                {
                    var x0 = 0.5f * (TvgMath.MultiplyAdd(-radial.fr, radial.fr, TvgMath.MultiplyAdd(rx, rx, ry * ry))) / (TvgMath.MultiplyAdd(ry, radial.dy, TvgMath.MultiplyAdd(radial.dr, radial.fr, rx * radial.dx)));
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
                var rx = TvgMath.MultiplyAdd(x + 0.5f, radial.a11, (y + 0.5f) * radial.a12) + radial.a13 - radial.fx;
                var ry = TvgMath.MultiplyAdd(x + 0.5f, radial.a21, (y + 0.5f) * radial.a22) + radial.a23 - radial.fy;
                for (uint i = 0; i < len; ++i, ++dst, ++cmp)
                {
                    var x0 = 0.5f * (TvgMath.MultiplyAdd(-radial.fr, radial.fr, TvgMath.MultiplyAdd(rx, rx, ry * ry))) / (TvgMath.MultiplyAdd(ry, radial.dy, TvgMath.MultiplyAdd(radial.dr, radial.fr, rx * radial.dx)));
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

        public static void fillRadial(SwSurface surface, SwFill fill, uint* dst, uint y, uint x, uint len, SwBlenderA op, SwBlender op2, byte a)
        {
            if (fill.radial.a < RADIAL_A_THRESHOLD)
            {
                var radial = fill.radial;
                var rx = TvgMath.MultiplyAdd(x + 0.5f, radial.a11, (y + 0.5f) * radial.a12) + radial.a13 - radial.fx;
                var ry = TvgMath.MultiplyAdd(x + 0.5f, radial.a21, (y + 0.5f) * radial.a22) + radial.a23 - radial.fy;

                if (a == 255)
                {
                    for (uint i = 0; i < len; ++i, ++dst)
                    {
                        var x0 = 0.5f * (TvgMath.MultiplyAdd(-radial.fr, radial.fr, TvgMath.MultiplyAdd(rx, rx, ry * ry))) / (TvgMath.MultiplyAdd(ry, radial.dy, TvgMath.MultiplyAdd(radial.dr, radial.fr, rx * radial.dx)));
                        var tmp = op(_pixel(fill, x0), *dst, 255);
                        *dst = op2(surface, tmp, *dst);
                        rx += radial.a11;
                        ry += radial.a21;
                    }
                }
                else
                {
                    for (uint i = 0; i < len; ++i, ++dst)
                    {
                        var x0 = 0.5f * (TvgMath.MultiplyAdd(-radial.fr, radial.fr, TvgMath.MultiplyAdd(rx, rx, ry * ry))) / (TvgMath.MultiplyAdd(ry, radial.dy, TvgMath.MultiplyAdd(radial.dr, radial.fr, rx * radial.dx)));
                        var tmp = op(_pixel(fill, x0), *dst, 255);
                        var tmp2 = op2(surface, tmp, *dst);
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
                        *dst = op2(surface, tmp, *dst);
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
                        var tmp2 = op2(surface, tmp, *dst);
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
            float t = (TvgMath.MultiplyAdd(fill.linear.dx, rx, fill.linear.dy * ry) + fill.linear.offset) * (SwConstants.SW_COLOR_TABLE - 1);
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
                var v = TvgMath.MultiplyAdd(inc2, len, t);

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
                var v = TvgMath.MultiplyAdd(inc2, len, t);

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
            float t = (TvgMath.MultiplyAdd(fill.linear.dx, rx, fill.linear.dy * ry) + fill.linear.offset) * (SwConstants.SW_COLOR_TABLE - 1);
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
            var v = TvgMath.MultiplyAdd(inc2, len, t);

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
            float t = (TvgMath.MultiplyAdd(fill.linear.dx, rx, fill.linear.dy * ry) + fill.linear.offset) * (SwConstants.SW_COLOR_TABLE - 1);
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
            var v = TvgMath.MultiplyAdd(inc2, len, t);

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
            float t = (TvgMath.MultiplyAdd(fill.linear.dx, rx, fill.linear.dy * ry) + fill.linear.offset) * (SwConstants.SW_COLOR_TABLE - 1);
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
            var v = TvgMath.MultiplyAdd(inc2, len, t);

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

        public static void fillLinear(SwSurface surface, SwFill fill, uint* dst, uint y, uint x, uint len, SwBlenderA op, SwBlender op2, byte a)
        {
            float rx = x + 0.5f;
            float ry = y + 0.5f;
            float t = (TvgMath.MultiplyAdd(fill.linear.dx, rx, fill.linear.dy * ry) + fill.linear.offset) * (SwConstants.SW_COLOR_TABLE - 1);
            float inc2 = fill.linear.dx * (SwConstants.SW_COLOR_TABLE - 1);

            if (TvgMath.Zero(inc2))
            {
                var color = _fixedPixel(fill, (int)(t * FIXPT_SIZE));
                if (a == 255)
                {
                    for (uint i = 0; i < len; ++i, ++dst)
                    {
                        var tmp = op(color, *dst, a);
                        *dst = op2(surface, tmp, *dst);
                    }
                }
                else
                {
                    for (uint i = 0; i < len; ++i, ++dst)
                    {
                        var tmp = op(color, *dst, a);
                        var tmp2 = op2(surface, tmp, *dst);
                        *dst = INTERPOLATE(tmp2, *dst, a);
                    }
                }
                return;
            }

            var vMax = (float)(int.MaxValue >> (FIXPT_BITS + 1));
            var vMin = -vMax;
            var v = TvgMath.MultiplyAdd(inc2, len, t);

            if (a == 255)
            {
                if (v < vMax && v > vMin)
                {
                    var t2 = (int)(t * FIXPT_SIZE);
                    var inc2i = (int)(inc2 * FIXPT_SIZE);
                    for (uint j = 0; j < len; ++j, ++dst)
                    {
                        var tmp = op(_fixedPixel(fill, t2), *dst, 255);
                        *dst = op2(surface, tmp, *dst);
                        t2 += inc2i;
                    }
                }
                else
                {
                    uint counter = 0;
                    while (counter++ < len)
                    {
                        var tmp = op(_pixel(fill, t / SwConstants.SW_COLOR_TABLE), *dst, 255);
                        *dst = op2(surface, tmp, *dst);
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
                        var tmp2 = op2(surface, tmp, *dst);
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
                        var tmp2 = op2(surface, tmp, *dst);
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
            float t = (TvgMath.MultiplyAdd(fill.linear.dx, rx, fill.linear.dy * ry) + fill.linear.offset) * (SwConstants.SW_COLOR_TABLE - 1);
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
            var v = TvgMath.MultiplyAdd(inc2, len, t);

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
                var rx = TvgMath.MultiplyAdd(x + 0.5f, radial.a11, (y + 0.5f) * radial.a12) + radial.a13 - radial.fx;
                var ry = TvgMath.MultiplyAdd(x + 0.5f, radial.a21, (y + 0.5f) * radial.a22) + radial.a23 - radial.fy;
                for (uint i = 0; i < len; ++i, ++dst)
                {
                    var x0 = 0.5f * (TvgMath.MultiplyAdd(-radial.fr, radial.fr, TvgMath.MultiplyAdd(rx, rx, ry * ry))) / (TvgMath.MultiplyAdd(ry, radial.dy, TvgMath.MultiplyAdd(radial.dr, radial.fr, rx * radial.dx)));
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
            float t = (TvgMath.MultiplyAdd(fill.linear.dx, rx, fill.linear.dy * ry) + fill.linear.offset) * (SwConstants.SW_COLOR_TABLE - 1);
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
            var v = TvgMath.MultiplyAdd(inc2, len, t);

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
                var rx = TvgMath.MultiplyAdd(x + 0.5f, radial.a11, (y + 0.5f) * radial.a12) + radial.a13 - radial.fx;
                var ry = TvgMath.MultiplyAdd(x + 0.5f, radial.a21, (y + 0.5f) * radial.a22) + radial.a23 - radial.fy;
                for (uint i = 0; i < len; ++i, ++dst)
                {
                    var x0 = 0.5f * (TvgMath.MultiplyAdd(-radial.fr, radial.fr, TvgMath.MultiplyAdd(rx, rx, ry * ry))) / (TvgMath.MultiplyAdd(ry, radial.dy, TvgMath.MultiplyAdd(radial.dr, radial.fr, rx * radial.dx)));
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

            if (fdata.GetColorStops(out _) == 1) { fill.solid = true; return true; }
            bool extentChanged = false;
            fill.spread = fdata.GetSpread();
            fill.type = fdata.GetFillType();

            if (fdata.GetFillType() == Type.LinearGradient)
            {
                if (!_prepareLinear(fill, (LinearGradient)fdata, transform, ref extentChanged)) return false;
            }
            else if (fdata.GetFillType() == Type.RadialGradient)
            {
                if (!_prepareRadial(fill, (RadialGradient)fdata, transform, ref extentChanged)) return false;
            }
            else if (fdata.GetFillType() == Type.ConicGradient)
            {
                if (!_prepareConic(fill, (ConicGradient)fdata, transform)) return false;
            }

            if (ctable) return _updateColorTable(fill, fdata, surface, opacity);
            if (extentChanged && fill.spread == FillSpread.Repeat) return _updateAAMargin(fill, fdata, surface, opacity);
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
