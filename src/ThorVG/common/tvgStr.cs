// Ported from ThorVG/src/common/tvgStr.h and tvgStr.cpp

using System;
using System.Runtime.CompilerServices;

namespace ThorVG
{
    public static class TvgStr
    {
        // ---- String equality (case-sensitive) ---------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Equal(string? a, string? b)
        {
            return string.Equals(a, b, StringComparison.Ordinal);
        }

        // ---- Concat -----------------------------------------------------

        public static string Concat(string a, string b)
        {
            return string.Concat(a, b);
        }

        // ---- Duplicate --------------------------------------------------

        public static string? Duplicate(string? str, int n = int.MaxValue)
        {
            if (str == null) return null;
            var len = str.Length;
            if (len < n) n = len;
            return str.Substring(0, n);
        }

        public static string? Duplicate(string? str, int n, out uint size)
        {
            if (str == null) { size = 0; return null; }
            var len = str.Length;
            if (len < n) n = len;
            size = (uint)n;
            return str.Substring(0, n);
        }

        // ---- Append -----------------------------------------------------

        public static string? Append(string? lhs, string? rhs, int n)
        {
            if (rhs == null) return lhs;
            if (lhs == null) return Duplicate(rhs, n);
            var part = rhs.Length > n ? rhs.Substring(0, n) : rhs;
            return string.Concat(lhs, part);
        }

        // ---- Dirname ----------------------------------------------------

        public static string Dirname(string path)
        {
            var idx = path.LastIndexOf('/');
            var idx2 = path.LastIndexOf('\\');
            if (idx2 > idx) idx = idx2;
            if (idx >= 0) return path.Substring(0, idx + 1);
            return path;
        }

        // ---- Filename (without extension) ------------------------------

        public static string Filename(string path)
        {
            var sep = path.LastIndexOf('/');
            var sep2 = path.LastIndexOf('\\');
            if (sep2 > sep) sep = sep2;
            var start = (sep >= 0) ? sep + 1 : 0;
            var name = path.Substring(start);
            var ext = Fileext(name);
            if (ext.Length > 0 && ext.Length < name.Length)
            {
                // ext includes everything after the last dot
                return name.Substring(0, name.Length - ext.Length - 1);
            }
            return name;
        }

        // ---- File extension (after last '.') ---------------------------

        public static string Fileext(string path)
        {
            var result = path;
            while (true)
            {
                var dot = result.IndexOf('.');
                if (dot < 0) break;
                result = result.Substring(dot + 1);
            }
            // If result == path and there was no dot, return empty
            if (ReferenceEquals(result, path) || result == path) return string.Empty;
            return result;
        }

        // ---- ToFloat (custom parser, from tvgStr.cpp) ------------------

        /// <summary>
        /// Parses a float from <paramref name="str"/> starting at
        /// <paramref name="index"/>.  On return, <paramref name="index"/>
        /// points past the consumed characters (mirrors the C++ char** end
        /// parameter).
        /// </summary>
        public static float ToFloat(string str, ref int index)
        {
            if (str == null) return 0.0f;
            int startIndex = index;
            int iter = index;
            int len = str.Length;
            float val = 0.0f;
            ulong integerPart = 0;
            int minus = 1;
            int a = iter; // tracks the "successful parse" position

            // skip leading whitespace
            while (iter < len && char.IsWhiteSpace(str[iter])) iter++;

            // sign
            if (iter < len && str[iter] == '-') { minus = -1; iter++; }
            else if (iter < len && str[iter] == '+') { iter++; }

            // INF / INFINITY
            if (iter < len && char.ToLowerInvariant(str[iter]) == 'i')
            {
                if (iter + 2 < len &&
                    char.ToLowerInvariant(str[iter + 1]) == 'n' &&
                    char.ToLowerInvariant(str[iter + 2]) == 'f')
                {
                    iter += 3;
                    // optional "inity"
                    if (iter < len && char.ToLowerInvariant(str[iter]) == 'i')
                    {
                        if (iter + 4 < len &&
                            char.ToLowerInvariant(str[iter + 1]) == 'n' &&
                            char.ToLowerInvariant(str[iter + 2]) == 'i' &&
                            char.ToLowerInvariant(str[iter + 3]) == 't' &&
                            char.ToLowerInvariant(str[iter + 4]) == 'y')
                        {
                            iter += 5;
                        }
                        else { index = startIndex; return 0.0f; }
                    }
                    index = iter;
                    return (minus == -1) ? float.NegativeInfinity : float.PositiveInfinity;
                }
                index = startIndex;
                return 0.0f;
            }

            // NAN
            if (iter < len && char.ToLowerInvariant(str[iter]) == 'n')
            {
                if (iter + 2 < len &&
                    char.ToLowerInvariant(str[iter + 1]) == 'a' &&
                    char.ToLowerInvariant(str[iter + 2]) == 'n')
                {
                    iter += 3;
                    index = iter;
                    return (minus == -1) ? -float.NaN : float.NaN;
                }
                index = startIndex;
                return 0.0f;
            }

            // Optional integer part before dot
            if (iter < len && char.IsAsciiDigit(str[iter]))
            {
                while (iter < len && char.IsAsciiDigit(str[iter]))
                {
                    integerPart = integerPart * 10UL + (ulong)(str[iter] - '0');
                    iter++;
                }
                a = iter;
            }
            else if (iter >= len || str[iter] != '.')
            {
                // success with 0
                index = a;
                return 0.0f;
            }

            val = (float)integerPart;

            // Optional decimal part after dot
            if (iter < len && str[iter] == '.')
            {
                ulong decimalPart = 0;
                ulong pow10 = 1;
                int decCount = 0;
                iter++;

                if (iter < len && char.IsAsciiDigit(str[iter]))
                {
                    while (iter < len && char.IsAsciiDigit(str[iter]))
                    {
                        if (decCount < 19)
                        {
                            decimalPart = decimalPart * 10UL + (ulong)(str[iter] - '0');
                            pow10 *= 10UL;
                        }
                        iter++;
                        decCount++;
                    }
                }
                else if (iter < len && char.IsWhiteSpace(str[iter]))
                {
                    a = iter;
                    goto success;
                }

                val += (float)decimalPart / (float)pow10;
                a = iter;
            }

            // Optional exponent
            if (iter < len && (str[iter] == 'e' || str[iter] == 'E'))
            {
                iter++;

                // Exception: svg may have 'em' unit
                if (iter < len && (str[iter] == 'm' || str[iter] == 'M'))
                {
                    a = iter + 1;
                    goto success;
                }

                int minus_e = 1;
                if (iter < len && str[iter] == '-') { minus_e = -1; iter++; }
                else if (iter < len && str[iter] == '+') { iter++; }

                uint exponentPart = 0;
                if (iter < len && char.IsAsciiDigit(str[iter]))
                {
                    while (iter < len && str[iter] == '0') iter++;
                    while (iter < len && char.IsAsciiDigit(str[iter]))
                    {
                        exponentPart = exponentPart * 10U + (uint)(str[iter] - '0');
                        iter++;
                    }
                }
                else if (a > startIndex && a > 0 && !char.IsAsciiDigit(str[a - 1]))
                {
                    a = startIndex;
                    goto success;
                }
                else if (iter >= len)
                {
                    goto success;
                }

                if (FloatExact(val, 1.175494351f) && (minus_e * (int)exponentPart) <= -38)
                {
                    val *= 1.0e-38f;
                    a = iter;
                    goto success;
                }

                a = iter;
                var scale = 1.0f;
                while (exponentPart >= 8U) { scale *= 1E8f; exponentPart -= 8U; }
                while (exponentPart > 0U) { scale *= 10.0f; exponentPart--; }
                val = (minus_e == -1) ? (val / scale) : (val * scale);
            }
            else if (iter > startIndex && iter > 0 && !char.IsAsciiDigit(str[iter - 1]))
            {
                a = startIndex;
                goto success;
            }

        success:
            index = a;
            if (!float.IsFinite(val)) return 0.0f;
            return minus * val;
        }

        // ---- Private helpers -------------------------------------------

        private static unsafe bool FloatExact(float a, float b)
        {
            return *(int*)&a == *(int*)&b;
        }
    }
}
