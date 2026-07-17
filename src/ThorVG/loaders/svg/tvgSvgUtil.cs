// Ported from ThorVG/src/loaders/svg/tvgSvgUtil.h and tvgSvgUtil.cpp
// Whitespace/URL helpers.

using System;

namespace ThorVG
{
    public static class SvgUtil
    {
        private static byte HexCharToDec(char c)
        {
            if (c >= 'a') return (byte)(c - 'a' + 10);
            else if (c >= 'A') return (byte)(c - 'A' + 10);
            else return (byte)(c - '0');
        }

        public static int SkipWhiteSpace(string str, int itr, int itrEnd)
        {
            while ((itrEnd >= 0 && itr < itrEnd) || (itrEnd < 0 && itr < str.Length))
            {
                if (!char.IsWhiteSpace(str[itr])) break;
                itr++;
            }
            return itr;
        }

        /// <summary>
        /// Skip whitespace from the given position. If itrEnd is -1, scans until end of string.
        /// </summary>
        public static int SkipWhiteSpace(ReadOnlySpan<char> str, int itr, int itrEnd)
        {
            int end = itrEnd < 0 ? str.Length : itrEnd;
            while (itr < end)
            {
                if (!char.IsWhiteSpace(str[itr])) break;
                itr++;
            }
            return itr;
        }

        public static int UnskipWhiteSpace(string str, int itr, int itrStart)
        {
            for (itr--; itr > itrStart; itr--)
            {
                if (!char.IsWhiteSpace(str[itr])) break;
            }
            return itr + 1;
        }

        public static int UnskipWhiteSpace(ReadOnlySpan<char> str, int itr, int itrStart)
        {
            for (itr--; itr > itrStart; itr--)
            {
                if (!char.IsWhiteSpace(str[itr])) break;
            }
            return itr + 1;
        }

        public static int SkipWhiteSpaceAndComma(string content, int pos)
        {
            pos = SkipWhiteSpace(content, pos, -1);
            if (pos < content.Length && content[pos] == ',') return pos + 1;
            return pos;
        }

        /// <summary>URL-decodes the source string, returning the decoded string and its length.</summary>
        public static string URLDecode(string src)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;

            var decoded = new char[src.Length];
            int idx = 0;
            int i = 0;

            while (i < src.Length)
            {
                if (src[i] == '%' && i + 2 < src.Length &&
                    IsHexDigit(src[i + 1]) && IsHexDigit(src[i + 2]))
                {
                    decoded[idx++] = (char)((HexCharToDec(src[i + 1]) << 4) + HexCharToDec(src[i + 2]));
                    i += 3;
                }
                else if (src[i] == '+')
                {
                    decoded[idx++] = ' ';
                    i++;
                }
                else
                {
                    decoded[idx++] = src[i++];
                }
            }

            return new string(decoded, 0, idx);
        }

        public static void HslToRgb(float h, float s, float l, out byte r, out byte g, out byte b)
        {
            if (TvgMath.Zero(s))
            {
                r = g = b = (byte)MathF.Round(l * 255.0f);
                return;
            }

            if (TvgMath.Equal(h, 360.0f)) h = 0.0f;
            else
            {
                h %= 360.0f;
                if (h < 0.0f) h += 360.0f;
                h /= 60.0f;
            }

            var v = l <= 0.5f ? l * (1.0f + s) : l + s - l * s;
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
            r = (byte)MathF.Round(tr * 255.0f);
            g = (byte)MathF.Round(tg * 255.0f);
            b = (byte)MathF.Round(tb * 255.0f);
        }

        private static bool IsHexDigit(char c)
        {
            return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        }
    }
}
