// Ported from ThorVG/src/renderer/cpu_engine/tvgSwBlendOp.cpp

using System;
using System.Runtime.CompilerServices;
using static ThorVG.SwHelper;

namespace ThorVG
{
    public static class SwBlendOp
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Premultiply(uint c1, uint c2, byte a)
        {
            if (a == 255) return c1;
            if (a == 0) return c2;
            return ALPHA_BLEND(c1, a) + ALPHA_BLEND(c2, (uint)(255 - a));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static RenderColor Unpremultiply(uint c)
        {
            var o = new RenderColor(C1(c), C2(c), C3(c), A(c));
            if (o.a > 0 && o.a < 255)
            {
                o.r = (byte)Math.Min(o.r * 255u / o.a, 255u);
                o.g = (byte)Math.Min(o.g * 255u / o.a, 255u);
                o.b = (byte)Math.Min(o.b * 255u / o.a, 255u);
            }
            return o;
        }

        private static void Saturate(ref byte c1, ref byte c2, ref byte c3, byte s1, byte s2, byte s3)
        {
            var saturation = Math.Max(s1, Math.Max(s2, s3)) - Math.Min(s1, Math.Min(s2, s3));
            Span<byte> values = stackalloc byte[3] { c1, c2, c3 };
            var lo = 0;
            var mid = 1;
            var hi = 2;
            if (values[lo] > values[mid]) (lo, mid) = (mid, lo);
            if (values[mid] > values[hi]) (mid, hi) = (hi, mid);
            if (values[lo] > values[mid]) (lo, mid) = (mid, lo);

            var min = values[lo];
            var max = values[hi];
            if (max > min)
            {
                values[mid] = (byte)(((values[mid] - min) * saturation) / (max - min));
                values[hi] = (byte)saturation;
            }
            else
            {
                values[mid] = values[hi] = 0;
            }
            values[lo] = 0;
            c1 = values[0];
            c2 = values[1];
            c3 = values[2];
        }

        private static void Luminance(ref byte c1, ref byte c2, ref byte c3, int current, int luminance)
        {
            var delta = luminance - current;
            var r = c1 + delta;
            var g = c2 + delta;
            var b = c3 + delta;
            var min = Math.Min(r, Math.Min(g, b));
            var max = Math.Max(r, Math.Max(g, b));

            if (min < 0)
            {
                r = luminance + ((r - luminance) * luminance) / (luminance - min);
                g = luminance + ((g - luminance) * luminance) / (luminance - min);
                b = luminance + ((b - luminance) * luminance) / (luminance - min);
                max = Math.Max(r, Math.Max(g, b));
            }
            if (max > 255)
            {
                r = luminance + ((r - luminance) * (255 - luminance)) / (max - luminance);
                g = luminance + ((g - luminance) * (255 - luminance)) / (max - luminance);
                b = luminance + ((b - luminance) * (255 - luminance)) / (max - luminance);
            }
            c1 = (byte)r;
            c2 = (byte)g;
            c3 = (byte)b;
        }

        public static uint BlendDifference(SwSurface surface, uint s, uint d)
            => JOIN(255, (byte)Math.Abs(C1(s) - C1(d)), (byte)Math.Abs(C2(s) - C2(d)), (byte)Math.Abs(C3(s) - C3(d)));

        public static uint BlendExclusion(SwSurface surface, uint s, uint d)
        {
            byte F(byte x, byte y) => (byte)TvgMath.Clamp(x + y - 2 * MULTIPLY(x, y), 0, 255);
            return JOIN(255, F(C1(s), C1(d)), F(C2(s), C2(d)), F(C3(s), C3(d)));
        }

        public static uint BlendAdd(SwSurface surface, uint s, uint d)
        {
            byte F(byte x, byte y) => (byte)Math.Min(x + y, 255);
            return JOIN(255, F(C1(s), C1(d)), F(C2(s), C2(d)), F(C3(s), C3(d)));
        }

        public static uint BlendScreen(SwSurface surface, uint s, uint d)
        {
            byte F(byte x, byte y) => (byte)(x + y - MULTIPLY(x, y));
            return JOIN(255, F(C1(s), C1(d)), F(C2(s), C2(d)), F(C3(s), C3(d)));
        }

        private static uint Separable(uint s, uint d, Func<byte, byte, byte> operation)
        {
            var o = Unpremultiply(d);
            return Premultiply(JOIN(255, operation(C1(s), o.r), operation(C2(s), o.g), operation(C3(s), o.b)), s, o.a);
        }

        public static uint BlendMultiply(SwSurface surface, uint s, uint d) => Separable(s, d, (x, y) => (byte)MULTIPLY(x, y));
        public static uint BlendOverlay(SwSurface surface, uint s, uint d) => Separable(s, d, (x, y) => (byte)(y < 128 ? Math.Min(255, 2 * MULTIPLY(x, y)) : 255 - Math.Min(255, 2 * MULTIPLY(255 - x, 255 - y))));
        public static uint BlendDarken(SwSurface surface, uint s, uint d) => Separable(s, d, Math.Min);

        public static uint BlendLighten(SwSurface surface, uint s, uint d)
            => JOIN(255, Math.Max(C1(s), C1(d)), Math.Max(C2(s), C2(d)), Math.Max(C3(s), C3(d)));

        public static uint BlendColorDodge(SwSurface surface, uint s, uint d) => Separable(s, d, (x, y) => (byte)(y == 0 ? 0 : x == 255 ? 255 : Math.Min(y * 255 / (255 - x), 255)));
        public static uint BlendColorBurn(SwSurface surface, uint s, uint d) => Separable(s, d, (x, y) => (byte)(y == 255 ? 255 : x == 0 ? 0 : 255 - Math.Min((255 - y) * 255 / x, 255)));
        public static uint BlendHardLight(SwSurface surface, uint s, uint d) => Separable(s, d, (x, y) => (byte)(x < 128 ? Math.Min(255, 2 * MULTIPLY(x, y)) : 255 - Math.Min(255, 2 * MULTIPLY(255 - x, 255 - y))));

        public static uint BlendSoftLight(SwSurface surface, uint s, uint d)
        {
            return Separable(s, d, static (x, y) =>
            {
                if (x <= 127) return (byte)(y - ((255 - 2 * x) * y * (255 - y)) / 65025);
                var curve = y <= 64
                    ? 4 * y - (12 * y * y) / 255 + (16 * y * y * y) / 65025
                    : (int)(MathF.Sqrt(y / 255.0f) * 255.0f);
                return (byte)(y + ((2 * x - 255) * (curve - y)) / 255);
            });
        }

        public static uint BlendHue(SwSurface surface, uint s, uint d)
        {
            var o = Unpremultiply(d);
            var c1 = C1(s); var c2 = C2(s); var c3 = C3(s);
            Saturate(ref c1, ref c2, ref c3, o.r, o.g, o.b);
            Luminance(ref c1, ref c2, ref c3, surface.Luma(((uint)c1 << 16) | ((uint)c2 << 8) | c3), surface.Luma(((uint)o.r << 16) | ((uint)o.g << 8) | o.b));
            return Premultiply(JOIN(255, c1, c2, c3), s, o.a);
        }

        public static uint BlendSaturation(SwSurface surface, uint s, uint d)
        {
            var o = Unpremultiply(d);
            var c1 = o.r; var c2 = o.g; var c3 = o.b;
            Saturate(ref c1, ref c2, ref c3, C1(s), C2(s), C3(s));
            Luminance(ref c1, ref c2, ref c3, surface.Luma(((uint)c1 << 16) | ((uint)c2 << 8) | c3), surface.Luma(((uint)o.r << 16) | ((uint)o.g << 8) | o.b));
            return Premultiply(JOIN(255, c1, c2, c3), s, o.a);
        }

        public static uint BlendColor(SwSurface surface, uint s, uint d)
        {
            var o = Unpremultiply(d);
            var c1 = C1(s); var c2 = C2(s); var c3 = C3(s);
            Luminance(ref c1, ref c2, ref c3, surface.Luma(s), surface.Luma(((uint)o.r << 16) | ((uint)o.g << 8) | o.b));
            return Premultiply(JOIN(255, c1, c2, c3), s, o.a);
        }

        public static uint BlendLuminosity(SwSurface surface, uint s, uint d)
        {
            var o = Unpremultiply(d);
            Luminance(ref o.r, ref o.g, ref o.b, surface.Luma(((uint)o.r << 16) | ((uint)o.g << 8) | o.b), surface.Luma(s));
            return Premultiply(JOIN(255, o.r, o.g, o.b), s, o.a);
        }
    }
}
