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

        // Exact pinned native lookup avoids a square root for every soft-light channel.
        private static ReadOnlySpan<byte> SoftLightSqrt =>
        [
            0, 15, 22, 27, 31, 35, 39, 42, 45, 47, 50, 52, 55, 57, 59, 61,
            63, 65, 67, 69, 71, 73, 74, 76, 78, 79, 81, 82, 84, 85, 87, 88,
            90, 91, 93, 94, 95, 97, 98, 99, 100, 102, 103, 104, 105, 107, 108, 109,
            110, 111, 112, 114, 115, 116, 117, 118, 119, 120, 121, 122, 123, 124, 125, 126,
            127, 128, 129, 130, 131, 132, 133, 134, 135, 136, 137, 138, 139, 140, 141, 141,
            142, 143, 144, 145, 146, 147, 148, 148, 149, 150, 151, 152, 153, 153, 154, 155,
            156, 157, 158, 158, 159, 160, 161, 162, 162, 163, 164, 165, 165, 166, 167, 168,
            168, 169, 170, 171, 171, 172, 173, 174, 174, 175, 176, 177, 177, 178, 179, 179,
            180, 181, 182, 182, 183, 184, 184, 185, 186, 186, 187, 188, 188, 189, 190, 190,
            191, 192, 192, 193, 194, 194, 195, 196, 196, 197, 198, 198, 199, 200, 200, 201,
            201, 202, 203, 203, 204, 205, 205, 206, 206, 207, 208, 208, 209, 210, 210, 211,
            211, 212, 213, 213, 214, 214, 215, 216, 216, 217, 217, 218, 218, 219, 220, 220,
            221, 221, 222, 222, 223, 224, 224, 225, 225, 226, 226, 227, 228, 228, 229, 229,
            230, 230, 231, 231, 232, 233, 233, 234, 234, 235, 235, 236, 236, 237, 237, 238,
            238, 239, 240, 240, 241, 241, 242, 242, 243, 243, 244, 244, 245, 245, 246, 246,
            247, 247, 248, 248, 249, 249, 250, 250, 251, 251, 252, 252, 253, 253, 254, 255,
        ];

        public static uint BlendSoftLight(SwSurface surface, uint s, uint d)
        {
            return Separable(s, d, static (x, y) =>
            {
                if (x <= 127) return (byte)(y - ((255 - 2 * x) * y * (255 - y)) / 65025);
                var curve = y <= 64
                    ? 4 * y - (12 * y * y) / 255 + (16 * y * y * y) / 65025
                    : SoftLightSqrt[y];
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
