// Ported from src/compute/grid/placement.rs at 802e497b.
using System;
using System.Collections.Generic;
using ItemPlacement = Marius.Winter.Taffy.InBothAbsAxis<Marius.Winter.Taffy.Line<Marius.Winter.Taffy.GenericGridPlacement<Marius.Winter.Taffy.OriginZeroLine>>>;

namespace Marius.Winter.Taffy;

public static class PlacementUtils
{
    internal const short MinGridLine = -10000, MaxGridLine = 10000;
    private static OriginZeroLine Advance(OriginZeroLine value) => new((short)Math.Min(value.Value + 1, short.MaxValue));
    private static Line<OriginZeroLine> Span(OriginZeroLine position, ushort span) => new(position,
        new((short)Math.Clamp((int)position.Value + span, short.MinValue, short.MaxValue)));
    private static Line<OriginZeroLine> Clamp(Line<OriginZeroLine> span)
    {
        short start = (short)Math.Clamp((int)span.Start.Value, MinGridLine, MaxGridLine - 1);
        return new(new(start), new((short)Math.Clamp(span.End.Value, start + 1, MaxGridLine)));
    }
    public static void PlaceGridItems(CellOccupancyMatrix matrix, ref ValueList<GridItem> items,
        ReadOnlySpan<ItemPlacement> placements, GridAutoFlow flow)
    {
        if (items.Count != placements.Length) throw new ArgumentException("Items and placements must have equal lengths", nameof(placements));
        var primary = flow.PrimaryAxis(); var secondary = primary.OtherAxis();
        for (int i = 0; i < items.Count; i++)
        {
            var p = placements[i];
            if (p.Horizontal.IsDefinite() && p.Vertical.IsDefinite())
                Record(matrix, items[i], primary, p.Get(primary).ResolveDefiniteGridLines(), p.Get(secondary).ResolveDefiniteGridLines(), CellOccupancyState.DefinitelyPlaced);
        }
        for (int i = 0; i < items.Count; i++)
        {
            var placement = placements[i];
            if (!placement.Get(secondary).IsDefinite() || placement.Get(primary).IsDefinite()) continue;
            var secondarySpan = placement.Get(secondary).ResolveDefiniteGridLines();
            var start = matrix.GetTrackCounts(primary).ImplicitStartLine();
            var position = flow.IsDense() ? start : matrix.AutoPlacementCursor(primary, secondarySpan.Start) ?? start;
            ushort count = placement.Get(primary).IndefiniteSpan();
            while (true)
            {
                var span = Span(position, count);
                if (matrix.LineAreaCollisionJump(primary, span, secondarySpan) is { } next) { position = next; continue; }
                Record(matrix, items[i], primary, span, secondarySpan, CellOccupancyState.AutoPlaced); break;
            }
        }
        var gridStartPrimary = matrix.GetTrackCounts(primary).ImplicitStartLine();
        var gridStartSecondary = matrix.GetTrackCounts(secondary).ImplicitStartLine();
        var cursor = (Primary: gridStartPrimary, Secondary: gridStartSecondary);
        for (int i = 0; i < items.Count; i++)
        {
            var placement = placements[i];
            if (placement.Get(secondary).IsDefinite()) continue;
            var (p, s) = PlaceIndefinite(matrix, placement, flow, cursor);
            Record(matrix, items[i], primary, p, s, CellOccupancyState.AutoPlaced);
            cursor = flow.IsDense() ? (gridStartPrimary, gridStartSecondary) : (p.End, s.Start);
        }
    }
    private static (Line<OriginZeroLine>, Line<OriginZeroLine>) PlaceIndefinite(CellOccupancyMatrix matrix, ItemPlacement placement,
        GridAutoFlow flow, (OriginZeroLine Primary, OriginZeroLine Secondary) cursor)
    {
        var primary = flow.PrimaryAxis(); var secondary = primary.OtherAxis();
        var pStyle = placement.Get(primary); ushort sCount = placement.Get(secondary).IndefiniteSpan();
        var pStart = matrix.GetTrackCounts(primary).ImplicitStartLine(); var pEnd = matrix.GetTrackCounts(primary).ImplicitEndLine();
        var sStart = matrix.GetTrackCounts(secondary).ImplicitStartLine();
        var (pIndex, sIndex) = cursor;
        if (pStyle.IsDefinite())
        {
            var p = pStyle.ResolveDefiniteGridLines();
            sIndex = flow.IsDense() ? sStart : p.Start < pIndex ? Advance(sIndex) : sIndex;
            while (true)
            {
                var s = Span(sIndex, sCount);
                if (matrix.LineAreaCollisionJump(secondary, s, p) is { } next) { sIndex = next; continue; }
                return (p, s);
            }
        }
        ushort pCount = pStyle.IndefiniteSpan();
        bool allPrimary = pCount >= matrix.GetTrackCounts(primary).Len();
        while (true)
        {
            var p = Span(pIndex, pCount); var s = Span(sIndex, sCount);
            if (p.End > pEnd)
            {
                if (pIndex == pStart) return (p, s);
                sIndex = Advance(sIndex); pIndex = pStart; continue;
            }
            if (allPrimary)
            {
                if (matrix.OccupiedTrackJump(secondary, s) is { } next) { sIndex = next; pIndex = pStart; continue; }
                return (p, s);
            }
            if (matrix.LineAreaCollisionJump(primary, p, s) is { } collision) { pIndex = collision; continue; }
            return (p, s);
        }
    }
    private static void Record(CellOccupancyMatrix matrix, GridItem item, AbsoluteAxis primary,
        Line<OriginZeroLine> p, Line<OriginZeroLine> s, CellOccupancyState state)
    {
        p = Clamp(p); s = Clamp(s); matrix.MarkAreaAs(primary, p, s, state);
        item.Column = primary == AbsoluteAxis.Horizontal ? p : s;
        item.Row = primary == AbsoluteAxis.Horizontal ? s : p;
    }
    // Source-compatible entry point. Placement is in logical coordinates for both directions.
    public static void PlaceGridItems(CellOccupancyMatrix matrix, ref ValueList<GridItem> items,
        Func<IEnumerable<(int index, NodeId node, Style style)>> childrenIter, Direction direction,
        GridAutoFlow flow, AlignItems alignItems, AlignItems justifyItems, NamedLineResolver resolver)
    {
        var placements = new List<ItemPlacement>();
        foreach (var (index, node, style) in childrenIter())
        {
            placements.Add(new() { Horizontal = resolver.ResolveColumnNames(style.GridColumn).IntoOriginZero(matrix.GetTrackCounts(AbsoluteAxis.Horizontal).Explicit),
                Vertical = resolver.ResolveRowNames(style.GridRow).IntoOriginZero(matrix.GetTrackCounts(AbsoluteAxis.Vertical).Explicit) });
            items.Add(GridItem.NewWithPlacementStyleAndOrder(node, default, default, style, alignItems, justifyItems, unchecked((ushort)index)));
        }
        PlaceGridItems(matrix, ref items, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(placements), flow);
    }
}
