// Ported from taffy/src/compute/grid/types/
// Grid layout types: GridTrack, GridItem, TrackCounts, CellOccupancyMatrix, coordinate types, named area resolution

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Marius.Winter.Taffy
{
    // =========================================================================
    // Coordinate types (from coordinates.rs)
    // =========================================================================

    /// <summary>
    /// Represents a grid line position in "CSS Grid Line" coordinates.
    /// - The line at left hand (or top) edge of the explicit grid is line 1 (and counts up from there)
    /// - The line at the right hand (or bottom) edge of the explicit grid is -1 (and counts down from there)
    /// - 0 is not a valid index
    /// </summary>
    public readonly struct GridLine : IEquatable<GridLine>, IComparable<GridLine>
    {
        private readonly short _value;

        public GridLine(short value) => _value = value;

        /// <summary>Returns the underlying i16</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public short AsI16() => _value;

        /// <summary>Convert into OriginZero coordinates using the specified explicit track count</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public OriginZeroLine IntoOriginZeroLine(ushort explicitTrackCount)
        {
            int explicitLineCount = explicitTrackCount + 1;
            int ozLine;
            if (_value > 0)
                ozLine = _value - 1;
            else if (_value < 0)
                ozLine = _value + explicitLineCount;
            else
                throw new InvalidOperationException("Grid line of zero is invalid");
            return new OriginZeroLine((short)Math.Clamp(ozLine, -10000, 10000));
        }

        public static implicit operator GridLine(short value) => new(value);

        public bool Equals(GridLine other) => _value == other._value;
        public override bool Equals(object? obj) => obj is GridLine other && Equals(other);
        public override int GetHashCode() => _value.GetHashCode();
        public int CompareTo(GridLine other) => _value.CompareTo(other._value);
        public static bool operator ==(GridLine left, GridLine right) => left._value == right._value;
        public static bool operator !=(GridLine left, GridLine right) => left._value != right._value;
        public override string ToString() => $"GridLine({_value})";
    }

    /// <summary>
    /// Represents a grid line position in "OriginZero" coordinates.
    /// - The line at left hand (or top) edge of the explicit grid is line 0
    /// - The next line to the right (or down) is 1, and so on
    /// - The next line to the left (or up) is -1, and so on
    /// </summary>
    public struct OriginZeroLine : IEquatable<OriginZeroLine>, IComparable<OriginZeroLine>
    {
        public short Value;

        public OriginZeroLine(short value) => Value = value;

        // Add and Sub with Self
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static OriginZeroLine operator +(OriginZeroLine a, OriginZeroLine b) => new((short)(a.Value + b.Value));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static OriginZeroLine operator -(OriginZeroLine a, OriginZeroLine b) => new((short)(a.Value - b.Value));

        // Add and Sub with ushort
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static OriginZeroLine operator +(OriginZeroLine a, ushort b) => new((short)(a.Value + b));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static OriginZeroLine operator -(OriginZeroLine a, ushort b) => new((short)(a.Value - b));

        /// <summary>
        /// Converts a grid line in OriginZero coordinates into the index of that same grid line in the GridTrackVec.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int IntoTrackVecIndex(TrackCounts trackCounts)
        {
            return TryIntoTrackVecIndex(trackCounts) ?? throw new InvalidOperationException(
                Value > 0
                    ? "OriginZero grid line cannot be more than the number of positive grid lines"
                    : "OriginZero grid line cannot be less than the number of negative grid lines");
        }

        /// <summary>
        /// Converts a grid line in OriginZero coordinates into the index of that same grid line in the GridTrackVec.
        /// This fallible version is used for the placement of absolutely positioned grid items.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int? TryIntoTrackVecIndex(TrackCounts trackCounts)
        {
            // OriginZero grid line cannot be less than the number of negative grid lines
            if (Value < -(short)trackCounts.NegativeImplicit)
                return null;
            // OriginZero grid line cannot be more than the number of positive grid lines
            if (Value > (short)(trackCounts.Explicit + trackCounts.PositiveImplicit))
                return null;

            return 2 * (Value + (short)trackCounts.NegativeImplicit);
        }

        /// <summary>The minimum number of negative implicit tracks there must be if a grid item starts at this line.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort ImpliedNegativeImplicitTracks()
        {
            return Value < 0 ? (ushort)Math.Abs(Value) : (ushort)0;
        }

        /// <summary>The minimum number of positive implicit tracks there must be if a grid item ends at this line.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort ImpliedPositiveImplicitTracks(ushort explicitTrackCount)
        {
            return Value > (short)explicitTrackCount ? (ushort)(Value - explicitTrackCount) : (ushort)0;
        }

        public static bool operator <(OriginZeroLine a, OriginZeroLine b) => a.Value < b.Value;
        public static bool operator >(OriginZeroLine a, OriginZeroLine b) => a.Value > b.Value;
        public static bool operator <=(OriginZeroLine a, OriginZeroLine b) => a.Value <= b.Value;
        public static bool operator >=(OriginZeroLine a, OriginZeroLine b) => a.Value >= b.Value;

        public bool Equals(OriginZeroLine other) => Value == other.Value;
        public override bool Equals(object? obj) => obj is OriginZeroLine other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public int CompareTo(OriginZeroLine other) => Value.CompareTo(other.Value);
        public static bool operator ==(OriginZeroLine left, OriginZeroLine right) => left.Value == right.Value;
        public static bool operator !=(OriginZeroLine left, OriginZeroLine right) => left.Value != right.Value;
        public override string ToString() => $"OriginZeroLine({Value})";
    }

    /// <summary>
    /// Extension methods for Line&lt;OriginZeroLine&gt;
    /// </summary>
    public static class LineOriginZeroLineExtensions
    {
        /// <summary>The number of tracks between the start and end lines</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort Span(this Line<OriginZeroLine> self)
        {
            return (ushort)Math.Max(self.End.Value - self.Start.Value, 0);
        }
    }

    // =========================================================================
    // TrackCounts (from grid_track_counts.rs)
    // =========================================================================

    /// <summary>
    /// Stores the number of tracks in a given dimension.
    /// Stores separately the number of tracks in the implicit and explicit grids.
    /// </summary>
    public struct TrackCounts : IEquatable<TrackCounts>
    {
        /// <summary>The number of tracks in the implicit grid before the explicit grid</summary>
        public ushort NegativeImplicit;
        /// <summary>The number of tracks in the explicit grid</summary>
        public ushort Explicit;
        /// <summary>The number of tracks in the implicit grid after the explicit grid</summary>
        public ushort PositiveImplicit;

        /// <summary>Create a TrackCounts instance from raw track count numbers</summary>
        public TrackCounts(ushort negativeImplicit, ushort @explicit, ushort positiveImplicit)
        {
            NegativeImplicit = negativeImplicit;
            Explicit = @explicit;
            PositiveImplicit = positiveImplicit;
        }

        /// <summary>Create a TrackCounts instance from raw track count numbers</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TrackCounts FromRaw(ushort negativeImplicit, ushort @explicit, ushort positiveImplicit) =>
            new(negativeImplicit, @explicit, positiveImplicit);

        /// <summary>Count the total number of tracks in the axis</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Len() => NegativeImplicit + Explicit + PositiveImplicit;

        /// <summary>The OriginZeroLine representing the start of the implicit grid</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public OriginZeroLine ImplicitStartLine() => new((short)(-(short)NegativeImplicit));

        /// <summary>The OriginZeroLine representing the end of the implicit grid</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public OriginZeroLine ImplicitEndLine() => new((short)(Explicit + PositiveImplicit));

        // Conversion functions between OriginZero coordinates and CellOccupancyMatrix track indexes

        /// <summary>
        /// Converts a grid line in OriginZero coordinates into the track immediately
        /// following that grid line as an index into the CellOccupancyMatrix.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public short OzLineToNextTrack(OriginZeroLine index) => (short)(index.Value + NegativeImplicit);

        /// <summary>
        /// Converts start and end grid lines in OriginZero coordinates into a range of tracks
        /// as indexes into the CellOccupancyMatrix.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (short Start, short End) OzLineRangeToTrackRange(Line<OriginZeroLine> input)
        {
            var start = OzLineToNextTrack(input.Start);
            var end = OzLineToNextTrack(input.End); // Don't subtract 1 as output range is exclusive
            return (start, end);
        }

        /// <summary>
        /// Converts a track as an index into the CellOccupancyMatrix into the grid line immediately
        /// preceding that track in OriginZero coordinates.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public OriginZeroLine TrackToPrevOzLine(ushort index) => new((short)(index - NegativeImplicit));

        /// <summary>
        /// Converts a range of tracks as indexes into the CellOccupancyMatrix into
        /// start and end grid lines in OriginZero coordinates.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Line<OriginZeroLine> TrackRangeToOzLineRange((short Start, short End) input) =>
            new(TrackToPrevOzLine((ushort)input.Start), TrackToPrevOzLine((ushort)input.End));

        public bool Equals(TrackCounts other) =>
            NegativeImplicit == other.NegativeImplicit && Explicit == other.Explicit &&
            PositiveImplicit == other.PositiveImplicit;
        public override bool Equals(object? obj) => obj is TrackCounts other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(NegativeImplicit, Explicit, PositiveImplicit);
        public static bool operator ==(TrackCounts left, TrackCounts right) => left.Equals(right);
        public static bool operator !=(TrackCounts left, TrackCounts right) => !left.Equals(right);
        public override string ToString() =>
            $"TrackCounts {{ NegativeImplicit = {NegativeImplicit}, Explicit = {Explicit}, PositiveImplicit = {PositiveImplicit} }}";
    }

    // =========================================================================
    // GridTrack (from grid_track.rs)
    // =========================================================================

    /// <summary>Whether a GridTrack represents an actual track or a gutter.</summary>
    public enum GridTrackKind
    {
        /// Track is an actual track
        Track,
        /// Track is a gutter (aka grid line) (aka gap)
        Gutter,
    }

    /// <summary>
    /// Internal sizing information for a single grid track (row/column).
    /// Gutters between tracks are sized similarly to actual tracks, so they
    /// are also represented by this struct.
    /// </summary>
    public class GridTrack
    {
        /// <summary>Whether the track is a full track, a gutter, or a placeholder</summary>
        public GridTrackKind Kind;

        /// <summary>Whether the track is a collapsed track/gutter</summary>
        public bool IsCollapsed;

        /// <summary>The minimum track sizing function of the track</summary>
        public MinTrackSizingFunction MinTrackSizingFunction;

        /// <summary>The maximum track sizing function of the track</summary>
        public MaxTrackSizingFunction MaxTrackSizingFunction;

        /// <summary>The distance of the start of the track from the start of the grid container</summary>
        public float Offset;

        /// <summary>The size (width/height as applicable) of the track</summary>
        public float BaseSize;

        /// <summary>A temporary scratch value when sizing tracks. Note: can be infinity</summary>
        public float GrowthLimit;
        private float _resolvedFitContentLimit = float.PositiveInfinity;

        /// <summary>
        /// A temporary scratch value when sizing tracks. Is used as an additional amount to add to the
        /// estimate for the available space in the opposite axis when content sizing items.
        /// </summary>
        public float ContentAlignmentAdjustment;

        /// <summary>A temporary scratch value when "distributing space" to avoid clobbering planned increase variable</summary>
        public float ItemIncurredIncrease;
        /// <summary>A temporary scratch value when "distributing space" to avoid clobbering the main variable</summary>
        public float BaseSizePlannedIncrease;
        /// <summary>A temporary scratch value when "distributing space" to avoid clobbering the main variable</summary>
        public float GrowthLimitPlannedIncrease;
        /// <summary>A temporary scratch value when "distributing space". See: https://www.w3.org/TR/css3-grid-layout/#infinitely-growable</summary>
        public bool InfinitelyGrowable;

        /// <summary>GridTrack constructor with all configuration parameters</summary>
        private GridTrack(GridTrackKind kind, MinTrackSizingFunction minFunc, MaxTrackSizingFunction maxFunc)
        {
            Kind = kind;
            IsCollapsed = false;
            MinTrackSizingFunction = minFunc;
            MaxTrackSizingFunction = maxFunc;
            Offset = 0f;
            BaseSize = 0f;
            GrowthLimit = 0f;
            ContentAlignmentAdjustment = 0f;
            ItemIncurredIncrease = 0f;
            BaseSizePlannedIncrease = 0f;
            GrowthLimitPlannedIncrease = 0f;
            InfinitelyGrowable = false;
        }

        /// <summary>Create new GridTrack representing an actual track (not a gutter)</summary>
        public static GridTrack New(MinTrackSizingFunction minFunc, MaxTrackSizingFunction maxFunc) =>
            new(GridTrackKind.Track, minFunc, maxFunc);

        /// <summary>Create a new GridTrack representing a gutter</summary>
        public static GridTrack Gutter(LengthPercentage size) =>
            new(GridTrackKind.Gutter, (MinTrackSizingFunction)size, (MaxTrackSizingFunction)size);

        /// <summary>Mark a GridTrack as collapsed. Also sets both sizing functions to fixed zero-sized.</summary>
        public void Collapse()
        {
            IsCollapsed = true;
            MinTrackSizingFunction = MinTrackSizingFunction.ZERO;
            MaxTrackSizingFunction = MaxTrackSizingFunction.ZERO;
        }

        /// <summary>Returns true if the track is flexible (has a Flex MaxTrackSizingFunction), else false.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsFlexible() => MaxTrackSizingFunction.IsFr();

        /// <summary>Returns true if the track uses percentage sizing</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool UsesPercentage() =>
            MinTrackSizingFunction.UsesPercentage() || MaxTrackSizingFunction.UsesPercentage();

        /// <summary>Returns true if the track has an intrinsic min and/or max sizing function</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasIntrinsicSizingFunction() =>
            MinTrackSizingFunction.IsIntrinsic() || MaxTrackSizingFunction.IsIntrinsic();

        public void ResolveFitContentLimit(float? basis, Func<IntPtr, float, float> calc)
        {
            var value = MaxTrackSizingFunction.Inner;
            _resolvedFitContentLimit = value.IsFitContentCalc() ? basis.HasValue ? calc(value.CalcValue, basis.Value) : float.PositiveInfinity
                : FitContentLimit(basis);
        }
        public float CachedFitContentLimit() => _resolvedFitContentLimit;
        public float CachedFitContentLimitedGrowthLimit() => TaffySys.F32Min(GrowthLimit, _resolvedFitContentLimit);

        /// <summary>Returns the fit-content limit for this track</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float FitContentLimit(float? axisAvailableGridSpace)
        {
            return MaxTrackSizingFunction.Inner.Tag switch
            {
                CompactLength.FIT_CONTENT_PX_TAG => MaxTrackSizingFunction.Inner.Value,
                CompactLength.FIT_CONTENT_PERCENT_TAG => axisAvailableGridSpace.HasValue
                    ? axisAvailableGridSpace.Value * MaxTrackSizingFunction.Inner.Value
                    : float.PositiveInfinity,
                _ => float.PositiveInfinity,
            };
        }

        /// <summary>Returns the fit-content limited growth limit</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float FitContentLimitedGrowthLimit(float? axisAvailableGridSpace) =>
            TaffySys.F32Min(GrowthLimit, FitContentLimit(axisAvailableGridSpace));

        /// <summary>Returns the track's flex factor if it is a flex track, else 0.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float FlexFactor() =>
            MaxTrackSizingFunction.IsFr() ? MaxTrackSizingFunction.Inner.Value : 0f;
    }

    // =========================================================================
    // CellOccupancyMatrix (from cell_occupancy.rs)
    // =========================================================================

    /// <summary>The occupancy state of a single grid cell</summary>
    public enum CellOccupancyState
    {
        /// Indicates that a grid cell is unoccupied
        Unoccupied = 0,
        /// Indicates that a grid cell is occupied by a definitely placed item
        DefinitelyPlaced,
        /// Indicates that a grid cell is occupied by an item that was placed by the auto placement algorithm
        AutoPlaced,
    }

    // =========================================================================
    // GridItem (from grid_item.rs)
    // =========================================================================

    /// <summary>
    /// Represents a single grid item
    /// </summary>
    public class GridItem
    {
        public OofCandidates OofCandidates;
        /// <summary>The id of the node that this item represents</summary>
        public NodeId Node;
        /// <summary>The order of the item in the children array</summary>
        public ushort SourceOrder;

        /// <summary>The item's definite row-start and row-end (in origin-zero coordinates)</summary>
        public Line<OriginZeroLine> Row;
        /// <summary>The item's definite column-start and column-end (in origin-zero coordinates)</summary>
        public Line<OriginZeroLine> Column;

        /// <summary>Is it a compressible replaced element?</summary>
        public bool IsCompressibleReplaced;
        public bool IsReplaced;
        private Size<float?>? _knownDimensionsArea;
        private Size<float?> _knownDimensions;
        /// <summary>The item's overflow style</summary>
        public Point<Overflow> OverflowStyle;
        /// <summary>The item's box_sizing style</summary>
        public BoxSizing BoxSizingStyle;
        /// <summary>The item's size style</summary>
        public Size<Dimension> SizeStyle;
        /// <summary>The item's min_size style</summary>
        public Size<Dimension> MinSizeStyle;
        /// <summary>The item's max_size style</summary>
        public Size<Dimension> MaxSizeStyle;
        /// <summary>The item's aspect_ratio style</summary>
        public float? AspectRatio;
        /// <summary>The item's padding style</summary>
        public Rect<LengthPercentage> PaddingStyle;
        /// <summary>The item's border style</summary>
        public Rect<LengthPercentage> BorderStyle;
        /// <summary>The item's margin style</summary>
        public Rect<LengthPercentageAuto> MarginStyle;
        /// <summary>The item's align_self property, or the parent's align_items property if not set</summary>
        public AlignItems AlignSelfStyle;
        /// <summary>The item's justify_self property, or the parent's justify_items property if not set</summary>
        public AlignItems JustifySelfStyle;
        /// <summary>The items first baseline (horizontal)</summary>
        public float? Baseline;
        /// <summary>Shim for baseline alignment that acts like an extra top margin</summary>
        public float BaselineShim;

        /// <summary>The item's definite row-start and row-end (as indexes into Vec&lt;GridTrack&gt;)</summary>
        public Line<ushort> RowIndexes;
        /// <summary>The item's definite column-start and column-end (as indexes into Vec&lt;GridTrack&gt;)</summary>
        public Line<ushort> ColumnIndexes;

        /// <summary>Whether the item crosses a flexible row</summary>
        public bool CrossesFlexibleRow;
        /// <summary>Whether the item crosses a flexible column</summary>
        public bool CrossesFlexibleColumn;
        /// <summary>Whether the item crosses an intrinsic row</summary>
        public bool CrossesIntrinsicRow;
        /// <summary>Whether the item crosses an intrinsic column</summary>
        public bool CrossesIntrinsicColumn;

        // Caches for intrinsic size computation
        /// <summary>Cache for the grid area size input to intrinsic sizing computation</summary>
        public Size<float?>? GridAreaSizeCache;
        /// <summary>Cache for the min-content size</summary>
        public Size<float?> MinContentContributionCache;
        /// <summary>Cache for the minimum contribution</summary>
        public Size<float?> MinimumContributionCache;
        /// <summary>Cache for the max-content size</summary>
        public Size<float?> MaxContentContributionCache;

        /// <summary>Final y position. Used to compute baseline alignment for the container.</summary>
        public float YPosition;
        /// <summary>Final height. Used to compute baseline alignment for the container.</summary>
        public float Height;

        public bool HasCyclicBlockSizeDependency => SizeStyle.Height.IntoRaw().UsesPercentage() && (CrossesIntrinsicRow || CrossesFlexibleRow);
        public bool ParticipatesInBaselineAlignment => AlignSelfStyle.Keyword == AlignItemsKeyword.Baseline
            && !MarginStyle.Top.IsAuto() && !MarginStyle.Bottom.IsAuto() && !HasCyclicBlockSizeDependency;
        public void ClearKnownDimensionsCache() => _knownDimensionsArea = null;

        /// <summary>Create a new item given a concrete placement in both axes</summary>
        public static GridItem NewWithPlacementStyleAndOrder(
            NodeId node,
            Line<OriginZeroLine> colSpan,
            Line<OriginZeroLine> rowSpan,
            IGridItemStyle style,
            AlignItems parentAlignItems,
            AlignItems parentJustifyItems,
            ushort sourceOrder)
        {
            return new GridItem
            {
                Node = node,
                SourceOrder = sourceOrder,
                Row = rowSpan,
                Column = colSpan,
                IsCompressibleReplaced = style.IsCompressibleReplaced(),
                IsReplaced = style.IsReplaced(),
                OverflowStyle = style.Overflow(),
                BoxSizingStyle = style.BoxSizing(),
                SizeStyle = style.Size(),
                MinSizeStyle = style.MinSize(),
                MaxSizeStyle = style.MaxSize(),
                AspectRatio = style.AspectRatio(),
                PaddingStyle = style.Padding(),
                BorderStyle = style.Border(),
                MarginStyle = style.Margin(),
                AlignSelfStyle = style.AlignSelf() ?? parentAlignItems,
                JustifySelfStyle = style.JustifySelf() ?? parentJustifyItems,
                Baseline = null,
                BaselineShim = 0f,
                RowIndexes = new Line<ushort>(0, 0),
                ColumnIndexes = new Line<ushort>(0, 0),
                CrossesFlexibleRow = false,
                CrossesFlexibleColumn = false,
                CrossesIntrinsicRow = false,
                CrossesIntrinsicColumn = false,
                GridAreaSizeCache = null,
                MinContentContributionCache = SizeExtensions.NoneF32,
                MaxContentContributionCache = SizeExtensions.NoneF32,
                MinimumContributionCache = SizeExtensions.NoneF32,
                YPosition = 0f,
                Height = 0f,
            };
        }

        /// <summary>This item's placement in the specified axis in OriginZero coordinates</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Line<OriginZeroLine> Placement(AbstractAxis axis) => axis switch
        {
            AbstractAxis.Block => Row,
            AbstractAxis.Inline => Column,
            _ => throw new ArgumentOutOfRangeException(nameof(axis)),
        };

        /// <summary>This item's placement in the specified axis as GridTrackVec indices</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Line<ushort> PlacementIndexes(AbstractAxis axis) => axis switch
        {
            AbstractAxis.Block => RowIndexes,
            AbstractAxis.Inline => ColumnIndexes,
            _ => throw new ArgumentOutOfRangeException(nameof(axis)),
        };

        /// <summary>
        /// Returns a range which can be used as an index into the GridTrackVec in the specified axis
        /// which will produce a sub-slice covering all the tracks and lines that this item spans
        /// excluding the lines that bound it.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (int Start, int End) TrackRangeExcludingLines(AbstractAxis axis)
        {
            var indexes = PlacementIndexes(axis);
            return (indexes.Start + 1, indexes.End);
        }

        /// <summary>Returns the number of tracks that this item spans in the specified axis</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort Span(AbstractAxis axis) => axis switch
        {
            AbstractAxis.Block => Row.Span(),
            AbstractAxis.Inline => Column.Span(),
            _ => throw new ArgumentOutOfRangeException(nameof(axis)),
        };

        /// <summary>Returns the pre-computed value indicating whether the grid item crosses a flexible track</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CrossesFlexibleTrack(AbstractAxis axis) => axis switch
        {
            AbstractAxis.Inline => CrossesFlexibleColumn,
            AbstractAxis.Block => CrossesFlexibleRow,
            _ => throw new ArgumentOutOfRangeException(nameof(axis)),
        };

        /// <summary>Returns the pre-computed value indicating whether the grid item crosses an intrinsic track</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CrossesIntrinsicTrack(AbstractAxis axis) => axis switch
        {
            AbstractAxis.Inline => CrossesIntrinsicColumn,
            AbstractAxis.Block => CrossesIntrinsicRow,
            _ => throw new ArgumentOutOfRangeException(nameof(axis)),
        };

        /// <summary>
        /// For an item spanning multiple tracks, the upper limit used to calculate its limited min-/max-content contribution.
        /// </summary>
        public float? SpannedTrackLimit(
            AbstractAxis axis,
            ref ValueList<GridTrack> axisTracks,
            float? axisParentSize,
            Func<IntPtr, float, float> calcResolver)
        {
            var range = TrackRangeExcludingLines(axis);
            bool tracksAllFixed = true;
            float limit = 0f;
            // Iterate ALL tracks in the range (including gutters) to match Rust sub-slice iteration
            for (int i = range.Start; i < range.End; i++)
            {
                var def = axisTracks[i].MaxTrackSizingFunction.DefiniteLimit(axisParentSize, calcResolver);
                if (!def.HasValue) { tracksAllFixed = false; break; }
                limit += def.Value;
            }
            return tracksAllFixed ? limit : null;
        }

        /// <summary>
        /// Similar to SpannedTrackLimit, but excludes FitContent arguments from the limit.
        /// Used to clamp the automatic minimum contributions of an item.
        /// </summary>
        public float? SpannedFixedTrackLimit(
            AbstractAxis axis,
            ref ValueList<GridTrack> axisTracks,
            float? axisParentSize,
            Func<IntPtr, float, float> calcResolver)
        {
            var range = TrackRangeExcludingLines(axis);
            bool tracksAllFixed = true;
            float limit = 0f;
            // Iterate ALL tracks in the range (including gutters) to match Rust sub-slice iteration
            for (int i = range.Start; i < range.End; i++)
            {
                var def = axisTracks[i].MaxTrackSizingFunction.DefiniteValue(axisParentSize, calcResolver);
                if (!def.HasValue) { tracksAllFixed = false; break; }
                limit += def.Value;
            }
            return tracksAllFixed ? limit : null;
        }

        /// <summary>Compute the known_dimensions to be passed to the child sizing functions</summary>
        public Size<float?> KnownDimensions(
            ILayoutPartialTree tree,
            Size<float?> gridAreaSize)
        {
            if (_knownDimensionsArea is { } cached && cached.Equals(gridAreaSize)) return _knownDimensions;
            var margins = MarginsAxisSumsWithBaselineShims(gridAreaSize.Width, tree);

            var aspectRatio = AspectRatio;
            var padding = PaddingStyle.ResolveOrZero(gridAreaSize.Width, (val, basis) => tree.Calc(val, basis));
            var border = BorderStyle.ResolveOrZero(gridAreaSize.Width, (val, basis) => tree.Calc(val, basis));
            var paddingBorderSize = padding.Add(border).SumAxes();
            var boxSizingAdj = (BoxSizingStyle == BoxSizing.ContentBox ? paddingBorderSize : SizeExtensions.ZeroF32).Map<float?>(v => v);
            var inherentSize = SizeStyle
                .MaybeResolve(gridAreaSize, (val, basis) => tree.Calc(val, basis))
                .MaybeApplyAspectRatio(aspectRatio)
                .MaybeAdd(boxSizingAdj);
            var minSize = MinSizeStyle
                .MaybeResolve(gridAreaSize, (val, basis) => tree.Calc(val, basis))
                .MaybeApplyAspectRatio(aspectRatio)
                .MaybeAdd(boxSizingAdj).Or(paddingBorderSize.Map<float?>(v => v)).MaybeMax(paddingBorderSize.Map<float?>(v => v));
            var maxSize = MaxSizeStyle
                .MaybeResolve(gridAreaSize, (val, basis) => tree.Calc(val, basis))
                .MaybeApplyAspectRatio(aspectRatio)
                .MaybeAdd(boxSizingAdj);

            var gridAreaMinusMargins = gridAreaSize.MaybeSub(margins.Map<float?>(v => v)).MaybeMax(new Size<float?>(0, 0));

            float? ResolveKeyword(Dimension style, float? stretch, float? basis)
            {
                var result = SizingKeywordUtils.ResolveSizingKeyword(style, stretch, basis, tree.Calc);
                return result is { IsExact: true } exact ? exact.Size : null;
            }
            // Width
            var width = inherentSize.Width ?? (SizeStyle.Width.IsSizingKeyword() ? ResolveKeyword(SizeStyle.Width, gridAreaMinusMargins.Width, gridAreaSize.Width) : (
                !MarginStyle.Left.IsAuto() && !MarginStyle.Right.IsAuto() && (JustifySelfStyle.Keyword == AlignItemsKeyword.Stretch || (JustifySelfStyle.Keyword == AlignItemsKeyword.Normal && !IsReplaced))
                    ? gridAreaMinusMargins.Width
                    : null
            ));
            // Reapply aspect ratio after stretch
            var ar1 = new Size<float?>(width, inherentSize.Height).MaybeApplyAspectRatio(aspectRatio);
            width = ar1.Width;
            var height = ar1.Height;

            height = height ?? (SizeStyle.Height.IsSizingKeyword() ? ResolveKeyword(SizeStyle.Height, gridAreaMinusMargins.Height, gridAreaSize.Height) : (
                !MarginStyle.Top.IsAuto() && !MarginStyle.Bottom.IsAuto() && (AlignSelfStyle.Keyword == AlignItemsKeyword.Stretch || (AlignSelfStyle.Keyword == AlignItemsKeyword.Normal && !IsReplaced))
                    ? gridAreaMinusMargins.Height
                    : null
            ));
            // Reapply aspect ratio
            var ar2 = new Size<float?>(width, height).MaybeApplyAspectRatio(aspectRatio);
            width = ar2.Width;
            height = ar2.Height;

            // Clamp
            var clamped = new Size<float?>(width, height).MaybeClamp(minSize, maxSize);
            _knownDimensionsArea = gridAreaSize; _knownDimensions = clamped;
            return clamped;
        }

        /// <summary>Compute the grid area size to be passed to the child sizing functions.</summary>
        public Size<float?> GridAreaSize(
            AbstractAxis axis,
            ref ValueList<GridTrack> axisTracks,
            ref ValueList<GridTrack> otherAxisTracks,
            Size<float?> availableSpace,
            Func<GridTrack, float?, float?> getTrackSizeEstimate,
            Func<IntPtr, float, float> resolveCalcValue)
        {
            var size = SizeExtensions.NoneF32;

            var axisRange = TrackRangeExcludingLines(axis);
            float? itemAxisSize = 0f;
            for (int i = axisRange.Start; i < axisRange.End; i++)
            {
                var track = axisTracks[i];
                var minSize = track.MinTrackSizingFunction.DefiniteValue(availableSpace.Get(axis), resolveCalcValue);
                var maxSize = track.MaxTrackSizingFunction.DefiniteValue(availableSpace.Get(axis), resolveCalcValue);
                if (minSize.HasValue && maxSize.HasValue && minSize.Value == maxSize.Value)
                    itemAxisSize = itemAxisSize.Value + track.BaseSize;
                else
                {
                    itemAxisSize = null;
                    break;
                }
            }
            size.Set(axis, itemAxisSize);

            var range = TrackRangeExcludingLines(axis.Other());
            float? itemOtherAxisSize = 0f;
            // Iterate ALL tracks in the range (including gutters) to match Rust sub-slice iteration
            for (int i = range.Start; i < range.End; i++)
            {
                var track = otherAxisTracks[i];
                var est = getTrackSizeEstimate(track, availableSpace.Get(axis.Other()));
                if (est.HasValue)
                    itemOtherAxisSize = itemOtherAxisSize.Value + est.Value + track.ContentAlignmentAdjustment;
                else
                {
                    itemOtherAxisSize = null;
                    break;
                }
            }

            size.Set(axis.Other(), itemOtherAxisSize);
            return size;
        }

        /// <summary>Retrieve the grid area size from the cache or compute it.</summary>
        public Size<float?> GridAreaSizeCached(
            AbstractAxis axis,
            ref ValueList<GridTrack> axisTracks,
            ref ValueList<GridTrack> otherAxisTracks,
            Size<float?> availableSpace,
            Func<GridTrack, float?, float?> getTrackSizeEstimate,
            Func<IntPtr, float, float> resolveCalcValue)
        {
            if (GridAreaSizeCache.HasValue)
                return GridAreaSizeCache.Value;
            var gridAreaSize = GridAreaSize(axis, ref axisTracks, ref otherAxisTracks, availableSpace,
                getTrackSizeEstimate, resolveCalcValue);
            GridAreaSizeCache = gridAreaSize;
            return gridAreaSize;
        }

        /// <summary>
        /// Compute the item's resolved margins for size contributions. Horizontal percentage margins always resolve
        /// to zero if the container size is indefinite.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Size<float> MarginsAxisSumsWithBaselineShims(float? innerNodeWidth, ILayoutPartialTree tree)
        {
            var resolved = new Rect<float>(
                MarginStyle.Left.ResolveOrZero(0f, (val, basis) => tree.Calc(val, basis)),
                MarginStyle.Right.ResolveOrZero(0f, (val, basis) => tree.Calc(val, basis)),
                MarginStyle.Top.ResolveOrZero(innerNodeWidth, (val, basis) => tree.Calc(val, basis)) + BaselineShim,
                MarginStyle.Bottom.ResolveOrZero(innerNodeWidth, (val, basis) => tree.Calc(val, basis))
            );
            return resolved.SumAxes();
        }

        /// <summary>Compute the item's min content contribution</summary>
        public float MinContentContribution(
            AbstractAxis axis,
            ILayoutPartialTree tree,
            Size<float?> gridAreaSize,
            Size<float?> availableSpace)
        {
            var knownDims = KnownDimensions(tree, gridAreaSize);
            if (knownDims.Get(axis) is { } definite) return definite;
            var margin = MarginStyle.ResolveOrZero(gridAreaSize.Width, tree.Calc).SumAxes();
            margin.Height += BaselineShim;
            availableSpace = availableSpace.MaybeSub(margin.Map<float?>(v => v)).MaybeMax(new Size<float?>(0, 0));
            var space = availableSpace.Map(opt => opt.HasValue ? AvailableSpace.Definite(opt.Value) : AvailableSpace.MinContent);
            foreach (var keywordAxis in new[] { AbsoluteAxis.Horizontal, AbsoluteAxis.Vertical })
            {
                var keyword = SizingKeywordUtils.ResolveSizingKeyword(SizeStyle.GetAbs(keywordAxis), gridAreaSize.GetAbs(keywordAxis).MaybeSub(MarginsAxisSumsWithBaselineShims(gridAreaSize.Width, tree).GetAbs(keywordAxis)).MaybeMax(0), gridAreaSize.GetAbs(keywordAxis), tree.Calc);
                if (keyword is { IsExact: false } measure) space.SetAbs(keywordAxis, measure.AvailableSpace);
            }
            return tree.MeasureChildSize(
                Node, knownDims, gridAreaSize,
                space,
                SizingMode.InherentSize,
                axis.AsAbsNaive(),
                LineExtensions.FalseLine);
        }

        /// <summary>Retrieve min content contribution from cache or compute it</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float MinContentContributionCached(
            AbstractAxis axis,
            ILayoutPartialTree tree,
            Size<float?> gridAreaSize,
            Size<float?> availableSpace)
        {
            var cached = MinContentContributionCache.Get(axis);
            if (cached.HasValue)
                return cached.Value;
            var size = MinContentContribution(axis, tree, gridAreaSize, availableSpace);
            MinContentContributionCache.Set(axis, size);
            return size;
        }

        /// <summary>Compute the item's max content contribution</summary>
        public float MaxContentContribution(
            AbstractAxis axis,
            ILayoutPartialTree tree,
            Size<float?> gridAreaSize,
            Size<float?> availableSpace)
        {
            var knownDims = KnownDimensions(tree, gridAreaSize);
            if (knownDims.Get(axis) is { } definite) return definite;
            var margin = MarginStyle.ResolveOrZero(gridAreaSize.Width, tree.Calc).SumAxes();
            margin.Height += BaselineShim;
            availableSpace = availableSpace.MaybeSub(margin.Map<float?>(v => v)).MaybeMax(new Size<float?>(0, 0));
            var space = availableSpace.Map(opt => opt.HasValue ? AvailableSpace.Definite(opt.Value) : AvailableSpace.MaxContent);
            foreach (var keywordAxis in new[] { AbsoluteAxis.Horizontal, AbsoluteAxis.Vertical })
            {
                var keyword = SizingKeywordUtils.ResolveSizingKeyword(SizeStyle.GetAbs(keywordAxis), gridAreaSize.GetAbs(keywordAxis).MaybeSub(MarginsAxisSumsWithBaselineShims(gridAreaSize.Width, tree).GetAbs(keywordAxis)).MaybeMax(0), gridAreaSize.GetAbs(keywordAxis), tree.Calc);
                if (keyword is { IsExact: false } measure) space.SetAbs(keywordAxis, measure.AvailableSpace);
            }
            return tree.MeasureChildSize(
                Node, knownDims, gridAreaSize,
                space,
                SizingMode.InherentSize,
                axis.AsAbsNaive(),
                LineExtensions.FalseLine);
        }

        /// <summary>Retrieve max content contribution from cache or compute it</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float MaxContentContributionCached(
            AbstractAxis axis,
            ILayoutPartialTree tree,
            Size<float?> gridAreaSize,
            Size<float?> availableSpace)
        {
            var cached = MaxContentContributionCache.Get(axis);
            if (cached.HasValue)
                return cached.Value;
            var size = MaxContentContribution(axis, tree, gridAreaSize, availableSpace);
            MaxContentContributionCache.Set(axis, size);
            return size;
        }

        /// <summary>
        /// The minimum contribution of an item is the smallest outer size it can have.
        /// See: https://www.w3.org/TR/css-grid-1/#min-size-auto
        /// </summary>
        public float MinimumContribution(ILayoutPartialTree tree, AbstractAxis axis, ref ValueList<GridTrack> axisTracks,
            Size<float?> gridAreaSize, Size<float?> innerNodeSize)
        {
            var pb = PaddingStyle.ResolveOrZero(gridAreaSize.Width, tree.Calc).Add(BorderStyle.ResolveOrZero(gridAreaSize.Width, tree.Calc)).SumAxes();
            var adjustment = BoxSizingStyle == BoxSizing.ContentBox ? pb : SizeExtensions.ZeroF32;
            float? preferred = SizeStyle.MaybeResolve(gridAreaSize, tree.Calc).MaybeApplyAspectRatio(AspectRatio).MaybeAdd(adjustment.Map<float?>(v => v)).Get(axis);
            if (preferred is { } size)
            {
                var minStyle = MinSizeStyle.Get(axis); var maxStyle = MaxSizeStyle.Get(axis);
                if (minStyle.IsAuto() && maxStyle.IsAuto()) return size;
                float? basis = gridAreaSize.Get(axis);
                return size.MaybeClamp(minStyle.MaybeResolve(basis, tree.Calc).MaybeAdd(adjustment.Get(axis)),
                    maxStyle.MaybeResolve(IsReplaced ? basis ?? 0 : basis, tree.Calc).MaybeAdd(adjustment.Get(axis)));
            }
            var dimension = SizeStyle.Get(axis);
            if (dimension.IsSizingKeyword() && !dimension.IsStretch()) return MinContentContributionCached(axis, tree, gridAreaSize, gridAreaSize);
            var explicitMin = MinSizeStyle.MaybeResolve(gridAreaSize.Map<float?>(v => v ?? 0), tree.Calc)
                .MaybeApplyAspectRatio(AspectRatio).MaybeAdd(adjustment.Map<float?>(v => v)).Get(axis);
            if (explicitMin is { } min) return min;
            if (OverflowStyle.Get(axis).MaybeIntoAutomaticMinSize() is { } overflowMin) return overflowMin;
            var range = TrackRangeExcludingLines(axis);
            bool auto = false, flex = false;
            for (int i = range.Start; i < range.End; i++)
            {
                var track = axisTracks[i];
                auto |= track.MinTrackSizingFunction.IsAuto() || (track.MinTrackSizingFunction.UsesPercentage() && !innerNodeSize.Get(axis).HasValue);
                flex |= track.MaxTrackSizingFunction.IsFr();
            }
            if (!auto || (range.End - range.Start != 1 && flex)) return 0;
            float contribution = MinContentContributionCached(axis, tree, gridAreaSize, gridAreaSize);
            if (IsCompressibleReplaced) contribution = contribution.MaybeMin(SizeStyle.Get(axis).MaybeResolve(0, tree.Calc)).MaybeMin(MaxSizeStyle.Get(axis).MaybeResolve(0, tree.Calc));
            var limit = SpannedFixedTrackLimit(axis, ref axisTracks, innerNodeSize.Get(axis), tree.Calc);
            if (limit is { } maximum) contribution = TaffySys.F32Min(contribution, TaffySys.F32Max(maximum, pb.Get(axis)));
            return contribution;
        }

        /// <summary>Retrieve minimum contribution from cache or compute it</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float MinimumContributionCached(
            ILayoutPartialTree tree,
            AbstractAxis axis,
            ref ValueList<GridTrack> axisTracks,
            Size<float?> gridAreaSize,
            Size<float?> innerNodeSize)
        {
            var cached = MinimumContributionCache.Get(axis);
            if (cached.HasValue)
                return cached.Value;
            var size = MinimumContribution(tree, axis, ref axisTracks, gridAreaSize, innerNodeSize);
            MinimumContributionCache.Set(axis, size);
            return size;
        }
    }

    // =========================================================================
    // OriginZeroGridPlacement and NonNamedGridPlacement helpers
    // =========================================================================

    /// <summary>
    /// Type alias: OriginZeroGridPlacement = GenericGridPlacement&lt;OriginZeroLine&gt;
    /// NonNamedGridPlacement = GenericGridPlacement&lt;GridLine&gt;
    ///
    /// Extension methods for grid placement operations needed by the grid algorithm.
    /// </summary>
    public static class GridPlacementAlgorithmExtensions
    {
        /// <summary>Convert GridPlacement to OriginZeroGridPlacement, ignoring named lines</summary>
        public static GenericGridPlacement<OriginZeroLine> IntoOriginZeroPlacementIgnoringNamed(
            this GridPlacement self, ushort explicitTrackCount)
        {
            return self.Kind switch
            {
                GridPlacement.GridPlacementKind.Auto => GenericGridPlacement<OriginZeroLine>.AutoPlacement,
                GridPlacement.GridPlacementKind.Span => GenericGridPlacement<OriginZeroLine>.FromSpan((ushort)Math.Clamp((int)(ushort)self.Value, 1, 10000)),
                GridPlacement.GridPlacementKind.Line => self.Value == 0
                    ? GenericGridPlacement<OriginZeroLine>.AutoPlacement
                    : GenericGridPlacement<OriginZeroLine>.FromLine(
                        new GridLine(self.Value).IntoOriginZeroLine(explicitTrackCount)),
                GridPlacement.GridPlacementKind.NamedLine => GenericGridPlacement<OriginZeroLine>.AutoPlacement,
                GridPlacement.GridPlacementKind.NamedSpan => GenericGridPlacement<OriginZeroLine>.AutoPlacement,
                _ => GenericGridPlacement<OriginZeroLine>.AutoPlacement,
            };
        }

        /// <summary>Convert NonNamedGridPlacement to OriginZeroGridPlacement</summary>
        public static GenericGridPlacement<OriginZeroLine> IntoOriginZeroPlacement(
            this GenericGridPlacement<GridLine> self, ushort explicitTrackCount)
        {
            if (self.IsAuto) return GenericGridPlacement<OriginZeroLine>.AutoPlacement;
            if (self.IsSpan) return GenericGridPlacement<OriginZeroLine>.FromSpan((ushort)Math.Clamp((int)self.SpanValue, 1, 10000));
            if (self.IsLine)
            {
                var line = self.LineValue;
                if (line.AsI16() == 0) return GenericGridPlacement<OriginZeroLine>.AutoPlacement;
                return GenericGridPlacement<OriginZeroLine>.FromLine(line.IntoOriginZeroLine(explicitTrackCount));
            }
            return GenericGridPlacement<OriginZeroLine>.AutoPlacement;
        }

        /// <summary>Convert Line of GridPlacement to Line of OriginZeroGridPlacement, ignoring named lines</summary>
        public static Line<GenericGridPlacement<OriginZeroLine>> IntoOriginZeroIgnoringNamed(
            this Line<GridPlacement> self, ushort explicitTrackCount)
        {
            return new Line<GenericGridPlacement<OriginZeroLine>>(
                self.Start.IntoOriginZeroPlacementIgnoringNamed(explicitTrackCount),
                self.End.IntoOriginZeroPlacementIgnoringNamed(explicitTrackCount));
        }

        /// <summary>Convert Line of NonNamedGridPlacement to Line of OriginZeroGridPlacement</summary>
        public static Line<GenericGridPlacement<OriginZeroLine>> IntoOriginZero(
            this Line<GenericGridPlacement<GridLine>> self, ushort explicitTrackCount)
        {
            return new Line<GenericGridPlacement<OriginZeroLine>>(
                self.Start.IntoOriginZeroPlacement(explicitTrackCount),
                self.End.IntoOriginZeroPlacement(explicitTrackCount));
        }

        /// <summary>Whether the track position is definite for OriginZeroGridPlacement</summary>
        public static bool IsDefinite(this Line<GenericGridPlacement<OriginZeroLine>> self) =>
            self.Start.IsLine || self.End.IsLine;

        /// <summary>Whether the track position is definite for NonNamedGridPlacement</summary>
        public static bool IsDefiniteNonNamed(this Line<GenericGridPlacement<GridLine>> self)
        {
            if (self.Start.IsLine && self.Start.LineValue.AsI16() != 0) return true;
            if (self.End.IsLine && self.End.LineValue.AsI16() != 0) return true;
            return false;
        }

        /// <summary>Resolves the span for an indefinite placement</summary>
        public static ushort IndefiniteSpan(this Line<GenericGridPlacement<OriginZeroLine>> self)
        {
            if (self.Start.IsLine && self.End.IsLine) throw new InvalidOperationException("indefinite_span should only be called on indefinite grid tracks");
            if (self.Start.IsSpan) return self.Start.SpanValue;
            if (self.End.IsSpan) return self.End.SpanValue;
            return 1;
        }

        /// <summary>Resolve definite grid lines for OriginZeroGridPlacement</summary>
        public static Line<OriginZeroLine> ResolveDefiniteGridLines(this Line<GenericGridPlacement<OriginZeroLine>> self)
        {
            if (self.Start.IsLine && self.End.IsLine)
            {
                var line1 = self.Start.LineValue;
                var line2 = self.End.LineValue;
                if (line1 == line2) return new Line<OriginZeroLine>(line1, line1 + 1);
                return line1.Value < line2.Value
                    ? new Line<OriginZeroLine>(line1, line2)
                    : new Line<OriginZeroLine>(line2, line1);
            }
            if (self.Start.IsLine && self.End.IsSpan)
                return new Line<OriginZeroLine>(self.Start.LineValue, self.Start.LineValue + self.End.SpanValue);
            if (self.Start.IsLine && self.End.IsAuto)
                return new Line<OriginZeroLine>(self.Start.LineValue, self.Start.LineValue + 1);
            if (self.Start.IsSpan && self.End.IsLine)
                return new Line<OriginZeroLine>(self.End.LineValue - self.Start.SpanValue, self.End.LineValue);
            if (self.Start.IsAuto && self.End.IsLine)
                return new Line<OriginZeroLine>(self.End.LineValue - 1, self.End.LineValue);
            throw new InvalidOperationException("resolve_definite_grid_tracks should only be called on definite grid tracks");
        }

        /// <summary>Resolve absolutely positioned grid tracks</summary>
        public static Line<OriginZeroLine?> ResolveAbsolutelyPositionedGridTracks(
            this Line<GenericGridPlacement<OriginZeroLine>> self)
        {
            if (self.Start.IsLine && self.End.IsLine)
            {
                var t1 = self.Start.LineValue;
                var t2 = self.End.LineValue;
                if (t1 == t2) return new Line<OriginZeroLine?>(t1, t1 + 1);
                return t1.Value < t2.Value
                    ? new Line<OriginZeroLine?>(t1, t2)
                    : new Line<OriginZeroLine?>(t2, t1);
            }
            if (self.Start.IsLine && self.End.IsSpan)
                return new Line<OriginZeroLine?>(self.Start.LineValue, self.Start.LineValue + self.End.SpanValue);
            if (self.Start.IsLine && self.End.IsAuto)
                return new Line<OriginZeroLine?>(self.Start.LineValue, null);
            if (self.Start.IsSpan && self.End.IsLine)
                return new Line<OriginZeroLine?>(self.End.LineValue - self.Start.SpanValue, self.End.LineValue);
            if (self.Start.IsAuto && self.End.IsLine)
                return new Line<OriginZeroLine?>(null, self.End.LineValue);
            return new Line<OriginZeroLine?>(null, null);
        }

        /// <summary>Resolve indefinite grid tracks given an external start position</summary>
        public static Line<OriginZeroLine> ResolveIndefiniteGridTracks(
            this Line<GenericGridPlacement<OriginZeroLine>> self, OriginZeroLine start)
        {
            if (self.Start.IsAuto && self.End.IsAuto) return new Line<OriginZeroLine>(start, start + 1);
            if (self.Start.IsSpan && self.End.IsAuto) return new Line<OriginZeroLine>(start, start + self.Start.SpanValue);
            if (self.Start.IsAuto && self.End.IsSpan) return new Line<OriginZeroLine>(start, start + self.End.SpanValue);
            if (self.Start.IsSpan && self.End.IsSpan) return new Line<OriginZeroLine>(start, start + self.Start.SpanValue);
            throw new InvalidOperationException("resolve_indefinite_grid_tracks should only be called on indefinite grid tracks");
        }
    }

    // =========================================================================
    // Overflow extension for automatic minimum size
    // =========================================================================

    public static class OverflowGridExtensions
    {
        /// <summary>Get the point value for the specified abstract axis</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Overflow Get(this Point<Overflow> self, AbstractAxis axis) => axis switch
        {
            AbstractAxis.Inline => self.X,
            AbstractAxis.Block => self.Y,
            _ => throw new ArgumentOutOfRangeException(nameof(axis)),
        };

    }
}
