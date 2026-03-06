// Ported from taffy/src/util/sys.rs
// System utility functions. In C# with the standard library available,
// these simply delegate to MathF.

using System;
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
        public static float Round(float value) => MathF.Round(value, MidpointRounding.AwayFromZero);

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
        public static float F32Max(float a, float b) => MathF.Max(a, b);

        /// <summary>Returns the smallest of two f32 values.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float F32Min(float a, float b) => MathF.Min(a, b);
    }
}
