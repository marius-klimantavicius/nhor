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

        private static bool IsHexDigit(char c)
        {
            return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        }
    }
}
