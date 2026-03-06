// Ported from taffy/src/style/grid.rs
// Grid-related style types: GridPlacement, TrackSizingFunction, GridTemplateComponent, etc.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Marius.Winter.Taffy
{
    /// <summary>
    /// Maximum track sizing function.
    /// Specifies the maximum size of a grid track.
    /// See https://developer.mozilla.org/en-US/docs/Web/CSS/grid-template-columns
    /// </summary>
    public readonly struct MaxTrackSizingFunction : IEquatable<MaxTrackSizingFunction>,
        IFromLength<MaxTrackSizingFunction>, IFromPercent<MaxTrackSizingFunction>,
        IFromFr<MaxTrackSizingFunction>, ITaffyFitContent<MaxTrackSizingFunction>
    {
        internal readonly CompactLength Inner;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal MaxTrackSizingFunction(CompactLength inner) => Inner = inner;

        public static readonly MaxTrackSizingFunction ZERO = new(CompactLength.ZERO);
        public static readonly MaxTrackSizingFunction AUTO = new(CompactLength.AUTO);
        public static readonly MaxTrackSizingFunction MIN_CONTENT = new(CompactLength.MIN_CONTENT);
        public static readonly MaxTrackSizingFunction MAX_CONTENT = new(CompactLength.MAX_CONTENT);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MaxTrackSizingFunction Length(float val) => new(CompactLength.Length(val));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MaxTrackSizingFunction Percent(float val) => new(CompactLength.Percent(val));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MaxTrackSizingFunction Fr(float val) => new(CompactLength.Fr(val));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MaxTrackSizingFunction FitContent(LengthPercentage argument) =>
            new(CompactLength.FitContent(argument));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MaxTrackSizingFunction FitContentPx(float limit) =>
            new(CompactLength.FitContentPx(limit));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MaxTrackSizingFunction FitContentPercent(float limit) =>
            new(CompactLength.FitContentPercent(limit));

        static MaxTrackSizingFunction IFromLength<MaxTrackSizingFunction>.FromLength(float value) => Length(value);
        static MaxTrackSizingFunction IFromPercent<MaxTrackSizingFunction>.FromPercent(float value) => Percent(value);
        static MaxTrackSizingFunction IFromFr<MaxTrackSizingFunction>.FromFr(float value) => Fr(value);
        static MaxTrackSizingFunction ITaffyFitContent<MaxTrackSizingFunction>.FitContent(LengthPercentage argument) => FitContent(argument);

        public static implicit operator MaxTrackSizingFunction(LengthPercentage lp) => new(lp.IntoRaw());
        public static implicit operator MaxTrackSizingFunction(LengthPercentageAuto lpa) => new(lpa.IntoRaw());
        public static implicit operator MaxTrackSizingFunction(Dimension d) => new(d.IntoRaw());
        public static implicit operator MaxTrackSizingFunction(MinTrackSizingFunction min) => new(min.Inner);

        /// <summary>Create from a raw CompactLength</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MaxTrackSizingFunction FromRaw(CompactLength val) => new(val);

        /// <summary>Get the underlying CompactLength representation of the value</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CompactLength IntoRaw() => Inner;

        /// <summary>Whether the track sizing function is a fixed value (length or percentage)</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsLengthOrPercentage() => Inner.IsLengthOrPercentage();

        /// <summary>Whether the sizing function is an intrinsic sizing function (min-content, max-content, fit-content, auto)</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsIntrinsic() => Inner.IsIntrinsic();

        /// <summary>
        /// Returns true if the max track sizing function is MaxContent, FitContent or Auto.
        /// "In all cases, treat auto and fit-content() as max-content, except where specified
        /// otherwise for fit-content()."
        /// See: https://www.w3.org/TR/css-grid-1/#algo-terms
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsMaxContentAlike() => Inner.IsMaxContentAlike();

        /// <summary>Whether the sizing function is a max-content sizing function</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsMaxContent() => Inner.IsMaxContent();

        /// <summary>Whether the sizing function is a min-content sizing function</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsMinContent() => Inner.IsMinContent();

        /// <summary>Whether the sizing function is a fit-content(...) sizing function</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsFitContent() => Inner.IsFitContent();

        /// <summary>Whether the sizing function is MaxContent or FitContent</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsMaxOrFitContent() => Inner.IsMaxOrFitContent();

        /// <summary>Whether the sizing function is an auto sizing function</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsAuto() => Inner.IsAuto();

        /// <summary>Whether the sizing function is a flexible (fr) sizing function</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsFr() => Inner.IsFr();

        /// <summary>Whether the sizing function depends on the size of the containing block (percentage or calc)</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool UsesPercentage() => Inner.UsesPercentage();

        /// <summary>Returns whether the value can be resolved using DefiniteValue</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasDefiniteValue(float? parentSize)
        {
            return Inner.Tag switch
            {
                CompactLength.LENGTH_TAG => true,
                CompactLength.PERCENT_TAG => parentSize.HasValue,
                _ when Inner.IsCalc() => parentSize.HasValue,
                _ => false,
            };
        }

        /// <summary>
        /// Returns fixed point values directly. Attempts to resolve percentage values against
        /// the passed available_space and returns if this results in a concrete value.
        /// Otherwise returns null.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float? DefiniteValue(float? parentSize, Func<IntPtr, float, float> calcResolver)
        {
            return Inner.Tag switch
            {
                CompactLength.LENGTH_TAG => Inner.Value,
                CompactLength.PERCENT_TAG => parentSize.HasValue ? Inner.Value * parentSize.Value : null,
                _ when Inner.IsCalc() => parentSize.HasValue ? calcResolver(Inner.CalcValue, parentSize.Value) : null,
                _ => null,
            };
        }

        /// <summary>
        /// Resolve the maximum size of the track as defined by either:
        ///   - A fixed track sizing function
        ///   - A percentage track sizing function (with definite available space)
        ///   - A fit-content sizing function with fixed argument
        ///   - A fit-content sizing function with percentage argument (with definite available space)
        /// All other kinds of track sizing function return null.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float? DefiniteLimit(float? parentSize, Func<IntPtr, float, float> calcResolver)
        {
            return Inner.Tag switch
            {
                CompactLength.FIT_CONTENT_PX_TAG => Inner.Value,
                CompactLength.FIT_CONTENT_PERCENT_TAG => parentSize.HasValue ? Inner.Value * parentSize.Value : null,
                _ => DefiniteValue(parentSize, calcResolver),
            };
        }

        /// <summary>
        /// Resolve percentage values against the passed parent_size, returning Some(value).
        /// Non-percentage values always return null.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float? ResolvedPercentageSize(float parentSize, Func<IntPtr, float, float> calcResolver)
        {
            return Inner.ResolvedPercentageSize(parentSize, calcResolver);
        }

        public bool Equals(MaxTrackSizingFunction other) => Inner == other.Inner;
        public override bool Equals(object? obj) => obj is MaxTrackSizingFunction other && Equals(other);
        public override int GetHashCode() => Inner.GetHashCode();
        public static bool operator ==(MaxTrackSizingFunction left, MaxTrackSizingFunction right) => left.Equals(right);
        public static bool operator !=(MaxTrackSizingFunction left, MaxTrackSizingFunction right) => !left.Equals(right);
        public override string ToString() => $"MaxTrackSizingFunction({Inner})";
    }

    /// <summary>
    /// Minimum track sizing function.
    /// Specifies the minimum size of a grid track.
    /// See https://developer.mozilla.org/en-US/docs/Web/CSS/grid-template-columns
    /// </summary>
    public readonly struct MinTrackSizingFunction : IEquatable<MinTrackSizingFunction>,
        IFromLength<MinTrackSizingFunction>, IFromPercent<MinTrackSizingFunction>
    {
        internal readonly CompactLength Inner;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal MinTrackSizingFunction(CompactLength inner) => Inner = inner;

        public static readonly MinTrackSizingFunction ZERO = new(CompactLength.ZERO);
        public static readonly MinTrackSizingFunction AUTO = new(CompactLength.AUTO);
        public static readonly MinTrackSizingFunction MIN_CONTENT = new(CompactLength.MIN_CONTENT);
        public static readonly MinTrackSizingFunction MAX_CONTENT = new(CompactLength.MAX_CONTENT);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MinTrackSizingFunction Length(float val) => new(CompactLength.Length(val));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MinTrackSizingFunction Percent(float val) => new(CompactLength.Percent(val));

        static MinTrackSizingFunction IFromLength<MinTrackSizingFunction>.FromLength(float value) => Length(value);
        static MinTrackSizingFunction IFromPercent<MinTrackSizingFunction>.FromPercent(float value) => Percent(value);

        public static implicit operator MinTrackSizingFunction(LengthPercentage lp) => new(lp.IntoRaw());
        public static implicit operator MinTrackSizingFunction(LengthPercentageAuto lpa) => new(lpa.IntoRaw());
        public static implicit operator MinTrackSizingFunction(Dimension d) => new(d.IntoRaw());

        /// <summary>Create from a raw CompactLength</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MinTrackSizingFunction FromRaw(CompactLength val) => new(val);

        /// <summary>Get the underlying CompactLength representation of the value</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CompactLength IntoRaw() => Inner;

        /// <summary>Whether the track sizing function is a fixed value (length or percentage)</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsLengthOrPercentage() => Inner.IsLengthOrPercentage();

        /// <summary>Whether the sizing function is an intrinsic sizing function</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsIntrinsic() => Inner.IsIntrinsic();

        /// <summary>Whether the sizing function is a min-content or max-content sizing function</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsMinOrMaxContent() => Inner.IsMinOrMaxContent();

        /// <summary>Whether the sizing function is a max-content sizing function</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsMaxContent() => Inner.IsMaxContent();

        /// <summary>Whether the sizing function is a min-content sizing function</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsMinContent() => Inner.IsMinContent();

        /// <summary>Whether the sizing function is an auto sizing function</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsAuto() => Inner.IsAuto();

        /// <summary>Whether the sizing function is a flexible (fr) sizing function</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsFr() => Inner.IsFr();

        /// <summary>Whether the sizing function depends on the size of the containing block (percentage or calc)</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool UsesPercentage() =>
            Inner.Tag == CompactLength.PERCENT_TAG || Inner.IsCalc();

        /// <summary>
        /// Returns fixed point values directly. Attempts to resolve percentage values against
        /// the passed available_space and returns if this results in a concrete value.
        /// Otherwise returns null.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float? DefiniteValue(float? parentSize, Func<IntPtr, float, float> calcResolver)
        {
            return Inner.Tag switch
            {
                CompactLength.LENGTH_TAG => Inner.Value,
                CompactLength.PERCENT_TAG => parentSize.HasValue ? Inner.Value * parentSize.Value : null,
                _ when Inner.IsCalc() => parentSize.HasValue ? calcResolver(Inner.CalcValue, parentSize.Value) : null,
                _ => null,
            };
        }

        /// <summary>
        /// Resolve percentage values against the passed parent_size, returning Some(value).
        /// Non-percentage values always return null.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float? ResolvedPercentageSize(float parentSize, Func<IntPtr, float, float> calcResolver)
        {
            return Inner.ResolvedPercentageSize(parentSize, calcResolver);
        }

        public bool Equals(MinTrackSizingFunction other) => Inner == other.Inner;
        public override bool Equals(object? obj) => obj is MinTrackSizingFunction other && Equals(other);
        public override int GetHashCode() => Inner.GetHashCode();
        public static bool operator ==(MinTrackSizingFunction left, MinTrackSizingFunction right) => left.Equals(right);
        public static bool operator !=(MinTrackSizingFunction left, MinTrackSizingFunction right) => !left.Equals(right);
        public override string ToString() => $"MinTrackSizingFunction({Inner})";
    }

    /// <summary>
    /// The sizing function for a grid track (row/column).
    /// Type alias: MinMax&lt;MinTrackSizingFunction, MaxTrackSizingFunction&gt;
    /// </summary>
    public static class TrackSizingFunctionExtensions
    {
        /// <summary>A track sizing function with auto min and max</summary>
        public static readonly MinMax<MinTrackSizingFunction, MaxTrackSizingFunction> AUTO =
            new(MinTrackSizingFunction.AUTO, MaxTrackSizingFunction.AUTO);

        /// <summary>A track sizing function with zero min and max</summary>
        public static readonly MinMax<MinTrackSizingFunction, MaxTrackSizingFunction> ZERO =
            new(MinTrackSizingFunction.ZERO, MaxTrackSizingFunction.ZERO);

        /// <summary>A track sizing function with min-content min and max</summary>
        public static readonly MinMax<MinTrackSizingFunction, MaxTrackSizingFunction> MIN_CONTENT =
            new(MinTrackSizingFunction.MIN_CONTENT, MaxTrackSizingFunction.MIN_CONTENT);

        /// <summary>A track sizing function with max-content min and max</summary>
        public static readonly MinMax<MinTrackSizingFunction, MaxTrackSizingFunction> MAX_CONTENT =
            new(MinTrackSizingFunction.MAX_CONTENT, MaxTrackSizingFunction.MAX_CONTENT);

        /// <summary>Extract the min track sizing function</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MinTrackSizingFunction MinSizingFunction(
            this MinMax<MinTrackSizingFunction, MaxTrackSizingFunction> self) => self.Min;

        /// <summary>Extract the max track sizing function</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MaxTrackSizingFunction MaxSizingFunction(
            this MinMax<MinTrackSizingFunction, MaxTrackSizingFunction> self) => self.Max;

        /// <summary>Determine whether at least one of the components is a fixed sizing function</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasFixedComponent(
            this MinMax<MinTrackSizingFunction, MaxTrackSizingFunction> self) =>
            self.Min.IsLengthOrPercentage() || self.Max.IsLengthOrPercentage();

        /// <summary>Create a fit-content TrackSizingFunction</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MinMax<MinTrackSizingFunction, MaxTrackSizingFunction> FitContent(
            LengthPercentage argument) =>
            new(MinTrackSizingFunction.AUTO, MaxTrackSizingFunction.FitContent(argument));

        /// <summary>Create a TrackSizingFunction from a length</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MinMax<MinTrackSizingFunction, MaxTrackSizingFunction> FromLength(float value) =>
            new(MinTrackSizingFunction.Length(value), MaxTrackSizingFunction.Length(value));

        /// <summary>Create a TrackSizingFunction from a percent</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MinMax<MinTrackSizingFunction, MaxTrackSizingFunction> FromPercent(float value) =>
            new(MinTrackSizingFunction.Percent(value), MaxTrackSizingFunction.Percent(value));

        /// <summary>Create a TrackSizingFunction from an fr value</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MinMax<MinTrackSizingFunction, MaxTrackSizingFunction> FromFr(float value) =>
            new(MinTrackSizingFunction.AUTO, MaxTrackSizingFunction.Fr(value));

        /// <summary>Create a TrackSizingFunction from a LengthPercentage</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MinMax<MinTrackSizingFunction, MaxTrackSizingFunction> FromLengthPercentage(
            LengthPercentage lp) =>
            new((MinTrackSizingFunction)lp, (MaxTrackSizingFunction)lp);

        /// <summary>Create a TrackSizingFunction from a LengthPercentageAuto</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MinMax<MinTrackSizingFunction, MaxTrackSizingFunction> FromLengthPercentageAuto(
            LengthPercentageAuto lpa) =>
            new((MinTrackSizingFunction)lpa, (MaxTrackSizingFunction)lpa);

        /// <summary>Create a TrackSizingFunction from a Dimension</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MinMax<MinTrackSizingFunction, MaxTrackSizingFunction> FromDimension(
            Dimension d) =>
            new((MinTrackSizingFunction)d, (MaxTrackSizingFunction)d);
    }

    // Type alias used throughout: TrackSizingFunction = MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>
    // In C# we cannot create a type alias for a generic struct instantiation, so we use the full type.

    /// <summary>
    /// The first argument to a repeated track definition.
    /// Represents the type of automatic repetition to perform, or a fixed count.
    ///
    /// See https://www.w3.org/TR/css-grid-1/#auto-repeat for an explanation of how auto-repeated
    /// track definitions work and the difference between AutoFit and AutoFill.
    /// </summary>
    public readonly struct RepetitionCount : IEquatable<RepetitionCount>
    {
        private readonly RepetitionKind _kind;
        private readonly ushort _count;

        private enum RepetitionKind : byte
        {
            AutoFill,
            AutoFit,
            Count,
        }

        private RepetitionCount(RepetitionKind kind, ushort count = 0)
        {
            _kind = kind;
            _count = count;
        }

        /// <summary>
        /// Auto-repeating tracks should be generated to fill the container.
        /// See: https://developer.mozilla.org/en-US/docs/Web/CSS/repeat#auto-fill
        /// </summary>
        public static readonly RepetitionCount AutoFill = new(RepetitionKind.AutoFill);

        /// <summary>
        /// Auto-repeating tracks should be generated to fit the container.
        /// See: https://developer.mozilla.org/en-US/docs/Web/CSS/repeat#auto-fit
        /// </summary>
        public static readonly RepetitionCount AutoFit = new(RepetitionKind.AutoFit);

        /// <summary>The specified tracks should be repeated exactly N times</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RepetitionCount Count(ushort count) => new(RepetitionKind.Count, count);

        /// <summary>Whether this is an auto-fill repetition</summary>
        public bool IsAutoFill => _kind == RepetitionKind.AutoFill;

        /// <summary>Whether this is an auto-fit repetition</summary>
        public bool IsAutoFit => _kind == RepetitionKind.AutoFit;

        /// <summary>Whether this is an auto (auto-fill or auto-fit) repetition</summary>
        public bool IsAuto => _kind == RepetitionKind.AutoFill || _kind == RepetitionKind.AutoFit;

        /// <summary>Whether this is a fixed count repetition</summary>
        public bool IsCount => _kind == RepetitionKind.Count;

        /// <summary>Get the count value. Only valid if IsCount is true.</summary>
        public ushort CountValue => _count;

        public static implicit operator RepetitionCount(ushort count) => Count(count);

        public bool Equals(RepetitionCount other) => _kind == other._kind && _count == other._count;
        public override bool Equals(object? obj) => obj is RepetitionCount other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(_kind, _count);
        public static bool operator ==(RepetitionCount left, RepetitionCount right) => left.Equals(right);
        public static bool operator !=(RepetitionCount left, RepetitionCount right) => !left.Equals(right);

        public override string ToString() => _kind switch
        {
            RepetitionKind.AutoFill => "AutoFill",
            RepetitionKind.AutoFit => "AutoFit",
            RepetitionKind.Count => $"Count({_count})",
            _ => "Unknown",
        };

        /// <summary>
        /// Try to parse from a string. Only "auto-fit" and "auto-fill" are valid.
        /// Returns false if the string is not a valid repetition value.
        /// </summary>
        public static bool TryFromString(string value, out RepetitionCount result)
        {
            switch (value)
            {
                case "auto-fit":
                    result = AutoFit;
                    return true;
                case "auto-fill":
                    result = AutoFill;
                    return true;
                default:
                    result = default;
                    return false;
            }
        }
    }

    /// <summary>
    /// Defines a grid area.
    /// </summary>
    public class GridTemplateArea
    {
        /// <summary>The name of the grid area</summary>
        public string Name;
        /// <summary>The index of the row at which the grid area starts in grid coordinates</summary>
        public ushort RowStart;
        /// <summary>The index of the row at which the grid area ends in grid coordinates</summary>
        public ushort RowEnd;
        /// <summary>The index of the column at which the grid area starts in grid coordinates</summary>
        public ushort ColumnStart;
        /// <summary>The index of the column at which the grid area ends in grid coordinates</summary>
        public ushort ColumnEnd;

        public GridTemplateArea(string name, ushort rowStart, ushort rowEnd, ushort columnStart, ushort columnEnd)
        {
            Name = name;
            RowStart = rowStart;
            RowEnd = rowEnd;
            ColumnStart = columnStart;
            ColumnEnd = columnEnd;
        }
    }

    /// <summary>
    /// Defines a named grid line.
    /// </summary>
    public class NamedGridLine
    {
        /// <summary>The name of the grid line</summary>
        public string Name;
        /// <summary>The index of the grid line in grid coordinates</summary>
        public ushort Index;

        public NamedGridLine(string name, ushort index)
        {
            Name = name;
            Index = index;
        }
    }

    /// <summary>
    /// A grid line placement specification. Used for grid-[row/column]-[start/end].
    /// Defaults to Auto.
    /// </summary>
    public struct GridPlacement : IEquatable<GridPlacement>, ITaffyGridLine<GridPlacement>, ITaffyGridSpan<GridPlacement>
    {
        /// <summary>The kind of placement</summary>
        public GridPlacementKind Kind;
        /// <summary>The line index (for Line) or span count (for Span)</summary>
        public short Value;
        /// <summary>Named line (for NamedLine/NamedSpan)</summary>
        public string? Name;

        public enum GridPlacementKind : byte
        {
            Auto,
            Line,
            NamedLine,
            Span,
            NamedSpan,
        }

        /// <summary>Auto placement</summary>
        public static readonly GridPlacement Auto = new() { Kind = GridPlacementKind.Auto };

        /// <summary>Place at specified line index</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GridPlacement FromLine(short index) => new() { Kind = GridPlacementKind.Line, Value = index };

        /// <summary>Place at specified named line</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GridPlacement FromNamedLine(string name, short index) =>
            new() { Kind = GridPlacementKind.NamedLine, Name = name, Value = index };

        /// <summary>Span specified number of tracks</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GridPlacement FromSpanCount(ushort span) =>
            new() { Kind = GridPlacementKind.Span, Value = (short)span };

        /// <summary>Span until the nth line named name</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GridPlacement FromNamedSpan(string name, ushort span) =>
            new() { Kind = GridPlacementKind.NamedSpan, Name = name, Value = (short)span };

        // ITaffyGridLine implementation
        static GridPlacement ITaffyGridLine<GridPlacement>.FromLineIndex(short index) => FromLine(index);

        // ITaffyGridSpan implementation
        static GridPlacement ITaffyGridSpan<GridPlacement>.FromSpan(ushort span) => FromSpanCount(span);

        /// <summary>Whether this placement is Auto</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsAuto() => Kind == GridPlacementKind.Auto;

        /// <summary>Whether this placement is a Line</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsLine() => Kind == GridPlacementKind.Line;

        /// <summary>Whether this placement is a Span</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsSpan() => Kind == GridPlacementKind.Span;

        /// <summary>Whether this placement is a NamedLine</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsNamedLine() => Kind == GridPlacementKind.NamedLine;

        /// <summary>Whether this placement is a NamedSpan</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsNamedSpan() => Kind == GridPlacementKind.NamedSpan;

        public bool Equals(GridPlacement other) =>
            Kind == other.Kind && Value == other.Value && Name == other.Name;
        public override bool Equals(object? obj) => obj is GridPlacement other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Kind, Value, Name);
        public static bool operator ==(GridPlacement left, GridPlacement right) => left.Equals(right);
        public static bool operator !=(GridPlacement left, GridPlacement right) => !left.Equals(right);
        public override string ToString() => Kind switch
        {
            GridPlacementKind.Auto => "Auto",
            GridPlacementKind.Line => $"Line({Value})",
            GridPlacementKind.NamedLine => $"NamedLine({Name}, {Value})",
            GridPlacementKind.Span => $"Span({Value})",
            GridPlacementKind.NamedSpan => $"NamedSpan({Name}, {Value})",
            _ => "Unknown",
        };
    }

    /// <summary>
    /// A generic grid line placement specification generic over the coordinate system.
    /// GenericGridPlacement&lt;GridLine&gt; is used for CSS Grid Line coordinates.
    /// GenericGridPlacement&lt;OriginZeroLine&gt; is used internally for placement computations.
    /// </summary>
    public readonly struct GenericGridPlacement<T> : IEquatable<GenericGridPlacement<T>> where T : struct, IEquatable<T>
    {
        private readonly GenericGridPlacementKind _kind;
        private readonly T _line;
        private readonly ushort _span;

        private enum GenericGridPlacementKind : byte
        {
            Auto,
            Line,
            Span,
        }

        private GenericGridPlacement(GenericGridPlacementKind kind, T line, ushort span)
        {
            _kind = kind;
            _line = line;
            _span = span;
        }

        /// <summary>Auto placement</summary>
        public static readonly GenericGridPlacement<T> AutoPlacement = new(GenericGridPlacementKind.Auto, default, 0);

        /// <summary>Place item at specified line index</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GenericGridPlacement<T> FromLine(T line) =>
            new(GenericGridPlacementKind.Line, line, 0);

        /// <summary>Item should span specified number of tracks</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GenericGridPlacement<T> FromSpan(ushort span) =>
            new(GenericGridPlacementKind.Span, default, span);

        /// <summary>Whether this is Auto</summary>
        public bool IsAuto => _kind == GenericGridPlacementKind.Auto;

        /// <summary>Whether this is a Line placement</summary>
        public bool IsLine => _kind == GenericGridPlacementKind.Line;

        /// <summary>Whether this is a Span placement</summary>
        public bool IsSpan => _kind == GenericGridPlacementKind.Span;

        /// <summary>Get the line value. Only valid when IsLine is true.</summary>
        public T LineValue => _line;

        /// <summary>Get the span value. Only valid when IsSpan is true.</summary>
        public ushort SpanValue => _span;

        public bool Equals(GenericGridPlacement<T> other) =>
            _kind == other._kind && EqualityComparer<T>.Default.Equals(_line, other._line) && _span == other._span;
        public override bool Equals(object? obj) => obj is GenericGridPlacement<T> other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(_kind, _line, _span);
        public static bool operator ==(GenericGridPlacement<T> left, GenericGridPlacement<T> right) => left.Equals(right);
        public static bool operator !=(GenericGridPlacement<T> left, GenericGridPlacement<T> right) => !left.Equals(right);
        public override string ToString() => _kind switch
        {
            GenericGridPlacementKind.Auto => "Auto",
            GenericGridPlacementKind.Line => $"Line({_line})",
            GenericGridPlacementKind.Span => $"Span({_span})",
            _ => "Unknown",
        };
    }

    /// <summary>
    /// Axis as Row or Column (for grid area resolution).
    /// </summary>
    public enum GridAreaAxis
    {
        Row,
        Column,
    }

    /// <summary>
    /// Logical end (Start or End) for grid area resolution.
    /// </summary>
    internal enum GridAreaEnd
    {
        Start,
        End,
    }

    /// <summary>
    /// A grid template repetition specification containing a count and a list of track sizing functions.
    /// </summary>
    public class GridTemplateRepetition
    {
        /// <summary>The repetition count</summary>
        public RepetitionCount Count;
        /// <summary>The repeated tracks</summary>
        public ImmutableList<MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>> Tracks;
        /// <summary>Line names for the repeated tracks</summary>
        public ImmutableList<ImmutableList<string>> LineNames;

        public GridTemplateRepetition(
            RepetitionCount count,
            ImmutableList<MinMax<MinTrackSizingFunction, MaxTrackSizingFunction>> tracks,
            ImmutableList<ImmutableList<string>>? lineNames = null)
        {
            Count = count;
            Tracks = tracks;
            LineNames = lineNames ?? ImmutableList<ImmutableList<string>>.Empty;
        }

        /// <summary>Returns the number of repeated tracks</summary>
        public ushort TrackCount => (ushort)Tracks.Count;
    }

    /// <summary>
    /// A component of a grid template definition.
    /// May be a single track sizing function or a repeat() clause.
    /// </summary>
    public class GridTemplateComponent
    {
        /// <summary>The kind of component</summary>
        public GridTemplateComponentKind Kind;
        /// <summary>The single track sizing function (when Kind == Single)</summary>
        public MinMax<MinTrackSizingFunction, MaxTrackSizingFunction> Single;
        /// <summary>The repetition specification (when Kind == Repeat)</summary>
        public GridTemplateRepetition? Repetition;

        public enum GridTemplateComponentKind : byte
        {
            Single,
            Repeat,
        }

        /// <summary>Create a single track component</summary>
        public static GridTemplateComponent FromSingle(MinMax<MinTrackSizingFunction, MaxTrackSizingFunction> track) =>
            new() { Kind = GridTemplateComponentKind.Single, Single = track };

        /// <summary>Create a repeat component</summary>
        public static GridTemplateComponent FromRepeat(GridTemplateRepetition repetition) =>
            new() { Kind = GridTemplateComponentKind.Repeat, Repetition = repetition };

        /// <summary>Whether the track definition is an auto-repeated fragment</summary>
        public bool IsAutoRepetition() =>
            Kind == GridTemplateComponentKind.Repeat &&
            Repetition != null &&
            Repetition.Count.IsAuto;

        // --- Static factory constants matching Rust's TaffyAuto/TaffyMinContent/etc trait impls ---

        /// <summary>Create an auto single-track component</summary>
        public static GridTemplateComponent AutoComponent() =>
            FromSingle(TrackSizingFunctionExtensions.AUTO);

        /// <summary>Create a min-content single-track component</summary>
        public static GridTemplateComponent MinContentComponent() =>
            FromSingle(TrackSizingFunctionExtensions.MIN_CONTENT);

        /// <summary>Create a max-content single-track component</summary>
        public static GridTemplateComponent MaxContentComponent() =>
            FromSingle(TrackSizingFunctionExtensions.MAX_CONTENT);

        /// <summary>Create a zero single-track component</summary>
        public static GridTemplateComponent ZeroComponent() =>
            FromSingle(TrackSizingFunctionExtensions.ZERO);

        /// <summary>Create a fit-content single-track component</summary>
        public static GridTemplateComponent FitContentComponent(LengthPercentage argument) =>
            FromSingle(TrackSizingFunctionExtensions.FitContent(argument));

        /// <summary>Create a single-track component from a length value</summary>
        public static GridTemplateComponent FromLength(float value) =>
            FromSingle(TrackSizingFunctionExtensions.FromLength(value));

        /// <summary>Create a single-track component from a percent value</summary>
        public static GridTemplateComponent FromPercent(float value) =>
            FromSingle(TrackSizingFunctionExtensions.FromPercent(value));

        /// <summary>Create a single-track component from an fr value</summary>
        public static GridTemplateComponent FromFr(float value) =>
            FromSingle(TrackSizingFunctionExtensions.FromFr(value));
    }

    /// <summary>
    /// Extension methods for Line&lt;GridPlacement&gt; to support grid placement operations.
    /// </summary>
    public static class LineGridPlacementExtensions
    {
        /// <summary>
        /// Creates a Line&lt;GridPlacement&gt; from a line index, with Auto for the end.
        /// Equivalent to Rust's TaffyGridLine for Line&lt;GridPlacement&gt;.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Line<GridPlacement> GridLineFromIndex(short index) =>
            new(GridPlacement.FromLine(index), GridPlacement.Auto);

        /// <summary>
        /// Creates a Line&lt;GridPlacement&gt; from a span, with Auto for the end.
        /// Equivalent to Rust's TaffyGridSpan for Line&lt;GridPlacement&gt;.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Line<GridPlacement> GridLineFromSpan(ushort span) =>
            new(GridPlacement.FromSpanCount(span), GridPlacement.Auto);

        /// <summary>
        /// Returns the default Line&lt;GridPlacement&gt; (Auto, Auto).
        /// </summary>
        public static Line<GridPlacement> DefaultGridPlacementLine() =>
            new(GridPlacement.Auto, GridPlacement.Auto);

        /// <summary>
        /// Whether the track position is definite in this axis.
        /// The track position is definite if at least one of the start and end positions is a NON-ZERO track index
        /// (0 is an invalid line in GridLine coordinates, and falls back to "auto" which is indefinite).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsDefinite(this Line<GridPlacement> self)
        {
            if (self.Start.Kind == GridPlacement.GridPlacementKind.Line && self.Start.Value != 0)
                return true;
            if (self.End.Kind == GridPlacement.GridPlacementKind.Line && self.End.Value != 0)
                return true;
            if (self.Start.Kind == GridPlacement.GridPlacementKind.NamedLine)
                return true;
            if (self.End.Kind == GridPlacement.GridPlacementKind.NamedLine)
                return true;
            return false;
        }
    }
}
