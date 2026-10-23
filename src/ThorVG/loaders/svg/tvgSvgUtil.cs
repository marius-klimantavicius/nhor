// Ported from ThorVG/src/loaders/svg/tvgSvgUtil.h and tvgSvgUtil.cpp
// Whitespace/URL helpers.

using System;

namespace ThorVG
{
    public static class SvgUtil
    {
        public static bool IsWhiteSpace(char c) => c == ' ' || c == '\t' || c == '\r' || c == '\n' || c == '\v' || c == '\f';

        public static string Trim(string value)
        {
            int begin = 0, end = value.Length;
            while (begin < end && IsWhiteSpace(value[begin])) ++begin;
            while (end > begin && IsWhiteSpace(value[end - 1])) --end;
            return begin == 0 && end == value.Length ? value : value.Substring(begin, end - begin);
        }
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
                if (!SvgUtil.IsWhiteSpace(str[itr])) break;
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
                if (!SvgUtil.IsWhiteSpace(str[itr])) break;
                itr++;
            }
            return itr;
        }

        public static int UnskipWhiteSpace(string str, int itr, int itrStart)
        {
            for (itr--; itr > itrStart; itr--)
            {
                if (!SvgUtil.IsWhiteSpace(str[itr])) break;
            }
            return itr + 1;
        }

        public static int UnskipWhiteSpace(ReadOnlySpan<char> str, int itr, int itrStart)
        {
            for (itr--; itr > itrStart; itr--)
            {
                if (!SvgUtil.IsWhiteSpace(str[itr])) break;
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
            return System.Text.Encoding.UTF8.GetString(URLDecodeBytes(src));
        }

        // Native URL decoding operates on UTF8 bytes. Keep those bytes for
        // embedded images instead of promoting each percent byte to UTF16.
        internal static byte[] URLDecodeBytes(string src)
        {
            var decoded = System.Text.Encoding.UTF8.GetBytes(src);
            int idx = 0;
            int i = 0;

            while (i < decoded.Length)
            {
                if (decoded[i] == '%' && i + 2 < decoded.Length &&
                    IsHexDigit((char)decoded[i + 1]) && IsHexDigit((char)decoded[i + 2]))
                {
                    decoded[idx++] = (byte)((HexCharToDec((char)decoded[i + 1]) << 4) + HexCharToDec((char)decoded[i + 2]));
                    i += 3;
                }
                else if (decoded[i] == '+')
                {
                    decoded[idx++] = (byte)' ';
                    i++;
                }
                else
                {
                    decoded[idx++] = decoded[i++];
                }
            }

            if (idx != decoded.Length) System.Array.Resize(ref decoded, idx);
            return decoded;
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

            var v = l <= 0.5f ? l * (1.0f + s) : TvgMath.MultiplyAdd(-l, s, l + s);
            var p = TvgMath.MultiplyAdd(2, l, -v);
            var sv = TvgMath.Zero(v) ? 0.0f : (v - p) / v;
            var i = (byte)h;
            var f = h - i;
            var vs = v * sv;
            var t = TvgMath.MultiplyAdd(vs, f, p);
            var q = TvgMath.MultiplyAdd(-vs, f, v);
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
