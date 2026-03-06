using System;
using System.Collections.Generic;
using System.Numerics;

namespace Marius.Winter;

/// <summary>
/// Track size definition for grid columns/rows.
/// </summary>
public struct TrackSize
{
    public enum TrackType { Fixed, Auto, Fraction }

    public TrackType Type;
    public float Value;

    private TrackSize(TrackType type, float value) { Type = type; Value = value; }

    /// <summary>Fixed pixel size.</summary>
    public static TrackSize Px(float pixels) => new(TrackType.Fixed, pixels);

    /// <summary>Auto-size to content.</summary>
    public static TrackSize Auto() => new(TrackType.Auto, 0);

    /// <summary>Fractional unit (like CSS 'fr'). Distributes remaining space proportionally.</summary>
    public static TrackSize Fr(float fraction = 1f) => new(TrackType.Fraction, fraction);
}

/// <summary>
/// Per-child layout hints for GridLayout. Set on Element.LayoutData.
/// </summary>
public class GridItem
{
    public int Column { get; set; }
    public int Row { get; set; }
    public int ColumnSpan { get; set; } = 1;
    public int RowSpan { get; set; } = 1;
    public Alignment HorizontalAlignment { get; set; } = Alignment.Stretch;
    public Alignment VerticalAlignment { get; set; } = Alignment.Stretch;
}

/// <summary>
/// CSS Grid-style layout. Define template columns/rows with fixed, auto, or fractional sizes.
/// Children are placed by GridItem hints or auto-placed in row-major order.
/// </summary>
public class GridLayout : ILayout
{
    private TrackSize[] _columns = Array.Empty<TrackSize>();
    private TrackSize[] _rows = Array.Empty<TrackSize>();

    public TrackSize[] Columns
    {
        get => _columns;
        set => _columns = value ?? Array.Empty<TrackSize>();
    }

    public TrackSize[] Rows
    {
        get => _rows;
        set => _rows = value ?? Array.Empty<TrackSize>();
    }

    public float ColumnGap { get; set; } = 4f;
    public float RowGap { get; set; } = 4f;

    private struct CellPlacement
    {
        public Element Element;
        public int Col, Row, ColSpan, RowSpan;
    }

    public Vector2 Measure(Element container, float availableWidth, float availableHeight)
    {
        var padding = container.Style.Padding;
        float innerW = availableWidth - padding.HorizontalTotal;
        float innerH = availableHeight - padding.VerticalTotal;

        var placements = PlaceChildren(container);
        int numCols = _columns.Length;
        int numRows = ComputeRowCount(placements);

        // Measure children
        foreach (var p in placements)
            p.Element.Measure(innerW, innerH);

        float[] colWidths = ResolveTrackSizes(_columns, numCols, innerW, ColumnGap, placements, true);
        float[] rowHeights = ResolveTrackSizes(EnsureRows(numRows), numRows, innerH, RowGap, placements, false);

        float totalW = Sum(colWidths) + (numCols > 1 ? ColumnGap * (numCols - 1) : 0);
        float totalH = Sum(rowHeights) + (numRows > 1 ? RowGap * (numRows - 1) : 0);

        return new Vector2(totalW + padding.HorizontalTotal, totalH + padding.VerticalTotal);
    }

    public void Arrange(Element container, RectF bounds)
    {
        var padding = container.Style.Padding;
        float startX = bounds.X + padding.Left;
        float startY = bounds.Y + padding.Top;
        float innerW = bounds.W - padding.HorizontalTotal;
        float innerH = bounds.H - padding.VerticalTotal;

        var placements = PlaceChildren(container);
        int numCols = _columns.Length;
        int numRows = ComputeRowCount(placements);

        float[] colWidths = ResolveTrackSizes(_columns, numCols, innerW, ColumnGap, placements, true);
        float[] rowHeights = ResolveTrackSizes(EnsureRows(numRows), numRows, innerH, RowGap, placements, false);

        // Compute track start positions
        float[] colStarts = new float[numCols];
        float cx = 0;
        for (int c = 0; c < numCols; c++)
        {
            colStarts[c] = cx;
            cx += colWidths[c] + ColumnGap;
        }

        float[] rowStarts = new float[numRows];
        float ry = 0;
        for (int r = 0; r < numRows; r++)
        {
            rowStarts[r] = ry;
            ry += rowHeights[r] + RowGap;
        }

        // Place children
        foreach (var p in placements)
        {
            if (p.Col >= numCols || p.Row >= numRows) continue;

            float cellX = colStarts[p.Col];
            float cellY = rowStarts[p.Row];

            // Span width/height
            float cellW = 0;
            for (int c = p.Col; c < Math.Min(p.Col + p.ColSpan, numCols); c++)
            {
                cellW += colWidths[c];
                if (c > p.Col) cellW += ColumnGap;
            }
            float cellH = 0;
            for (int r = p.Row; r < Math.Min(p.Row + p.RowSpan, numRows); r++)
            {
                cellH += rowHeights[r];
                if (r > p.Row) cellH += RowGap;
            }

            var hint = p.Element.LayoutData as GridItem;
            var hAlign = hint?.HorizontalAlignment ?? Alignment.Stretch;
            var vAlign = hint?.VerticalAlignment ?? Alignment.Stretch;
            var desired = p.Element.DesiredSize;

            float childW = hAlign == Alignment.Stretch ? cellW : Math.Min(desired.X, cellW);
            float childH = vAlign == Alignment.Stretch ? cellH : Math.Min(desired.Y, cellH);
            float childX = cellX;
            float childY = cellY;

            if (hAlign == Alignment.Center) childX += (cellW - childW) / 2f;
            else if (hAlign == Alignment.End) childX += cellW - childW;

            if (vAlign == Alignment.Center) childY += (cellH - childH) / 2f;
            else if (vAlign == Alignment.End) childY += cellH - childH;

            p.Element.Arrange(new RectF(startX + childX, startY + childY, childW, childH));
        }
    }

    private List<CellPlacement> PlaceChildren(Element container)
    {
        var children = container.Children;
        int numCols = Math.Max(1, _columns.Length);
        var placements = new List<CellPlacement>();

        // Track occupied cells for auto-placement
        var occupied = new HashSet<long>();
        int autoRow = 0, autoCol = 0;

        for (int i = 0; i < children.Count; i++)
        {
            if (!children[i].Visible) continue;
            var child = children[i];
            var hint = child.LayoutData as GridItem;

            int col, row, colSpan, rowSpan;

            if (hint != null)
            {
                col = hint.Column;
                row = hint.Row;
                colSpan = Math.Max(1, hint.ColumnSpan);
                rowSpan = Math.Max(1, hint.RowSpan);

                // Mark cells occupied
                for (int r = row; r < row + rowSpan; r++)
                    for (int c = col; c < col + colSpan; c++)
                        occupied.Add(CellKey(r, c));
            }
            else
            {
                // Auto-place: find next unoccupied cell
                colSpan = 1;
                rowSpan = 1;

                while (occupied.Contains(CellKey(autoRow, autoCol)))
                {
                    autoCol++;
                    if (autoCol >= numCols)
                    {
                        autoCol = 0;
                        autoRow++;
                    }
                }

                col = autoCol;
                row = autoRow;
                occupied.Add(CellKey(row, col));

                autoCol++;
                if (autoCol >= numCols)
                {
                    autoCol = 0;
                    autoRow++;
                }
            }

            placements.Add(new CellPlacement
            {
                Element = child,
                Col = col, Row = row,
                ColSpan = colSpan, RowSpan = rowSpan,
            });
        }

        return placements;
    }

    private static long CellKey(int row, int col) => ((long)row << 32) | (uint)col;

    private int ComputeRowCount(List<CellPlacement> placements)
    {
        int maxRow = _rows.Length;
        foreach (var p in placements)
        {
            int end = p.Row + p.RowSpan;
            if (end > maxRow) maxRow = end;
        }
        return Math.Max(1, maxRow);
    }

    private TrackSize[] EnsureRows(int numRows)
    {
        if (_rows.Length >= numRows) return _rows;
        // Extend with Auto tracks for implicit rows
        var result = new TrackSize[numRows];
        Array.Copy(_rows, result, _rows.Length);
        for (int i = _rows.Length; i < numRows; i++)
            result[i] = TrackSize.Auto();
        return result;
    }

    private float[] ResolveTrackSizes(TrackSize[] tracks, int count, float available, float gap,
        List<CellPlacement> placements, bool isColumn)
    {
        float[] sizes = new float[count];
        float totalFixed = 0;
        float totalFr = 0;
        float gapTotal = (count > 1) ? gap * (count - 1) : 0;

        // Pass 1: Fixed and Auto
        for (int t = 0; t < count; t++)
        {
            var track = t < tracks.Length ? tracks[t] : TrackSize.Auto();

            if (track.Type == TrackSize.TrackType.Fixed)
            {
                sizes[t] = track.Value;
                totalFixed += track.Value;
            }
            else if (track.Type == TrackSize.TrackType.Auto)
            {
                // Find max desired size of children in this track (non-spanning only)
                float maxSize = 0;
                foreach (var p in placements)
                {
                    int start = isColumn ? p.Col : p.Row;
                    int span = isColumn ? p.ColSpan : p.RowSpan;
                    if (span != 1 || start != t) continue;

                    var desired = p.Element.DesiredSize;
                    float childSize = isColumn ? desired.X : desired.Y;
                    if (childSize > maxSize) maxSize = childSize;
                }
                sizes[t] = maxSize;
                totalFixed += maxSize;
            }
            else
            {
                totalFr += track.Value;
            }
        }

        // Pass 2: Distribute remaining space to fractional tracks
        float remaining = available - totalFixed - gapTotal;
        if (remaining > 0 && totalFr > 0)
        {
            for (int t = 0; t < count; t++)
            {
                var track = t < tracks.Length ? tracks[t] : TrackSize.Auto();
                if (track.Type == TrackSize.TrackType.Fraction)
                    sizes[t] = remaining * (track.Value / totalFr);
            }
        }

        return sizes;
    }

    private static float Sum(float[] values)
    {
        float s = 0;
        for (int i = 0; i < values.Length; i++) s += values[i];
        return s;
    }
}
