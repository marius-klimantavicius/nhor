// Ported from taffy/src/style_helpers.rs
// Helper functions and interfaces which make it easier to create instances of
// types in the style and geometry modules.

using System.Runtime.CompilerServices;

namespace Marius.Winter.Taffy
{
    // =========================================================================
    // Trait-equivalent interfaces
    // =========================================================================

    /// <summary>
    /// Trait to abstract over zero values.
    /// Types implementing this interface provide a ZERO constant.
    /// </summary>
    public interface ITaffyZero<T>
    {
        /// <summary>The zero value for the type</summary>
        static abstract T Zero { get; }
    }

    /// <summary>
    /// Trait to abstract over auto values.
    /// </summary>
    public interface ITaffyAuto<T>
    {
        /// <summary>The auto value for the type</summary>
        static abstract T TaffyAuto { get; }
    }

    /// <summary>
    /// Trait to abstract over min-content values.
    /// </summary>
    public interface ITaffyMinContent<T>
    {
        /// <summary>The min-content value for the type</summary>
        static abstract T TaffyMinContent { get; }
    }

    /// <summary>
    /// Trait to abstract over max-content values.
    /// </summary>
    public interface ITaffyMaxContent<T>
    {
        /// <summary>The max-content value for the type</summary>
        static abstract T TaffyMaxContent { get; }
    }

    /// <summary>
    /// Trait to abstract over fit-content values.
    /// </summary>
    public interface ITaffyFitContent<T>
    {
        /// <summary>Create a fit-content value from a LengthPercentage argument</summary>
        static abstract T FitContent(LengthPercentage argument);
    }

    /// <summary>
    /// Trait to abstract over creating absolute length values from plain numbers.
    /// </summary>
    public interface IFromLength<T>
    {
        /// <summary>Create Self from a length value</summary>
        static abstract T FromLength(float value);
    }

    /// <summary>
    /// Trait to abstract over creating percentage values from plain numbers.
    /// </summary>
    public interface IFromPercent<T>
    {
        /// <summary>Create Self from a percent value</summary>
        static abstract T FromPercent(float value);
    }

    /// <summary>
    /// Trait to abstract over creating fr (grid fraction) values from plain numbers.
    /// </summary>
    public interface IFromFr<T>
    {
        /// <summary>Create Self from an fr value</summary>
        static abstract T FromFr(float value);
    }

    /// <summary>
    /// Trait to abstract over grid line values.
    /// </summary>
    public interface ITaffyGridLine<T>
    {
        /// <summary>Converts an i16 line index into Self</summary>
        static abstract T FromLineIndex(short index);
    }

    /// <summary>
    /// Trait to abstract over grid span values.
    /// </summary>
    public interface ITaffyGridSpan<T>
    {
        /// <summary>Converts a u16 span into Self</summary>
        static abstract T FromSpan(ushort span);
    }

    // =========================================================================
    // Static helper methods (free functions from Rust)
    // =========================================================================

    /// <summary>
    /// Helper functions which make it easier to create instances of style and geometry types.
    /// Mirrors the free functions from taffy/src/style_helpers.rs.
    /// </summary>
    public static class StyleHelpers
    {
        // --- Zero ---

        /// <summary>Returns the zero value for CompactLength</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CompactLength Zero() => CompactLength.ZERO;

        /// <summary>Returns the zero value for LengthPercentage</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LengthPercentage ZeroLengthPercentage() => LengthPercentage.ZERO;

        /// <summary>Returns the zero value for LengthPercentageAuto</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LengthPercentageAuto ZeroLengthPercentageAuto() => LengthPercentageAuto.ZERO;

        /// <summary>Returns the zero value for Dimension</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Dimension ZeroDimension() => Dimension.ZERO;

        /// <summary>Returns a zero Point</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Point<float> ZeroPoint() => new Point<float>(0f, 0f);

        /// <summary>Returns a zero Size</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<float> ZeroSize() => new Size<float>(0f, 0f);

        /// <summary>Returns a zero Rect</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect<float> ZeroRect() => new Rect<float>(0f, 0f, 0f, 0f);

        /// <summary>Returns a zero Line</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Line<float> ZeroLine() => new Line<float>(0f, 0f);

        // --- Auto ---

        /// <summary>Returns the auto value for CompactLength</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CompactLength AutoCompact() => CompactLength.AUTO;

        /// <summary>Returns the auto value for LengthPercentageAuto</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LengthPercentageAuto AutoLengthPercentageAuto() => LengthPercentageAuto.AUTO;

        /// <summary>Returns the auto value for Dimension</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Dimension AutoDimension() => Dimension.AUTO;

        /// <summary>Returns an auto Size of LengthPercentageAuto</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<LengthPercentageAuto> AutoSizeLPA() =>
            new Size<LengthPercentageAuto>(LengthPercentageAuto.AUTO, LengthPercentageAuto.AUTO);

        /// <summary>Returns an auto Rect of LengthPercentageAuto</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect<LengthPercentageAuto> AutoRectLPA() =>
            new Rect<LengthPercentageAuto>(
                LengthPercentageAuto.AUTO, LengthPercentageAuto.AUTO,
                LengthPercentageAuto.AUTO, LengthPercentageAuto.AUTO);

        /// <summary>Returns an auto Size of Dimension</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<Dimension> AutoSizeDimension() =>
            new Size<Dimension>(Dimension.AUTO, Dimension.AUTO);

        /// <summary>Returns an auto Rect of Dimension</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect<Dimension> AutoRectDimension() =>
            new Rect<Dimension>(Dimension.AUTO, Dimension.AUTO, Dimension.AUTO, Dimension.AUTO);

        // --- MinContent ---

        /// <summary>Returns the min-content value for CompactLength</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CompactLength MinContent() => CompactLength.MIN_CONTENT;

        // --- MaxContent ---

        /// <summary>Returns the max-content value for CompactLength</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CompactLength MaxContent() => CompactLength.MAX_CONTENT;

        // --- FitContent ---

        /// <summary>Returns a fit-content CompactLength from a LengthPercentage argument</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CompactLength FitContent(LengthPercentage argument) =>
            CompactLength.FitContent(argument);

        // --- Length ---

        /// <summary>Returns a length value of type LengthPercentage</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LengthPercentage Length(float value) => LengthPercentage.Length(value);

        /// <summary>Returns a length value of type LengthPercentageAuto</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LengthPercentageAuto LengthLPA(float value) => LengthPercentageAuto.Length(value);

        /// <summary>Returns a length value of type Dimension</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Dimension LengthDimension(float value) => Dimension.Length(value);

        /// <summary>Returns a Size of LengthPercentage with both values set to the given length</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<LengthPercentage> LengthSize(float value)
        {
            var lp = LengthPercentage.Length(value);
            return new Size<LengthPercentage>(lp, lp);
        }

        /// <summary>Returns a Rect of LengthPercentage with all values set to the given length</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect<LengthPercentage> LengthRect(float value)
        {
            var lp = LengthPercentage.Length(value);
            return new Rect<LengthPercentage>(lp, lp, lp, lp);
        }

        // --- Percent ---

        /// <summary>Returns a percent value of type LengthPercentage</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LengthPercentage Percent(float value) => LengthPercentage.Percent(value);

        /// <summary>Returns a percent value of type LengthPercentageAuto</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LengthPercentageAuto PercentLPA(float value) => LengthPercentageAuto.Percent(value);

        /// <summary>Returns a percent value of type Dimension</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Dimension PercentDimension(float value) => Dimension.Percent(value);

        /// <summary>Returns a Size of LengthPercentage with both values set to the given percent</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<LengthPercentage> PercentSize(float value)
        {
            var lp = LengthPercentage.Percent(value);
            return new Size<LengthPercentage>(lp, lp);
        }

        /// <summary>Returns a Rect of LengthPercentage with all values set to the given percent</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect<LengthPercentage> PercentRect(float value)
        {
            var lp = LengthPercentage.Percent(value);
            return new Rect<LengthPercentage>(lp, lp, lp, lp);
        }

        // --- Fr (grid fraction) ---

        /// <summary>Returns an fr value for CompactLength</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CompactLength Fr(float value) => CompactLength.Fr(value);

        // --- Grid helpers ---

        /// <summary>
        /// Specifies a grid line to place a grid item between in CSS Grid Line coordinates.
        /// Positive indices count upwards from the start (top or left) of the explicit grid.
        /// Negative indices count downwards from the end (bottom or right) of the explicit grid.
        /// ZERO IS INVALID index, and will be treated as a GridPlacement::Auto.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GridLine<T>(short index) where T : ITaffyGridLine<T> =>
            T.FromLineIndex(index);

        /// <summary>
        /// Returns a GridPlacement::Span
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GridSpan<T>(ushort span) where T : ITaffyGridSpan<T> =>
            T.FromSpan(span);

        /// <summary>
        /// Returns a MinMax with min value of min and max value of max.
        /// Generic version for when MinTrackSizingFunction/MaxTrackSizingFunction are defined.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MinMax<TMin, TMax> Minmax<TMin, TMax>(TMin min, TMax max) =>
            new MinMax<TMin, TMax>(min, max);

        // --- Zero helpers for geometry types with specific Taffy types ---

        /// <summary>Returns a zero Size of LengthPercentage</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<LengthPercentage> ZeroSizeLP() =>
            new Size<LengthPercentage>(LengthPercentage.ZERO, LengthPercentage.ZERO);

        /// <summary>Returns a zero Rect of LengthPercentage</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect<LengthPercentage> ZeroRectLP() =>
            new Rect<LengthPercentage>(LengthPercentage.ZERO, LengthPercentage.ZERO,
                LengthPercentage.ZERO, LengthPercentage.ZERO);

        /// <summary>Returns a zero Size of LengthPercentageAuto</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<LengthPercentageAuto> ZeroSizeLPA() =>
            new Size<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO);

        /// <summary>Returns a zero Rect of LengthPercentageAuto</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect<LengthPercentageAuto> ZeroRectLPA() =>
            new Rect<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO,
                LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO);

        /// <summary>Returns a zero Size of Dimension</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<Dimension> ZeroSizeDimension() =>
            new Size<Dimension>(Dimension.ZERO, Dimension.ZERO);

        /// <summary>Returns a zero Rect of Dimension</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect<Dimension> ZeroRectDimension() =>
            new Rect<Dimension>(Dimension.ZERO, Dimension.ZERO, Dimension.ZERO, Dimension.ZERO);

        /// <summary>Returns a zero Line of LengthPercentage</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Line<LengthPercentage> ZeroLineLP() =>
            new Line<LengthPercentage>(LengthPercentage.ZERO, LengthPercentage.ZERO);

        /// <summary>Returns a zero Line of LengthPercentageAuto</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Line<LengthPercentageAuto> ZeroLineLPA() =>
            new Line<LengthPercentageAuto>(LengthPercentageAuto.ZERO, LengthPercentageAuto.ZERO);

        // --- MinContent helpers for geometry types ---

        /// <summary>Returns a min-content Size of CompactLength</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<CompactLength> MinContentSize() =>
            new Size<CompactLength>(CompactLength.MIN_CONTENT, CompactLength.MIN_CONTENT);

        /// <summary>Returns a max-content Size of CompactLength</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size<CompactLength> MaxContentSize() =>
            new Size<CompactLength>(CompactLength.MAX_CONTENT, CompactLength.MAX_CONTENT);
    }
}
