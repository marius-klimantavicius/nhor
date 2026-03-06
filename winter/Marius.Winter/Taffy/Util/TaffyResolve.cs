// Ported from taffy/src/util/resolve.rs
// Helper extension methods to resolve context-dependent sizes into absolute sizes.

using System;
using System.Runtime.CompilerServices;

namespace Marius.Winter.Taffy
{
    /// <summary>
    /// Extension methods implementing MaybeResolve and ResolveOrZero for
    /// LengthPercentage, LengthPercentageAuto, Dimension, and their Size/Rect wrappers.
    /// </summary>
    public static class TaffyResolve
    {
        // =====================================================================
        // LengthPercentage.MaybeResolve(float?) -> float?
        // =====================================================================

        /// <summary>
        /// Converts a LengthPercentage into an absolute length.
        /// Length -> Some(value), Percent -> Some(value * context) if context is Some else None.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float? MaybeResolve(this LengthPercentage self, float? context, Func<IntPtr, float, float> calc)
        {
            return self.Inner.Tag switch
            {
                CompactLength.LENGTH_TAG => self.Inner.Value,
                CompactLength.PERCENT_TAG => context.HasValue ? context.Value * self.Inner.Value : null,
                _ when self.Inner.IsCalc() => context.HasValue ? calc(self.Inner.CalcValue, context.Value) : null,
                _ => throw new InvalidOperationException("Unexpected CompactLength tag in LengthPercentage"),
            };
        }

        /// <summary>
        /// Converts a LengthPercentage into an absolute length using a definite context.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float? MaybeResolve(this LengthPercentage self, float context, Func<IntPtr, float, float> calc)
        {
            return self.MaybeResolve((float?)context, calc);
        }

        // =====================================================================
        // LengthPercentageAuto.MaybeResolve(float?) -> float?
        // =====================================================================

        /// <summary>
        /// Converts a LengthPercentageAuto into an absolute length.
        /// Auto -> None, Length -> Some(value), Percent -> Some(value * context) if context is Some else None.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float? MaybeResolve(this LengthPercentageAuto self, float? context, Func<IntPtr, float, float> calc)
        {
            return self.Inner.Tag switch
            {
                CompactLength.AUTO_TAG => null,
                CompactLength.LENGTH_TAG => self.Inner.Value,
                CompactLength.PERCENT_TAG => context.HasValue ? context.Value * self.Inner.Value : null,
                _ when self.Inner.IsCalc() => context.HasValue ? calc(self.Inner.CalcValue, context.Value) : null,
                _ => throw new InvalidOperationException("Unexpected CompactLength tag in LengthPercentageAuto"),
            };
        }

        /// <summary>
        /// Converts a LengthPercentageAuto into an absolute length using a definite context.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float? MaybeResolve(this LengthPercentageAuto self, float context, Func<IntPtr, float, float> calc)
        {
            return self.MaybeResolve((float?)context, calc);
        }

        // =====================================================================
        // Dimension.MaybeResolve(float?) -> float?
        // =====================================================================

        /// <summary>
        /// Converts a Dimension into an absolute length.
        /// Auto -> None, Length -> Some(value), Percent -> Some(value * context) if context is Some else None.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float? MaybeResolve(this Dimension self, float? context, Func<IntPtr, float, float> calc)
        {
            return self.Inner.Tag switch
            {
                CompactLength.AUTO_TAG => null,
                CompactLength.LENGTH_TAG => self.Inner.Value,
                CompactLength.PERCENT_TAG => context.HasValue ? context.Value * self.Inner.Value : null,
                _ when self.Inner.IsCalc() => context.HasValue ? calc(self.Inner.CalcValue, context.Value) : null,
                _ => throw new InvalidOperationException("Unexpected CompactLength tag in Dimension"),
            };
        }

        /// <summary>
        /// Converts a Dimension into an absolute length using a definite context.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float? MaybeResolve(this Dimension self, float context, Func<IntPtr, float, float> calc)
        {
            return self.MaybeResolve((float?)context, calc);
        }

        // =====================================================================
        // Size<Dimension>.MaybeResolve(Size<float?>) -> Size<float?>
        // =====================================================================

        /// <summary>
        /// Converts a Size of Dimension into a Size of float? given a parent size context.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<float?> MaybeResolve(this Size<Dimension> self, Size<float?> context, Func<IntPtr, float, float> calc)
        {
            return new Size<float?>(
                self.Width.MaybeResolve(context.Width, calc),
                self.Height.MaybeResolve(context.Height, calc));
        }

        // =====================================================================
        // Size<LengthPercentage>.MaybeResolve(Size<float?>) -> Size<float?>
        // =====================================================================

        /// <summary>
        /// Converts a Size of LengthPercentage into a Size of float? given a parent size context.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<float?> MaybeResolve(this Size<LengthPercentage> self, Size<float?> context, Func<IntPtr, float, float> calc)
        {
            return new Size<float?>(
                self.Width.MaybeResolve(context.Width, calc),
                self.Height.MaybeResolve(context.Height, calc));
        }

        // =====================================================================
        // Size<LengthPercentageAuto>.MaybeResolve(Size<float?>) -> Size<float?>
        // =====================================================================

        /// <summary>
        /// Converts a Size of LengthPercentageAuto into a Size of float? given a parent size context.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<float?> MaybeResolve(this Size<LengthPercentageAuto> self, Size<float?> context, Func<IntPtr, float, float> calc)
        {
            return new Size<float?>(
                self.Width.MaybeResolve(context.Width, calc),
                self.Height.MaybeResolve(context.Height, calc));
        }

        // =====================================================================
        // ResolveOrZero: LengthPercentage
        // =====================================================================

        /// <summary>
        /// Resolves a LengthPercentage to an absolute value, returning 0 if resolution fails.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveOrZero(this LengthPercentage self, float? context, Func<IntPtr, float, float> calc)
        {
            return self.MaybeResolve(context, calc) ?? 0.0f;
        }

        // =====================================================================
        // ResolveOrZero: LengthPercentageAuto
        // =====================================================================

        /// <summary>
        /// Resolves a LengthPercentageAuto to an absolute value, returning 0 if resolution fails.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveOrZero(this LengthPercentageAuto self, float? context, Func<IntPtr, float, float> calc)
        {
            return self.MaybeResolve(context, calc) ?? 0.0f;
        }

        // =====================================================================
        // ResolveOrZero: Dimension
        // =====================================================================

        /// <summary>
        /// Resolves a Dimension to an absolute value, returning 0 if resolution fails.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveOrZero(this Dimension self, float? context, Func<IntPtr, float, float> calc)
        {
            return self.MaybeResolve(context, calc) ?? 0.0f;
        }

        // =====================================================================
        // ResolveOrZero: Size<T> -> Size<float>
        // =====================================================================

        /// <summary>
        /// Resolves a Size of Dimension to a Size of float, using 0 as fallback.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<float> ResolveOrZero(this Size<Dimension> self, Size<float?> context, Func<IntPtr, float, float> calc)
        {
            return new Size<float>(
                self.Width.ResolveOrZero(context.Width, calc),
                self.Height.ResolveOrZero(context.Height, calc));
        }

        /// <summary>
        /// Resolves a Size of LengthPercentage to a Size of float, using 0 as fallback.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<float> ResolveOrZero(this Size<LengthPercentage> self, Size<float?> context, Func<IntPtr, float, float> calc)
        {
            return new Size<float>(
                self.Width.ResolveOrZero(context.Width, calc),
                self.Height.ResolveOrZero(context.Height, calc));
        }

        /// <summary>
        /// Resolves a Size of LengthPercentageAuto to a Size of float, using 0 as fallback.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<float> ResolveOrZero(this Size<LengthPercentageAuto> self, Size<float?> context, Func<IntPtr, float, float> calc)
        {
            return new Size<float>(
                self.Width.ResolveOrZero(context.Width, calc),
                self.Height.ResolveOrZero(context.Height, calc));
        }

        // =====================================================================
        // ResolveOrZero: Rect<T> against Size<In> -> Rect<float>
        // =====================================================================

        /// <summary>
        /// Resolves a Rect of Dimension to a Rect of float against a parent Size.
        /// Left/right resolve against width; top/bottom resolve against height.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect<float> ResolveOrZero(this Rect<Dimension> self, Size<float?> context, Func<IntPtr, float, float> calc)
        {
            return new Rect<float>(
                self.Left.ResolveOrZero(context.Width, calc),
                self.Right.ResolveOrZero(context.Width, calc),
                self.Top.ResolveOrZero(context.Height, calc),
                self.Bottom.ResolveOrZero(context.Height, calc));
        }

        /// <summary>
        /// Resolves a Rect of LengthPercentage to a Rect of float against a parent Size.
        /// Left/right resolve against width; top/bottom resolve against height.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect<float> ResolveOrZero(this Rect<LengthPercentage> self, Size<float?> context, Func<IntPtr, float, float> calc)
        {
            return new Rect<float>(
                self.Left.ResolveOrZero(context.Width, calc),
                self.Right.ResolveOrZero(context.Width, calc),
                self.Top.ResolveOrZero(context.Height, calc),
                self.Bottom.ResolveOrZero(context.Height, calc));
        }

        /// <summary>
        /// Resolves a Rect of LengthPercentageAuto to a Rect of float against a parent Size.
        /// Left/right resolve against width; top/bottom resolve against height.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect<float> ResolveOrZero(this Rect<LengthPercentageAuto> self, Size<float?> context, Func<IntPtr, float, float> calc)
        {
            return new Rect<float>(
                self.Left.ResolveOrZero(context.Width, calc),
                self.Right.ResolveOrZero(context.Width, calc),
                self.Top.ResolveOrZero(context.Height, calc),
                self.Bottom.ResolveOrZero(context.Height, calc));
        }

        // =====================================================================
        // ResolveOrZero: Rect<T> against float? -> Rect<float>
        // (all sides resolve against the same single context value)
        // =====================================================================

        /// <summary>
        /// Resolves a Rect of Dimension to a Rect of float against a single float? context.
        /// All four sides resolve against the same value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect<float> ResolveOrZero(this Rect<Dimension> self, float? context, Func<IntPtr, float, float> calc)
        {
            return new Rect<float>(
                self.Left.ResolveOrZero(context, calc),
                self.Right.ResolveOrZero(context, calc),
                self.Top.ResolveOrZero(context, calc),
                self.Bottom.ResolveOrZero(context, calc));
        }

        /// <summary>
        /// Resolves a Rect of LengthPercentage to a Rect of float against a single float? context.
        /// All four sides resolve against the same value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect<float> ResolveOrZero(this Rect<LengthPercentage> self, float? context, Func<IntPtr, float, float> calc)
        {
            return new Rect<float>(
                self.Left.ResolveOrZero(context, calc),
                self.Right.ResolveOrZero(context, calc),
                self.Top.ResolveOrZero(context, calc),
                self.Bottom.ResolveOrZero(context, calc));
        }

        /// <summary>
        /// Resolves a Rect of LengthPercentageAuto to a Rect of float against a single float? context.
        /// All four sides resolve against the same value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect<float> ResolveOrZero(this Rect<LengthPercentageAuto> self, float? context, Func<IntPtr, float, float> calc)
        {
            return new Rect<float>(
                self.Left.ResolveOrZero(context, calc),
                self.Right.ResolveOrZero(context, calc),
                self.Top.ResolveOrZero(context, calc),
                self.Bottom.ResolveOrZero(context, calc));
        }

        // =====================================================================
        // MaybeResolve: Rect<LengthPercentageAuto> against Size<float?> -> Rect<float?>
        // =====================================================================

        /// <summary>
        /// Resolves a Rect of LengthPercentageAuto to a Rect of float? against a parent Size.
        /// Left/right resolve against width; top/bottom resolve against height.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect<float?> MaybeResolve(this Rect<LengthPercentageAuto> self, Size<float?> context, Func<IntPtr, float, float> calc)
        {
            return new Rect<float?>(
                self.Left.MaybeResolve(context.Width, calc),
                self.Right.MaybeResolve(context.Width, calc),
                self.Top.MaybeResolve(context.Height, calc),
                self.Bottom.MaybeResolve(context.Height, calc));
        }
    }
}
