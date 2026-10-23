// Ported from taffy/src/style/compact_length.rs
// Managed fields retain the native tag/value/handle accessors without storing tagged GC pointers.

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
        public const byte NON_POINTER_BIT = 0b0001;
        public const byte KEYWORD_BIT = 0b0010;
        public const byte PERCENTAGE_BIT = 0b0100;
        public const byte SIZING_KEYWORD_BIT = 0b1000;
        public const byte CALC_TAG = 0b0000_0100;
        public const byte FIT_CONTENT_CALC_TAG = 0b0000_0110;
        public const byte CONTENT_TAG = 0b0010_0011;
        public const byte FIT_CONTENT_KEYWORD_TAG = 0b0011_1011;
        public const byte STRETCH_TAG = 0b0100_1011;
        /// <summary>The tag indicating a length value</summary>
        public const byte LENGTH_TAG = 0b0000_0001;
        /// <summary>The tag indicating a percentage value</summary>
        public const byte PERCENT_TAG = 0b0000_0101;
        /// <summary>The tag indicating an auto value</summary>
        public const byte AUTO_TAG = 0b0000_0011;
        /// <summary>The tag indicating an fr value</summary>
        public const byte FR_TAG = 0b0001_0011;
        /// <summary>The tag indicating a min-content value</summary>
        public const byte MIN_CONTENT_TAG = 0b0000_1011;
        /// <summary>The tag indicating a max-content value</summary>
        public const byte MAX_CONTENT_TAG = 0b0001_1011;
        /// <summary>The tag indicating a fit-content value with px limit</summary>
        public const byte FIT_CONTENT_PX_TAG = 0b0010_1011;
        /// <summary>The tag indicating a fit-content value with percent limit</summary>
        public const byte FIT_CONTENT_PERCENT_TAG = 0b0000_1111;

        // --- Fields ---

        /// <summary>The low byte of the native tag; on 64-bit systems calc tags also include handle address bits 3..7.</summary>
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
        // On 64-bit native targets serde preserves all bits of the tag word, even
        // those above the low byte inspected by tag(). For non-pointer values the
        // otherwise unused handle field retains these bits without enlarging the struct.
        internal static CompactLength FromSerializedNumeric(ulong raw) => new((uint)(raw >> 32), BitConverter.UInt32BitsToSingle((uint)raw));
        private CompactLength(uint tagWord, float value)
        {
            Tag = (byte)tagWord; Value = value; CalcValue = (IntPtr)(long)(tagWord & ~255u);
        }
        internal ulong SerializedValue() => HasCalc()
            ? IntPtr.Size == 8 ? ((ulong)(nuint)CalcValue | Tag) << 32 | ((ulong)(nuint)CalcValue | Tag) >> 32 : (ulong)Tag << 32 | (uint)(nuint)CalcValue
            : ((ulong)(uint)(nuint)CalcValue | Tag) << 32 | BitConverter.SingleToUInt32Bits(Value);

        private CompactLength(byte tag)
        {
            Tag = tag;
            Value = 0f;
            CalcValue = IntPtr.Zero;
        }

        private CompactLength(IntPtr calcPtr, byte tag = CALC_TAG)
        {
            if (calcPtr == IntPtr.Zero || ((nuint)calcPtr & 7) != 0)
                throw new ArgumentException("A calc handle must be nonzero and aligned to eight bytes", nameof(calcPtr));
            Tag = IntPtr.Size == 8 ? (byte)((nuint)calcPtr | tag) : tag;
            Value = BitConverter.UInt32BitsToSingle(IntPtr.Size == 8 ? (uint)((ulong)(nuint)calcPtr >> 32) : (uint)(nuint)calcPtr);
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
        public static CompactLength Length(double val) => Length((float)val);
        public static CompactLength Length(float val) => new CompactLength(LENGTH_TAG, val);

        /// <summary>
        /// A percentage length relative to the size of the containing block.
        /// NOTE: percentages are represented as a f32 value in the range [0.0, 1.0] NOT [0.0, 100.0].
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CompactLength Percent(double val) => Percent((float)val);
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
        public static CompactLength Fr(double val) => Fr((float)val);
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
        public static CompactLength FitContentPx(double limit) => FitContentPx((float)limit);
        public static CompactLength FitContentPx(float limit) => new CompactLength(FIT_CONTENT_PX_TAG, limit);

        /// <summary>
        /// fit-content(limit) with a PERCENTAGE limit.
        /// Computes as: max(min_content, min(max_content, limit))
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CompactLength FitContentPercent(double limit) => FitContentPercent((float)limit);
        public static CompactLength FitContentPercent(float limit) => new CompactLength(FIT_CONTENT_PERCENT_TAG, limit);

        public static CompactLength FitContentKeyword() => new CompactLength(FIT_CONTENT_KEYWORD_TAG);
        public static CompactLength Stretch() => new CompactLength(STRETCH_TAG);
        public static CompactLength Content() => new CompactLength(CONTENT_TAG);
        public static CompactLength FitContentCalc(IntPtr ptr) => new CompactLength(ptr, FIT_CONTENT_CALC_TAG);

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
                _ when lp.IntoRaw().IsPlainCalc() => FitContentCalc(lp.IntoRaw().CalcValue),
                _ => throw new InvalidOperationException("LengthPercentage must be Length or Percent"),
            };
        }

        // --- Query methods ---

        /// <summary>Returns true if the value is a calc() value</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsCalc() => IsPlainCalc();

        public bool IsPlainCalc() => (Tag & (NON_POINTER_BIT | KEYWORD_BIT)) == 0;
        public bool IsFitContentCalc() => (Tag & (NON_POINTER_BIT | KEYWORD_BIT)) == KEYWORD_BIT;
        public bool HasCalc() => (Tag & NON_POINTER_BIT) == 0;
        public bool IsLengthPercentageOrCalc() => (Tag & KEYWORD_BIT) == 0;
        public bool IsSizingKeyword() =>
            (Tag & (NON_POINTER_BIT | KEYWORD_BIT | SIZING_KEYWORD_BIT)) == (NON_POINTER_BIT | KEYWORD_BIT | SIZING_KEYWORD_BIT)
            || IsFitContentCalc();
        public bool IsContent() => Tag == CONTENT_TAG;

        /// <summary>Returns true if the value is 0px</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsZero() => Tag == LENGTH_TAG && BitConverter.SingleToUInt32Bits(Value) == 0 && CalcValue == IntPtr.Zero;

        /// <summary>Returns true if the value is a length or percentage value</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsLengthOrPercentage() => (Tag & (NON_POINTER_BIT | KEYWORD_BIT)) == NON_POINTER_BIT;

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
        public bool IsFitContent() => Tag == FIT_CONTENT_PX_TAG || Tag == FIT_CONTENT_PERCENT_TAG || IsFitContentCalc();

        /// <summary>Returns true if the value is max-content or a fit-content(...) value</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsMaxOrFitContent() =>
            Tag == MAX_CONTENT_TAG || Tag == FIT_CONTENT_PX_TAG || Tag == FIT_CONTENT_PERCENT_TAG || IsFitContentCalc();

        /// <summary>
        /// Returns true if the max track sizing function is MaxContent, FitContent or Auto.
        /// "In all cases, treat auto and fit-content() as max-content, except where specified
        /// otherwise for fit-content()."
        /// See: https://www.w3.org/TR/css-grid-1/#algo-terms
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsMaxContentAlike() =>
            Tag == AUTO_TAG || Tag == MAX_CONTENT_TAG ||
            Tag == FIT_CONTENT_PX_TAG || Tag == FIT_CONTENT_PERCENT_TAG || IsFitContentCalc();

        /// <summary>Returns true if the min track sizing function is MinContent or MaxContent</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsMinOrMaxContent() => Tag == MIN_CONTENT_TAG || Tag == MAX_CONTENT_TAG;

        /// <summary>Returns true if the value is auto, min-content, max-content, or fit-content(...)</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsIntrinsic() =>
            Tag == AUTO_TAG || Tag == MIN_CONTENT_TAG || Tag == MAX_CONTENT_TAG ||
            Tag == FIT_CONTENT_PX_TAG || Tag == FIT_CONTENT_PERCENT_TAG || IsFitContentCalc();

        /// <summary>Returns true if the value is an fr value</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsFr() => Tag == FR_TAG;

        /// <summary>Whether the track sizing function depends on the size of the parent node</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool UsesPercentage() => (Tag & PERCENTAGE_BIT) != 0;

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
        public static CompactLength FromLength(double value) => FromLength((float)value);
        public static CompactLength FromLength(float value) => Length(value);

        /// <summary>Create a Percent from any numeric value</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CompactLength FromPercent(double value) => FromPercent((float)value);
        public static CompactLength FromPercent(float value) => Percent(value);

        /// <summary>Create an Fr from any numeric value</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CompactLength FromFr(double value) => FromFr((float)value);
        public static CompactLength FromFr(float value) => Fr(value);

        // --- Equality ---

        public bool Equals(CompactLength other) =>
            Tag == other.Tag && BitConverter.SingleToUInt32Bits(Value) == BitConverter.SingleToUInt32Bits(other.Value) && CalcValue == other.CalcValue;

        public override bool Equals(object? obj) => obj is CompactLength other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Tag, BitConverter.SingleToUInt32Bits(Value), CalcValue);

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
                _ when IsPlainCalc() => $"Calc({CalcValue})",
                _ when IsFitContentCalc() => $"FitContentCalc({CalcValue})",
                FIT_CONTENT_KEYWORD_TAG => "FitContent",
                STRETCH_TAG => "Stretch",
                CONTENT_TAG => "Content",
                _ => $"Unknown(tag={Tag}, value={Value})",
            };
        }
    }
}
