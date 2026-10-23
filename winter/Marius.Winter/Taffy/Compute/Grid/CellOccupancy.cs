// Port of src/compute/grid/types/cell_occupancy.rs. ValueList replaces SmallVec.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Marius.Winter.Taffy;

internal struct OccupiedInterval
{
    public short Start, End;
    public CellOccupancyState State;
    public OccupiedInterval(short start, short end, CellOccupancyState state)
    {
        Start = start;
        End = end;
        State = state;
    }
}

internal struct TrackIntervals
{
    public ValueList<OccupiedInterval> Intervals;
    public OriginZeroLine? AutoPlacementCursor;

    public readonly CellOccupancyState StateAt(short coordinate)
    {
        foreach (var interval in Intervals)
            if (interval.Start <= coordinate && coordinate < interval.End) return interval.State;
        return CellOccupancyState.Unoccupied;
    }

    public readonly short? CollisionExtent(short start, short end)
    {
        for (int i = Intervals.Count - 1; i >= 0 && Intervals[i].End > start; i--)
            if (Intervals[i].Start < end && Intervals[i].End > start)
                return (short)(Intervals[i].End - 1);
        return null;
    }

    private static void PushMerged(ref ValueList<OccupiedInterval> intervals, OccupiedInterval interval)
    {
        if (intervals.Count > 0 && intervals[intervals.Count - 1].State == interval.State
            && intervals[intervals.Count - 1].End == interval.Start)
            intervals[intervals.Count - 1].End = interval.End;
        else
            intervals.Add(interval);
    }

    public void Paint(short start, short end, CellOccupancyState state)
    {
        if (start >= end) return;
        if (Intervals.Count == 0 || Intervals[Intervals.Count - 1].End <= start)
        {
            PushMerged(ref Intervals, new OccupiedInterval(start, end, state));
            return;
        }
        ValueList<OccupiedInterval> result = default;
        foreach (var interval in Intervals)
        {
            if (interval.End <= start) result.Add(interval);
            else if (interval.Start < start) result.Add(new OccupiedInterval(interval.Start, start, interval.State));
        }
        PushMerged(ref result, new OccupiedInterval(start, end, state));
        foreach (var interval in Intervals)
        {
            if (interval.Start >= end) PushMerged(ref result, interval);
            else if (interval.End > end) PushMerged(ref result, new OccupiedInterval(end, interval.End, interval.State));
        }
        Intervals = result;
    }
}

/// <summary>Grid occupancy stored as sorted disjoint intervals in both orientations.</summary>
public class CellOccupancyMatrix
{
    private TrackCounts _columns, _rows;
    private readonly List<TrackIntervals> _rowIntervals = new();
    private readonly List<TrackIntervals> _columnIntervals = new();

    public override string ToString()
    {
        var output = new StringBuilder();
        output.Append($"Rows: neg_implicit={_rows.NegativeImplicit} explicit={_rows.Explicit} pos_implicit={_rows.PositiveImplicit}\n");
        output.Append($"Cols: neg_implicit={_columns.NegativeImplicit} explicit={_columns.Explicit} pos_implicit={_columns.PositiveImplicit}\n");
        if (_rows.Len() > 100 || _columns.Len() > 100)
            return output.Append("State: (not printed: more than 100 tracks)\n").ToString();
        output.Append("State:\n");
        foreach (var row in _rowIntervals)
        {
            for (int column = 0; column < _columns.Len(); column++)
                output.Append(row.StateAt(_columns.TrackToPrevOzLine((ushort)column).Value) switch
                {
                    CellOccupancyState.Unoccupied => '_',
                    CellOccupancyState.DefinitelyPlaced => 'D',
                    CellOccupancyState.AutoPlaced => 'A',
                    _ => throw new InvalidOperationException("Invalid cell occupancy state"),
                });
            output.Append('\n');
        }
        return output.ToString();
    }

    public static CellOccupancyMatrix WithTrackCounts(TrackCounts columns, TrackCounts rows)
    {
        var result = new CellOccupancyMatrix { _columns = columns, _rows = rows };
        Resize(result._rowIntervals, rows.Len());
        Resize(result._columnIntervals, columns.Len());
        return result;
    }

    private static void Resize(List<TrackIntervals> tracks, int count)
    {
        if (tracks.Capacity < count) tracks.Capacity = count;
        while (tracks.Count < count) tracks.Add(default);
    }

    private List<TrackIntervals> TrackLists(AbsoluteAxis axis) => axis == AbsoluteAxis.Horizontal ? _columnIntervals : _rowIntervals;
    public TrackCounts GetTrackCounts(AbsoluteAxis axis) => axis == AbsoluteAxis.Horizontal ? _columns : _rows;

    public bool IsAreaInRange(AbsoluteAxis axis, (short Start, short End) primary, (short Start, short End) secondary) =>
        primary.Start >= 0 && primary.End <= GetTrackCounts(axis).Len()
        && secondary.Start >= 0 && secondary.End <= GetTrackCounts(axis.OtherAxis()).Len();

    private void ExpandToFitRange(Line<OriginZeroLine> rows, Line<OriginZeroLine> columns)
    {
        int negativeRows = Math.Max(-_rows.NegativeImplicit - rows.Start.Value, 0);
        int positiveRows = Math.Max(rows.End.Value - _rows.ImplicitEndLine().Value, 0);
        int negativeColumns = Math.Max(-_columns.NegativeImplicit - columns.Start.Value, 0);
        int positiveColumns = Math.Max(columns.End.Value - _columns.ImplicitEndLine().Value, 0);
        if (negativeRows > 0) _rowIntervals.InsertRange(0, new TrackIntervals[negativeRows]);
        Resize(_rowIntervals, _rowIntervals.Count + positiveRows);
        if (negativeColumns > 0) _columnIntervals.InsertRange(0, new TrackIntervals[negativeColumns]);
        Resize(_columnIntervals, _columnIntervals.Count + positiveColumns);
        _rows.NegativeImplicit += (ushort)negativeRows;
        _rows.PositiveImplicit += (ushort)positiveRows;
        _columns.NegativeImplicit += (ushort)negativeColumns;
        _columns.PositiveImplicit += (ushort)positiveColumns;
    }

    public void MarkAreaAs(AbsoluteAxis primaryAxis, Line<OriginZeroLine> primary, Line<OriginZeroLine> secondary, CellOccupancyState value)
    {
        var rows = primaryAxis == AbsoluteAxis.Horizontal ? secondary : primary;
        var columns = primaryAxis == AbsoluteAxis.Horizontal ? primary : secondary;
        ExpandToFitRange(rows, columns);
        if (value == CellOccupancyState.AutoPlaced && primary.Start < primary.End && secondary.Start < secondary.End)
        {
            int index = GetTrackCounts(primaryAxis.OtherAxis()).OzLineToNextTrack(secondary.Start);
            ref var track = ref CollectionsMarshal.AsSpan(TrackLists(primaryAxis.OtherAxis()))[index];
            if (!track.AutoPlacementCursor.HasValue || track.AutoPlacementCursor.Value < primary.End)
                track.AutoPlacementCursor = primary.End;
        }
        var rowRange = _rows.OzLineRangeToTrackRange(rows);
        var columnRange = _columns.OzLineRangeToTrackRange(columns);
        for (int i = rowRange.Start; i < rowRange.End; i++)
            CollectionsMarshal.AsSpan(_rowIntervals)[i].Paint(columns.Start.Value, columns.End.Value, value);
        for (int i = columnRange.Start; i < columnRange.End; i++)
            CollectionsMarshal.AsSpan(_columnIntervals)[i].Paint(rows.Start.Value, rows.End.Value, value);
    }

    public OriginZeroLine? LineAreaCollisionJump(AbsoluteAxis primaryAxis, Line<OriginZeroLine> primary, Line<OriginZeroLine> secondary)
    {
        var tracks = TrackLists(primaryAxis.OtherAxis());
        var range = GetTrackCounts(primaryAxis.OtherAxis()).OzLineRangeToTrackRange(secondary);
        short? extent = null;
        for (int i = Math.Max(range.Start, (short)0); i < Math.Min(range.End, tracks.Count); i++)
        {
            short? cell = tracks[i].CollisionExtent(primary.Start.Value, primary.End.Value);
            if (cell.HasValue) extent = extent.HasValue ? Math.Max(extent.Value, cell.Value) : cell;
        }
        return extent.HasValue ? new OriginZeroLine((short)(extent.Value + 1)) : null;
    }

    public bool LineAreaIsUnoccupied(AbsoluteAxis axis, Line<OriginZeroLine> primary, Line<OriginZeroLine> secondary) =>
        !LineAreaCollisionJump(axis, primary, secondary).HasValue;

    // Compatibility for the public C# track-index query; engine placement uses origin-zero queries.
    public bool TrackAreaIsUnoccupied(AbsoluteAxis axis, (short Start, short End) primary, (short Start, short End) secondary)
    {
        var primaryCounts = GetTrackCounts(axis);
        var secondaryCounts = GetTrackCounts(axis.OtherAxis());
        return LineAreaIsUnoccupied(axis,
            new Line<OriginZeroLine>(new OriginZeroLine((short)(primary.Start - primaryCounts.NegativeImplicit)),
                new OriginZeroLine((short)(primary.End - primaryCounts.NegativeImplicit))),
            new Line<OriginZeroLine>(new OriginZeroLine((short)(secondary.Start - secondaryCounts.NegativeImplicit)),
                new OriginZeroLine((short)(secondary.End - secondaryCounts.NegativeImplicit))));
    }

    public OriginZeroLine? OccupiedTrackJump(AbsoluteAxis axis, Line<OriginZeroLine> span)
    {
        var counts = GetTrackCounts(axis);
        var tracks = TrackLists(axis);
        var range = counts.OzLineRangeToTrackRange(span);
        for (int i = Math.Min(range.End, tracks.Count) - 1; i >= Math.Max(range.Start, (short)0); i--)
            if (!tracks[i].Intervals.IsEmpty) return counts.TrackToPrevOzLine((ushort)i) + 1;
        return null;
    }

    public bool RowIsOccupied(int index) => index >= 0 && index < _rowIntervals.Count && !_rowIntervals[index].Intervals.IsEmpty;
    public bool ColumnIsOccupied(int index) => index >= 0 && index < _columnIntervals.Count && !_columnIntervals[index].Intervals.IsEmpty;

    public OriginZeroLine? AutoPlacementCursor(AbsoluteAxis axis, OriginZeroLine start)
    {
        var counts = GetTrackCounts(axis.OtherAxis());
        var tracks = TrackLists(axis.OtherAxis());
        int index = counts.OzLineToNextTrack(start);
        return index < 0 || index >= tracks.Count ? null : tracks[index].AutoPlacementCursor;
    }

    // Legacy placement APIs remain until the containing grid algorithm is moved to logical RTL coordinates.
    public OriginZeroLine? LastOfType(AbsoluteAxis axis, OriginZeroLine start, CellOccupancyState state) => FindOfType(axis, start, state, true);
    public OriginZeroLine? FirstOfType(AbsoluteAxis axis, OriginZeroLine start, CellOccupancyState state) => FindOfType(axis, start, state, false);
    private OriginZeroLine? FindOfType(AbsoluteAxis axis, OriginZeroLine start, CellOccupancyState state, bool last)
    {
        int index = GetTrackCounts(axis.OtherAxis()).OzLineToNextTrack(start);
        var tracks = TrackLists(axis.OtherAxis());
        if (index < 0 || index >= tracks.Count) return null;
        var intervals = tracks[index].Intervals;
        for (int step = 0; step < intervals.Count; step++)
        {
            var interval = intervals[last ? intervals.Count - step - 1 : step];
            if (interval.State == state) return new OriginZeroLine(last ? (short)(interval.End - 1) : interval.Start);
        }
        return null;
    }
}
