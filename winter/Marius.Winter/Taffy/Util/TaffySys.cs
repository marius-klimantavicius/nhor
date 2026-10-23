// Ported from taffy/src/util/sys.rs
// System utility functions. In C# with the standard library available,
// these simply delegate to MathF.

using System;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Marius.Winter.Taffy
{
    /// <summary>
    /// System utility functions ported from taffy's sys module.
    /// These are trivial wrappers around MathF since we always have the standard library in .NET.
    /// </summary>
    public static class TaffySys
    {
        /// <summary>Rounds to the nearest whole number.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Round(float value) => MathF.Floor(value + 0.5f);

        /// <summary>Rounds up to the nearest whole number.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Ceil(float value) => MathF.Ceiling(value);

        /// <summary>Rounds down to the nearest whole number.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Floor(float value) => MathF.Floor(value);

        /// <summary>Computes the absolute value.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Abs(float value) => MathF.Abs(value);

        /// <summary>Returns the largest of two f32 values.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float F32Max(float a, float b) => float.IsNaN(a) ? b : float.IsNaN(b) || a >= b ? a : b;

        /// <summary>Returns the smallest of two f32 values.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float F32Min(float a, float b) => float.IsNaN(a) ? b : float.IsNaN(b) || a <= b ? a : b;

        /// <summary>Rust f32 Display formatting: shortest round-trip digits in fixed notation.</summary>
        public static string FormatFloat(float value)
        {
            if (float.IsPositiveInfinity(value)) return "inf";
            if (float.IsNegativeInfinity(value)) return "-inf";
            var text = value.ToString("R", CultureInfo.InvariantCulture);
            int exponentIndex = text.IndexOf('E');
            if (exponentIndex < 0) return AdjustShortestTie(text, value);
            int exponent = int.Parse(text.AsSpan(exponentIndex + 1), CultureInfo.InvariantCulture);
            bool negative = text[0] == '-';
            var mantissa = text[(negative ? 1 : 0)..exponentIndex];
            int dot = mantissa.IndexOf('.');
            int point = (dot < 0 ? mantissa.Length : dot) + exponent;
            var digits = mantissa.Replace(".", "");
            string number = point <= 0 ? "0." + new string('0', -point) + digits
                : point >= digits.Length ? digits + new string('0', point - digits.Length) : digits.Insert(point, ".");
            return AdjustShortestTie(negative ? "-" + number : number, value);
        }

        private static string AdjustShortestTie(string text, float value)
        {
            if (!float.IsFinite(value)) return text;
            int dot = text.IndexOf('.');
            if (dot < 0) return text;
            int decimals = text.Length - dot - 1;
            uint bits = BitConverter.SingleToUInt32Bits(value) & 0x7fffffff;
            int exponent = (int)(bits >> 23);
            uint mantissa = bits & 0x7fffff;
            int shift = exponent == 0 ? -149 : exponent - 150;
            if (exponent != 0) mantissa |= 0x800000;
            BigInteger numerator = new BigInteger(mantissa) * BigInteger.Pow(10, decimals), denominator = BigInteger.One;
            if (shift < 0) denominator <<= -shift; else numerator <<= shift;
            var integral = BigInteger.DivRem(numerator, denominator, out var remainder);
            // Rust's shortest decimal formatter rounds exact decimal midpoints away from zero;
            // .NET's round-trip formatter chooses the even decimal in that case.
            if (remainder * 2 != denominator) return text;
            var digits = (integral + 1).ToString(CultureInfo.InvariantCulture).PadLeft(decimals + 1, '0');
            string rounded = digits.Insert(digits.Length - decimals, ".");
            return value < 0 ? "-" + rounded : rounded;
        }
    }
}
