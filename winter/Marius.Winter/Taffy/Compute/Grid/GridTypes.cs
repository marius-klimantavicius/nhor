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
            var explicitLineCount = (ushort)(explicitTrackCount + 1);
            short ozLine;
            if (_value > 0)
                ozLine = (short)(_value - 1);
            else if (_value < 0)
                ozLine = (short)(_value + explicitLineCount);
            else
                throw new InvalidOperationException("Grid line of zero is invalid");
            return new OriginZeroLine(ozLine);
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
            MathF.Min(GrowthLimit, FitContentLimit(axisAvailableGridSpace));

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

    /// <summary>
    /// A dynamically sized matrix (2d grid) which tracks the occupancy of each grid cell during auto-placement.
    /// It also keeps tabs on how many tracks there are and which tracks are implicit and which are explicit.
    /// </summary>
    public class CellOccupancyMatrix
    {
        /// <summary>The grid of occupancy states (row-major)</summary>
        private CellOccupancyState[] _data = Array.Empty<CellOccupancyState>();
        private int _rows;
        private int _cols;

        /// <summary>The counts of implicit and explicit columns</summary>
        private TrackCounts _columns;
        /// <summary>The counts of implicit and explicit rows</summary>
        private TrackCounts _rowCounts;

        /// <summary>Create a CellOccupancyMatrix given a set of provisional track counts.</summary>
        public static CellOccupancyMatrix WithTrackCounts(TrackCounts columns, TrackCounts rows)
        {
            var rowLen = rows.Len();
            var colLen = columns.Len();
            return new CellOccupancyMatrix
            {
                _data = new CellOccupancyState[rowLen * colLen],
                _rows = rowLen,
                _cols = colLen,
                _columns = columns,
                _rowCounts = rows,
            };
        }

        private CellOccupancyState Get(int row, int col)
        {
            if (row < 0 || row >= _rows || col < 0 || col >= _cols) return CellOccupancyState.Unoccupied;
            return _data[row * _cols + col];
        }

        private void Set(int row, int col, CellOccupancyState value)
        {
            _data[row * _cols + col] = value;
        }

        /// <summary>Returns the track counts of this CellOccupancyMatrix in the relevant axis</summary>
        public TrackCounts GetTrackCounts(AbsoluteAxis trackType) => trackType switch
        {
            AbsoluteAxis.Horizontal => _columns,
            AbsoluteAxis.Vertical => _rowCounts,
            _ => throw new ArgumentOutOfRangeException(nameof(trackType)),
        };

        /// <summary>Determines whether the specified area fits within the tracks currently represented by the matrix</summary>
        public bool IsAreaInRange(AbsoluteAxis primaryAxis, (short Start, short End) primaryRange, (short Start, short End) secondaryRange)
        {
            var primaryCounts = GetTrackCounts(primaryAxis);
            var secondaryCounts = GetTrackCounts(primaryAxis.OtherAxis());
            if (primaryRange.Start < 0 || primaryRange.End > primaryCounts.Len())
                return false;
            if (secondaryRange.Start < 0 || secondaryRange.End > secondaryCounts.Len())
                return false;
            return true;
        }

        /// <summary>Expands the grid (potentially in all 4 directions) to accommodate the passed area.</summary>
        private void ExpandToFitRange((short Start, short End) rowRange, (short Start, short End) colRange)
        {
            var reqNegativeRows = Math.Max(-rowRange.Start, 0);
            var reqPositiveRows = Math.Max(rowRange.End - _rowCounts.Len(), 0);
            var reqNegativeCols = Math.Max(-colRange.Start, 0);
            var reqPositiveCols = Math.Max(colRange.End - _columns.Len(), 0);

            var oldRowCount = _rows;
            var oldColCount = _cols;
            var newRowCount = oldRowCount + reqNegativeRows + reqPositiveRows;
            var newColCount = oldColCount + reqNegativeCols + reqPositiveCols;

            var data = new CellOccupancyState[newRowCount * newColCount];

            // Push new negative rows (already zeroed as Unoccupied == 0)

            // Push existing rows
            for (int row = 0; row < oldRowCount; row++)
            {
                // Existing columns go at offset reqNegativeCols
                for (int col = 0; col < oldColCount; col++)
                {
                    data[(reqNegativeRows + row) * newColCount + (reqNegativeCols + col)] = _data[row * oldColCount + col];
                }
            }

            // New positive rows (already zeroed)

            _data = data;
            _rows = newRowCount;
            _cols = newColCount;
            _rowCounts.NegativeImplicit += (ushort)reqNegativeRows;
            _rowCounts.PositiveImplicit += (ushort)reqPositiveRows;
            _columns.NegativeImplicit += (ushort)reqNegativeCols;
            _columns.PositiveImplicit += (ushort)reqPositiveCols;
        }

        /// <summary>Mark an area of the matrix as occupied, expanding as necessary.</summary>
        public void MarkAreaAs(
            AbsoluteAxis primaryAxis,
            Line<OriginZeroLine> primarySpan,
            Line<OriginZeroLine> secondarySpan,
            CellOccupancyState value)
        {
            var (rowSpan, columnSpan) = primaryAxis switch
            {
                AbsoluteAxis.Horizontal => (secondarySpan, primarySpan),
                AbsoluteAxis.Vertical => (primarySpan, secondarySpan),
                _ => throw new ArgumentOutOfRangeException(nameof(primaryAxis)),
            };

            var colRange = _columns.OzLineRangeToTrackRange(columnSpan);
            var rowRange = _rowCounts.OzLineRangeToTrackRange(rowSpan);

            // Check if ranges fit, expand if needed
            var isInRange = IsAreaInRange(AbsoluteAxis.Horizontal, colRange, rowRange);
            if (!isInRange)
            {
                ExpandToFitRange(rowRange, colRange);
                colRange = _columns.OzLineRangeToTrackRange(columnSpan);
                rowRange = _rowCounts.OzLineRangeToTrackRange(rowSpan);
            }

            for (int x = rowRange.Start; x < rowRange.End; x++)
            {
                for (int y = colRange.Start; y < colRange.End; y++)
                {
                    Set(x, y, value);
                }
            }
        }

        /// <summary>
        /// Determines whether a grid area specified by bounding grid lines in OriginZero coordinates
        /// is entirely unoccupied.
        /// </summary>
        public bool LineAreaIsUnoccupied(
            AbsoluteAxis primaryAxis,
            Line<OriginZeroLine> primarySpan,
            Line<OriginZeroLine> secondarySpan)
        {
            var primaryRange = GetTrackCounts(primaryAxis).OzLineRangeToTrackRange(primarySpan);
            var secondaryRange = GetTrackCounts(primaryAxis.OtherAxis()).OzLineRangeToTrackRange(secondarySpan);
            return TrackAreaIsUnoccupied(primaryAxis, primaryRange, secondaryRange);
        }

        /// <summary>
        /// Determines whether a grid area specified by a range of indexes is entirely unoccupied.
        /// </summary>
        public bool TrackAreaIsUnoccupied(
            AbsoluteAxis primaryAxis,
            (short Start, short End) primaryRange,
            (short Start, short End) secondaryRange)
        {
            var (rowRange, colRange) = primaryAxis switch
            {
                AbsoluteAxis.Horizontal => (secondaryRange, primaryRange),
                AbsoluteAxis.Vertical => (primaryRange, secondaryRange),
                _ => throw new ArgumentOutOfRangeException(nameof(primaryAxis)),
            };

            for (int x = rowRange.Start; x < rowRange.End; x++)
            {
                for (int y = colRange.Start; y < colRange.End; y++)
                {
                    var cell = Get(x, y);
                    if (cell != CellOccupancyState.Unoccupied)
                        return false;
                }
            }
            return true;
        }

        /// <summary>Determines whether the specified row contains any items</summary>
        public bool RowIsOccupied(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _rows) return false;
            for (int col = 0; col < _cols; col++)
            {
                if (_data[rowIndex * _cols + col] != CellOccupancyState.Unoccupied)
                    return true;
            }
            return false;
        }

        /// <summary>Determines whether the specified column contains any items</summary>
        public bool ColumnIsOccupied(int columnIndex)
        {
            if (columnIndex < 0 || columnIndex >= _cols) return false;
            for (int row = 0; row < _rows; row++)
            {
                if (_data[row * _cols + columnIndex] != CellOccupancyState.Unoccupied)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Given an axis and a track index, search backwards from the end and find the last grid cell
        /// matching the specified state (if any). Return the index of that cell or null.
        /// </summary>
        public OriginZeroLine? LastOfType(AbsoluteAxis trackType, OriginZeroLine startAt, CellOccupancyState kind)
        {
            var trackCounts = GetTrackCounts(trackType.OtherAxis());
            var trackComputedIndex = trackCounts.OzLineToNextTrack(startAt);

            int? maybeIndex = null;
            if (trackType == AbsoluteAxis.Horizontal)
            {
                if (trackComputedIndex < 0 || trackComputedIndex >= _rows)
                    return null;
                // Search row for last match (rposition)
                for (int col = _cols - 1; col >= 0; col--)
                {
                    if (_data[trackComputedIndex * _cols + col] == kind)
                    {
                        maybeIndex = col;
                        break;
                    }
                }
            }
            else
            {
                if (trackComputedIndex < 0 || trackComputedIndex >= _cols)
                    return null;
                // Search column for last match (rposition)
                for (int row = _rows - 1; row >= 0; row--)
                {
                    if (_data[row * _cols + trackComputedIndex] == kind)
                    {
                        maybeIndex = row;
                        break;
                    }
                }
            }

            if (maybeIndex.HasValue)
                return trackCounts.TrackToPrevOzLine((ushort)maybeIndex.Value);
            return null;
        }
    }

    // =========================================================================
    // GridItem (from grid_item.rs)
    // =========================================================================

    /// <summary>
    /// Represents a single grid item
    /// </summary>
    public class GridItem
    {
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
        /// <summary>Cache for the known_dimensions input to intrinsic sizing computation</summary>
        public Size<float?>? AvailableSpaceCache;
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
                AvailableSpaceCache = null,
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
            List<GridTrack> axisTracks,
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
            List<GridTrack> axisTracks,
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
            Size<float?> innerNodeSize,
            Size<float?> gridAreaSize)
        {
            var margins = MarginsAxisSumsWithBaselineShims(innerNodeSize.Width, tree);

            var aspectRatio = AspectRatio;
            var padding = PaddingStyle.ResolveOrZero(gridAreaSize, (val, basis) => tree.Calc(val, basis));
            var border = BorderStyle.ResolveOrZero(gridAreaSize, (val, basis) => tree.Calc(val, basis));
            var paddingBorderSize = padding.Add(border).SumAxes();
            var boxSizingAdj = (BoxSizingStyle == BoxSizing.ContentBox ? paddingBorderSize : SizeExtensions.ZeroF32).Map<float?>(v => v);
            var inherentSize = SizeStyle
                .MaybeResolve(gridAreaSize, (val, basis) => tree.Calc(val, basis))
                .MaybeApplyAspectRatio(aspectRatio)
                .MaybeAdd(boxSizingAdj);
            var minSize = MinSizeStyle
                .MaybeResolve(gridAreaSize, (val, basis) => tree.Calc(val, basis))
                .MaybeApplyAspectRatio(aspectRatio)
                .MaybeAdd(boxSizingAdj);
            var maxSize = MaxSizeStyle
                .MaybeResolve(gridAreaSize, (val, basis) => tree.Calc(val, basis))
                .MaybeApplyAspectRatio(aspectRatio)
                .MaybeAdd(boxSizingAdj);

            var gridAreaMinusMargins = gridAreaSize.MaybeSub(margins.Map<float?>(v => v));

            // Width
            var width = inherentSize.Width ?? (
                !MarginStyle.Left.IsAuto() && !MarginStyle.Right.IsAuto() && JustifySelfStyle == AlignItems.Stretch
                    ? gridAreaMinusMargins.Width
                    : null
            );
            // Reapply aspect ratio after stretch
            var ar1 = new Size<float?>(width, inherentSize.Height).MaybeApplyAspectRatio(aspectRatio);
            width = ar1.Width;
            var height = ar1.Height;

            height = height ?? (
                !MarginStyle.Top.IsAuto() && !MarginStyle.Bottom.IsAuto() && AlignSelfStyle == AlignItems.Stretch
                    ? gridAreaMinusMargins.Height
                    : null
            );
            // Reapply aspect ratio
            var ar2 = new Size<float?>(width, height).MaybeApplyAspectRatio(aspectRatio);
            width = ar2.Width;
            height = ar2.Height;

            // Clamp
            var clamped = new Size<float?>(width, height).MaybeClamp(minSize, maxSize);
            return clamped;
        }

        /// <summary>Compute the available_space to be passed to the child sizing functions</summary>
        public Size<float?> ComputeAvailableSpace(
            AbstractAxis axis,
            List<GridTrack> otherAxisTracks,
            float? otherAxisAvailableSpace,
            Func<GridTrack, float?, float?> getTrackSizeEstimate)
        {
            var range = TrackRangeExcludingLines(axis.Other());
            float? itemOtherAxisSize = 0f;
            // Iterate ALL tracks in the range (including gutters) to match Rust sub-slice iteration
            for (int i = range.Start; i < range.End; i++)
            {
                var track = otherAxisTracks[i];
                var est = getTrackSizeEstimate(track, otherAxisAvailableSpace);
                if (est.HasValue)
                    itemOtherAxisSize = itemOtherAxisSize.Value + est.Value + track.ContentAlignmentAdjustment;
                else
                {
                    itemOtherAxisSize = null;
                    break;
                }
            }

            var size = SizeExtensions.NoneF32;
            size.Set(axis.Other(), itemOtherAxisSize);
            return size;
        }

        /// <summary>Retrieve the available_space from the cache or compute them</summary>
        public Size<float?> AvailableSpaceCached(
            AbstractAxis axis,
            List<GridTrack> otherAxisTracks,
            float? otherAxisAvailableSpace,
            Func<GridTrack, float?, float?> getTrackSizeEstimate)
        {
            if (AvailableSpaceCache.HasValue)
                return AvailableSpaceCache.Value;
            var availableSpaces = ComputeAvailableSpace(axis, otherAxisTracks, otherAxisAvailableSpace, getTrackSizeEstimate);
            AvailableSpaceCache = availableSpaces;
            return availableSpaces;
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
            Size<float?> availableSpace,
            Size<float?> innerNodeSize)
        {
            var knownDims = KnownDimensions(tree, innerNodeSize, availableSpace);
            return tree.MeasureChildSize(
                Node, knownDims, innerNodeSize,
                availableSpace.Map(opt => opt.HasValue ? AvailableSpace.Definite(opt.Value) : AvailableSpace.MinContent),
                SizingMode.InherentSize,
                axis.AsAbsNaive(),
                LineExtensions.FalseLine);
        }

        /// <summary>Retrieve min content contribution from cache or compute it</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float MinContentContributionCached(
            AbstractAxis axis,
            ILayoutPartialTree tree,
            Size<float?> availableSpace,
            Size<float?> innerNodeSize)
        {
            var cached = MinContentContributionCache.Get(axis);
            if (cached.HasValue)
                return cached.Value;
            var size = MinContentContribution(axis, tree, availableSpace, innerNodeSize);
            MinContentContributionCache.Set(axis, size);
            return size;
        }

        /// <summary>Compute the item's max content contribution</summary>
        public float MaxContentContribution(
            AbstractAxis axis,
            ILayoutPartialTree tree,
            Size<float?> availableSpace,
            Size<float?> innerNodeSize)
        {
            var knownDims = KnownDimensions(tree, innerNodeSize, availableSpace);
            return tree.MeasureChildSize(
                Node, knownDims, innerNodeSize,
                availableSpace.Map(opt => opt.HasValue ? AvailableSpace.Definite(opt.Value) : AvailableSpace.MaxContent),
                SizingMode.InherentSize,
                axis.AsAbsNaive(),
                LineExtensions.FalseLine);
        }

        /// <summary>Retrieve max content contribution from cache or compute it</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float MaxContentContributionCached(
            AbstractAxis axis,
            ILayoutPartialTree tree,
            Size<float?> availableSpace,
            Size<float?> innerNodeSize)
        {
            var cached = MaxContentContributionCache.Get(axis);
            if (cached.HasValue)
                return cached.Value;
            var size = MaxContentContribution(axis, tree, availableSpace, innerNodeSize);
            MaxContentContributionCache.Set(axis, size);
            return size;
        }

        /// <summary>
        /// The minimum contribution of an item is the smallest outer size it can have.
        /// See: https://www.w3.org/TR/css-grid-1/#min-size-auto
        /// </summary>
        public float MinimumContribution(
            ILayoutPartialTree tree,
            AbstractAxis axis,
            List<GridTrack> axisTracks,
            Size<float?> knownDimensions,
            Size<float?> innerNodeSize)
        {
            var padding = PaddingStyle.ResolveOrZero(innerNodeSize, (val, basis) => tree.Calc(val, basis));
            var border = BorderStyle.ResolveOrZero(innerNodeSize, (val, basis) => tree.Calc(val, basis));
            var paddingBorderSize = padding.Add(border).SumAxes();
            var boxSizingAdj = (BoxSizingStyle == BoxSizing.ContentBox ? paddingBorderSize : SizeExtensions.ZeroF32).Map<float?>(v => v);
            var resolvedSize = SizeStyle
                .MaybeResolve(innerNodeSize, (val, basis) => tree.Calc(val, basis))
                .MaybeApplyAspectRatio(AspectRatio)
                .MaybeAdd(boxSizingAdj)
                .Get(axis);

            var resolvedMinSize = MinSizeStyle
                .MaybeResolve(innerNodeSize, (val, basis) => tree.Calc(val, basis))
                .MaybeApplyAspectRatio(AspectRatio)
                .MaybeAdd(boxSizingAdj)
                .Get(axis);

            float? overflowMinSize = OverflowStyle.Get(axis).MaybeIntoAutomaticMinSize();

            var size = resolvedSize
                ?? resolvedMinSize
                ?? overflowMinSize
                ?? ComputeAutoMinimumSize(tree, axis, axisTracks, knownDimensions, innerNodeSize);

            // Clamp by spanned fixed track limit
            var limit = SpannedFixedTrackLimit(axis, axisTracks, innerNodeSize.Get(axis),
                (val, basis) => tree.ResolveCalcValue(val, basis));
            return limit.HasValue ? MathF.Min(size, limit.Value) : size;
        }

        private float ComputeAutoMinimumSize(
            ILayoutPartialTree tree,
            AbstractAxis axis,
            List<GridTrack> axisTracks,
            Size<float?> knownDimensions,
            Size<float?> innerNodeSize)
        {
            // Automatic minimum size. See https://www.w3.org/TR/css-grid-1/#min-size-auto
            var range = TrackRangeExcludingLines(axis);
            var itemAxisTracks = new List<GridTrack>();
            for (int i = range.Start; i < range.End; i += 2)
                itemAxisTracks.Add(axisTracks[i]);

            var spansAutoMinTrack = false;
            for (int i = range.Start; i < range.End; i += 2)
            {
                if (axisTracks[i].MinTrackSizingFunction.IsAuto())
                {
                    spansAutoMinTrack = true;
                    break;
                }
            }

            var onlySpanOneTrack = itemAxisTracks.Count == 1;
            var spansFlexTrack = false;
            for (int i = range.Start; i < range.End; i += 2)
            {
                if (axisTracks[i].MaxTrackSizingFunction.IsFr())
                {
                    spansFlexTrack = true;
                    break;
                }
            }

            var useContentBasedMinimum = spansAutoMinTrack && (onlySpanOneTrack || !spansFlexTrack);

            if (useContentBasedMinimum)
            {
                var minimumContribution = MinContentContributionCached(axis, tree, knownDimensions, innerNodeSize);

                if (IsCompressibleReplaced)
                {
                    var sizeVal = SizeStyle.Get(axis).MaybeResolve(0f, (val, basis) => tree.Calc(val, basis));
                    var maxSizeVal = MaxSizeStyle.Get(axis).MaybeResolve(0f, (val, basis) => tree.Calc(val, basis));
                    if (sizeVal.HasValue) minimumContribution = MathF.Min(minimumContribution, sizeVal.Value);
                    if (maxSizeVal.HasValue) minimumContribution = MathF.Min(minimumContribution, maxSizeVal.Value);
                }
                return minimumContribution;
            }
            return 0f;
        }

        /// <summary>Retrieve minimum contribution from cache or compute it</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float MinimumContributionCached(
            ILayoutPartialTree tree,
            AbstractAxis axis,
            List<GridTrack> axisTracks,
            Size<float?> knownDimensions,
            Size<float?> innerNodeSize)
        {
            var cached = MinimumContributionCache.Get(axis);
            if (cached.HasValue)
                return cached.Value;
            var size = MinimumContribution(tree, axis, axisTracks, knownDimensions, innerNodeSize);
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
                GridPlacement.GridPlacementKind.Span => GenericGridPlacement<OriginZeroLine>.FromSpan((ushort)self.Value),
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
            if (self.IsSpan) return GenericGridPlacement<OriginZeroLine>.FromSpan(self.SpanValue);
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
    // Named line resolver (simplified, from named.rs)
    // =========================================================================

    /// <summary>
    /// Resolver that takes grid line names and area names as input and can then be used to
    /// resolve line names of grid placement properties into line numbers.
    ///
    /// This is a simplified version that works with string-based naming.
    /// </summary>
    public class NamedLineResolver
    {
        /// <summary>Map of row line names to line numbers</summary>
        private readonly SortedDictionary<string, List<ushort>> _rowLines = new();
        /// <summary>Map of column line names to line numbers</summary>
        private readonly SortedDictionary<string, List<ushort>> _columnLines = new();
        /// <summary>Map of area names to area definitions</summary>
        private readonly SortedDictionary<string, GridTemplateArea> _areas = new();
        /// <summary>Number of columns implied by grid area definitions</summary>
        private ushort _areaColumnCount;
        /// <summary>Number of rows implied by grid area definitions</summary>
        private ushort _areaRowCount;
        /// <summary>The number of explicit columns in the grid</summary>
        private ushort _explicitColumnCount;
        /// <summary>The number of explicit rows in the grid</summary>
        private ushort _explicitRowCount;

        private static void UpsertLineNameMap(SortedDictionary<string, List<ushort>> map, string key, ushort value)
        {
            if (map.TryGetValue(key, out var lines))
                lines.Add(value);
            else
                map[key] = new List<ushort> { value };
        }

        /// <summary>Create and initialise a new NamedLineResolver</summary>
        public NamedLineResolver(Style style, ushort columnAutoRepetitions, ushort rowAutoRepetitions)
        {
            _areaColumnCount = 0;
            _areaRowCount = 0;

            // Process grid areas
            foreach (var area in style.GridTemplateAreas)
            {
                _areas[area.Name] = area;
                _areaColumnCount = Math.Max(_areaColumnCount, (ushort)(Math.Max(area.ColumnEnd, (ushort)1) - 1));
                _areaRowCount = Math.Max(_areaRowCount, (ushort)(Math.Max(area.RowEnd, (ushort)1) - 1));

                UpsertLineNameMap(_columnLines, $"{area.Name}-start", area.ColumnStart);
                UpsertLineNameMap(_columnLines, $"{area.Name}-end", area.ColumnEnd);
                UpsertLineNameMap(_rowLines, $"{area.Name}-start", area.RowStart);
                UpsertLineNameMap(_rowLines, $"{area.Name}-end", area.RowEnd);
            }

            // Process column line names
            ProcessLineNames(
                style.GetGridTemplateColumns(), style.GetGridTemplateColumnNames(),
                _columnLines, columnAutoRepetitions);

            // Process row line names
            ProcessLineNames(
                style.GetGridTemplateRows(), style.GetGridTemplateRowNames(),
                _rowLines, rowAutoRepetitions);
        }

        private static void ProcessLineNames(
            ImmutableList<GridTemplateComponent> template,
            ImmutableList<ImmutableList<string>> lineNames,
            SortedDictionary<string, List<ushort>> lineMap,
            ushort autoRepetitions)
        {
            if (template.Count == 0 || lineNames.Count == 0) return;

            ushort currentLine = 0;
            int templateIdx = 0;

            foreach (var names in lineNames)
            {
                currentLine++;
                foreach (var name in names)
                {
                    UpsertLineNameMap(lineMap, name, currentLine);
                }

                if (templateIdx < template.Count)
                {
                    var comp = template[templateIdx];
                    if (comp.Kind == GridTemplateComponent.GridTemplateComponentKind.Repeat && comp.Repetition != null)
                    {
                        var repeat = comp.Repetition;
                        var repeatCount = repeat.Count.IsCount ? repeat.Count.CountValue : autoRepetitions;

                        for (int r = 0; r < repeatCount; r++)
                        {
                            foreach (var lineNameSet in repeat.LineNames)
                            {
                                foreach (var name in lineNameSet)
                                {
                                    UpsertLineNameMap(lineMap, name, currentLine);
                                }
                                currentLine++;
                            }
                            // Last line name set collapses with following
                            currentLine--;
                        }
                        // Last line name set collapses with following
                        currentLine--;
                    }
                    templateIdx++;
                }
            }

            // Sort and dedup
            foreach (var lines in lineMap.Values)
            {
                lines.Sort();
                // Dedup
                int write = 0;
                for (int read = 0; read < lines.Count; read++)
                {
                    if (write == 0 || lines[read] != lines[write - 1])
                    {
                        lines[write] = lines[read];
                        write++;
                    }
                }
                lines.RemoveRange(write, lines.Count - write);
            }
        }

        /// <summary>Resolve named lines for both start and end of a row-axis grid placement</summary>
        public Line<GenericGridPlacement<GridLine>> ResolveRowNames(Line<GridPlacement> line) =>
            ResolveLineNames(line, GridAreaAxis.Row);

        /// <summary>Resolve named lines for both start and end of a column-axis grid placement</summary>
        public Line<GenericGridPlacement<GridLine>> ResolveColumnNames(Line<GridPlacement> line) =>
            ResolveLineNames(line, GridAreaAxis.Column);

        /// <summary>Resolve named lines for both start and end of a grid placement</summary>
        public Line<GenericGridPlacement<GridLine>> ResolveLineNames(Line<GridPlacement> line, GridAreaAxis axis)
        {
            var startResolved = ResolveOnePlacement(line.Start, axis, GridAreaEnd.Start);
            var endResolved = ResolveOnePlacement(line.End, axis, GridAreaEnd.End);

            // Handle NamedSpan + Line combinations
            if (startResolved.IsLine && endResolved.kind == ResolvedKind.NamedSpan)
            {
                var explicitTrackCount = axis == GridAreaAxis.Row ? _explicitRowCount : _explicitColumnCount;
                var normalizedStart = startResolved.lineValue.AsI16() > 0
                    ? (ushort)startResolved.lineValue.AsI16()
                    : (ushort)Math.Max(0, explicitTrackCount + 1 + startResolved.lineValue.AsI16());
                var endLine = FindLineIndex(endResolved.name!, endResolved.idx, axis, GridAreaEnd.End,
                    lines => FilterLinesAfter(lines, normalizedStart));
                return new Line<GenericGridPlacement<GridLine>>(
                    GenericGridPlacement<GridLine>.FromLine(startResolved.lineValue),
                    GenericGridPlacement<GridLine>.FromLine(endLine));
            }
            if (startResolved.kind == ResolvedKind.NamedSpan && endResolved.IsLine)
            {
                var explicitTrackCount = axis == GridAreaAxis.Row ? _explicitRowCount : _explicitColumnCount;
                var normalizedEnd = endResolved.lineValue.AsI16() > 0
                    ? (ushort)endResolved.lineValue.AsI16()
                    : (ushort)Math.Max(0, explicitTrackCount + 1 + endResolved.lineValue.AsI16());
                var startLine = FindLineIndex(startResolved.name!, startResolved.idx, axis, GridAreaEnd.Start,
                    lines => FilterLinesBefore(lines, normalizedEnd));
                return new Line<GenericGridPlacement<GridLine>>(
                    GenericGridPlacement<GridLine>.FromLine(startLine),
                    GenericGridPlacement<GridLine>.FromLine(endResolved.lineValue));
            }

            return new Line<GenericGridPlacement<GridLine>>(
                startResolved.ToGenericGridPlacement(),
                endResolved.ToGenericGridPlacement());
        }

        private static List<ushort> FilterLinesAfter(List<ushort> lines, ushort normalizedStart)
        {
            var point = lines.BinarySearch(normalizedStart);
            if (point < 0) point = ~point;
            else point++; // skip equal
            // partition_point: first where *line > normalizedStart
            int pp = 0;
            foreach (var l in lines) { if (l <= normalizedStart) pp++; else break; }
            return lines.GetRange(pp, lines.Count - pp);
        }

        private static List<ushort> FilterLinesBefore(List<ushort> lines, ushort normalizedEnd)
        {
            int pp = 0;
            foreach (var l in lines) { if (l < normalizedEnd) pp++; else break; }
            return lines.GetRange(0, pp);
        }

        private enum ResolvedKind { Auto, Line, Span, NamedSpan }

        private struct ResolvedPlacement
        {
            public ResolvedKind kind;
            public GridLine lineValue;
            public ushort spanValue;
            public string? name;
            public short idx;

            public bool IsLine => kind == ResolvedKind.Line;

            public GenericGridPlacement<GridLine> ToGenericGridPlacement() => kind switch
            {
                ResolvedKind.Auto => GenericGridPlacement<GridLine>.AutoPlacement,
                ResolvedKind.Line => GenericGridPlacement<GridLine>.FromLine(lineValue),
                ResolvedKind.Span => GenericGridPlacement<GridLine>.FromSpan(spanValue),
                ResolvedKind.NamedSpan => GenericGridPlacement<GridLine>.FromSpan(1), // fallback
                _ => GenericGridPlacement<GridLine>.AutoPlacement,
            };
        }

        private ResolvedPlacement ResolveOnePlacement(GridPlacement p, GridAreaAxis axis, GridAreaEnd end)
        {
            if (p.Kind == GridPlacement.GridPlacementKind.NamedLine)
            {
                var line = FindLineIndex(p.Name!, p.Value, axis, end, l => l);
                return new ResolvedPlacement { kind = ResolvedKind.Line, lineValue = line };
            }
            if (p.Kind == GridPlacement.GridPlacementKind.NamedSpan)
            {
                return new ResolvedPlacement { kind = ResolvedKind.NamedSpan, name = p.Name, idx = p.Value, spanValue = (ushort)p.Value };
            }
            return p.Kind switch
            {
                GridPlacement.GridPlacementKind.Auto => new ResolvedPlacement { kind = ResolvedKind.Auto },
                GridPlacement.GridPlacementKind.Line => new ResolvedPlacement { kind = ResolvedKind.Line, lineValue = new GridLine(p.Value) },
                GridPlacement.GridPlacementKind.Span => new ResolvedPlacement { kind = ResolvedKind.Span, spanValue = (ushort)p.Value },
                _ => new ResolvedPlacement { kind = ResolvedKind.Auto },
            };
        }

        /// <summary>Resolve the grid line for a named grid line or span</summary>
        private GridLine FindLineIndex(
            string name,
            short idx,
            GridAreaAxis axis,
            GridAreaEnd end,
            Func<List<ushort>, List<ushort>> filterLines)
        {
            var explicitTrackCount = axis == GridAreaAxis.Row ? (short)_explicitRowCount : (short)_explicitColumnCount;

            if (idx == 0) idx = 1;

            short GetLine(List<ushort> lines, short explTrackCount, short index)
            {
                var absIdx = Math.Abs(index);
                var enoughLines = absIdx <= lines.Count;
                if (enoughLines)
                {
                    return index > 0
                        ? (short)lines[absIdx - 1]
                        : (short)lines[lines.Count - absIdx];
                }
                else
                {
                    var remainingLines = (absIdx - lines.Count) * Math.Sign(index);
                    return index > 0
                        ? (short)(explTrackCount + 1 + remainingLines)
                        : (short)(-(explTrackCount + 1 + remainingLines));
                }
            }

            // Lookup lines
            var lineLookup = axis == GridAreaAxis.Row ? _rowLines : _columnLines;
            if (lineLookup.TryGetValue(name, out var foundLines))
            {
                var filtered = filterLines(foundLines);
                return new GridLine(GetLine(filtered, explicitTrackCount, idx));
            }

            // Try implicit name
            var implicitName = end == GridAreaEnd.Start ? $"{name}-start" : $"{name}-end";
            if (lineLookup.TryGetValue(implicitName, out var implicitLines))
            {
                var filtered = filterLines(implicitLines);
                return new GridLine(GetLine(filtered, explicitTrackCount, idx));
            }

            // Fallback: non-existent name
            var fallbackLine = idx > 0
                ? (short)(explicitTrackCount + 1 + idx)
                : (short)(-(explicitTrackCount + 1 + idx));
            return new GridLine(fallbackLine);
        }

        /// <summary>Get the number of columns defined by the grid areas</summary>
        public ushort AreaColumnCount => _areaColumnCount;

        /// <summary>Get the number of rows defined by the grid areas</summary>
        public ushort AreaRowCount => _areaRowCount;

        /// <summary>Set the number of columns in the explicit grid</summary>
        public void SetExplicitColumnCount(ushort count) => _explicitColumnCount = count;

        /// <summary>Set the number of rows in the explicit grid</summary>
        public void SetExplicitRowCount(ushort count) => _explicitRowCount = count;
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
