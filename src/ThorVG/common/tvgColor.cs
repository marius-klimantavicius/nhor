// Ported from ThorVG/src/common/tvgColor.h and tvgColor.cpp

using System;

namespace ThorVG
{
    public struct RGB
    {
        public byte r, g, b;

        public RGB(byte r, byte g, byte b) { this.r = r; this.g = g; this.b = b; }
    }

    public struct RGBA
    {
        public byte r, g, b, a;

        public RGBA(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; }
    }

    public struct HSL
    {
        public float h, s, l;

        public HSL(float h, float s, float l) { this.h = h; this.s = s; this.l = l; }
    }

    public static class TvgColor
    {
        public static void Hsl2Rgb(float h, float s, float l, out byte r, out byte g, out byte b)
        {
            if (TvgMath.Zero(s))
            {
                r = g = b = (byte)MathF.Round(l * 255.0f, MidpointRounding.ToEven);
                return;
            }

            if (TvgMath.Equal(h, 360.0f))
            {
                h = 0.0f;
            }
            else
            {
                h = h % 360.0f;
                if (h < 0.0f) h += 360.0f;
                h /= 60.0f;
            }

            var v = (l <= 0.5f) ? (l * (1.0f + s)) : (l + s - (l * s));
            var p = l + l - v;
            var sv = TvgMath.Zero(v) ? 0.0f : (v - p) / v;
            var i = (byte)h;
            var f = h - i;
            var vsf = v * sv * f;
            var t = p + vsf;
            var q = v - vsf;

            float tr, tg, tb;
            switch (i)
            {
                case 0: tr = v; tg = t; tb = p; break;
                case 1: tr = q; tg = v; tb = p; break;
                case 2: tr = p; tg = v; tb = t; break;
                case 3: tr = p; tg = q; tb = v; break;
                case 4: tr = t; tg = p; tb = v; break;
                case 5: tr = v; tg = p; tb = q; break;
                default: tr = tg = tb = 0.0f; break;
            }

            r = (byte)MathF.Round(tr * 255.0f, MidpointRounding.ToEven);
            g = (byte)MathF.Round(tg * 255.0f, MidpointRounding.ToEven);
            b = (byte)MathF.Round(tb * 255.0f, MidpointRounding.ToEven);
        }
    }
}
