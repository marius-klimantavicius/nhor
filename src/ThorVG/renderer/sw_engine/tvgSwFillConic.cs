// Ported from pinned ThorVG/src/renderer/cpu_engine/tvgSwFill.cpp.
using System;
using static ThorVG.SwHelper;
using static ThorVG.SwBlendOps;

namespace ThorVG
{
    public static unsafe partial class SwFillOps
    {
        private struct ConicAARange { public float begin, end, distance; }

        private static ConicAARange _conicAARange(SwFill fill, float rx, float ry, uint len)
        {
            var c = fill.conic;
            if (c.invFwidth <= 0 || len == 0) return default;
            float distance = (TvgMath.MultiplyAdd(c.normal.x, rx, c.normal.y * ry)) * c.invFwidth;
            float dx = c.distanceDx;
            var range = new ConicAARange { begin = 0, end = len };
            if (TvgMath.Zero(dx))
            {
                if (distance <= -.5f || distance >= .5f) return default;
            }
            else
            {
                float first = (-.5f - distance) / dx, last = (.5f - distance) / dx;
                if (first > last) (first, last) = (last, first);
                range.begin = MathF.Max(range.begin, MathF.Floor(first) + 1);
                range.end = MathF.Min(range.end, MathF.Ceiling(last));
            }
            float projection = TvgMath.MultiplyAdd(rx, c.normal.y, -ry * c.normal.x);
            if (TvgMath.Zero(c.seamProjectionDx))
            {
                if (projection < 0) return default;
            }
            else if (c.seamProjectionDx > 0) range.begin = MathF.Max(range.begin, MathF.Ceiling(-projection / c.seamProjectionDx));
            else range.end = MathF.Min(range.end, MathF.Floor(-projection / c.seamProjectionDx) + 1);
            if (range.begin >= range.end) return default;
            range.distance = TvgMath.MultiplyAdd(range.begin, dx, distance);
            return range;
        }

        private static uint _conicPixel(SwFill fill, float rx, float ry, uint i, in ConicAARange range)
        {
            if (i < range.begin || i >= range.end)
            {
                float t = TvgMath.MultiplyAdd(MathF.Atan2(ry, rx), .5f / MathConstants.MATH_PI, fill.conic.offset);
                return _pixel(fill, t - MathF.Floor(t));
            }
            float distance = TvgMath.MultiplyAdd(i - range.begin, fill.conic.distanceDx, range.distance);
            return INTERPOLATE(fill.ctable.data[0], fill.ctable.data[SwConstants.SW_COLOR_TABLE - 1], (byte)(255 * (distance + .5f)));
        }

        private static bool _prepareConic(SwFill fill, ConicGradient gradient, in Matrix pTransform)
        {
            gradient.Conic(out float cx, out float cy, out float angle);
            ref var c = ref fill.conic;
            c.cx = cx; c.cy = cy; c.offset = -angle / 360;
            var transform = TvgMath.Multiply(pTransform, gradient.GetTransform());
            if (!TvgMath.Inverse(transform, out var inv)) return false;
            c.a11 = inv.e11; c.a12 = inv.e12; c.a13 = inv.e13;
            c.a21 = inv.e21; c.a22 = inv.e22; c.a23 = inv.e23;
            float radians = angle * (MathConstants.MATH_PI / 180);
            var seam = new Point(MathF.Cos(radians), MathF.Sin(radians));
            c.normal = new Point(-seam.y, seam.x);
            float dx = TvgMath.MultiplyAdd(c.normal.x, c.a11, c.normal.y * c.a21);
            float dy = TvgMath.MultiplyAdd(c.normal.y, c.a22, c.normal.x * c.a12);
            float width = MathF.Abs(dx) + MathF.Abs(dy);
            c.invFwidth = TvgMath.Zero(dx) || TvgMath.Zero(dy) ? 0 : 1 / width;
            c.distanceDx = dx * c.invFwidth;
            c.seamProjectionDx = TvgMath.MultiplyAdd(seam.x, c.a11, seam.y * c.a21);
            return true;
        }

        private static ConicAARange _conicRow(SwFill fill, uint x, uint y, uint len, out float rx, out float ry)
        {
            var c = fill.conic;
            rx = TvgMath.MultiplyAdd(x + .5f, c.a11, (y + .5f) * c.a12) + c.a13 - c.cx;
            ry = TvgMath.MultiplyAdd(x + .5f, c.a21, (y + .5f) * c.a22) + c.a23 - c.cy;
            return _conicAARange(fill, rx, ry, len);
        }

        public static void fillConic(SwFill fill, uint* dst, uint y, uint x, uint len, byte* cmp, SwAlpha alpha, byte csize, byte opacity)
        {
            var range = _conicRow(fill, x, y, len, out float rx, out float ry);
            for (uint i = 0; i < len; ++i, ++dst, cmp += csize)
            {
                byte a = opacity == 255 ? alpha(cmp) : (byte)MULTIPLY(opacity, alpha(cmp));
                *dst = opBlendNormal(_conicPixel(fill, rx, ry, i, range), *dst, a);
                rx += fill.conic.a11; ry += fill.conic.a21;
            }
        }
        public static void fillConic(SwFill fill, uint* dst, uint y, uint x, uint len, SwBlenderA op, byte a)
        {
            var range = _conicRow(fill, x, y, len, out float rx, out float ry);
            for (uint i = 0; i < len; ++i, ++dst)
            {
                *dst = op(_conicPixel(fill, rx, ry, i, range), *dst, a);
                rx += fill.conic.a11; ry += fill.conic.a21;
            }
        }
        public static void fillConic(SwFill fill, byte* dst, uint y, uint x, uint len, SwMask op, byte a)
        {
            var range = _conicRow(fill, x, y, len, out float rx, out float ry);
            for (uint i = 0; i < len; ++i, ++dst)
            {
                byte src = (byte)MULTIPLY(a, A(_conicPixel(fill, rx, ry, i, range)));
                *dst = op(src, *dst, (byte)~src);
                rx += fill.conic.a11; ry += fill.conic.a21;
            }
        }
        public static void fillConic(SwFill fill, byte* dst, uint y, uint x, uint len, byte* cmp, SwMask op, byte a)
        {
            var range = _conicRow(fill, x, y, len, out float rx, out float ry);
            for (uint i = 0; i < len; ++i, ++dst, ++cmp)
            {
                byte src = (byte)MULTIPLY(A(_conicPixel(fill, rx, ry, i, range)), a);
                byte tmp = op(src, *cmp, 0);
                *dst = (byte)(tmp + MULTIPLY(*dst, (byte)~tmp));
                rx += fill.conic.a11; ry += fill.conic.a21;
            }
        }
        public static void fillConic(SwSurface surface, SwFill fill, uint* dst, uint y, uint x, uint len, SwBlenderA op, SwBlender op2, byte a)
        {
            var range = _conicRow(fill, x, y, len, out float rx, out float ry);
            for (uint i = 0; i < len; ++i, ++dst)
            {
                uint tmp = op(_conicPixel(fill, rx, ry, i, range), *dst, 255);
                uint color = op2(surface, tmp, *dst);
                *dst = a == 255 ? color : INTERPOLATE(color, *dst, a);
                rx += fill.conic.a11; ry += fill.conic.a21;
            }
        }
        public static void fillConic<TOp>(SwFill fill, uint* dst, uint y, uint x, uint len, byte a) where TOp : struct, IBlenderAOp
        {
            var range = _conicRow(fill, x, y, len, out float rx, out float ry);
            for (uint i = 0; i < len; ++i, ++dst)
            {
                *dst = TOp.Apply(_conicPixel(fill, rx, ry, i, range), *dst, a);
                rx += fill.conic.a11; ry += fill.conic.a21;
            }
        }
        public static void fillConic<TMask>(SwFill fill, byte* dst, uint y, uint x, uint len, byte a) where TMask : struct, IMaskOp
        {
            var range = _conicRow(fill, x, y, len, out float rx, out float ry);
            for (uint i = 0; i < len; ++i, ++dst)
            {
                byte src = (byte)MULTIPLY(a, A(_conicPixel(fill, rx, ry, i, range)));
                *dst = TMask.Apply(src, *dst, (byte)~src);
                rx += fill.conic.a11; ry += fill.conic.a21;
            }
        }
    }
}
