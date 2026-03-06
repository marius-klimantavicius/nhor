// Ported from taffy/src/style/compact_length.rs
// A simplified representation of Taffy's tagged-pointer CompactLength.
// In Rust this packs a tag + f32 value into a 64-bit pointer. In C# we use
// a simple byte tag + float value + IntPtr for calc pointers.

using System;
using System.Runtime.CompilerServices;

namespace Marius.Winter.Taffy
{
    /// <summary>
    /// A compact representation of a CSS length value.
    /// Wraps a byte tag, a float value, and an optional calc pointer.
    /// </summary>
    public readonly struct CompactLength : IEquatable<CompactLength>
    {
        // --- Tag constants ---

        /// <summary>The tag indicating a calc() value</summary>
        public const byte CALC_TAG = 0b000;
        /// <summary>The tag indicating a length value</summary>
        public const byte LENGTH_TAG = 0b0000_0001;
        /// <summary>The tag indicating a percentage value</summary>
        public const byte PERCENT_TAG = 0b0000_0010;
        /// <summary>The tag indicating an auto value</summary>
        public const byte AUTO_TAG = 0b0000_0011;
        /// <summary>The tag indicating an fr value</summary>
        public const byte FR_TAG = 0b0000_0100;
        /// <summary>The tag indicating a min-content value</summary>
        public const byte MIN_CONTENT_TAG = 0b00000111;
        /// <summary>The tag indicating a max-content value</summary>
        public const byte MAX_CONTENT_TAG = 0b00001111;
        /// <summary>The tag indicating a fit-content value with px limit</summary>
        public const byte FIT_CONTENT_PX_TAG = 0b00010111;
        /// <summary>The tag indicating a fit-content value with percent limit</summary>
        public const byte FIT_CONTENT_PERCENT_TAG = 0b00011111;

        // --- Fields ---

        /// <summary>The tag indicating what kind of value this is</summary>
        public readonly byte Tag;

        /// <summary>The numeric value (e.g. pixels, percentage fraction, fr fraction)</summary>
        public readonly float Value;

        /// <summary>Opaque pointer for calc() values</summary>
        public readonly IntPtr CalcValue;

        // --- Constructors (private) ---

        private CompactLength(byte tag, float value)
        {
            Tag = tag;
            Value = value;
            CalcValue = IntPtr.Zero;
        }

        private CompactLength(byte tag)
        {
            Tag = tag;
            Value = 0f;
            CalcValue = IntPtr.Zero;
        }

        private CompactLength(IntPtr calcPtr)
        {
            Tag = CALC_TAG;
            Value = 0f;
            CalcValue = calcPtr;
        }

        // --- Well-known constants ---

        /// <summary>A zero-length value (0px)</summary>
        public static readonly CompactLength ZERO = Length(0.0f);

        /// <summary>An auto value</summary>
        public static readonly CompactLength AUTO = Auto();

        /// <summary>A min-content value</summary>
        public static readonly CompactLength MIN_CONTENT = MinContent();

        /// <summary>A max-content value</summary>
        public static readonly CompactLength MAX_CONTENT = MaxContent();

        // --- Static factory methods ---

        /// <summary>
        /// An absolute length in some abstract units. Users of Taffy may define what they correspond
        /// to in their application (pixels, logical pixels, mm, etc) as they see fit.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CompactLength Length(float val) => new CompactLength(LENGTH_TAG, val);

        /// <summary>
        /// A percentage length relative to the size of the containing block.
        /// NOTE: percentages are represented as a f32 value in the range [0.0, 1.0] NOT [0.0, 100.0].
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CompactLength Percent(float val) => new CompactLength(PERCENT_TAG, val);

        /// <summary>
        /// A calc() value. The value passed is an opaque handle to the actual calc representation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CompactLength Calc(IntPtr ptr) => new CompactLength(ptr);

        /// <summary>
        /// The dimension should be automatically computed according to algorithm-specific rules.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CompactLength Auto() => new CompactLength(AUTO_TAG);

        /// <summary>
        /// The dimension as a fraction of the total available grid space (fr units in CSS).
        /// Specified value is the numerator of the fraction. Denominator is the sum of all
        /// fractions specified in that grid dimension.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CompactLength Fr(float val) => new CompactLength(FR_TAG, val);

        /// <summary>
        /// The size should be the "min-content" size.
        /// This is the smallest size that can fit the item's contents with ALL soft
        /// line-wrapping opportunities taken.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CompactLength MinContent() => new CompactLength(MIN_CONTENT_TAG);

        /// <summary>
        /// The size should be the "max-content" size.
        /// This is the smallest size that can fit the item's contents with NO soft
        /// line-wrapping opportunities taken.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CompactLength MaxContent() => new CompactLength(MAX_CONTENT_TAG);

        /// <summary>
        /// fit-content(limit) with a LENGTH (px) limit.
        /// Computes as: max(min_content, min(max_content, limit))
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CompactLength FitContentPx(float limit) => new CompactLength(FIT_CONTENT_PX_TAG, limit);

        /// <summary>
        /// fit-content(limit) with a PERCENTAGE limit.
        /// Computes as: max(min_content, min(max_content, limit))
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CompactLength FitContentPercent(float limit) => new CompactLength(FIT_CONTENT_PERCENT_TAG, limit);

        /// <summary>
        /// Create a fit-content value from a LengthPercentage (dispatches to FitContentPx or FitContentPercent).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CompactLength FitContent(LengthPercentage lp)
        {
            return lp.IntoRaw().Tag switch
            {
                LENGTH_TAG => FitContentPx(lp.IntoRaw().Value),
                PERCENT_TAG => FitContentPercent(lp.IntoRaw().Value),
                _ => throw new InvalidOperationException("LengthPercentage must be Length or Percent"),
            };
        }

        // --- Query methods ---

        /// <summary>Returns true if the value is a calc() value</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsCalc() => Tag == CALC_TAG && CalcValue != IntPtr.Zero;

        /// <summary>Returns true if the value is 0px</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsZero() => Tag == LENGTH_TAG && Value == 0.0f;

        /// <summary>Returns true if the value is a length or percentage value</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsLengthOrPercentage() => Tag == LENGTH_TAG || Tag == PERCENT_TAG;

        /// <summary>Returns true if the value is auto</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsAuto() => Tag == AUTO_TAG;

        /// <summary>Returns true if the value is min-content</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsMinContent() => Tag == MIN_CONTENT_TAG;

        /// <summary>Returns true if the value is max-content</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsMaxContent() => Tag == MAX_CONTENT_TAG;

        /// <summary>Returns true if the value is a fit-content(...) value</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsFitContent() => Tag == FIT_CONTENT_PX_TAG || Tag == FIT_CONTENT_PERCENT_TAG;

        /// <summary>Returns true if the value is max-content or a fit-content(...) value</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsMaxOrFitContent() =>
            Tag == MAX_CONTENT_TAG || Tag == FIT_CONTENT_PX_TAG || Tag == FIT_CONTENT_PERCENT_TAG;

        /// <summary>
        /// Returns true if the max track sizing function is MaxContent, FitContent or Auto.
        /// "In all cases, treat auto and fit-content() as max-content, except where specified
        /// otherwise for fit-content()."
        /// See: https://www.w3.org/TR/css-grid-1/#algo-terms
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsMaxContentAlike() =>
            Tag == AUTO_TAG || Tag == MAX_CONTENT_TAG ||
            Tag == FIT_CONTENT_PX_TAG || Tag == FIT_CONTENT_PERCENT_TAG;

        /// <summary>Returns true if the min track sizing function is MinContent or MaxContent</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsMinOrMaxContent() => Tag == MIN_CONTENT_TAG || Tag == MAX_CONTENT_TAG;

        /// <summary>Returns true if the value is auto, min-content, max-content, or fit-content(...)</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsIntrinsic() =>
            Tag == AUTO_TAG || Tag == MIN_CONTENT_TAG || Tag == MAX_CONTENT_TAG ||
            Tag == FIT_CONTENT_PX_TAG || Tag == FIT_CONTENT_PERCENT_TAG;

        /// <summary>Returns true if the value is an fr value</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsFr() => Tag == FR_TAG;

        /// <summary>Whether the track sizing function depends on the size of the parent node</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool UsesPercentage() =>
            Tag == PERCENT_TAG || Tag == FIT_CONTENT_PERCENT_TAG || IsCalc();

        /// <summary>
        /// Resolve percentage values against the passed parent_size, returning Some(value).
        /// Non-percentage values always return null.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float? ResolvedPercentageSize(float parentSize, Func<IntPtr, float, float> calcResolver)
        {
            if (Tag == PERCENT_TAG)
                return Value * parentSize;
            if (IsCalc())
                return calcResolver(CalcValue, parentSize);
            return null;
        }

        // --- FromLength / FromPercent / FromFr helpers ---

        /// <summary>Create a Length from any numeric value</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CompactLength FromLength(float value) => Length(value);

        /// <summary>Create a Percent from any numeric value</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CompactLength FromPercent(float value) => Percent(value);

        /// <summary>Create an Fr from any numeric value</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CompactLength FromFr(float value) => Fr(value);

        // --- Equality ---

        public bool Equals(CompactLength other) =>
            Tag == other.Tag && Value == other.Value && CalcValue == other.CalcValue;

        public override bool Equals(object? obj) => obj is CompactLength other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Tag, Value, CalcValue);

        public static bool operator ==(CompactLength left, CompactLength right) => left.Equals(right);
        public static bool operator !=(CompactLength left, CompactLength right) => !left.Equals(right);

        public override string ToString()
        {
            return Tag switch
            {
                LENGTH_TAG => $"Length({Value})",
                PERCENT_TAG => $"Percent({Value})",
                AUTO_TAG => "Auto",
                FR_TAG => $"Fr({Value})",
                MIN_CONTENT_TAG => "MinContent",
                MAX_CONTENT_TAG => "MaxContent",
                FIT_CONTENT_PX_TAG => $"FitContentPx({Value})",
                FIT_CONTENT_PERCENT_TAG => $"FitContentPercent({Value})",
                CALC_TAG => $"Calc({CalcValue})",
                _ => $"Unknown(tag={Tag}, value={Value})",
            };
        }
    }
}
