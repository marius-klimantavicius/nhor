// Ported from taffy/src/style/dimension.rs
// Style types for representing lengths / sizes

using System;
using System.Runtime.CompilerServices;

namespace Marius.Winter.Taffy
{
    /// <summary>
    /// A unit of linear measurement that is either a length or a percentage.
    /// </summary>
    public readonly struct LengthPercentage : IEquatable<LengthPercentage>
    {
        public ExpandedLengthPercentage Expand() => Inner.Tag switch
        {
            CompactLength.LENGTH_TAG => ExpandedLengthPercentage.Length(Inner.Value),
            CompactLength.PERCENT_TAG => ExpandedLengthPercentage.Percent(Inner.Value),
            _ when Inner.IsPlainCalc() => ExpandedLengthPercentage.Calc(Inner.CalcValue),
            _ => throw new InvalidOperationException("Invalid LengthPercentage tag"),
        };

        public static implicit operator LengthPercentage(ExpandedLengthPercentage value) => value.IntoLengthPercentage();
        internal readonly CompactLength Inner;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal LengthPercentage(CompactLength inner) => Inner = inner;

        // --- Well-known constants ---

        /// <summary>A zero-length value (0px)</summary>
        public static readonly LengthPercentage ZERO = new LengthPercentage(CompactLength.ZERO);

        // --- Static factory methods ---

        /// <summary>
        /// An absolute length in some abstract units. Users of Taffy may define what they correspond
        /// to in their application (pixels, logical pixels, mm, etc) as they see fit.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LengthPercentage Length(double val) => Length((float)val);
        public static LengthPercentage Length(float val) => new LengthPercentage(CompactLength.Length(val));

        /// <summary>
        /// A percentage length relative to the size of the containing block.
        /// NOTE: percentages are represented as a f32 value in the range [0.0, 1.0] NOT [0.0, 100.0].
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LengthPercentage Percent(double val) => Percent((float)val);
        public static LengthPercentage Percent(float val) => new LengthPercentage(CompactLength.Percent(val));

        /// <summary>
        /// A calc() value. The value passed is an opaque handle to the actual calc representation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LengthPercentage Calc(IntPtr ptr) => new LengthPercentage(CompactLength.Calc(ptr));

        /// <summary>Create a LengthPercentage from a raw CompactLength (unsafe: must be a valid variant)</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LengthPercentage FromRaw(CompactLength val) => new LengthPercentage(val);

        /// <summary>Get the underlying CompactLength representation of the value</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CompactLength IntoRaw() => Inner;

        // --- FromLength / FromPercent helpers ---

        /// <summary>Create a Length from any numeric value</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LengthPercentage FromLength(double value) => FromLength((float)value);
        public static LengthPercentage FromLength(float value) => Length(value);

        /// <summary>Create a Percent from any numeric value</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LengthPercentage FromPercent(double value) => FromPercent((float)value);
        public static LengthPercentage FromPercent(float value) => Percent(value);

        // --- Conversions ---

        /// <summary>Implicit conversion to LengthPercentageAuto</summary>
        public static implicit operator LengthPercentageAuto(LengthPercentage lp) =>
            new LengthPercentageAuto(lp.Inner);

        /// <summary>Implicit conversion to Dimension</summary>
        public static implicit operator Dimension(LengthPercentage lp) =>
            new Dimension(lp.Inner);

        // --- Equality ---

        public bool Equals(LengthPercentage other) => Inner == other.Inner;
        public override bool Equals(object? obj) => obj is LengthPercentage other && Equals(other);
        public override int GetHashCode() => Inner.GetHashCode();
        public static bool operator ==(LengthPercentage left, LengthPercentage right) => left.Equals(right);
        public static bool operator !=(LengthPercentage left, LengthPercentage right) => !left.Equals(right);
        public override string ToString() => Inner.ToString();
    }

    /// <summary>
    /// A unit of linear measurement that is either a length, a percentage, or auto.
    /// </summary>
    public readonly struct LengthPercentageAuto : IEquatable<LengthPercentageAuto>
    {
        public ExpandedLengthPercentageAuto Expand() => Inner.Tag switch
        {
            CompactLength.LENGTH_TAG => ExpandedLengthPercentageAuto.Length(Inner.Value),
            CompactLength.PERCENT_TAG => ExpandedLengthPercentageAuto.Percent(Inner.Value),
            CompactLength.AUTO_TAG => ExpandedLengthPercentageAuto.Auto,
            _ when Inner.IsPlainCalc() => ExpandedLengthPercentageAuto.Calc(Inner.CalcValue),
            _ => throw new InvalidOperationException("Invalid LengthPercentageAuto tag"),
        };

        public static implicit operator LengthPercentageAuto(ExpandedLengthPercentageAuto value) => value.IntoLengthPercentageAuto();
        internal readonly CompactLength Inner;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal LengthPercentageAuto(CompactLength inner) => Inner = inner;

        // --- Well-known constants ---

        /// <summary>A zero-length value (0px)</summary>
        public static readonly LengthPercentageAuto ZERO = new LengthPercentageAuto(CompactLength.ZERO);

        /// <summary>An auto value</summary>
        public static readonly LengthPercentageAuto AUTO = new LengthPercentageAuto(CompactLength.AUTO);

        // --- Static factory methods ---

        /// <summary>
        /// An absolute length in some abstract units.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LengthPercentageAuto Length(double val) => Length((float)val);
        public static LengthPercentageAuto Length(float val) => new LengthPercentageAuto(CompactLength.Length(val));

        /// <summary>
        /// A percentage length relative to the size of the containing block.
        /// NOTE: percentages are represented as a f32 value in the range [0.0, 1.0] NOT [0.0, 100.0].
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LengthPercentageAuto Percent(double val) => Percent((float)val);
        public static LengthPercentageAuto Percent(float val) => new LengthPercentageAuto(CompactLength.Percent(val));

        /// <summary>
        /// The dimension should be automatically computed according to algorithm-specific rules.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LengthPercentageAuto Auto() => new LengthPercentageAuto(CompactLength.Auto());

        /// <summary>
        /// A calc() value. The value passed is an opaque handle to the actual calc representation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LengthPercentageAuto Calc(IntPtr ptr) => new LengthPercentageAuto(CompactLength.Calc(ptr));

        /// <summary>Create from a raw CompactLength (unsafe: must be a valid variant)</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LengthPercentageAuto FromRaw(CompactLength val) => new LengthPercentageAuto(val);

        /// <summary>Get the underlying CompactLength representation of the value</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CompactLength IntoRaw() => Inner;

        // --- FromLength / FromPercent helpers ---

        /// <summary>Create a Length from any numeric value</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LengthPercentageAuto FromLength(double value) => FromLength((float)value);
        public static LengthPercentageAuto FromLength(float value) => Length(value);

        /// <summary>Create a Percent from any numeric value</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LengthPercentageAuto FromPercent(double value) => FromPercent((float)value);
        public static LengthPercentageAuto FromPercent(float value) => Percent(value);

        // --- Resolution methods ---

        /// <summary>
        /// Returns:
        ///   - Some(length) for Length variants
        ///   - Some(resolved) using the provided context for Percent variants
        ///   - None for Auto variants
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float? ResolveToOption(float context, Func<IntPtr, float, float> calcResolver)
        {
            return Inner.Tag switch
            {
                CompactLength.LENGTH_TAG => Inner.Value,
                CompactLength.PERCENT_TAG => context * Inner.Value,
                CompactLength.AUTO_TAG => null,
                _ when Inner.IsCalc() => calcResolver(Inner.CalcValue, context),
                _ => throw new InvalidOperationException("LengthPercentageAuto values cannot be constructed with other tags"),
            };
        }

        /// <summary>Returns true if value is Auto</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsAuto() => Inner.IsAuto();

        // --- Conversions ---

        /// <summary>Implicit conversion to Dimension</summary>
        public static implicit operator Dimension(LengthPercentageAuto lpa) =>
            new Dimension(lpa.Inner);

        // --- Equality ---

        public bool Equals(LengthPercentageAuto other) => Inner == other.Inner;
        public override bool Equals(object? obj) => obj is LengthPercentageAuto other && Equals(other);
        public override int GetHashCode() => Inner.GetHashCode();
        public static bool operator ==(LengthPercentageAuto left, LengthPercentageAuto right) => left.Equals(right);
        public static bool operator !=(LengthPercentageAuto left, LengthPercentageAuto right) => !left.Equals(right);
        public override string ToString() => Inner.ToString();
    }

    /// <summary>
    /// A unit of linear measurement that is either a length, a percentage, or auto.
    /// Used for sizing properties like width, height, min-width, etc.
    /// </summary>
    public readonly struct Dimension : IEquatable<Dimension>
    {
        public ExpandedDimension Expand() => Inner.Tag switch
        {
            CompactLength.LENGTH_TAG => ExpandedDimension.Length(Inner.Value),
            CompactLength.PERCENT_TAG => ExpandedDimension.Percent(Inner.Value),
            CompactLength.AUTO_TAG => ExpandedDimension.Auto,
            CompactLength.MIN_CONTENT_TAG => ExpandedDimension.MinContent,
            CompactLength.MAX_CONTENT_TAG => ExpandedDimension.MaxContent,
            CompactLength.FIT_CONTENT_PX_TAG => ExpandedDimension.FitContentPx(Inner.Value),
            CompactLength.FIT_CONTENT_PERCENT_TAG => ExpandedDimension.FitContentPercent(Inner.Value),
            CompactLength.FIT_CONTENT_KEYWORD_TAG => ExpandedDimension.FitContent,
            CompactLength.STRETCH_TAG => ExpandedDimension.Stretch,
            CompactLength.CONTENT_TAG => ExpandedDimension.Content,
            _ when Inner.IsFitContentCalc() => ExpandedDimension.FitContentCalc(Inner.CalcValue),
            _ when Inner.IsPlainCalc() => ExpandedDimension.Calc(Inner.CalcValue),
            _ => throw new InvalidOperationException("Invalid Dimension tag"),
        };

        public static implicit operator Dimension(ExpandedDimension value) => value.IntoDimension();
        internal readonly CompactLength Inner;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Dimension(CompactLength inner) => Inner = inner;

        // --- Well-known constants ---

        /// <summary>A zero-length value (0px)</summary>
        public static readonly Dimension ZERO = new Dimension(CompactLength.ZERO);

        /// <summary>An auto value</summary>
        public static readonly Dimension AUTO = new Dimension(CompactLength.AUTO);
        public static readonly Dimension MIN_CONTENT = new Dimension(CompactLength.MIN_CONTENT);
        public static readonly Dimension MAX_CONTENT = new Dimension(CompactLength.MAX_CONTENT);
        public static Dimension MinContent() => MIN_CONTENT;
        public static Dimension MaxContent() => MAX_CONTENT;
        public static Dimension FitContent() => new Dimension(CompactLength.FitContentKeyword());
        public static Dimension FitContentPx(double limit) => FitContentPx((float)limit);
        public static Dimension FitContentPx(float limit) => new Dimension(CompactLength.FitContentPx(limit));
        public static Dimension FitContentPercent(double limit) => FitContentPercent((float)limit);
        public static Dimension FitContentPercent(float limit) => new Dimension(CompactLength.FitContentPercent(limit));
        public static Dimension FitContentCalc(IntPtr ptr) => new Dimension(CompactLength.FitContentCalc(ptr));
        public static Dimension Stretch() => new Dimension(CompactLength.Stretch());
        public static Dimension Content() => new Dimension(CompactLength.Content());
        public bool IsSizingKeyword() => Inner.IsSizingKeyword();
        public bool IsStretch() => Inner.Tag == CompactLength.STRETCH_TAG;
        public bool IsContent() => Inner.IsContent();
        public bool IsFitContentCalc() => Inner.IsFitContentCalc();
        public IntPtr CalcValue() => Inner.CalcValue;

        // --- Static factory methods ---

        /// <summary>
        /// An absolute length in some abstract units.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Dimension Length(double val) => Length((float)val);
        public static Dimension Length(float val) => new Dimension(CompactLength.Length(val));

        /// <summary>
        /// A percentage length relative to the size of the containing block.
        /// NOTE: percentages are represented as a f32 value in the range [0.0, 1.0] NOT [0.0, 100.0].
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Dimension Percent(double val) => Percent((float)val);
        public static Dimension Percent(float val) => new Dimension(CompactLength.Percent(val));

        /// <summary>
        /// The dimension should be automatically computed according to algorithm-specific rules.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Dimension Auto() => new Dimension(CompactLength.Auto());

        /// <summary>
        /// A calc() value. The value passed is an opaque handle to the actual calc representation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Dimension Calc(IntPtr ptr) => new Dimension(CompactLength.Calc(ptr));

        /// <summary>Create from a raw CompactLength (unsafe: must be a valid variant)</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Dimension FromRaw(CompactLength val) => new Dimension(val);

        /// <summary>Get the underlying CompactLength representation of the value</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CompactLength IntoRaw() => Inner;

        // --- FromLength / FromPercent helpers ---

        /// <summary>Create a Length from any numeric value</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Dimension FromLength(double value) => FromLength((float)value);
        public static Dimension FromLength(float value) => Length(value);

        /// <summary>Create a Percent from any numeric value</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Dimension FromPercent(double value) => FromPercent((float)value);
        public static Dimension FromPercent(float value) => Percent(value);

        // --- Query methods ---

        /// <summary>Get Length value if value is Length variant, else null</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float? IntoOption()
        {
            return Inner.Tag == CompactLength.LENGTH_TAG ? Inner.Value : null;
        }

        /// <summary>Returns true if value is Auto</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsAuto() => Inner.IsAuto();

        /// <summary>Get the raw CompactLength tag</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte GetTag() => Inner.Tag;

        /// <summary>Get the raw CompactLength value for non-calc variants that have a numeric parameter</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetValue() => Inner.Value;

        // --- Equality ---

        public bool Equals(Dimension other) => Inner == other.Inner;
        public override bool Equals(object? obj) => obj is Dimension other && Equals(other);
        public override int GetHashCode() => Inner.GetHashCode();
        public static bool operator ==(Dimension left, Dimension right) => left.Equals(right);
        public static bool operator !=(Dimension left, Dimension right) => !left.Equals(right);
        public override string ToString() => Inner.ToString();
    }

    // --- Rect<Dimension> extension methods ---

    public static class RectDimensionExtensions
    {
        /// <summary>Create a new Rect with length values</summary>
        public static Rect<Dimension> FromLength(float start, float end, float top, float bottom)
        {
            return new Rect<Dimension>(
                Dimension.Length(start),
                Dimension.Length(end),
                Dimension.Length(top),
                Dimension.Length(bottom)
            );
        }

        /// <summary>Create a new Rect with percentage values</summary>
        public static Rect<Dimension> FromPercent(float start, float end, float top, float bottom)
        {
            return new Rect<Dimension>(
                Dimension.Percent(start),
                Dimension.Percent(end),
                Dimension.Percent(top),
                Dimension.Percent(bottom)
            );
        }
    }
}
