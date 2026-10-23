// Ported from detailed grid output in src/compute/grid/mod.rs at 802e497b.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Marius.Winter.Taffy;

public sealed class DetailedGridTracksInfo
{
    public TrackCounts TrackCounts { get; }
    public List<Line<float>> Positions { get; } = new();
    public List<(int Start, int End)> CollapsedTracks { get; } = new();
    public float? EmptyAxisLine { get; }
    public GridLineNames LineNames { get; }
    public DetailedGridTracksInfo(TrackCounts counts, in ValueList<GridTrack> tracks, GridLineNames lineNames)
    {
        TrackCounts = counts;
        LineNames = lineNames;
        for (int i = 0; i < tracks.Count; i++)
        {
            var track = tracks[i];
            if (track.Kind != GridTrackKind.Track) continue;
            if (track.IsCollapsed)
            {
                int index = Positions.Count;
                if (CollapsedTracks.Count > 0 && CollapsedTracks[^1].End == index)
                    CollapsedTracks[^1] = (CollapsedTracks[^1].Start, index + 1);
                else CollapsedTracks.Add((index, index + 1));
            }
            Positions.Add(new(track.Offset, track.Offset + track.BaseSize));
        }
        if (Positions.Count == 0 && tracks.Count != 0) EmptyAxisLine = tracks[0].Offset;
    }
    public ReadOnlySpan<string> NamesForLine(int index)
    {
        int stored = index - TrackCounts.NegativeImplicit;
        return LineNames.Line(stored);
    }
    public IEnumerable<IReadOnlyList<string>> IterLineNames()
    {
        if (LineNames.Count == 0) yield break;
        for (int i = 0; i <= Positions.Count; i++) yield return LineNames[i - TrackCounts.NegativeImplicit];
    }
    public string ToTrackListString(float zoom = 1)
    {
        if (Positions.Count == 0) return "none";
        var text = new StringBuilder();
        for (int i = 0; i < Positions.Count; i++)
        {
            var names = NamesForLine(i);
            if (i != 0) text.Append(' ');
            if (names.Length != 0)
            {
                text.Append('[');
                for (int n = 0; n < names.Length; n++) { if (n != 0) text.Append(' '); text.Append(names[n]); }
                text.Append("] ");
            }
            text.Append(TaffySys.FormatFloat((Positions[i].End - Positions[i].Start) / zoom)).Append("px");
        }
        var trailing = NamesForLine(Positions.Count);
        if (trailing.Length != 0)
        {
            text.Append(" [");
            for (int n = 0; n < trailing.Length; n++) { if (n != 0) text.Append(' '); text.Append(trailing[n]); }
            text.Append(']');
        }
        return text.ToString();
    }
    private float AbsoluteTrackEdge(int index, bool start, bool reversed)
    {
        var track = Positions[index];
        // Find the first run whose end is past this track, using the native partition point.
        int low = 0, high = CollapsedTracks.Count;
        while (low < high) { int mid = low + (high - low) / 2; if (CollapsedTracks[mid].End <= index) low = mid + 1; else high = mid; }
        if (low < CollapsedTracks.Count && CollapsedTracks[low].Start <= index)
        {
            var range = CollapsedTracks[low];
            int adjacent = start ? range.End : range.Start - 1;
            if ((uint)adjacent < (uint)Positions.Count) track = Positions[adjacent];
        }
        return start != reversed ? track.Start : track.End;
    }
    internal Line<float> ResolveAbsoluteAxis(Line<OriginZeroLine?> placement, float paddingStart, float paddingEnd, bool reversed)
    {
        int min = -TrackCounts.NegativeImplicit, max = TrackCounts.Explicit + TrackCounts.PositiveImplicit;
        int? Index(OriginZeroLine? line) => line is { } l && l.Value >= min && l.Value <= max
            ? l.TryIntoTrackVecIndex(TrackCounts) / 2 : null;
        int? first = Index(placement.Start), last = Index(placement.End);
        float start = reversed ? paddingEnd : paddingStart;
        float end = reversed ? paddingStart : paddingEnd;
        if (first is { } s)
            start = s < Positions.Count ? AbsoluteTrackEdge(s, true, reversed)
                : Positions.Count != 0 ? (reversed ? Positions[^1].Start : Positions[^1].End) : EmptyAxisLine ?? start;
        if (last is { } e)
            end = e > 0 && e - 1 < Positions.Count ? AbsoluteTrackEdge(e - 1, false, reversed)
                : Positions.Count != 0 ? (reversed ? Positions[0].End : Positions[0].Start) : EmptyAxisLine ?? end;
        return reversed ? new(TaffySys.F32Min(start, end), start) : new(start, TaffySys.F32Max(start, end));
    }
}
public readonly record struct DetailedGridItemsInfo(ushort RowStart, ushort RowEnd, ushort ColumnStart, ushort ColumnEnd);
public sealed class DetailedGridInfo
{
    public DetailedGridTracksInfo Rows { get; }
    public DetailedGridTracksInfo Columns { get; }
    public List<DetailedGridItemsInfo> Items { get; } = new();
    private readonly NamedLineResolver _resolver;
    public DetailedGridInfo(DetailedGridTracksInfo rows, DetailedGridTracksInfo columns, in ValueList<GridItem> items, NamedLineResolver resolver)
    {
        Rows = rows; Columns = columns; _resolver = resolver;
        foreach (var item in items) Items.Add(new((ushort)(item.RowIndexes.Start / 2 + 1), (ushort)(item.RowIndexes.End / 2 + 1),
            (ushort)(item.ColumnIndexes.Start / 2 + 1), (ushort)(item.ColumnIndexes.End / 2 + 1)));
    }
    public string GridTemplateRows(float zoom = 1) => Rows.ToTrackListString(zoom);
    public string GridTemplateColumns(float zoom = 1) => Columns.ToTrackListString(zoom);
    public Rect<float> ResolveAbsoluteGridArea(Line<GridPlacement> row, Line<GridPlacement> column, Direction direction, Rect<float> paddingBox)
    {
        var columns = Columns.ResolveAbsoluteAxis(_resolver.ResolveColumnNames(column).IntoOriginZero(Columns.TrackCounts.Explicit)
            .ResolveAbsolutelyPositionedGridTracks(), paddingBox.Left, paddingBox.Right, direction.IsRtl());
        var rows = Rows.ResolveAbsoluteAxis(_resolver.ResolveRowNames(row).IntoOriginZero(Rows.TrackCounts.Explicit)
            .ResolveAbsolutelyPositionedGridTracks(), paddingBox.Top, paddingBox.Bottom, false);
        return new(columns.Start, columns.End, rows.Start, rows.End);
    }
    public (Point<float> Location, Size<float> Size)? ItemGridArea(int index)
    {
        if ((uint)index >= (uint)Items.Count) return null;
        var item = Items[index];
        var first = Columns.Positions[item.ColumnStart - 1];
        var last = Columns.Positions[item.ColumnEnd - 2];
        float left = TaffySys.F32Min(first.Start, last.Start), right = TaffySys.F32Max(first.End, last.End);
        float top = Rows.Positions[item.RowStart - 1].Start, bottom = Rows.Positions[item.RowEnd - 2].End;
        return (new(left, top), new(right - left, bottom - top));
    }
}
