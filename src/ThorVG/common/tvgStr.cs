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
            if (a == null || b == null) return a == b;
            if (TvgUtf8.HasRaw(a) || TvgUtf8.HasRaw(b))
                return CStringBytes(TvgUtf8.Encode(a)).SequenceEqual(CStringBytes(TvgUtf8.Encode(b)));
            return CString(a).SequenceEqual(CString(b));
        }

        // ---- Concat -----------------------------------------------------

        public static string Concat(string a, string b)
        {
            return TvgUtf8.Concat(Duplicate(a)!, Duplicate(b)!);
        }

        // ---- Duplicate --------------------------------------------------

        public static string? Duplicate(string? str, int n = int.MaxValue)
        {
            if (str == null) return null;
            var len = CString(str).Length;
            if (len < n) n = len;
            return TvgUtf8.Slice(str, 0, n);
        }

        public static string? Duplicate(string? str, int n, out uint size)
        {
            if (str == null) { size = 0; return null; }
            var len = CString(str).Length;
            if (len < n) n = len;
            size = (uint)n;
            return TvgUtf8.Slice(str, 0, n);
        }

        /// <summary>
        /// Native byte-limit duplication, including the terminating NUL. Raw
        /// byte limits can split UTF-8; string overloads retain their existing
        /// UTF-16 character limits for managed callers.
        /// </summary>
        public static byte[]? DuplicateUtf8(byte[]? str, nuint max, out uint size)
        {
            size = 0;
            if (str == null) return null;
            int length = CStringBytes(str).Length;
            int count = max < (nuint)length ? (int)max : length;
            var result = new byte[checked(count + 1)];
            str.AsSpan(0, count).CopyTo(result);
            size = (uint)count;
            return result;
        }

        // ---- Append -----------------------------------------------------

        public static string? Append(string? lhs, string? rhs, int n)
        {
            if (rhs == null) return lhs;
            if (lhs == null) return Duplicate(rhs, n);
            var part = CString(rhs);
            if (part.Length > n) part = part.Slice(0, n);
            return TvgUtf8.Concat(Duplicate(lhs)!, TvgUtf8.Slice(rhs, 0, part.Length));
        }

        /// <summary>Append native CString bytes with a byte limit and trailing NUL.</summary>
        public static byte[]? AppendUtf8(byte[]? lhs, byte[]? rhs, nuint max)
        {
            if (rhs == null) return lhs;
            if (lhs == null) return DuplicateUtf8(rhs, max, out _);
            var left = CStringBytes(lhs);
            var right = CStringBytes(rhs);
            int count = max < (nuint)right.Length ? (int)max : right.Length;
            var result = new byte[checked(left.Length + count + 1)];
            left.CopyTo(result);
            right.Slice(0, count).CopyTo(result.AsSpan(left.Length));
            return result;
        }

        // ---- Dirname ----------------------------------------------------

        public static string Dirname(string path)
        {
            var text = CString(path);
            var idx = Separator(text);
            return TvgUtf8.Slice(path, 0, idx >= 0 ? idx + 1 : text.Length);
        }

        // ---- Filename (without extension) ------------------------------

        public static string Filename(string path)
        {
            var text = CString(path);
            var sep = Separator(text);
            var start = (sep >= 0) ? sep + 1 : 0;
            var name = text.Slice(start);
            var dot = name.LastIndexOf('.');
            return TvgUtf8.Slice(path, start, dot >= 0 ? dot : name.Length);
        }

        // ---- File extension (after last '.') ---------------------------

        public static string Fileext(string path)
        {
            var text = CString(path);
            var dot = text.LastIndexOf('.');
            return dot >= 0 ? TvgUtf8.Slice(path, dot + 1, text.Length - dot - 1) : TvgUtf8.Slice(path, 0, text.Length);
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
            while (iter < len && IsSpace(str[iter])) iter++;

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
                    return BitConverter.UInt32BitsToSingle(minus == -1 ? 0xffc00000U : 0x7fc00000U);
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
                return minus * 0.0f;
            }

            val = UnsignedFloat(integerPart);

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
                else if (iter < len && IsSpace(str[iter]))
                {
                    a = iter;
                    goto success;
                }

                val += UnsignedFloat(decimalPart) / UnsignedFloat(pow10);
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

        // Preserve native UInt64-to-float nearest-even rounding when the
        // runtime's scalar unsigned conversion would round twice. Above the
        // signed range, retain the discarded low bit before scaling back.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float UnsignedFloat(ulong value) =>
            value <= long.MaxValue ? (float)(long)value : (float)(long)((value >> 1) | (value & 1)) * 2.0f;

        private static ReadOnlySpan<char> CString(string value)
        {
            var text = value.AsSpan();
            var end = text.IndexOf('\0');
            return end < 0 ? text : text.Slice(0, end);
        }

        private static ReadOnlySpan<byte> CStringBytes(byte[] value)
        {
            var bytes = value.AsSpan();
            int end = bytes.IndexOf((byte)0);
            return end < 0 ? bytes : bytes.Slice(0, end);
        }

        private static int Separator(ReadOnlySpan<char> path)
        {
            int sep = path.LastIndexOf('/');
            if (OperatingSystem.IsWindows()) sep = Math.Max(sep, path.LastIndexOf('\\'));
            return sep;
        }

        private static bool IsSpace(char ch) => ch is ' ' or '\t' or '\n' or '\r' or '\f' or '\v';

        private static unsafe bool FloatExact(float a, float b)
        {
            return *(int*)&a == *(int*)&b;
        }
    }
}
